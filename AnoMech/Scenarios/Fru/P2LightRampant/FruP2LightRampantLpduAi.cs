using System;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Fru.FruConstants;
using static AnoMech.Scenarios.Fru.P2LightRampant.FruP2LightRampantClock;
using static AnoMech.Scenarios.Fru.P2LightRampant.FruP2LightRampantState;

namespace AnoMech.Scenarios.Fru.P2LightRampant;

public sealed class FruP2LightRampantLpduAi : IScenarioAi<FruP2LightRampantState>
{
    public string Name => "EU · LPDU";
    public string Group => "EU";

    private const float CongaRadius = 5.5f;
    private const float HexagonRadius = 5f;
    private const float TowerSpotRadius = 17f;
    private const float BaitRadius = 8.5f;
    private const float BaitStep = 30f;
    private const float StackRadius = 19f;
    private const float TuckRadius = 3.5f;
    private const float TowerWaitRadius = 6.5f;
    private const float BaitersLeaveTheirLastPuddleBy = 24f;

    private static readonly PartyRole[] SupportsWestToEast = [PartyRole.RegenHealer, PartyRole.ShieldHealer, PartyRole.MainTank, PartyRole.OffTank];
    private static readonly PartyRole[] DpsWestToEast = [PartyRole.PhysRangedDps, PartyRole.CasterDps, PartyRole.MeleeDpsA, PartyRole.MeleeDpsB];
    private static readonly PartyRole[] RingClockwiseFromWest = [.. SupportsWestToEast, .. DpsWestToEast.Reverse()];
    private static readonly float[] RingDegrees = [295f, 340f, 25f, 70f, 115f, 160f, 205f, 250f];
    private static readonly int[] HexagonTowers = [2, 1, 0, 5, 4, 3];
    private static readonly float[] TowerDegrees = [300f, 180f, 60f, 240f, 0f, 120f];

    private FruP2LightRampantState state = null!;
    private SimWorld world = null!;

    public void Run(FruP2LightRampantState stateParam, SimWorld worldParam)
    {
        state = stateParam;
        world = worldParam;
        var ai = new AiManager(world);
        ai.Move(3f, () => ByRole(CongaSpot), arrivalTime: 8.75f);
        ai.Move(9.75f, () => ByRole(role => state.BaitsHammer(role) ? BaitSpot(role, 0) : null), arrivalTime: HammerSnapshotAt(0));
        ai.Move(9.75f, () => ByRole(role => state.BaitsHammer(role) ? null : HexagonSpot(role)), arrivalTime: 12.3f);
        ai.Move(12.3f, () => ByRole(role => state.BaitsHammer(role) ? null : TowerSpot(role)), jitter: 0f, arrivalTime: 15.5f);
        for (var wave = 0; wave < HammerWaves - 1; wave++)
        {
            var dropped = wave + 1;
            ai.Move(HammerSnapshotAt(wave) + 0.05f, () => ByRole(role => state.BaitsHammer(role) ? BaitSpot(role, dropped) : null), jitter: 0f,
                arrivalTime: HammerSnapshotAt(wave + 1));
        }
        ai.Move(HammerSnapshotAt(HammerWaves - 1) + 0.05f, () => ByRole(role => state.BaitsHammer(role) ? StackSpot(role) : null), arrivalTime: BaitersLeaveTheirLastPuddleBy);
        ai.Move(19.1f, () => ByRole(role => state.BaitsHammer(role) ? null : HalfwayRoundTheRim(role)), arrivalTime: 22.4f);
        ai.Move(22.4f, () => ByRole(role => state.BaitsHammer(role) ? null : StackSpot(role)), arrivalTime: PowerfulLightAt);
        ai.Move(27.2f, () => ByRole(OwnSpokeIntoTheFirstOrbOrShortOfTheSecond), jitter: 0f, arrivalTime: 30f);
        ai.Move(30.15f, () => ByRole(role => Stacks(role) == 2 ? Vector3.Zero : OwnSpokeClearOfTheLastPuddles(role)), arrivalTime: 33.8f);
        ai.Move(33.85f, () => ByRole(role => state.Pairs ? PairSpot(role) : Spot(role)), arrivalTime: 37.1f);
        ai.Move(37.15f, () => ByRole(role => Spot(role)), arrivalTime: 45.9f);
    }

    private static float HammerSnapshotAt(int wave) => HammerAt(wave) - FruHolyPuddles.SnapshotBeforeHit;

    private int Stacks(PartyRole role) => world.Party.Get(role)?.FindStatus(StatusId.Lightsteeped)?.Stacks ?? 0;

    private static Vector3? CongaSpot(PartyRole role) => Spot(RingDegrees[Array.IndexOf(RingClockwiseFromWest, role)], CongaRadius);

    private int HexagonSlot(PartyRole role)
    {
        var chained = RingClockwiseFromWest.Where(r => !state.BaitsHammer(r)).ToList();
        var shift = state.HammerBaiters.All(r => !r.IsDps()) ? 1 : 0;
        return (chained.IndexOf(role) + shift) % 6;
    }

    private Vector3? HexagonSpot(PartyRole role) => Spot(300f + 60f * HexagonSlot(role), HexagonRadius);

    private int TowerOf(PartyRole role) => HexagonTowers[HexagonSlot(role)];

    private Vector3? TowerSpot(PartyRole role) => Tower(TowerOf(role)) * (TowerSpotRadius / 16f);

    private bool NorthTower(PartyRole role) => TowerOf(role) is 0 or 2 or 4;

    private bool BaitsNorth(PartyRole role)
    {
        var other = state.HammerBaiters.Single(r => r != role);
        if (role.IsDps() != other.IsDps()) return !role.IsDps();
        var line = role.IsDps() ? DpsWestToEast : SupportsWestToEast;
        return Array.IndexOf(line, role) < Array.IndexOf(line, other);
    }

    private Vector3? BaitSpot(PartyRole role, int dropped) => Spot((BaitsNorth(role) ? 0f : 180f) + BaitStep * dropped, BaitRadius);

    private bool NorthStack(PartyRole role) => state.BaitsHammer(role) ? !BaitsNorth(role) : NorthTower(role);

    private float StackDegreesBesideTheBaitersLastPuddle(PartyRole role) => (NorthStack(role), state.NorthOrbsFirst) switch
    {
        (true, true) => 305f,
        (true, false) => 342f,
        (false, true) => 165f,
        _ => 125f,
    };

    private Vector3? StackSpot(PartyRole role) => Spot(StackDegreesBesideTheBaitersLastPuddle(role), StackRadius);

    private Vector3? HalfwayRoundTheRim(PartyRole role)
    {
        var tower = TowerDegrees[TowerOf(role)];
        var turn = (StackDegreesBesideTheBaitersLastPuddle(role) - tower + 540f) % 360f - 180f;
        return Spot(tower + turn / 2f, StackRadius);
    }

    private Vector3? OwnSpokeClearOfTheLastPuddles(PartyRole role) => Spot(NorthStack(role) ? 0f : 180f, TowerWaitRadius);

    private Vector3? OwnSpokeIntoTheFirstOrbOrShortOfTheSecond(PartyRole role)
        => Spot(NorthStack(role) ? 0f : 180f, NorthStack(role) == state.NorthOrbsFirst ? StackRadius : TuckRadius);

    private static IAiMove ByRole(Func<PartyRole, Vector3?> at)
        => AiMove.Create(Enum.GetValues<PartyRole>().Select(role => at(role) is { } p ? new Vector2(p.X, p.Z) : (Vector2?)null).ToArray()).NaturalOrder();
}
