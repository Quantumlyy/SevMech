using System;
using System.Numerics;
using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios.Fru.P2LightRampant;

// P2's clock spots: tanks north and east, healers west and south, melee on the southern
// intercardinals, ranged on the northern ones. Banish pairs put each support on a DPS's spot.
public static class FruP2LightRampantClock
{
    private const float Radius = 7.6f;

    public static Vector3 Spot(PartyRole role) => role switch
    {
        PartyRole.MainTank => Spot(0f),
        PartyRole.OffTank => Spot(90f),
        PartyRole.RegenHealer => Spot(270f),
        PartyRole.ShieldHealer => Spot(180f),
        PartyRole.MeleeDpsA => Spot(225f),
        PartyRole.MeleeDpsB => Spot(135f),
        PartyRole.PhysRangedDps => Spot(315f),
        _ => Spot(45f),
    };

    public static Vector3 PairSpot(PartyRole role) => Spot(role switch
    {
        PartyRole.MainTank => PartyRole.PhysRangedDps,
        PartyRole.OffTank => PartyRole.CasterDps,
        PartyRole.RegenHealer => PartyRole.MeleeDpsA,
        PartyRole.ShieldHealer => PartyRole.MeleeDpsB,
        _ => role,
    });

    private static Vector3 Spot(float degrees) => Spot(degrees, Radius);

    // Degrees clockwise from north.
    public static Vector3 Spot(float degrees, float radius)
        => new(MathF.Sin(degrees * MathF.PI / 180f) * radius, 0, -MathF.Cos(degrees * MathF.PI / 180f) * radius);
}
