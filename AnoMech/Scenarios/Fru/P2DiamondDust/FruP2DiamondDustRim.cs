using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Party;
using static AnoMech.Scenarios.Fru.P2DiamondDust.FruP2DiamondDustAi;
using static AnoMech.Scenarios.Fru.P2DiamondDust.FruP2DiamondDustState;

namespace AnoMech.Scenarios.Fru.P2DiamondDust;

// A strat's rim work, in octants clockwise from north: the spoke each light party takes Heavenly
// Strike along and lands on clear of the Frigid Needles, where it stands for each Sinbound Holy,
// and where it waits for the ice.
public interface IFruP2DiamondDustRim
{
    string Name { get; }
    string Group { get; }
    float LandingOctant(FruP2DiamondDustState state, PartyRole role);
    float HolyOctant(FruP2DiamondDustState state, PartyRole role, int step);
    float IceOctant(FruP2DiamondDustState state, PartyRole role);
}

public sealed class FruP2DiamondDustNaRim : IFruP2DiamondDustRim
{
    public string Name => "NA · Partner Swap";
    public string Group => "NA";

    private const float NeedleCircleRadius = 5f;
    private const float NeedleLineHalfWidth = 2.5f;
    private const float EdgeSearchOctants = 25f / 45f;

    public float LandingOctant(FruP2DiamondDustState state, PartyRole role)
    {
        var holyOctant = KnockbackOctant(state, role) + (Cursed(state) ? 1f / 3 : 0);
        var stars = Enum.GetValues<PartyRole>().Where(state.Marked).Select(r => StoneSpot(state, r)).ToList();
        return Enumerable.Range(-50, 101)
            .Select(step => holyOctant + step / 50f * EdgeSearchOctants)
            .OrderByDescending(octant => NeedleClearance(Point(octant, EdgeRadius), stars))
            .ThenBy(octant => MathF.Abs(octant - holyOctant))
            .First();
    }

    public float HolyOctant(FruP2DiamondDustState state, PartyRole role, int step)
        => KnockbackOctant(state, role) + (Cursed(state) ? 1f / 3 : 0) + RotationAroundRim(state, role) * step * 0.5f;

    public float IceOctant(FruP2DiamondDustState state, PartyRole role) => HolyOctant(state, role, Cursed(state) ? 5 : 4);

    private static bool Cursed(FruP2DiamondDustState state) => (state.MirrorOctant - state.FirstIcicle + 8) % 4 == 0;

    private static int RotationAroundRim(FruP2DiamondDustState state, PartyRole role)
    {
        if (Cursed(state)) return 1;
        var clockwiseDestination = Point(KnockbackOctant(state, role) + 2, 1);
        return Vector3.Dot(clockwiseDestination, state.MirrorPosition) < 0 ? 1 : -1;
    }

    private static float NeedleClearance(Vector3 spot, List<Vector3> stars)
    {
        var clearance = float.PositiveInfinity;
        foreach (var star in stars)
        {
            var offset = spot - star;
            clearance = MathF.Min(clearance, offset.Length() - NeedleCircleRadius);
            for (var line = 0; line < 4; line++)
            {
                var along = Point(line, 1);
                var across = MathF.Abs(offset.X * along.Z - offset.Z * along.X);
                clearance = MathF.Min(clearance, across - NeedleLineHalfWidth);
            }
        }
        return clearance;
    }
}

// Each light party lands on its first-icicle spoke and walks the rim clockwise, or counterclockwise
// when the Mirror stands one octant clockwise of it, then on to whichever of behind and opposite
// the Mirror comes first.
public sealed class FruP2DiamondDustLpduRim : IFruP2DiamondDustRim
{
    public string Name => "EU · LPDU";
    public string Group => "EU";

    public float LandingOctant(FruP2DiamondDustState state, PartyRole role) => KnockbackOctant(state, role);

    public float HolyOctant(FruP2DiamondDustState state, PartyRole role, int step)
        => KnockbackOctant(state, role) + Rotation(state, role) * step * 0.5f;

    public float IceOctant(FruP2DiamondDustState state, PartyRole role)
        => KnockbackOctant(state, role) + Rotation(state, role) * OctantsWalkedPastThePuddles(state, role);

    private static int MirrorOffset(FruP2DiamondDustState state, PartyRole role)
        => (state.MirrorOctant - KnockbackOctant(state, role) + 8) % 8;

    private static int Rotation(FruP2DiamondDustState state, PartyRole role) => MirrorOffset(state, role) == 1 ? -1 : 1;

    // A counterclockwise party meets the other in the gap between the two puddle trails. With the
    // Mirror on the party's own spoke, the party stops where a slide still reaches behind it.
    private static float OctantsWalkedPastThePuddles(FruP2DiamondDustState state, PartyRole role)
    {
        if (Rotation(state, role) < 0) return 2f;
        var toBehind = MirrorOffset(state, role);
        var toOpposite = (toBehind + 4) % 8;
        return Math.Min(toBehind, toOpposite) switch
        {
            0 => 2.5f,
            3 => 3f,
            _ => 2f,
        };
    }
}
