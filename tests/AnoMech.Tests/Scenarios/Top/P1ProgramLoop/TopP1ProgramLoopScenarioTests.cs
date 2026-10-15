using AnoMech.Core.Game.Party;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Top.P1ProgramLoop;
using static AnoMech.Core.Game.Party.PartyRole;
using static AnoMech.Scenarios.Top.TopConstants;
using static AnoMech.Tests.NegativeRun;

namespace AnoMech.Tests;

// In Line 1 MT OT, 2 RH SH, 3 M1 M2, 4 C R; M1 and M2 hold the first tethers. The tower sets, two
// clockwise-shifted then two counter-: N E, S W, N S, E W. Under the NA strat (left party H1 R1 M1 T1):
//   28.5  MT soaks E (12,3) and OT N (3,-12); M1's tether blasts at W (-15,0), M2's at S (0,15).
//   37.5  RH soaks W (-12,-3) and SH S (-3,12); C's tether blasts at E (15,0), R's at N (0,-15).
public class TopP1ProgramLoopScenarioTests
{
    private static NegativeRun<TopP1ProgramLoopScenario> ProgramLoop(PartyRole player)
        => Negative<TopP1ProgramLoopScenario>(player)
            .Overrides<TopP1ProgramLoopStateOverrides>(o =>
            {
                o.Number[MainTank] = 1;
                o.Number[OffTank] = 1;
                o.Number[RegenHealer] = 2;
                o.Number[ShieldHealer] = 2;
                o.Number[MeleeDpsA] = 3;
                o.Number[MeleeDpsB] = 3;
                o.Number[CasterDps] = 4;
                o.Number[PhysRangedDps] = 4;
                o.Towers = [new(0, 1, 1), new(2, 3, 1), new(0, 2, -1), new(1, 3, -1)];
                o.FirstTetherHolders = [MeleeDpsA, MeleeDpsB];
            });

    // Beside M1, off the beam so as not to take the tether.
    [Test]
    public void BystanderInBlasterDies()
        => ProgramLoop(RegenHealer)
            .TeleportAt(28.3f, to: new(-11, 4))
            .ShouldKill(ActionId.BlasterAoe, RegenHealer);

    [Test]
    public void TowerLeftWithoutLooperObliteratesTheParty()
        => ProgramLoop(MainTank)
            .TeleportAt(28.3f, to: new(6, -6))
            .ShouldKill(ActionId.StorageViolationObliteration, PerRole.All);

    // MT soaked E at 28.5 and still carries its Twice-come Ruin. Stepping onto R's beam once R's
    // strat has stopped looking for it takes the tether, so the second blast lands on MT; R, 4y
    // behind, is inside it.
    [Test]
    public void SecondHitInsideTwiceComeRuinKills()
        => ProgramLoop(MainTank)
            .TeleportAt(37.2f, to: new(0, -11))
            .ShouldKill(ActionId.BlasterAoe, MainTank, PhysRangedDps);

    // M1's own blast at 28.6 left the HP Penalty; still a Looper holder, M1 soaks W with RH. The
    // Twice-come Ruin from that blast would kill too, so it is taken off first.
    [Test]
    public void HpPenaltyHolderDiesToTheNextHit()
        => ProgramLoop(MeleeDpsA)
            .At(37f, p => p.Member(MeleeDpsA)?.RemoveStatus(StatusId.TwiceComeRuin))
            .TeleportAt(37.3f, to: new(-12, -3))
            .ShouldKill(ActionId.StorageViolationSolo, MeleeDpsA);

    // SH, a Looper holder of another number, soaks N in OT's place and is cleansed; OT's Looper runs
    // out at 31.05 and Memory Loss walks them south into the wall.
    [Test]
    public void UnsoakedLooperWalksIntoTheWall()
        => ProgramLoop(OffTank)
            .MoveBotAt(28.3f, ShieldHealer, to: new(3, -12))
            .TeleportAt(28.3f, to: new(6, -6), facing: 0f)
            .ShouldKill(TheEnvironment, OffTank);
}
