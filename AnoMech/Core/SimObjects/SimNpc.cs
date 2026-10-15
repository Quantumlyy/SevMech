using System;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Motion;
using AnoMech.Core.Native.Interfaces;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace AnoMech.Core.SimObjects;

// SimCharacter backed by a BattleChara we allocated in a CharacterManager slot. Overlay state
// (statuses, played actions) lives on the base; this layer adds the packet spawn and the "free the
// slot on Despawn" lifecycle.
public class SimNpc : SimCharacter
{
    private IBattleCharaProxy? proxy;

    internal override IBattleCharaProxy? Proxy => proxy;

    // Forget the slot without touching what it holds: a packet spawn whose slot ended up with
    // somebody else's actor.
    protected void DetachSlot() => proxy = null;

    // The engine creates a packet-spawned actor a few frames after the spawn packet, so the
    // wrapper polls its reserved slot each tick. Failed = nothing arrived within
    // PacketSpawnTimeoutFrames, or the slot holds somebody else's actor.
    public bool PacketSpawnPending { get; private set; }
    public bool PacketSpawnFailed { get; private set; }
    private const int PacketSpawnTimeoutFrames = 20;
    protected int PacketSpawnFrames { get; private set; }
    private uint packetEntityId;
    private Placement packetSpawnPlacement;

    // Pending counts as alive, or SimWorld's reaper would drop the wrapper before its actor
    // exists; every consumer of IsActive null-checks the native pointer.
    public override bool IsActive => PacketSpawnPending || base.IsActive;

    internal void BeginPacketSpawn(uint entityId, Placement placement)
    {
        packetEntityId = entityId;
        packetSpawnPlacement = placement;
        PacketSpawnPending = true;
        Motion.Seed(placement);
        ProbePacketSpawn();
    }

    // The sim-side settings a spawn packet doesn't carry go on here.
    protected virtual void OnPacketSpawned(IBattleCharaProxy chara) { }

    private void ProbePacketSpawn()
    {
        if (!PacketSpawnPending || proxy is not { } chara) return;
        if (!chara.Exists)
        {
            if (PacketSpawnFrames < PacketSpawnTimeoutFrames) return;
            PacketSpawnPending = false;
            PacketSpawnFailed = true;
            Natives.BattleCharas.ReleaseSlot(chara.Slot);
            DiagnosticLog.Warn($"[SimNpc.PacketSpawn] 0x{packetEntityId:X}: nothing arrived at slot {chara.Slot} within {PacketSpawnFrames} frames -- the engine dropped the spawn.");
            return;
        }
        PacketSpawnPending = false;
        Natives.BattleCharas.ReleaseSlot(chara.Slot);
        if (chara.EntityId != packetEntityId)
        {
            PacketSpawnFailed = true;
            DiagnosticLog.Warn($"[SimNpc.PacketSpawn] slot {chara.Slot} holds entity 0x{chara.EntityId:X}, not the packet's 0x{packetEntityId:X} -- not ours; treating the spawn as failed.");
            DetachSlot();
            return;
        }
        if (Position != packetSpawnPlacement.Position || Rotation != packetSpawnPlacement.Rotation) Motion.ResendTransform();
        OnPacketSpawned(chara);
    }

    private protected SimNpc(IBattleCharaProxy proxy, Coordinates coordinates, Func<SimCharacter, CharacterMotion> createMotion)
        : base(coordinates, createMotion)
    {
        this.proxy = proxy;
    }

    public void SetModelState(byte value) => ActorControl.SetModelState(value);

    // Sampled for peers.
    public byte ModelState => proxy?.ModelState ?? 0;

    // Which sub-meshes show, e.g. Omega-M's shield.
    public void SetModeAttributeFlags(byte value) => ActorControl.SetModeAttributeFlags(value);

    // A party doppel's own action with the real packet shape: the bar, then the release. Here
    // rather than on SimPartyNpc because the same seat is a SimNetworkPuppet on every peer, which
    // has to play it too; SimEnemy drives its own richer cast instead. Effects (statuses, damage)
    // stay the caller's job, exactly as for an enemy Cast.
    //
    // Sampled for peers; PlayedActionSeq is the edge trigger.
    public uint PlayedActionId { get; private set; }
    public float PlayedActionAnimationLock { get; private set; }
    public float PlayedActionCastSeconds { get; private set; }
    public float PlayedActionEffectDelay { get; private set; }
    public SimCharacter? PlayedActionTarget { get; private set; }
    public Vector3? PlayedActionLocation { get; private set; }
    public bool PlayedActionHoldsStill { get; private set; }
    public int PlayedActionSeq { get; private set; }

    private (uint ActionId, float AnimationLock, SimCharacter? Target, Vector3? Location)? pendingActionEffect;
    private float pendingActionEffectIn;
    private float heldStillFor;

    public override bool AnimationLock => PlayedActionHoldsStill && (pendingActionEffect != null || heldStillFor > 0f);

    // castSeconds > 0 puts the bar up first. A player's cast resolves before its bar fills, so
    // the effect lands effectDelay into it and the bar goes with it. A ground-targeted action goes
    // to `location` instead of a target. holdStill roots the user through the bar and the
    // animation lock, as a limit break does; a move ordered meanwhile waits for it.
    public void PlayAction(uint actionId, float animationLock = 0.6f, float castSeconds = 0f, float effectDelay = 0f,
        SimCharacter? target = null, Vector3? location = null, bool holdStill = false)
    {
        PlayedActionId = actionId;
        PlayedActionAnimationLock = animationLock;
        PlayedActionCastSeconds = castSeconds;
        PlayedActionEffectDelay = effectDelay;
        PlayedActionTarget = target;
        PlayedActionLocation = location;
        PlayedActionHoldsStill = holdStill;
        PlayedActionSeq++;
        heldStillFor = 0f;
        if (location is { } at) Face(at);
        else if (target != null) Face(target);
        if (castSeconds <= 0f)
        {
            FireAction(actionId, animationLock, target, location);
            return;
        }
        Casting.NativeCast(actionId, ActionType.Action, 0f, castSeconds, false, position: location,
            targetId: location != null ? null : TargetOrSelf(target));
        pendingActionEffect = (actionId, animationLock, target, location);
        pendingActionEffectIn = effectDelay > 0f ? MathF.Min(effectDelay, castSeconds) : castSeconds;
    }

    private void FireAction(uint actionId, float animationLock, SimCharacter? target, Vector3? location)
    {
        if (PlayedActionHoldsStill) heldStillFor = animationLock;
        if (location is { } at)
        {
            Casting.NativeActionEffect(actionId, animationLock, (ushort)actionId, 0, ActionType.Action, 0, position: at);
            return;
        }
        var targetId = TargetOrSelf(target);
        Casting.NativeActionEffect(actionId, animationLock, (ushort)actionId, 0, ActionType.Action, 0,
            position: target != null && targetId != GameObjectId ? target.Position : null,
            animationTargetId: targetId, targets: [new ActionEffectTarget(targetId)]);
    }

    // A target that left the world can't take the effect: ActionEffectHandler dereferences it.
    private GameObjectId TargetOrSelf(SimCharacter? target)
        => target is { IsActive: true, Proxy.Exists: true } ? target.GameObjectId : GameObjectId;

    // Death or the run's end takes the cast with it, bar and all.
    protected void AbortPlayedAction(CastCancelReason reason)
    {
        heldStillFor = 0f;
        if (pendingActionEffect is not { } pending) return;
        pendingActionEffect = null;
        EndPlayedCast(pending.ActionId, reason);
    }

    private void EndPlayedCast(uint actionId, CastCancelReason reason)
    {
        if (Proxy is { IsCasting: true }) ActorControl.CancelCast(actionId, reason);
    }

    private void TickPendingAction(float deltaSeconds)
    {
        if (heldStillFor > 0f) heldStillFor = MathF.Max(0f, heldStillFor - deltaSeconds);
        if (pendingActionEffect is not { } pending) return;
        pendingActionEffectIn -= deltaSeconds;
        if (pendingActionEffectIn > 0f) return;
        pendingActionEffect = null;
        FireAction(pending.ActionId, pending.AnimationLock, pending.Target, pending.Location);
        EndPlayedCast(pending.ActionId, CastCancelReason.Silent);
    }

    public override void Tick(float deltaSeconds)
    {
        base.Tick(deltaSeconds);
        TickPendingAction(deltaSeconds);
        PacketSpawnFrames++;
        ProbePacketSpawn();
    }

    internal override void ClearRunState()
    {
        AbortPlayedAction(CastCancelReason.Cancelled);
        base.ClearRunState();
    }

    public override void Despawn()
    {
        if (proxy == null) return;
        PacketSpawnPending = false;
        pendingActionEffect = null;
        heldStillFor = 0f;
        Casting.Despawn();
        base.Despawn();
        proxy.Despawn();
        proxy = null;
    }
}
