using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Fru.P2DiamondDust.FruP2DiamondDustState;

namespace AnoMech.Scenarios.Fru.P2DiamondDust;

public sealed class FruP2DiamondDustAi(IFruP2DiamondDustRim rim) : IScenarioAi<FruP2DiamondDustState>
{
    public string Name => rim.Name;
    public string Group => rim.Group;

    public const float EdgeRadius = 19f;

    private static readonly int[] Clocks = [0, 2, 6, 4, 5, 3, 7, 1];
    private const float SlideDistance = 32f;
    private const float SlideSeconds = 1f;
    private const float PuddleRadius = 6f;
    private const float HolyRadius = 18f;
    private const float IceRadius = 17f;
    private const int SlideSearchSteps = 720;
    private const int NextSlideSearchSteps = 180;
    private const float FirstSlideAt = 45.05f;
    private const float SecondSlideAt = 47.4f;

    private FruP2DiamondDustState state = null!;
    private SimWorld world = null!;
    private readonly List<(int Wave, Vector3 Position)> puddles = [];

    public void Run(FruP2DiamondDustState stateParam, SimWorld worldParam)
    {
        state = stateParam;
        world = worldParam;
        puddles.Clear();
        var ai = new AiManager(world);
        ai.Move(15.3f, () => ByRole(KickSpot), arrivalTime: 20.6f);
        ai.Move(20.7f, () => ByRole(role => StoneSpot(state, role)), arrivalTime: 23.3f);
        ai.Move(23.35f, () => ByRole(role => Point(rim.LandingOctant(state, role), 3)), jitter: 0f, arrivalTime: 27.4f);
        ai.Move(29.05f, () => ByRole(role => Point(rim.LandingOctant(state, role), EdgeRadius)), jitter: 0f, arrivalTime: 30.05f);
        ai.Move(30.1f, () => ByRole(role => HolySpot(role, 0)), arrivalTime: 31.4f);
        for (var wave = 0; wave < HolyWaves; wave++)
        {
            var w = wave;
            world.Events.Add(HolyAt(w) - FruHolyPuddles.SnapshotBeforeHit, () => RememberHolyPuddles(w));
            ai.Move(HolyAt(w), () => ByRole(role => HolySpot(role, w + 1)), arrivalTime: HolyAt(w + 1));
        }
        ai.Move(HolyAt(HolyWaves), () => ByRole(role => Point(rim.IceOctant(state, role), IceRadius)), arrivalTime: 39.9f);
        world.Events.Add(39.9f, FaceAwayFromGaze);
        world.Events.Add(IceStart + 0.7f, SlideToWhereBehindTheMirrorIsInReach);
        world.Events.Add(FirstSlideAt, () => SlideBots(behind: state.Stillness, FirstSlideAt, nextSlideAt: state.Stillness ? null : SecondSlideAt));
        world.Events.Add(SecondSlideAt, () => SlideBots(behind: !state.Stillness, SecondSlideAt, nextSlideAt: null));
        world.Events.Add(IceEnd, puddles.Clear);
        ai.Move(IceEnd + 0.3f, () => ByRole(_ => new Vector3(0, 0, 4)));
    }

    private static bool GroupOne(PartyRole role) => ((int)role & 1) == 0;

    private static int ClockOctantOfRequiredParity(FruP2DiamondDustState state, PartyRole role)
    {
        var original = Clocks[(int)role];
        var requiredParity = (state.FirstIcicle + (state.Marked(role) ? 1 : 0)) & 1;
        return (original + ((original & 1) == requiredParity ? 0 : (int)role < 4 ? 7 : 1)) % 8;
    }

    private Vector3? KickSpot(PartyRole role) => Point(ClockOctantOfRequiredParity(state, role), (state.Axe ? 16 : 0) + (state.Marked(role) ? 3 : 1));

    public static Vector3 StoneSpot(FruP2DiamondDustState state, PartyRole role)
        => Point(ClockOctantOfRequiredParity(state, role), state.Marked(role) ? state.Axe ? 19 : 8 : state.Axe ? 4 : 1);

    public static int KnockbackOctant(FruP2DiamondDustState state, PartyRole role)
    {
        var groupOne = state.FirstIcicle == 0 ? 0 : state.FirstIcicle + 4;
        return (groupOne + (GroupOne(role) ? 0 : 4)) % 8;
    }

    private Vector3 HolySpot(PartyRole role, int step) => Point(rim.HolyOctant(state, role, step), HolyRadius);

    private void RememberHolyPuddles(int wave)
    {
        foreach (var healer in new[] { PartyRole.RegenHealer, PartyRole.ShieldHealer })
            if (world.Party.Get(healer) is { } member && member.IsAlive()) puddles.Add((wave, member.Position));
    }

    private IEnumerable<Vector3> LivePuddles(float at)
        => puddles.Where(p => PuddleFadesAt(p.Wave) > at).Select(p => p.Position);

    private IEnumerable<SimCharacter> Bots()
        => world.Party.ActiveMembers().Where(world.Party.IsBotDriven).ToList();

    private void FaceAwayFromGaze()
    {
        foreach (var bot in Bots())
        {
            bot.StopMoving();
            bot.Face(2 * bot.Position - state.GazePosition);
        }
    }

    private void SlideToWhereBehindTheMirrorIsInReach()
    {
        var behindAt = state.Stillness ? FirstSlideAt : SecondSlideAt;
        foreach (var bot in Bots())
        {
            if (BehindMirror(bot.Position) || SlideDestination(bot.Position, behind: true, behindAt) is not null) continue;
            if (SlideDestination(bot.Position, behind: false, world.Events.Elapsed, nextSlideAt: behindAt) is { } destination)
                Slide(bot, destination);
        }
    }

    private void Slide(SimCharacter bot, Vector3 destination)
    {
        AnoMech.Core.DiagnosticLog.Info($"[FruP2DiamondDustAi] Slide@{world.Events.Elapsed:F1}: {(bot as ISimPartyMember)?.Role} ({bot.Position.X:F1},{bot.Position.Z:F1}) -> ({destination.X:F1},{destination.Z:F1}).");
        bot.IceSlideTo(destination, SlideSeconds);
    }

    private void SlideBots(bool behind, float at, float? nextSlideAt)
    {
        foreach (var bot in Bots())
        {
            if (BehindMirror(bot.Position) == behind || SlideDestination(bot.Position, behind, at, nextSlideAt) is not { } destination) continue;
            Slide(bot, destination);
        }
    }

    private bool BehindMirror(Vector3 position)
    {
        var offset = position - state.MirrorPosition;
        return offset.LengthSquared() > 0.001f
            && Vector3.Dot(Vector3.Normalize(offset), Vector3.Normalize(state.MirrorPosition)) > MathF.Sqrt(0.5f);
    }

    private Vector3? SlideDestination(Vector3 from, bool behind, float at, float? nextSlideAt = null)
        => BestSlideClearOfPuddlesLiveAt(from, behind, at, nextSlideAt, SlideSearchSteps).End;

    private (Vector3? End, float Margin) BestSlideClearOfPuddlesLiveAt(Vector3 from, bool behind, float at, float? nextSlideAt, int steps)
    {
        Vector3? best = null;
        var bestMargin = float.NegativeInfinity;
        var live = LivePuddles(at).ToList();
        for (var step = 0; step < steps; step++)
        {
            var end = from + Point(step * 8f / steps, SlideDistance);
            if (end.Length() > EdgeRadius || BehindMirror(end) != behind) continue;
            var clearance = live.Select(p => DistanceToSegment(p, from, end) - PuddleRadius).DefaultIfEmpty(20).Min();
            if (clearance < 0.35f) continue;
            var relative = end - state.MirrorPosition;
            var backDot = Vector3.Dot(Vector3.Normalize(relative), Vector3.Normalize(state.MirrorPosition));
            var coneMargin = (behind ? backDot - MathF.Sqrt(0.5f) : MathF.Sqrt(0.5f) - backDot) * relative.Length();
            var margin = MathF.Min(MathF.Min(20 - end.Length(), clearance), coneMargin);
            if (nextSlideAt is { } next) margin = MathF.Min(margin, BestSlideClearOfPuddlesLiveAt(end, true, next, null, NextSlideSearchSteps).Margin);
            if (margin > bestMargin)
            {
                bestMargin = margin;
                best = end;
            }
        }
        return (best, bestMargin);
    }

    private static float DistanceToSegment(Vector3 point, Vector3 from, Vector3 to)
    {
        var line = to - from;
        var t = line.LengthSquared() > 0 ? Math.Clamp(Vector3.Dot(point - from, line) / line.LengthSquared(), 0, 1) : 0;
        return Vector3.Distance(point, from + t * line);
    }

    private static IAiMove ByRole(Func<PartyRole, Vector3?> at)
        => AiMove.Create(Enum.GetValues<PartyRole>().Select(role => at(role) is { } p ? new Vector2(p.X, p.Z) : (Vector2?)null).ToArray()).NaturalOrder();
}
