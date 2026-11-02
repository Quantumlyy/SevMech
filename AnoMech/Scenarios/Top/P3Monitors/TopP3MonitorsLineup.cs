using System.Numerics;
using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios.Top.P3Monitors;

// A strat's layout: the line the party forms as the monitors load, then a spot per monitor
// holder and per other player, each taken in line order from the north. Spots are for Omega's
// monitor firing east and mirror in x for west. CleaveOf is the direction a holder's monitor
// fires, in degrees clockwise from north.
public interface ITopP3MonitorsLineup
{
    string Name { get; }
    PartyRole[] NorthToSouth { get; }
    Vector2 LineSpot(int index);
    Vector2 MonitorSpot(int monitor);
    Vector2 OtherSpot(int other);
    float CleaveOf(int monitor, CleaveSide bossSide);
}

public sealed class TopP3MonitorsNaLineup : ITopP3MonitorsLineup
{
    public string Name => "NA";

    public PartyRole[] NorthToSouth { get; } =
    [
        PartyRole.RegenHealer, PartyRole.CasterDps, PartyRole.MeleeDpsA, PartyRole.MainTank,
        PartyRole.OffTank, PartyRole.MeleeDpsB, PartyRole.PhysRangedDps, PartyRole.ShieldHealer,
    ];

    private static readonly Vector2[] MonitorSpots = [new(9.5f, -8.4f), new(9.5f, 8.4f), new(-9.9f, 11.6f)];
    private static readonly Vector2[] OtherSpots = [new(-1.4f, -18.3f), new(-8f, -12f), new(-16.5f, -4.2f), new(-16.8f, 5f), new(-1.7f, 18.3f)];

    public Vector2 LineSpot(int index) => new(-12.8f, -9.8f + 2.8f * index);
    public Vector2 MonitorSpot(int monitor) => MonitorSpots[monitor];
    public Vector2 OtherSpot(int other) => OtherSpots[other];

    public float CleaveOf(int monitor, CleaveSide bossSide) => monitor switch
    {
        0 => 0f,
        1 => 180f,
        _ => bossSide == CleaveSide.Left ? 90f : 270f,
    };
}

public sealed class TopP3MonitorsLpduLineup : ITopP3MonitorsLineup
{
    public string Name => "LPDU";

    public PartyRole[] NorthToSouth { get; } =
    [
        PartyRole.RegenHealer, PartyRole.ShieldHealer, PartyRole.MainTank, PartyRole.OffTank,
        PartyRole.MeleeDpsA, PartyRole.MeleeDpsB, PartyRole.PhysRangedDps, PartyRole.CasterDps,
    ];

    private static readonly Vector2[] LineSpots =
    [
        new(-8.4f, -10f), new(-10.5f, -7.6f), new(-11.9f, -4.8f), new(-12.7f, -1.6f),
        new(-12.3f, 2f), new(-11.3f, 5.4f), new(-9.8f, 8.2f), new(-7.7f, 10.3f),
    ];
    private static readonly Vector2[] MonitorSpots = [new(-9.5f, -12.5f), new(9.7f, -8.4f), new(9.8f, 8.3f)];
    private static readonly Vector2[] OtherSpots = [new(-2.5f, -18f), new(-12.5f, -4.5f), new(-12.5f, 4.5f), new(-6f, 10.5f), new(-2.5f, 18f)];

    public Vector2 LineSpot(int index) => LineSpots[index];
    public Vector2 MonitorSpot(int monitor) => MonitorSpots[monitor];
    public Vector2 OtherSpot(int other) => OtherSpots[other];

    public float CleaveOf(int monitor, CleaveSide bossSide) => monitor switch
    {
        0 => bossSide == CleaveSide.Left ? 90f : 270f,
        1 => 0f,
        _ => 180f,
    };
}
