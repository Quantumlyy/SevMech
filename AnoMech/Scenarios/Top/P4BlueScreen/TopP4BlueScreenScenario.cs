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
using AnoMech.Core.UserActions;
using AnoMech.Multiplayer;
using static AnoMech.Scenarios.Top.TopConstants;
using static AnoMech.Scenarios.Top.P4BlueScreen.TopP4BlueScreenConstants;
using Actions = AnoMech.Scenarios.Top.TopActions;

namespace AnoMech.Scenarios.Top.P4BlueScreen;

// From the P3 kill to Blue Screen's raidwide. The boss's HP stays full: the DPS check is left out.
public sealed class TopP4BlueScreenScenario : IMultiplayerReplayable, IPartyLimitBreakScenario
{
    public string Name => "Blue Screen";
    public IPhase Phase => TopZone.P4;
    public bool SupportsMultiplayer => true;
    public float BgmSecondsAtStart => 169.16f;
    public void DrawSettings() => settingsWindow.Draw();
    public object SettingsOverrides => settingsWindow.Overrides;
    private readonly TopP4BlueScreenSettingsWindow settingsWindow = new();

    public IReadOnlyList<IScenarioAi> AiStrats => [new TopP4BlueScreenAi()];

    // Every Wave Cannon line and repeater ring is cast from the arena centre.
    private static readonly Placement Centre = new(Vector3.Zero, MathF.PI);

    private SimWorld world = null!;
    private SimParty party = null!;
    private TopP4BlueScreenState state = null!;
    private SimEnemy? boss;
    private TopHelpers? helpers;
    private float gaugeUnits;
    private float nextGaugeTick;

    public TopP4BlueScreenState? LastState { get; private set; }

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        party = worldParam.Party;
        state = new TopP4BlueScreenState(world.Rng, settingsWindow.Overrides);
        LastState = state;
        boss = null;
        helpers = null;
        gaugeUnits = LimitBreak.GaugeFull;
        nextGaugeTick = float.MaxValue;
        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<TopP4BlueScreenState>)AiStrats[idx]).Run(state, world);

        world.Events.Add(0f, SpawnBoss);
        world.Events.Add(0f, () => boss?.Cast(Actions.IonEffluxUnderWay));
        world.Events.Add(0f, () => HelloWorld.ApplyDebuggers(party, state.Rng));
        world.Events.Add(0f, () => helpers = new TopHelpers(world, BNpcNameId.OmegaFinal, 32, "TopP4BlueScreen"));
        world.Events.Add(1.91f, () => boss?.CancelCast(CastCancelReason.Cancelled));
        world.Events.Add(1.91f, () => boss?.SetTargetable(false));
        world.Events.Add(1.91f, () => Talk(BattleTalkId.CriticalDamageDetected, 5000));
        world.Events.Add(2.00f, () => boss?.Cast(ActionId.P3End, animationLock: 7.1f));
        world.Events.Add(2.80f, () => boss?.SetModelState(0x04));
        world.Events.Add(3.92f, () => world.Map.AddEffect(0x00020001, 0x14));
        world.Events.Add(3.92f, () => world.Map.SetWeather(89, 2f));
        world.Events.Add(9.02f, () => Talk(BattleTalkId.InitiatingSystemReboot, 6000));
        world.Events.Add(9.11f, () => boss?.Cast(ActionId.P4Begin, animationLock: 8.1f));
        world.Events.Add(9.11f, () => boss?.AddStatus(StatusId.InfiniteLimit));
        world.Events.Add(9.11f, () => boss?.SetTargetable(true));
        world.Events.Add(9.78f, () => MeleeLimitBreak(LimitBreakTiming.PhaseStart));
        world.Events.Add(9.91f, () => boss?.SetModelState(0x0B));
        world.Events.Add(17.23f, () => boss?.Cast(ActionId.TeleportP3, Vector3.Zero, animationLock: 1.1f));
        world.Events.Add(17.81f, () => boss?.SetPosition(Vector3.Zero));
        world.Events.Add(18.56f, () => Talk(BattleTalkId.DefeatUnacceptable, 6000));
        world.Events.Add(18.65f, () => boss?.SetRotation(MathF.PI));
        world.Events.Add(18.65f, () => boss?.Cast(ActionId.P4WaveCannonVisualStart, animationLock: 5.1f));
        world.Events.Add(21.18f, () => MarkStackTargets(0));
        world.Events.Add(23.72f, AimLingeringLines);
        world.Events.Add(24.25f, Proteans);
        world.Events.Add(26.66f, CastWaveRepeater);
        world.Events.Add(28.77f, () => boss?.Cast(ActionId.P4WaveCannonVisual2, animationLock: 5.1f));
        world.Events.Add(29.20f, () => ResolveStacks(0));
        world.Events.Add(31.27f, () => MarkStackTargets(1));
        world.Events.Add(33.76f, () => WaveRepeaterRing(Actions.WaveRepeater2));
        world.Events.Add(33.86f, () => boss?.Cast(ActionId.P4WaveCannonVisual2, animationLock: 5.1f));
        world.Events.Add(33.86f, AimLingeringLines);
        world.Events.Add(34.40f, Proteans);
        world.Events.Add(35.81f, () => WaveRepeaterRing(Actions.WaveRepeater3));
        world.Events.Add(37.87f, () => WaveRepeaterRing(Actions.WaveRepeater4));
        world.Events.Add(38.99f, () => boss?.Cast(ActionId.P4WaveCannonVisual1, animationLock: 5.1f));
        world.Events.Add(39.30f, () => ResolveStacks(1));
        world.Events.Add(39.77f, () => boss?.SetModelState(0x05));
        world.Events.Add(41.52f, () => MarkStackTargets(2));
        world.Events.Add(44.02f, () => Talk(BattleTalkId.StructuralLimitationsExceeded, 4000));
        world.Events.Add(44.11f, () => boss?.Cast(ActionId.P4WaveCannonVisual3, animationLock: 5.1f));
        world.Events.Add(44.11f, CastWaveRepeater);
        world.Events.Add(44.11f, AimLingeringLines);
        world.Events.Add(44.64f, Proteans);
        world.Events.Add(49.23f, () => boss?.Cast(ActionId.P4WaveCannonVisual4, animationLock: 5.1f));
        world.Events.Add(49.55f, () => ResolveStacks(2));
        world.Events.Add(50.03f, () => boss?.SetModelState(0x06));
        world.Events.Add(51.20f, () => WaveRepeaterRing(Actions.WaveRepeater2));
        world.Events.Add(53.25f, () => WaveRepeaterRing(Actions.WaveRepeater3));
        world.Events.Add(55.31f, () => WaveRepeaterRing(Actions.WaveRepeater4));
        world.Events.Add(56.27f, () => Talk(BattleTalkId.StructuralFailureImminent, 6000));
        world.Events.Add(56.36f, () => boss?.Cast(ActionId.BlueScreen, animationLock: 2.1f));
        world.Events.Add(57.03f, () => MeleeLimitBreak(LimitBreakTiming.BlueScreen));
        world.Events.Add(64.33f, () => boss?.SetTargetable(false));
        world.Events.Add(64.33f, () => HelloWorld.RemoveDebuggers(party));
        world.Events.Add(64.42f, () => helpers?.Next(Centre)?.Cast(ActionId.BlueScreenAoe, animationLock: 1.1f));
        // UNVERIFIED: some real pulls gain 300 or 600 LB gauge here; the trigger is unknown, so none is added.
        world.Events.Add(66.34f, () => Talk(BattleTalkId.MustEvolve, 5000));
        world.Events.Add(66.34f, () => world.Map.AddEffect(0x00080004, 0x14));
        world.Events.Add(66.34f, () => world.Map.SetWeather(174, 0f));
    }

    // Host and peer alike: the gauge starts full for the melee LB3 the phase opens with.
    public void RunInstanceEvents(SimWorld instanceWorld)
    {
        Natives.TimelinePreload.Preload(GimmickTimelines, "TopP4BlueScreen");
        instanceWorld.Party.LimitBreak.Set(3f);
        instanceWorld.Events.Add(64.33f, () => Natives.Bgm.Silence());
        instanceWorld.Party.LimitBreak.Landed -= OnLimitBreakLanded;
        instanceWorld.Party.LimitBreak.Landed += OnLimitBreakLanded;
    }

    // The gauge follows the event clock, which the debug speed buttons scale.
    public void Tick(float delta, float elapsed) => TickGauge();

    private void Talk(uint textId, uint durationMs) => world.Map.BattleTalk(BNpcNameId.OmegaFinal, textId, durationMs);

    // Where the P3 boss goes down, short of the centre it teleports to.
    private void SpawnBoss()
    {
        boss = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.OmegaFinal,
            NameId: BNpcNameId.OmegaFinal,
            Level: Level,
            Targetable: true,
            EnemyList: EnemyListMode.Always,
            Placement: new Placement(new Vector3(0f, 0f, -2.54f), MathF.PI),
            MaxHealth: BossMaxHealth));
        if (party.Get(PartyRole.MainTank) is { } tank) boss?.SetTarget(tank, follow: false);
    }

    // The real headers: a line's helper is its own animation target, so it is turned to face the
    // player and cast with none; the stack marker alone plays on its target.
    private static Placement FromCentre(Vector3 toward) => new Placement(Vector3.Zero, 0f).Face(toward);

    // A dead player's line goes to a living one instead, who then takes two.
    private List<SimCharacter> LineTargets()
    {
        var alive = party.ActiveMembers().ToList();
        var targets = new List<SimCharacter>(alive);
        while (alive.Count > 0 && targets.Count < PerRole.All.Length) targets.Add(alive[state.Rng.NextInt(alive.Count)]);
        return targets;
    }

    // A target already dead is replaced by a living player.
    private void MarkStackTargets(int set)
    {
        var alive = PerRole.All.Where(r => party.Get(r).IsAlive()).ToList();
        var targets = state.StackTargets[set].ToArray();
        for (var i = 0; i < targets.Length; i++)
        {
            if (alive.Contains(targets[i])) continue;
            var spare = alive.Except(targets).ToList();
            if (spare.Count > 0) targets[i] = spare[state.Rng.NextInt(spare.Count)];
        }
        state.Retarget(set, targets);
        foreach (var role in targets)
            if (party.Get(role) is { } target && target.IsAlive())
                helpers?.Next(Centre)?.Cast(ActionId.P4WaveCannonStackTarget, target, animationLock: 8.1f);
    }

    // Aimed where each player stands as the cast starts; the telegraph shows only for its last 0.7s.
    private void AimLingeringLines()
    {
        foreach (var target in LineTargets())
            helpers?.Next(FromCentre(target.Position))?.Cast(Actions.P4WaveCannonProteanAoe);
    }

    private void Proteans()
    {
        var lines = LineTargets().Select(target => FromCentre(target.Position)).ToList();
        foreach (var line in lines)
            helpers?.Next(line)?.Cast(Actions.P4WaveCannonProtean);
    }

    // Both line stacks go off together. A target who died before it went off has their stack go
    // on someone random.
    private void ResolveStacks(int set)
    {
        var lines = new List<Placement>();
        foreach (var role in state.StackTargets[set])
        {
            var living = party.ActiveMembers().ToList();
            if (living.Count == 0) break;
            var target = party.Get(role) is { } marked && marked.IsAlive() ? marked : living[state.Rng.NextInt(living.Count)];
            lines.Add(FromCentre(target.Position));
        }
        foreach (var line in lines)
            helpers?.Next(line)?.Cast(Actions.P4WaveCannonStack);
    }

    // Its bar names no target: a ground-targeted cast on the helper's own spot.
    private void CastWaveRepeater() => helpers?.Next(Centre)?.Cast(Actions.WaveRepeater1, Vector3.Zero);

    private void WaveRepeaterRing(EnemyAction ring) => helpers?.Next(Centre)?.Cast(ring);

    // The melee LB3: NIN = MNK > DRG > VPR > SAM > RPR, M1 on a tie, and a lone melee always. It is
    // pressed as the boss turns targetable, or as long into Blue Screen's cast (UNVERIFIED: no pull
    // saved it). It does no damage with the boss's HP left alone. A human's seat presses its own;
    // the debug bot's presses through the client as a human would.
    private void MeleeLimitBreak(LimitBreakTiming at)
    {
        if (state.MeleeLimitBreakTiming != at || gaugeUnits < LimitBreak.GaugeFull) return;
        switch (LimitBreakMelee())
        {
            case SimPartyNpc npc when npc.IsAlive() && LimitBreak.ByJob.TryGetValue(npc.ClassJob, out var actionId):
                npc.PlayAction(actionId, LimitBreak.AnimationLockOf(actionId), LimitBreak.DpsBar, LimitBreak.DpsLands, boss, holdStill: true);
                world.Events.Add(LimitBreak.DpsLands, () =>
                {
                    if (npc.IsAlive() && gaugeUnits >= LimitBreak.GaugeFull)
                        LimitBreakHandler.Resolve(party, npc, actionId, new LimitBreakAim(npc.Position, npc.Rotation));
                });
                break;
            case SimPlayer player when DebugBotControl.Enabled && player.IsAlive() && !player.HasMoveInFlight && boss is { } target:
                if (Natives.PlayerInput.PressLimitBreakThree((ulong)target.GameObjectId, null))
                    player.HoldStill(LimitBreak.DpsLands + LimitBreak.AnimationLockOf(LimitBreakHandler.ActionId(player.ClassJob, 3)));
                break;
        }
    }

    private SimCharacter? LimitBreakMelee()
    {
        if (state.MeleeLimitBreakBy is { } forced) return party.Get(forced);
        var first = MeleePriority((party.Get(PartyRole.MeleeDpsA) as ISimPartyMember)?.ClassJob ?? 0);
        var second = MeleePriority((party.Get(PartyRole.MeleeDpsB) as ISimPartyMember)?.ClassJob ?? 0);
        if (Math.Min(first, second) == NotMelee) return null;
        return party.Get(second < first ? PartyRole.MeleeDpsB : PartyRole.MeleeDpsA);
    }

    private const int NotMelee = 5;

    private static int MeleePriority(byte job) => job switch
    {
        30 or 20 => 0,
        22 => 1,
        41 => 2,
        34 => 3,
        39 => 4,
        _ => NotMelee,
    };

    // Host only: a peer's gauge is the host's (IPartyLimitBreakScenario), and its own limit
    // break reaches the host as a report.
    private void OnLimitBreakLanded(uint actionId, LimitBreakAim aim)
    {
        if (Plugin.Game is not { } game || game.ActiveScenario != this || game.Mode == SessionMode.Peer) return;
        SpendGauge(LimitBreak.LevelOf(actionId));
    }

    public void OnPartyLimitBreak(PartyRole role, uint actionId, LimitBreakAim aim)
    {
        if (Plugin.Game is not { } game || game.ActiveScenario != this) return;
        SpendGauge(LimitBreak.LevelOf(actionId));
    }

    // A limit break spends the bars of its level; the gauge then fills on its own.
    private void SpendGauge(int level)
    {
        gaugeUnits = MathF.Max(0f, gaugeUnits - level * LimitBreak.GaugeBar);
        nextGaugeTick = world.Events.Elapsed + LimitBreak.GaugeFirstTick;
        PushGauge();
    }

    private void TickGauge()
    {
        if (world.Events.Elapsed < nextGaugeTick) return;
        nextGaugeTick += LimitBreak.GaugeTickSeconds;
        gaugeUnits = MathF.Min(LimitBreak.GaugeFull, gaugeUnits + LimitBreak.GaugeTick);
        PushGauge();
    }

    private void PushGauge() => world.Party.LimitBreak.Set(gaugeUnits / LimitBreak.GaugeBar);

    public MpMessage? BuildReplayStateMessage()
        => LastState is { } s ? new TopP4BlueScreenAiReplayStateMessage(s.StackTargets.SelectMany(t => t).ToArray()) : null;

    public object? StartReplay(MpMessage message, int aiIndex, PartyRole myRole, SimWorld replayWorld)
    {
        if (message is not TopP4BlueScreenAiReplayStateMessage msg) return null;
        if (TopP4BlueScreenState.FromNetworkReplay(msg.StackTargets) is not { } shadowState) return null;
        ((IScenarioAi<TopP4BlueScreenState>)AiStrats[aiIndex]).Run(shadowState, replayWorld);
        return shadowState;
    }
}
