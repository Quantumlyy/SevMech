using System;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Fru.FruConstants;
using static AnoMech.Scenarios.Fru.P2LightRampant.FruP2LightRampantState;

namespace AnoMech.Scenarios.Fru.P2LightRampant;

public sealed class FruP2LightRampantAi : IScenarioAi<FruP2LightRampantState>
{
    public string Name => "NA · Conga";
    public string Group => "NA";

    private const float S = 1f / 2.358f;

    private FruP2LightRampantState state = null!;
    private SimWorld world = null!;
    private PartyRole[] conga = [];

    public void Run(FruP2LightRampantState stateParam, SimWorld worldParam)
    {
        state = stateParam;
        world = worldParam;
        conga = CongaByPriorityThenBaiters();
        var ai = new AiManager(world);
        ai.Move(3f, () => ByRole(Preposition), arrivalTime: 8.75f);
        ai.Move(9.75f, () => ByRole(role => state.BaitsHammer(role) ? Lineup(role) : null), arrivalTime: HammerSnapshotAt(0));
        ai.Move(10.75f, () => ByRole(role => state.BaitsHammer(role) ? null : Lineup(role)), arrivalTime: 12.3f);
        ai.Move(12.3f, () => ByRole(role => state.BaitsHammer(role) ? null : Tower(Index(role))), jitter: 0f, arrivalTime: CursesAt);
        for (var wave = 0; wave < HammerWaves - 1; wave++)
        {
            var dropped = wave + 1;
            ai.Move(HammerSnapshotAt(wave) + 0.05f, () => ByRole(role => state.BaitsHammer(role) ? HammerSpot(role, dropped) : null), jitter: 0f,
                arrivalTime: HammerSnapshotAt(wave + 1));
        }
        ai.Move(HammerSnapshotAt(HammerWaves - 1) + 0.05f, () => ByRole(role => state.BaitsHammer(role) ? Intermediate(role) : null),
            arrivalTime: PowerfulLightAt);
        ai.Move(19.1f, () => ByRole(role => state.BaitsHammer(role) ? null : GroupSpot(role)), arrivalTime: 23.3f);
        ai.Move(23.3f, () => ByRole(role => state.BaitsHammer(role) ? null : Intermediate(role)), arrivalTime: PowerfulLightAt);
        ai.Move(PowerfulLightAt + 0.05f, () => ByRole(role => OrbSafeSpot(role, first: true)), jitter: 0f, arrivalTime: 27.1f);
        ai.Move(27.2f, () => ByRole(role => OrbSafeSpot(role, first: false)), jitter: 0f, arrivalTime: 30.1f);
        ai.Move(30.15f, () => ByRole(role => Stacks(role) == 2 ? Vector3.Zero : MiddleWait(role)), arrivalTime: 33.8f);
        ai.Move(33.85f, () => ByRole(BanishSpot), arrivalTime: 37.1f);
        ai.Move(37.15f, () => ByRole(ClockSpot), arrivalTime: 45.9f);
    }

    private static float HammerSnapshotAt(int wave) => HammerAt(wave) - FruHolyPuddles.SnapshotBeforeHit;

    private PartyRole[] CongaByPriorityThenBaiters()
    {
        var north = new[] { PartyRole.OffTank, PartyRole.MainTank, PartyRole.ShieldHealer, PartyRole.RegenHealer }.Where(r => !state.BaitsHammer(r)).ToList();
        var south = new[] { PartyRole.PhysRangedDps, PartyRole.CasterDps, PartyRole.MeleeDpsA, PartyRole.MeleeDpsB }.Where(r => !state.BaitsHammer(r)).ToList();
        if (north.Count == 4) { south.Add(north[0]); north.RemoveAt(0); }
        if (south.Count == 4) { north.Add(south[0]); south.RemoveAt(0); }
        return [.. north, .. south, .. state.HammerBaiters];
    }

    private int Index(PartyRole role) => Array.IndexOf(conga, role);
    private bool NorthGroup(PartyRole role) => Index(role) % 2 == 0;

    private int Stacks(PartyRole role) => world.Party.Get(role)?.FindStatus(StatusId.Lightsteeped)?.Stacks ?? 0;

    private static Vector3 FromSim(float north, float east) => new(east * S, 0, -north * S);

    private static Vector3? Preposition(PartyRole role) => role switch
    {
        PartyRole.MainTank => FromSim(14.5f, 4.5f),
        PartyRole.OffTank => FromSim(11, 11),
        PartyRole.RegenHealer => FromSim(11, -11),
        PartyRole.ShieldHealer => FromSim(14.5f, -4.5f),
        PartyRole.MeleeDpsA => FromSim(-14.5f, 4.5f),
        PartyRole.MeleeDpsB => FromSim(-11, 11),
        PartyRole.PhysRangedDps => FromSim(-11, -11),
        _ => FromSim(-14.5f, -4.5f),
    };

    private Vector3? Lineup(PartyRole role) => Index(role) switch
    {
        0 => FromSim(7, 13.7f),
        1 => FromSim(16.1f, 0),
        2 => FromSim(7, -13.7f),
        3 => FromSim(-7, -13.7f),
        4 => FromSim(-16.1f, 0),
        5 => FromSim(-7, 13.7f),
        6 => FromSim(0, -45),
        _ => FromSim(0, 45),
    };

    private Vector3? HammerSpot(PartyRole role, int dropped)
    {
        var spot = dropped switch
        {
            1 => FromSim(0, -30),
            2 => FromSim(0, -15),
            3 => FromSim(15, -15),
            _ => FromSim(30, -15),
        };
        return Index(role) == 6 ? spot : -spot;
    }

    private Vector3? GroupSpot(PartyRole role) => FromSim(NorthGroup(role) ? 45 : -45, 0);

    private Vector3? Intermediate(PartyRole role) => FromSim(43, 10.8f) * (NorthGroup(role) ? 1 : -1);

    private Vector3? OrbSafeSpot(PartyRole role, bool first)
    {
        var wideNorth = first == state.NorthOrbsFirst;
        return NorthGroup(role)
            ? FromSim(wideNorth ? 36.2f : 42, wideNorth ? 26.6f : 16.7f)
            : FromSim(wideNorth ? -42 : -36.2f, wideNorth ? -16.7f : -26.6f);
    }

    private Vector3 MiddleWait(PartyRole role) => NorthGroup(role)
        ? state.NorthOrbsFirst ? FromSim(16.1f, 0) : FromSim(7, 12)
        : state.NorthOrbsFirst ? FromSim(-7, -12) : FromSim(-16.1f, 0);

    private Vector3? BanishSpot(PartyRole role) => state.Pairs ? FruP2LightRampantClock.PairSpot(role) : ClockSpot(role);

    private static Vector3? ClockSpot(PartyRole role) => FruP2LightRampantClock.Spot(role);

    private static IAiMove ByRole(Func<PartyRole, Vector3?> at)
        => AiMove.Create(Enum.GetValues<PartyRole>().Select(role => at(role) is { } p ? new Vector2(p.X, p.Z) : (Vector2?)null).ToArray()).NaturalOrder();
}
