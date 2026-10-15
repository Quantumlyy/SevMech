using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.EnemyActions;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using AnoMech.Multiplayer;
using static AnoMech.Scenarios.Top.TopConstants;
using static AnoMech.Scenarios.Top.P3Intermission.TopP3IntermissionConstants;
using Actions = AnoMech.Scenarios.Top.TopActions;

namespace AnoMech.Scenarios.Top.P3Intermission;

// From P2's kill to Final Omega turning targetable. Nobody heals, and any hit a player should not
// take kills them: a ring, a hand, the void zone, someone else's spread, a second hit, a stack
// taken alone.
public sealed class TopP3IntermissionScenario : IMultiplayerReplayable
{
    public string Name => "Intermission";
    public IPhase Phase => TopZone.P3Intermission;
    public bool SupportsMultiplayer => true;
    public float BgmSecondsAtStart => 142.91f;
    public void DrawSettings() => settingsWindow.Draw();
    public bool HasPerPlayerSettings => true;
    public void DrawPerPlayerSettings() => settingsWindow.DrawPerPlayer();
    public object SettingsOverrides => settingsWindow.Overrides;
    public IReadOnlyList<string> SettingsConflicts => settingsWindow.Overrides.Validate().Problems;
    private readonly TopP3IntermissionSettingsWindow settingsWindow = new();

    public IReadOnlyList<IScenarioAi> AiStrats => [new TopP3IntermissionAi(automarkers: true), new TopP3IntermissionAi(automarkers: false)];

    private static readonly Placement Centre = new(Vector3.Zero, MathF.PI);

    private enum Fodder { Spread, Stack }

    private SimWorld world = null!;
    private SimParty party = null!;
    private TopP3IntermissionState state = null!;
    private TopHelpers? helpers;
    private TopHelpers? omegaFHelper;
    private SimEnemy? centreOmega;
    private SimEnemy? northOmega;
    private SimEnemy? finalOmega;
    private SimEventObject? voidzone;
    private readonly SimEnemy?[] arms = new SimEnemy?[6];
    private SimCharacter? armsTarget;
    private readonly Dictionary<SimCharacter, Fodder> fodder = new();
    private readonly HashSet<SimCharacter> doomed = [];
    private readonly HashSet<SimCharacter> jumpVictims = [];
    private float glideStart;
    private bool gliding;
    private bool voidzoneActive;
    private bool trackingMainTank;

    public TopP3IntermissionState? LastState { get; private set; }

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        party = worldParam.Party;
        state = new TopP3IntermissionState(world.Rng, party, settingsWindow.Overrides);
        LastState = state;
        helpers = null;
        omegaFHelper = null;
        centreOmega = null;
        northOmega = null;
        finalOmega = null;
        voidzone = null;
        Array.Clear(arms);
        armsTarget = null;
        fodder.Clear();
        doomed.Clear();
        jumpVictims.Clear();
        gliding = false;
        voidzoneActive = false;
        trackingMainTank = false;
        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<TopP3IntermissionState>)AiStrats[idx]).Run(state, world);

        world.Events.Add(0f, SpawnP2Omegas);
        world.Events.Add(0f, () => helpers = new TopHelpers(world, BNpcNameId.OmegaFinal, 16, "TopP3Intermission"));
        world.Events.Add(0f, () => omegaFHelper = new TopHelpers(world, BNpcNameId.OmegaF, 1, "TopP3Intermission"));
        world.Events.Add(0.1f, TakeDownedForms);
        world.Events.Add(0.1f, () => northOmega?.Cast(Actions.LaserShower));
        world.Events.Add(0.68f, StopLaserShower);
        world.Events.Add(0.77f, () => northOmega?.Cast(ActionId.DieF, animationLock: 2.1f));
        world.Events.Add(2.24f, PlaySuperfluidAnimation);
        world.Events.Add(2.42f, TurnSuperfluid);
        world.Events.Add(2.82f, () => northOmega?.Cast(ActionId.Unknown7b17, animationLock: 4.1f));
        world.Events.Add(3.45f, () => northOmega?.SetModelState(0x0B));
        world.Events.Add(6.97f, () => Talk(BNpcNameId.OmegaIntermission, BattleTalkId.ImitationOfMortalForm, 5000));
        world.Events.Add(7.19f, SpawnFinalOmega);
        world.Events.Add(7.19f, SpawnArms);
        world.Events.Add(10.00f, () => world.Map.AddEffect(0x00200010, 0x01));
        world.Events.Add(10.00f, ApplyDebuffs);
        world.Events.Add(10.09f, CastWaveRepeater);
        world.Events.Add(10.09f, MergeIntoTheCentre);
        world.Events.Add(10.71f, StartGlide);
        world.Events.Add(11.20f, () => centreOmega?.FadeOut());
        world.Events.Add(13.03f, () => Talk(BNpcNameId.OmegaIntermission, BattleTalkId.ReconfigurationSequence, 5000));
        world.Events.Add(13.08f, () => PlaceArms(0));
        world.Events.Add(13.12f, () => northOmega?.Cast(ActionId.IntermissionMergeStart, animationLock: 3.1f));
        world.Events.Add(13.13f, () => RevealArms(0));
        world.Events.Add(13.75f, () => northOmega?.SetModelState(0x1B));
        world.Events.Add(14.21f, () => ArmsTarget(0));
        world.Events.Add(16.06f, () => PlaceArms(1));
        world.Events.Add(16.06f, SpawnVoidzone);
        world.Events.Add(16.15f, RevealFinalOmega);
        world.Events.Add(16.15f, () => northOmega?.FadeOut());
        world.Events.Add(16.17f, () => RevealArms(1));
        world.Events.Add(17.16f, () => WaveRepeaterRing(Actions.WaveRepeater2));
        world.Events.Add(17.28f, () => ArmsTarget(1));
        world.Events.Add(18.23f, CastWaveRepeater);
        world.Events.Add(19.09f, () => Talk(BNpcNameId.OmegaIntermission, BattleTalkId.BlipBloopBleep, 5000));
        world.Events.Add(19.09f, () => world.Map.AddEffect(0x00020004, 0x0B));
        world.Events.Add(19.09f, () => world.Map.AddEffect(0x00080004, 0x0A));
        world.Events.Add(19.21f, () => WaveRepeaterRing(Actions.WaveRepeater3));
        world.Events.Add(21.10f, () => world.Map.AddEffect(0x04000004, 0x01));
        world.Events.Add(21.27f, () => WaveRepeaterRing(Actions.WaveRepeater4));
        world.Events.Add(22.07f, () => world.Map.SetWeather(79, 0.1f));
        world.Events.Add(25.15f, () => CastColossalBlows(0));
        world.Events.Add(25.31f, () => WaveRepeaterRing(Actions.WaveRepeater2));
        world.Events.Add(27.15f, () => Talk(BNpcNameId.OmegaFinal, BattleTalkId.ExperimentConcluded, 6000));
        world.Events.Add(27.36f, () => WaveRepeaterRing(Actions.WaveRepeater3));
        world.Events.Add(27.67f, () => CastColossalBlows(1));
        world.Events.Add(29.11f, ResolveSniperCannons);
        world.Events.Add(29.21f, () => ArmsLeave(0));
        world.Events.Add(29.42f, () => WaveRepeaterRing(Actions.WaveRepeater4));
        world.Events.Add(29.59f, () => ArmsDropTarget(0));
        world.Events.Add(31.60f, EndVoidzone);
        world.Events.Add(31.76f, () => ArmsLeave(1));
        world.Events.Add(32.15f, () => DespawnArms(0));
        world.Events.Add(32.64f, () => ArmsDropTarget(1));
        world.Events.Add(33.18f, () => finalOmega?.SetTargetable(true));
        world.Events.Add(34.09f, () => trackingMainTank = true);
        world.Events.Add(34.20f, () => finalOmega?.SetVisibleInEnemyList(true));
        world.Events.Add(34.60f, TargetMainTank);
        world.Events.Add(34.71f, () => DespawnArms(1));
    }

    // Host and peer alike: P3's track takes over from its top as the merge begins.
    public void RunInstanceEvents(SimWorld instanceWorld)
    {
        Natives.TimelinePreload.Preload(GimmickTimelines, "TopP3Intermission");
        instanceWorld.Events.Add(13.03f, () => Natives.Bgm.Switch(BgmId.TopP3));
    }

    public void Tick(float delta, float elapsed)
    {
        GlideIntoTheCentre();
        TurnToTheMainTank(delta);
        DetonateDeadHolders();
        KillInVoidzone();
    }

    private void Talk(uint speakerNameId, uint textId, uint durationMs) => world.Map.BattleTalk(speakerNameId, textId, durationMs);

    // P2's two Omegas as P2 ends: the one downed first at the centre, the other at north, still
    // in Omega-F's form until its kill plays out, and still targetable and casting P2's enrage
    // until the kill registers.
    private void SpawnP2Omegas()
    {
        centreOmega = SpawnP2Omega(BNpcBaseId.OmegaF, Vector3.Zero, 0x31, targetable: false);
        northOmega = SpawnP2Omega(BNpcBaseId.OmegaM, NorthOmega, 0x10, targetable: true);
    }

    private SimEnemy? SpawnP2Omega(uint baseId, Vector3 position, byte modeAttributeFlags, bool targetable) => world.SpawnEnemy(new EnemySpawnConfig(
        InitialModeAttributeFlags: modeAttributeFlags, BNpcBaseId: baseId, NameId: BNpcNameId.OmegaM_1DD3, Level: Level,
        Targetable: targetable, EnemyList: EnemyListMode.Never, Placement: new Placement(position, 0f)));

    private void StopLaserShower()
    {
        northOmega?.CancelCast(CastCancelReason.Cancelled);
        northOmega?.SetTargetable(false);
    }

    private void TakeDownedForms()
    {
        centreOmega?.AddStatus(StatusId.Superfluid, stacks: 493, overrideStacks: true);
        centreOmega?.SetModelState(0x0B);
        northOmega?.AddStatus(StatusId.OmegaF, stacks: 491, overrideStacks: true);
        northOmega?.SetModelState(0x0B);
    }

    // On a helper of its own, named for Omega-F.
    private void PlaySuperfluidAnimation()
        => omegaFHelper?.At(0, new Placement(NorthOmega, 0f))?.Cast(ActionId.SuperfluidAnimationF, animationLock: 2.1f);

    private void TurnSuperfluid()
    {
        northOmega?.SetModelState(0x05);
        northOmega?.RemoveStatus(StatusId.OmegaF);
        northOmega?.AddStatus(StatusId.Superfluid, stacks: 493, overrideStacks: true);
    }

    private void MergeIntoTheCentre()
    {
        centreOmega?.Cast(ActionId.IntermissionTeleportF, Vector3.Zero, animationLock: 1.1f);
        northOmega?.Cast(ActionId.IntermissionTeleportM, Vector3.Zero, animationLock: 1.1f);
    }

    private void StartGlide()
    {
        glideStart = world.Events.Elapsed;
        gliding = true;
    }

    // The server slides it in while the teleport's animation plays; a walk would wait out the lock.
    private void GlideIntoTheCentre()
    {
        if (!gliding || northOmega == null) return;
        var progress = MathF.Min(1f, (world.Events.Elapsed - glideStart) / GlideSeconds);
        northOmega.SetPosition(new Placement(Vector3.Lerp(NorthOmega, Vector3.Zero, progress), northOmega.Rotation));
        gliding = progress < 1f;
    }

    private void SpawnFinalOmega()
    {
        finalOmega = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.OmegaFinal, NameId: BNpcNameId.OmegaFinal, Level: Level,
            Targetable: false, EnemyList: EnemyListMode.Manual, Visibility: SpawnVisibility.HiddenUntilShown,
            Placement: new Placement(Vector3.Zero, 0f), MaxHealth: FinalOmegaMaxHealth));
    }

    // UNVERIFIED: whether the real transition warps it in or pops it in.
    private void RevealFinalOmega()
    {
        finalOmega?.PlayActionTimeline(ActionTimelineId.WarpEnd);
        finalOmega?.Cast(ActionId.IntermissionMergeEnd, animationLock: 3.1f);
    }

    // It swings round to its tank rather than snapping, and keeps facing it as the tank moves.
    private void TurnToTheMainTank(float delta)
    {
        if (trackingMainTank) TopBoss.TurnToMainTank(party, finalOmega, TopBoss.FinalOmegaTurnSpeed, delta);
    }

    private void TargetMainTank()
    {
        if (finalOmega is { IsActive: true } omega && party.Get(PartyRole.MainTank) is { } tank && tank.IsAlive())
            omega.SetTarget(tank, follow: false);
    }

    // All six wait hidden in the centre; each set is moved out to its spots and appears there.
    private void SpawnArms()
    {
        for (var spot = 0; spot < arms.Length; spot++)
        {
            var left = state.LeftArms[spot];
            arms[spot] = world.SpawnEnemy(new EnemySpawnConfig(
                BNpcBaseId: left ? BNpcBaseId.LeftArmUnit : BNpcBaseId.RightArmUnit,
                NameId: left ? BNpcNameId.LeftArmUnit : BNpcNameId.RightArmUnit,
                Level: Level, Targetable: false, EnemyList: EnemyListMode.OnlyWhenVisible,
                Visibility: SpawnVisibility.HiddenUntilShown, Placement: Centre));
        }
    }

    private static Placement ArmPlacement(int spot)
    {
        var at = TopCompass.Point(ArmRadius, TopP3IntermissionState.SpotBearing(spot));
        return new Placement(new Vector3(at.X, 0f, at.Y), 0f).Face(Vector3.Zero);
    }

    private void PlaceArms(int set)
    {
        foreach (var spot in state.SetSpots(set))
            arms[spot]?.SetPosition(ArmPlacement(spot));
    }

    private void RevealArms(int set)
    {
        foreach (var spot in state.SetSpots(set))
            arms[spot]?.PlayActionTimeline(state.LeftArms[spot] ? ActionTimelineId.WarpEnd2 : ActionTimelineId.WarpEnd);
    }

    private void ArmsLeave(int set)
    {
        foreach (var spot in state.SetSpots(set))
            arms[spot]?.PlayActionTimeline(state.LeftArms[spot] ? ActionTimelineId.WarpStart2 : ActionTimelineId.WarpStart);
    }

    private void DespawnArms(int set)
    {
        foreach (var spot in state.SetSpots(set))
            arms[spot]?.Despawn();
    }

    // UNVERIFIED stand-in: the real arms go for whoever tops their enmity, which varies by pull
    // and has never been a tank; the sim keeps no enmity, so both sets take one non-tank at random.
    private void ArmsTarget(int set)
    {
        if (armsTarget is not { } target || !target.IsAlive())
        {
            var candidates = party.ActiveMembers().Where(member => member is not ISimPartyMember { Role: var role } || !role.IsTank()).ToList();
            if (candidates.Count == 0) return;
            armsTarget = target = candidates[state.Rng.NextInt(candidates.Count)];
        }
        foreach (var spot in state.SetSpots(set))
            if (arms[spot] is { IsActive: true } arm) arm.SetTarget(target, follow: false);
    }

    private void ArmsDropTarget(int set)
    {
        foreach (var spot in state.SetSpots(set))
            if (arms[spot] is { IsActive: true } arm) arm.SetTarget(null);
    }

    private void CastColossalBlows(int set)
    {
        foreach (var spot in state.SetSpots(set))
            arms[spot]?.Cast(Actions.ColossalBlow);
    }

    // Its bar names no target: a ground-targeted cast on the helper's own spot.
    private void CastWaveRepeater() => helpers?.Next(Centre)?.Cast(Actions.WaveRepeater1, Vector3.Zero);

    private void WaveRepeaterRing(EnemyAction ring) => helpers?.Next(Centre)?.Cast(ring);

    private void SpawnVoidzone()
    {
        voidzone = world.SpawnEventObject(new EventObjectSpawnConfig
        {
            EObjId = EObjId.IntermissionVoidzone,
            Placement = new Placement(Vector3.Zero, 0f),
            TargetableStatus = 5,
            EventId = EObjId.DirectorEventId,
            EntityId = VoidzoneEntityId,
            Arg2 = VoidzoneArg2,
        });
        voidzoneActive = true;
    }

    private void EndVoidzone()
    {
        voidzone?.DirectorEObjMod(VoidzoneOff);
        voidzoneActive = false;
    }

    // UNVERIFIED: no pull stood in it; it kills like everything else nobody should be in.
    private void KillInVoidzone()
    {
        if (!voidzoneActive) return;
        foreach (var member in party.Find.InsideCircle(Vector3.Zero, VoidzoneRadius))
            Kill(member, "the void zone in the middle", LethalResultDelay);
    }

    private void ApplyDebuffs()
    {
        Give(state.Spreads, Fodder.Spread);
        Give(state.Stacks, Fodder.Stack);
    }

    private void Give(IEnumerable<PartyRole> roles, Fodder kind)
    {
        foreach (var role in roles)
        {
            if (party.Get(role) is not { } member || !member.IsAlive()) continue;
            member.AddStatus(StatusOf(kind), DebuffSeconds);
            fodder[member] = kind;
        }
    }

    private static EnemyAction ActionOf(Fodder kind) => kind == Fodder.Spread ? Actions.SniperCannon : Actions.HighPoweredSniperCannon;

    private static EnemyAction JumpOf(Fodder kind) => kind == Fodder.Spread ? Actions.SniperCannonJump : Actions.HighPoweredSniperCannonJump;

    private static ushort StatusOf(Fodder kind) => kind == Fodder.Spread ? StatusId.SniperCannonFodder : StatusId.HighPoweredSniperCannonFodder;

    // A holder who dies before the debuffs go off has theirs go off on someone random, who dies
    // along with anyone near them. It jumps once: a debuff on someone it kills goes with them.
    // UNVERIFIED: no pull lost a holder early, so when it goes off is a guess.
    private void DetonateDeadHolders()
    {
        foreach (var (holder, kind) in fodder.Where(f => !f.Key.IsAlive()).ToList())
        {
            fodder.Remove(holder);
            holder.RemoveStatus(StatusOf(kind));
            if (!jumpVictims.Contains(holder)) Jump(kind);
        }
    }

    private void Jump(Fodder kind)
    {
        var alive = party.ActiveMembers().ToList();
        if (alive.Count == 0) return;
        var target = alive[state.Rng.NextInt(alive.Count)];
        if (helpers?.Next(Centre)?.Cast(JumpOf(kind), target) is not { } cast) return;
        foreach (var hit in cast.Hits)
            jumpVictims.Add(hit.Who);
    }

    private void ResolveSniperCannons()
    {
        var holders = fodder.Where(f => f.Key.IsAlive()).ToList();
        fodder.Clear();
        foreach (var (holder, kind) in holders)
            helpers?.Next(Centre)?.Cast(ActionOf(kind), holder);
    }

    private void Kill(SimCharacter member, string cause, float resultDelay)
    {
        if (!doomed.Add(member)) return;
        world.Events.Add(resultDelay, () => member.Die(SimCharacterDeathExtensions.Environment, cause));
    }

    public MpMessage? BuildReplayStateMessage()
        => LastState is { } s ? new TopP3IntermissionAiReplayStateMessage(s.Debuffs.ToArray(), s.FirstHands == FirstHands.North) : null;

    public object? StartReplay(MpMessage message, int aiIndex, PartyRole myRole, SimWorld replayWorld)
    {
        if (message is not TopP3IntermissionAiReplayStateMessage msg) return null;
        if (TopP3IntermissionState.FromNetworkReplay(msg.Debuffs, msg.FirstHandsNorth) is not { } shadowState) return null;
        ((IScenarioAi<TopP3IntermissionState>)AiStrats[aiIndex]).Run(shadowState, replayWorld);
        return shadowState;
    }
}
