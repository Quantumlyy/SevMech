using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Top.P1Pantokrator;

public sealed class TopP1PantokratorAi : IScenarioAi<TopP1PantokratorState>
{
    public string Name => "LPDU";

    private const float MovePrompt = 0.3f;
    private const float LookEvery = 0.25f;
    private const float Lookahead = 0.8f;
    private const float Precise = 0f;
    private const float DebuffsLand = 8f;
    private const float FirstFlameSeen = 10.1f;
    private const float FirstFlame = 13.8f;
    private const float FlameInterval = 2f;
    private const int FlamePairs = 14;
    private const float FlamesEnd = 39.8f;
    private const float FirstPuddles = 14.5f;
    private static readonly float[] SetTimes = [20.1f, 26.1f, 32.1f, 38.1f];
    private const float GatherRadius = 7f;
    private const float GatherSpacing = 12f;
    private const float LineBehindCentre = 10f;
    private static readonly float[] LaneRadii = [8.5f, 11f, 13.5f, 16f];
    private const float MissileRadius = 18.5f;
    private const float MissileAhead = 18f;
    private const float PuddleCast = 2.7f;
    private const float PuddleReach = 5.6f;
    private const float PuddleInterval = 3f;
    private const int PuddleWaves = 8;
    private static readonly float[] NudgeRings = [1.5f, 3f, 4.5f, 6f, 7.5f];
    private const float InnerLimit = 4f;
    private const float WallReach = 19f;
    private const float TankRadius = 18f;
    private const float LaserRadius = 13f;
    private static readonly float[] LaserBearings = [70f, 90f, 110f];
    private static readonly int[] Groups = [0, 1];

    private static readonly PartyRole[][] LightParties =
    [
        [PartyRole.MainTank, PartyRole.MeleeDpsA, PartyRole.PhysRangedDps, PartyRole.RegenHealer],
        [PartyRole.OffTank, PartyRole.MeleeDpsB, PartyRole.CasterDps, PartyRole.ShieldHealer],
    ];

    private TopP1PantokratorState state = null!;
    private SimWorld world = null!;
    private AiManager ai = null!;
    private readonly Dictionary<PartyRole, int> groupOf = new();
    private readonly List<(Vector2 At, float Resolves)> puddles = new();
    private float safeSide;
    private int turn;

    public void Run(TopP1PantokratorState s, SimWorld simWorld)
    {
        state = s;
        world = simWorld;
        ai = new AiManager(world);
        puddles.Clear();
        FlexTheLowerPriority();
        turn = state.Clockwise ? 1 : -1;
        safeSide = SideNearestNorthElseClockwise();
        At(MovePrompt, Look);
        for (var wave = 0; wave < PuddleWaves; wave++)
            At(FirstPuddles + PuddleInterval * wave, RememberPuddles);
        At(FlamesEnd, SpreadForTheLasers);
    }

    private void RememberPuddles()
    {
        var now = world.Events.Elapsed;
        puddles.RemoveAll(puddle => puddle.Resolves < now);
        foreach (var role in PerRole.All)
            if (world.Party.Get(role) is { } member && member.IsAlive())
                puddles.Add((Live(role), now + PuddleCast));
    }

    private void FlexTheLowerPriority()
    {
        groupOf.Clear();
        foreach (var group in Groups)
            foreach (var role in LightParties[group])
                groupOf[role] = group;
        for (var number = 1; number <= 4; number++)
        {
            var pair = state.WithNumber(number).ToList();
            if (groupOf[pair[0]] != groupOf[pair[1]]) continue;
            var flexer = pair.MaxBy(role => Priority(role, groupOf[role]));
            groupOf[flexer] = 1 - groupOf[flexer];
        }
    }

    private float SideNearestNorthElseClockwise()
    {
        var cone = state.FlameBearing(0);
        var clockwise = TopCompass.Turn(0f, cone + 90f);
        var counter = TopCompass.Turn(0f, cone - 90f);
        if (MathF.Abs(MathF.Abs(clockwise) - MathF.Abs(counter)) < 1f) return clockwise > 0f ? 90f : -90f;
        return MathF.Abs(clockwise) < MathF.Abs(counter) ? 90f : -90f;
    }

    private float SafeBearing(float time)
    {
        var pair = Math.Clamp((time - FirstFlame) / FlameInterval, 0f, FlamePairs - 1);
        return state.FlameBearing(0) + turn * TopP1PantokratorState.FlameStepDegrees * pair + safeSide;
    }

    private void Look()
    {
        var now = world.Events.Elapsed;
        if (now >= FlamesEnd) return;
        foreach (var role in PerRole.All)
            if (Drives(role)) MoveTo(role, AroundThePuddles(role, Spot(role, now), now));
        world.Events.Add(LookEvery, Look);
    }

    private Vector2 AroundThePuddles(PartyRole role, Vector2 goal, float now)
    {
        var live = puddles.Where(puddle => puddle.Resolves >= now).OrderBy(puddle => puddle.Resolves).ToList();
        foreach (var (at, resolves) in live)
        {
            var planned = Spot(role, resolves);
            if (Vector2.Distance(planned, at) >= PuddleReach) continue;
            var clear = ClearOfPuddles(planned, live.Where(puddle => puddle.Resolves >= resolves).Select(puddle => puddle.At).ToList());
            return goal + (clear - planned);
        }
        return goal;
    }

    private static Vector2 ClearOfPuddles(Vector2 goal, IReadOnlyList<Vector2> live)
    {
        if (Clear(goal, live)) return goal;
        var radial = Vector2.Normalize(goal);
        var tangent = new Vector2(-radial.Y, radial.X);
        foreach (var ring in NudgeRings)
            foreach (var direction in new[] { radial, -radial, tangent, -tangent, radial + tangent, radial - tangent, -radial + tangent, -radial - tangent })
            {
                var spot = goal + Vector2.Normalize(direction) * ring;
                if (spot.Length() is < InnerLimit or > WallReach) continue;
                if (Clear(spot, live)) return spot;
            }
        return goal;
    }

    private static bool Clear(Vector2 spot, IReadOnlyList<Vector2> live) => live.All(puddle => Vector2.Distance(spot, puddle) >= PuddleReach);

    private float LineBearing(PartyRole role, float now) => SafeBearing(now + Lookahead) + 180f * groupOf[role] - turn * LineBehindCentre;

    private Vector2 Spot(PartyRole role, float now)
    {
        if (now < DebuffsLand) return GatherSpot(LightPartyOf(role), Array.IndexOf(LightParties[LightPartyOf(role)], role));
        var group = groupOf[role];
        if (now < FirstFlameSeen) return GatherSpot(group, Rank(role));
        var line = LineBearing(role, now);
        var set = SetTimes.Count(time => time <= now);
        if (set < SetTimes.Length && state.NumberOf(role) == TopP1PantokratorState.SpreadNumber(set))
            return TopCompass.Point(MissileRadius, line + turn * MissileAhead);
        return TopCompass.Point(LaneRadii[Rank(role)], line);
    }

    private static Vector2 GatherSpot(int group, int rank)
        => TopCompass.Point(GatherRadius, 180f * group + (rank - 1.5f) * GatherSpacing);

    private void SpreadForTheLasers()
    {
        foreach (var group in Groups)
        {
            var members = Members(group);
            var others = members.Where(role => !IsTank(role)).ToList();
            foreach (var role in members.Where(Drives))
                MoveTo(role, IsTank(role)
                    ? TopCompass.Point(TankRadius, 180f * group)
                    : TopCompass.Point(LaserRadius, LaserBearings[others.IndexOf(role)] + 180f * group));
        }
    }

    private static bool IsTank(PartyRole role) => role is PartyRole.MainTank or PartyRole.OffTank;

    private List<PartyRole> Members(int group)
        => PerRole.All.Where(role => groupOf[role] == group).OrderBy(role => Priority(role, group)).ToList();

    private int Rank(PartyRole role) => Members(groupOf[role]).IndexOf(role);

    private static int Priority(PartyRole role, int group)
    {
        var own = Array.IndexOf(LightParties[group], role);
        return own >= 0 ? own : LightParties[group].Length + Array.IndexOf(LightParties[1 - group], role);
    }

    private static int LightPartyOf(PartyRole role) => Array.IndexOf(LightParties[0], role) >= 0 ? 0 : 1;

    private bool Drives(PartyRole role) => world.Party.Get(role) is { } member && member.IsAlive() && world.Party.IsBotDriven(member);

    private Vector2 Live(PartyRole role) => world.Party.Get(role) is { } member ? new Vector2(member.Position.X, member.Position.Z) : Vector2.Zero;

    private void At(float time, Action action) => world.Events.Add(time, action);

    private void MoveTo(PartyRole role, Vector2 spot) => ai.Move(MovePrompt, () => AiMove.Single(role, spot), jitter: Precise);
}
