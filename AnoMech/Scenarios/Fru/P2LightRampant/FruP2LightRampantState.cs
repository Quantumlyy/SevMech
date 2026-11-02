using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios.Fru.P2LightRampant;

// Two Luminous Hammer baiters; the other six chained in a loop that takes the six towers, two of
// them neighbours on the loop marked for Powerful Light; two waves of orbs, north or south first;
// Banish as pairs or spreads.
//
// The chains link each of the six to their neighbours around the boss, so the loop is the ring the
// party stands in when they go out, counted clockwise from north; WeightSlot picks a spot on it.
public sealed class FruP2LightRampantState
{
    public const float CursesAt = 18.75f;
    public const float TowerAt = 19f;
    public const float PowerfulLightAt = 25f;
    public const float CursesEndAt = 27.8f;
    public static float HammerAt(int wave) => 15.85f + 1.58f * wave;
    public const int HammerWaves = 5;
    public const float TowerRadius = 4f;

    private static readonly PartyRole[] PuddlePriority =
        [PartyRole.PhysRangedDps, PartyRole.RegenHealer, PartyRole.CasterDps, PartyRole.ShieldHealer,
         PartyRole.MeleeDpsA, PartyRole.MainTank, PartyRole.MeleeDpsB, PartyRole.OffTank];

    public IReadOnlyList<PartyRole> HammerBaiters { get; }
    public IReadOnlyList<PartyRole> InitiallySteeped { get; }
    public int WeightSlot { get; }
    public bool NorthOrbsFirst { get; }
    public bool Pairs { get; }
    public bool SupportPairs { get; }

    public FruP2LightRampantState(Rng rng, PartyRole playerRole, FruP2LightRampantStateOverrides overrides)
    {
        var roles = rng.Shuffle(Enum.GetValues<PartyRole>());
        var duties = overrides.Duty.Resolve(playerRole);
        var baits = duties.Where(d => d.Value == LightRampantDuty.Bait).Select(d => d.Role);
        var tethers = duties.Where(d => d.Value == LightRampantDuty.Tether).Select(d => d.Role).ToList();
        HammerBaiters = baits.Concat(roles.Where(r => !tethers.Contains(r))).Concat(roles).Distinct().Take(2)
                             .OrderBy(r => Array.IndexOf(PuddlePriority, r)).ToArray();
        InitiallySteeped = overrides.InitiallySteeped ?? rng.Shuffle(Enum.GetValues<PartyRole>()).Take(4).ToList();
        WeightSlot = overrides.WeightSlot ?? rng.Next(6);
        NorthOrbsFirst = overrides.NorthOrbsFirst ?? rng.NextBool();
        Pairs = overrides.Pairs ?? rng.NextBool();
        SupportPairs = overrides.SupportPairs ?? rng.NextBool();
    }

    public bool BaitsHammer(PartyRole role) => HammerBaiters.Contains(role);

    public int InitialStacks(PartyRole role) => (InitiallySteeped.Contains(role) ? 1 : 0) + (BaitsHammer(role) ? 1 : 0);

    public static Vector3 Tower(int index) => index switch
    {
        0 => new(-8 * MathF.Sqrt(3), 0, -8),
        1 => new(0, 0, 16),
        2 => new(8 * MathF.Sqrt(3), 0, -8),
        3 => new(-8 * MathF.Sqrt(3), 0, 8),
        4 => new(0, 0, -16),
        _ => new(8 * MathF.Sqrt(3), 0, 8),
    };

    public Vector3[] Orbs(bool first)
        => first == NorthOrbsFirst ? [Tower(4), Tower(5), Tower(3)] : [Tower(1), Tower(2), Tower(0)];
}
