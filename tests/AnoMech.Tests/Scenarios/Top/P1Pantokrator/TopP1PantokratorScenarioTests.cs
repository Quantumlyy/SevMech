using AnoMech.Core.Game.Party;
using AnoMech.Scenarios.Top.P1Pantokrator;
using static AnoMech.Core.Game.Party.PartyRole;
using static AnoMech.Scenarios.Top.TopConstants;
using static AnoMech.Tests.NegativeRun;

namespace AnoMech.Tests;

// In Line 1 MT OT, 2 RH SH, 3 M1 M2, 4 R C: one of each number in both light parties, so nobody
// flexes. The first cones point E and W and turn clockwise, so group 1 (MT M1 R RH) starts north
// and group 2 (OT M2 C SH) south, both walking clockwise. At the first set (20.1) the cones point
// S and N, group 1 is around E: M1 R RH line up along bearing 90 and MT, ahead of them, takes the
// missile at (17.8,5.1). MT RH M1 R take the first lasers.
public class TopP1PantokratorScenarioTests
{
    private static NegativeRun<TopP1PantokratorScenario> Pantokrator(PartyRole player)
        => Negative<TopP1PantokratorScenario>(player)
            .Overrides<TopP1PantokratorStateOverrides>(o =>
            {
                o.Number[MainTank] = 1;
                o.Number[OffTank] = 1;
                o.Number[RegenHealer] = 2;
                o.Number[ShieldHealer] = 2;
                o.Number[MeleeDpsA] = 3;
                o.Number[MeleeDpsB] = 3;
                o.Number[PhysRangedDps] = 4;
                o.Number[CasterDps] = 4;
                o.FlameStart = 3;
                o.Clockwise = true;
                o.FirstLasers = [MainTank, RegenHealer, MeleeDpsA, PhysRangedDps];
            });

    [Test]
    public void StandingInTheFirstConeDies()
        => Pantokrator(MainTank)
            .TeleportAt(13.6f, to: new(10, 0))
            .ShouldKill(ActionId.FlameThrowerFirst, MainTank);

    // Frozen before the puddles at 20 drop, so one lands underfoot and resolves at 22.7.
    [Test]
    public void StandingStillInYourPuddleDies()
        => Pantokrator(MainTank)
            .FreezeAt(19.9f)
            .ShouldKill(ActionId.BallisticImpact, MainTank);

    [Test]
    public void BystanderInTheMissileDies()
        => Pantokrator(RegenHealer)
            .TeleportAt(19.9f, to: new(17.8f, 5.1f))
            .ShouldKill(ActionId.GuidedMissileKyrios, RegenHealer);

    // RH steps out of group 1's line, which leaves M1 and R to share it.
    [Test]
    public void LineStackTakenByTwoKillsThem()
        => Pantokrator(RegenHealer)
            .TeleportAt(19.9f, to: new(15.6f, -9f))
            .ShouldKill(ActionId.CondensedWaveCannonKyrios, MeleeDpsA, PhysRangedDps);

    // R stands farther out than the tanks, so a cleave aims at R and sweeps M1 and RH beside it.
    [Test]
    public void FarthestNonTankTakesACleave()
        => Pantokrator(PhysRangedDps)
            .TeleportAt(46.5f, to: new(19.5f, 0))
            .ShouldKill(ActionId.DiffuseWaveCannonKyrios, PhysRangedDps, MeleeDpsA, RegenHealer);

    // M1 stands between the boss and R: R's laser passes through M1, and M1's through R.
    [Test]
    public void LaserThroughAnotherPlayerKillsBoth()
        => Pantokrator(MeleeDpsA)
            .TeleportAt(46.5f, to: new(10, 0))
            .ShouldKill(ActionId.WaveCannonKyrios, MeleeDpsA, PhysRangedDps);
}
