using AnoMech.Core.Game.Party;
using AnoMech.Scenarios.Top.P3Intermission;
using static AnoMech.Core.Game.Party.PartyRole;
using static AnoMech.Scenarios.Top.TopConstants;
using static AnoMech.Tests.NegativeRun;

namespace AnoMech.Tests;

// Every roll is pinned: the first hands N/SE/SW, spreads on RH C M1 MT, stacks on OT M2, R and SH
// unmarked. In conga order the spreads take the wall at W, NNW, NNE and E, the pairs at SSW (OT R)
// and SSE (M2 SH).
//
//   14.79  Wave Repeater's circle; rings at 17.16, 19.21, 21.27 with everyone at the wall, ducking
//          to 15.4 y for the outer one.
//   26.85  Colossal Blow from N, SE and SW, hugged at the wall 38 degrees off each hand.
//   27.6   everyone tucks into the first hands at 16.2 y, 14 degrees off: RH 254, C 346, M1 14,
//          MT 106, OT R 226, M2 SH 134.
//   29.11  Sniper Cannons go off there; the second hands blow at 29.37.
public class TopP3IntermissionScenarioTests
{
    private static NegativeRun<TopP3IntermissionScenario> Intermission(PartyRole player)
        => Negative<TopP3IntermissionScenario>(player)
            .Overrides<TopP3IntermissionStateOverrides>(o =>
            {
                o.Hands = FirstHands.North;
                foreach (var role in new[] { RegenHealer, CasterDps, MeleeDpsA, MainTank }) o.Debuff[role] = SniperDebuff.Spread;
                foreach (var role in new[] { OffTank, MeleeDpsB }) o.Debuff[role] = SniperDebuff.Stack;
                foreach (var role in new[] { PhysRangedDps, ShieldHealer }) o.Debuff[role] = SniperDebuff.None;
            });

    [Test]
    public void DiesInWaveRepeaterCircle()
        => Intermission(RegenHealer)
            .TeleportAt(14.5f, to: new(0, 3))
            .ShouldKill(ActionId.WaveRepeater1, RegenHealer);

    [Test]
    public void DiesInWaveRepeaterRing()
        => Intermission(RegenHealer)
            .TeleportAt(19f, to: new(10, 10))
            .ShouldKill(ActionId.WaveRepeater3, RegenHealer);

    // Inside the SW hand's circle and under the rings' reach; the spread then goes off on someone.
    [Test]
    public void DiesInColossalBlowAndSpreadJumps()
        => Intermission(RegenHealer)
            .TeleportAt(25.5f, to: new(-8, 4))
            .ShouldKill(ActionId.ColossalBlow, RegenHealer)
            .ShouldKillSomeone(ActionId.SniperCannon);

    // Beside MT's spread, each inside the other's.
    [Test]
    public void OverlappingSpreadsKillBoth()
        => Intermission(RegenHealer)
            .TeleportAt(29f, to: new(16.5f, 6))
            .ShouldKill(ActionId.SniperCannon, RegenHealer, MainTank);

    // Away from R, out of every spread's reach.
    [Test]
    public void StackTakenAloneKillsItsHolder()
        => Intermission(OffTank)
            .TeleportAt(29f, to: new(0, -9))
            .ShouldKill(ActionId.HighPoweredSniperCannon, OffTank);
}
