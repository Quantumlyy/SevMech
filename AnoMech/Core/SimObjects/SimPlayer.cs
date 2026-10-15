using System;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Geometry;
using AnoMech.Core.Game.Motion;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.UserActions;

namespace AnoMech.Core.SimObjects;

public sealed class SimPlayer : SimCharacter, ISimPartyMember
{
    private readonly PlayerMotion motion;

    public SimPlayer(Coordinates coordinates, ObstacleField obstacles)
        : base(coordinates, self => new PlayerMotion(coordinates, self.ActorControl, obstacles))
        => motion = (PlayerMotion)Motion;

    public PartyRole Role { get; set; }

    public byte ClassJob => Proxy.ClassJob;

    // For stillness/movement mechanics: IsMoving = movement input, a jump, any action, or an
    // in-flight debug-bot MoveTo; IsActing also counts auto-attacks. Forced false while KO'd.
    public bool IsMoving { get; private set; }
    public bool IsActing { get; private set; }

    internal override IBattleCharaProxy Proxy => Natives.BattleCharas.LocalPlayer;

    // The client's own prediction runs the whole cast; this only reads it.
    public bool IsLimitBreaking => Proxy.IsCasting && LimitBreakHandler.IsLimitBreak(Proxy.CastActionId);

    // Under the debug bot a limit break roots the character as it roots its player: through the
    // cast, then for the animation its presser holds it.
    private float heldStillFor;

    public override bool AnimationLock => DebugBotControl.Enabled && !Dead && (IsLimitBreaking || heldStillFor > 0f);

    public void HoldStill(float seconds) => heldStillFor = MathF.Max(heldStillFor, seconds);

    public void UseSprint(float duration)
    {
        if (DebugBotControl.Enabled && !Dead && !ActionsLocked) SprintHandler.Apply(this, duration);
    }

    public override void Tick(float deltaSeconds)
    {
        base.Tick(deltaSeconds);
        if (heldStillFor > 0f) heldStillFor = MathF.Max(0f, heldStillFor - deltaSeconds);
        SyncStatusAffliction();
        SampleActivity();
    }

    // Compared against the client every tick, so a PlayerStateFlags from the real server that
    // clears it is undone on the next one.
    private void SyncStatusAffliction()
    {
        var status = AnyActiveStatusRow(row => row.LockMovement || row.LockActions);
        var control = AnyActiveStatusRow(row => row.LockControl);
        var conditions = Natives.PlayerConditions;
        if (conditions.StatusAffliction != status || conditions.ControlAffliction != control)
            conditions.SetAfflictions(status, control);
    }

    private void SampleActivity()
    {
        var hooks = Natives.PlayerInput;
        // Drained every frame, even while dead, so a stale press can't carry over.
        var actedThisFrame = hooks.PollActionUsed();
        if (Dead)
        {
            IsMoving = false;
            IsActing = false;
            return;
        }
        IsMoving = hooks.MovementInputActive || actedThisFrame || hooks.IsJumping || motion.IsMoving || motion.IsHeld;
        // The press is one frame, but the whole cast bar counts: an Acceleration Bomb landing
        // mid-cast catches the player acting, exactly as it would in the fight.
        IsActing = IsMoving || hooks.IsAutoAttacking || Proxy.IsCasting;
    }

    public void OnKilled(bool byEnvironment)
    {
        if (Proxy.IsCasting) ActorControl.CancelCast(Proxy.CastActionId, CastCancelReason.Interrupted);
        GoDown(byEnvironment);
    }

    internal override void ClearRunState()
    {
        base.ClearRunState();
        heldStillFor = 0f;
    }

    public override void Despawn()
    {
        base.Despawn();
        heldStillFor = 0f;
        Revive();
        if (Natives.PlayerConditions is { StatusAffliction: true } or { ControlAffliction: true })
            Natives.PlayerConditions.SetAfflictions(false, false);
    }
}
