using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using AnoMech.Multiplayer;
using static AnoMech.Scenarios.Top.TopConstants;
using Actions = AnoMech.Scenarios.Top.TopActions;

namespace AnoMech.Scenarios.Top.P1Pantokrator;

// From the last Blaster's aftermath to the second Wave Cannon Kyrios. Two opposite cones turn
// around the beetle while puddles drop under everyone; each In Line number in turn takes a
// missile and a line stack. Then the two farthest bait five tank cleaves while lasers go through
// the marked players.
public sealed class TopP1PantokratorScenario : IMultiplayerReplayable
{
    public string Name => "Pantokrator";
    public IPhase Phase => TopZone.P1;
    public bool SupportsMultiplayer => true;
    public void DrawSettings() => settingsWindow.Draw();
    public bool HasPerPlayerSettings => true;
    public void DrawPerPlayerSettings() => settingsWindow.DrawPerPlayer();
    public object SettingsOverrides => settingsWindow.Overrides;
    public IReadOnlyList<string> SettingsConflicts => settingsWindow.Overrides.Validate().Problems;
    private readonly TopP1PantokratorSettingsWindow settingsWindow = new();

    public IReadOnlyList<IScenarioAi> AiStrats => [new TopP1PantokratorAi()];

    private static readonly ushort[] InLineStatuses = [StatusId.FirstInLine, StatusId.SecondInLine, StatusId.ThirdInLine, StatusId.FourthInLine];

    private static readonly Vector3 BeetleSpot = new(0.02f, 0f, 0.27f);
    private const float BeetleAutoAttackReach = 15f;
    // Radians per second.
    private const float BeetleTurnSpeed = 8f;
    private const float FlameCastSeconds = 4f;
    // UNVERIFIED: the pairs' cadence and step were never measured. Fourteen pairs two seconds
    // apart, each a step on from the last, span the 26s the real ones do. Times are the
    // ActionEffect's; the hit snapshots 0.3s before it.
    private const int FlamePairs = 14;
    private const float FirstFlame = 14.1f;
    private const float FlameInterval = 2f;
    // UNVERIFIED: the puddles' cadence was never measured.
    private const int PuddleWaves = 8;
    private const float FirstPuddles = 14.5f;
    private const float PuddleInterval = 3f;
    private static readonly float[] SetTimes = [20.1f, 26.1f, 32.1f, 38.1f];
    private static readonly float[] CleaveTimes = [46.8f, 48.9f, 50.9f, 53f, 55.1f];

    private SimWorld world = null!;
    private SimParty party = null!;
    private TopP1PantokratorState state = null!;

    public TopP1PantokratorState? LastState { get; private set; }

    private SimEnemy? beetle;
    private TopHelpers? helpers;

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        party = worldParam.Party;
        state = new TopP1PantokratorState(world.Rng, party, settingsWindow.Overrides);
        LastState = state;
        beetle = null;
        helpers = null;
        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<TopP1PantokratorState>)AiStrats[idx]).Run(state, world);

        world.Events.Add(0.1f, SpawnBeetle);
        world.Events.Add(0.1f, () => helpers = new TopHelpers(world, BNpcNameId.OmegaBeetle, 32, "TopP1Pantokrator"));
        world.Events.Add(1.0f, AutoAttack);
        world.Events.Add(3.0f, () => beetle?.Cast(ActionId.Pantokrator, animationLock: 2.1f));
        world.Events.Add(8.0f, ApplyInLine);
        for (var pair = 0; pair < FlamePairs; pair++)
        {
            var p = pair;
            world.Events.Add(FirstFlame - FlameCastSeconds + FlameInterval * pair, () => CastFlames(p));
        }
        for (var wave = 0; wave < PuddleWaves; wave++)
            world.Events.Add(FirstPuddles + PuddleInterval * wave, DropPuddles);
        for (var set = 0; set < SetTimes.Length; set++)
        {
            var s = set;
            world.Events.Add(SetTimes[set], () => ResolveSet(s));
        }
        world.Events.Add(38.2f, ClearInLine);
        world.Events.Add(41.9f, () => MarkLasers(state.FirstLasers));
        world.Events.Add(46.8f, () => FireLasers(state.FirstLasers));
        foreach (var time in CleaveTimes)
            world.Events.Add(time, CastCleaves);
        world.Events.Add(50.7f, () => MarkLasers(state.SecondLasers));
        world.Events.Add(55.6f, () => FireLasers(state.SecondLasers));
    }

    public void Tick(float delta, float elapsed) => TurnBeetleToTank(delta);

    private Placement Origin => beetle?.Placement() ?? new Placement(BeetleSpot, MathF.PI);

    private void SpawnBeetle()
    {
        beetle = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.OmegaBeetle,
            NameId: BNpcNameId.OmegaBeetle,
            Level: Level,
            Targetable: true,
            Placement: new Placement(BeetleSpot, MathF.PI)));
        if (beetle is { IsActive: true } && party.Get(PartyRole.MainTank) is { } tank) beetle.SetTarget(tank, follow: false);
    }

    private void TurnBeetleToTank(float delta) => TopBoss.TurnToMainTank(party, beetle, BeetleTurnSpeed, delta);

    private void AutoAttack()
    {
        if (beetle is not { IsActive: true, IsCasting: false, AnimationLock: false } || party.Get(PartyRole.MainTank) is not { } tank || !tank.IsAlive()) return;
        if (Vector3.Distance(beetle.Position, tank.Position) > BeetleAutoAttackReach) return;
        beetle.SetTarget(tank, follow: false);
        beetle.Face(tank.Position);
        beetle.Cast(ActionId.AutoAttackP1, tank, animationLock: 0.1f, animationVariation: (byte)state.Rng.NextInt(3));
    }

    private void ApplyInLine()
    {
        for (var number = 1; number <= 4; number++)
            foreach (var role in state.WithNumber(number))
                party.Get(role)?.AddStatus(InLineStatuses[number - 1]);
    }

    private void ClearInLine()
    {
        foreach (var member in party.ActiveMembers())
            foreach (var inLine in InLineStatuses)
                member.RemoveStatus(inLine);
    }

    private void CastFlames(int pair)
    {
        var action = pair == 0 ? Actions.FlameThrowerFirst : Actions.FlameThrowerRest;
        foreach (var bearing in new[] { state.FlameBearing(pair), state.FlameBearing(pair) + 180f })
            helpers?.Next(Facing(bearing))?.Cast(action);
    }

    private Placement Facing(float bearing) => new(Origin.Position, MathF.PI - bearing * MathF.PI / 180f);

    private void DropPuddles()
    {
        foreach (var member in party.ActiveMembers().Where(m => m.IsAlive()).ToList())
            helpers?.Next(Origin)?.Cast(Actions.BallisticImpact, member.Position);
    }

    // The missiles land first, so a missile target standing in a line takes the stack with the
    // blast's vuln still on.
    private void ResolveSet(int set)
    {
        foreach (var member in Members(state.WithNumber(TopP1PantokratorState.SpreadNumber(set))))
            helpers?.Next(Origin)?.Cast(Actions.GuidedMissileKyrios, member);
        foreach (var member in Members(state.WithNumber(TopP1PantokratorState.StackNumber(set))))
            helpers?.Next(Origin)?.Cast(Actions.CondensedWaveCannonKyrios, member);
    }

    private void MarkLasers(IEnumerable<PartyRole> roles)
    {
        foreach (var member in Members(roles))
            member.AttachLockonVfx(LockonId.WaveCannonKyrios);
    }

    private void FireLasers(IEnumerable<PartyRole> roles)
    {
        foreach (var member in Members(roles))
            helpers?.Next(Origin)?.Cast(Actions.WaveCannonKyrios, member);
    }

    private void CastCleaves()
    {
        foreach (var member in party.Find.FarestN(Origin.Position, 2).ToList())
            helpers?.Next(Origin)?.Cast(Actions.DiffuseWaveCannonKyrios, member);
    }

    private List<SimCharacter> Members(IEnumerable<PartyRole> roles)
        => roles.Select(role => party.Get(role)).OfType<SimCharacter>().Where(member => member.IsAlive()).ToList();

    public MpMessage? BuildReplayStateMessage()
        => LastState is { } s ? new TopP1PantokratorAiReplayStateMessage(s.InLine.List, s.FlameStart, s.Clockwise, s.FirstLasers.ToArray()) : null;

    public object? StartReplay(MpMessage message, int aiIndex, PartyRole myRole, SimWorld replayWorld)
    {
        if (message is not TopP1PantokratorAiReplayStateMessage msg) return null;
        if (TopP1PantokratorState.FromNetworkReplay(replayWorld.Party, msg.InLine, msg.FlameStart, msg.Clockwise, msg.FirstLasers) is not { } shadowState)
            return null;
        ((IScenarioAi<TopP1PantokratorState>)AiStrats[aiIndex]).Run(shadowState, replayWorld);
        return shadowState;
    }
}
