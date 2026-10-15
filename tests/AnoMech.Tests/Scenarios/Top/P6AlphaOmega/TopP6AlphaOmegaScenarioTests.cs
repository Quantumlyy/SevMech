using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Top.P6AlphaOmega;
using static AnoMech.Core.Game.Party.PartyRole;
using static AnoMech.Scenarios.Top.TopConstants;
using static AnoMech.Tests.NegativeRun;

namespace AnoMech.Tests;

// Each test starts at the practice nearest its mechanic, on the strat's default picks: Cosmo Arrow
// 1 In first, MT left at Cosmo Dive, MT on the first tank LB3, RH on the first healer LB3. The
// exaflares, the spread waves, the flares and the stack target stay the seed's rolls.
//
//   18.14  Flash Gale on MT and the farthest player; OT baits it from (11.2,10.6).
//   29.27  Cosmo Arrow 1's cross, |x| < 5 and |z| < 5; the party dodges in the SW corner.
//   44.74  Cosmo Dive: busters on the two nearest the boss, the tanks at (0,-7) and (6.9,1.2); the
//          other six stack in the SW corner around (-8.5,8.5).
//   65.73  Unlimited Wave Cannon 1's first puddles under everyone at the centre, landing 2.7s later.
//   90.23  Wave Cannon 1's wild charge, the line up the +z axis with the tanks at z 5.4 and 7.
//  181.36  Cosmo Meteor's puddles; 182.83 and 183.86 the first round's spreads from MeteorSpot.
//  200.86  the flares at ring 18 and the stack at ring 15, each flare lethal inside 20y.
//  212.73  Magic Number 1 resolves; its debuff runs out ~8s later without RH's healer LB3.
public class TopP6AlphaOmegaScenarioTests
{
    private static IEnumerable<P6Practice> Practices => Enum.GetValues<P6Practice>().Where(p => p != P6Practice.WholePhase);

    // The catalog soak runs the whole phase; each practice start clears on its own too, with the
    // gauge and Dynamis the fast-forward hands it.
    [TestCaseSource(nameof(Practices))]
    public void PracticeStartClears(P6Practice practice)
    {
        var seeds = TestContext.Parameters.Get("SunnySeeds", 1);
        var failures = Enumerable.Range(0, seeds)
            .Select(_ => ScenarioRun.Execute(typeof(TopP6AlphaOmegaScenario), 0, Random.Shared.Next(),
                new ScenarioRunOptions { Overrides = o => ((TopP6AlphaOmegaStateOverrides)o).Practice = practice }))
            .Where(run => !run.Passed)
            .ToList();
        Assert.That(failures, Is.Empty, () => $"{failures.Count}/{seeds} seeds failed:{Environment.NewLine}"
            + string.Join(Environment.NewLine, failures.Select(run => $"{run}{Environment.NewLine}  replay: {run.ReplayTestCase}")));
    }

    private static NegativeRun<TopP6AlphaOmegaScenario> AlphaOmega(PartyRole player, P6Practice practice)
        => Negative<TopP6AlphaOmegaScenario>(player)
            .Overrides<TopP6AlphaOmegaStateOverrides>(o =>
            {
                o.Practice = practice;
                o.FirstArrowInFirst = true;
                o.SecondArrowInFirst = true;
            });

    [Test]
    public void NobodyPressesTheTankLimitBreakForCosmoMemory()
        => AlphaOmega(MainTank, P6Practice.CosmoMemory)
            .FreezeAt(5f)
            .ShouldKill(ActionId.CosmoMemory, PerRole.All);

    [Test]
    public void FarthestNonTankTakesFlashGale()
        => AlphaOmega(RegenHealer, P6Practice.CosmoArrowOne)
            .TeleportAt(17.5f, to: new(0, 19))
            .ShouldKill(ActionId.FlashGale, RegenHealer);

    [Test]
    public void DiesInCosmoArrowCross()
        => AlphaOmega(RegenHealer, P6Practice.CosmoArrowOne)
            .TeleportAt(25f, to: new(3, 8))
            .ShouldKill(ActionId.CosmoArrowOmen, RegenHealer);

    // Nearer the boss than either tank, and out of both tanks' circles.
    [Test]
    public void NonTankNearestTheBossTakesACosmoDiveBuster()
        => AlphaOmega(RegenHealer, P6Practice.CosmoArrowOne)
            .TeleportAt(44.5f, to: new(-5, 0))
            .ShouldKill(ActionId.CosmoDive_7BA7, RegenHealer);

    // Away from the corner the stack lands in, and out of the busters' reach.
    [Test]
    public void LeavingTheCosmoDiveStackLeavesItShort()
        => AlphaOmega(RegenHealer, P6Practice.CosmoArrowOne)
            .TeleportAt(44.5f, to: new(12, 12))
            .ShouldKillSomeone(ActionId.CosmoDive_7BA8);

    [Test]
    public void StandingInOwnWaveCannonPuddleKills()
        => AlphaOmega(RegenHealer, P6Practice.UnlimitedWaveCannonOne)
            .FreezeAt(66f)
            .ShouldKill(ActionId.WaveCannon_7BAF, RegenHealer);

    // Back at the centre for the first line's fourth explosion, before any puddle there lands.
    [Test]
    public void StandingInAnExaflareKills()
        => AlphaOmega(RegenHealer, P6Practice.UnlimitedWaveCannonOne)
            .TeleportAt(69.3f, to: new(0, 0))
            .ShouldKill(ActionId.WaveCannon_7BAE, RegenHealer);

    [Test]
    public void NonTankAtTheFrontOfTheWildChargeDies()
        => AlphaOmega(RegenHealer, P6Practice.WaveCannonOne)
            .TeleportAt(89.9f, to: new(0, 5))
            .ShouldKill(ActionId.WaveCannonWildCharge, RegenHealer);

    // The line follows its target: the rest of the party dies short of it, or the leaver alone when
    // the roll made them its target.
    [Test]
    public void LeavingTheWildChargeKillsTheLine()
        => AlphaOmega(RegenHealer, P6Practice.WaveCannonOne)
            .TeleportAt(89.9f, to: new(-10, 0))
            .ShouldKillSomeone(ActionId.WaveCannonWildCharge);

    // Beside M1's spot: whichever wave each is in, both are inside the other's spread.
    [Test]
    public void OverlappingCosmoMeteorSpreadsKillBoth()
        => AlphaOmega(RegenHealer, P6Practice.CosmoMeteor)
            .TeleportAt(182.6f, to: new(-10.5f, 7.5f))
            .ShouldKill(ActionId.CosmoMeteorSpread, RegenHealer, MeleeDpsA);

    // A stack member steps into the middle: the stack goes one short, and they stand 18y from
    // every flare.
    [Test]
    public void LeavingTheCosmoMeteorStackLeavesItShort()
        => AlphaOmega(RegenHealer, P6Practice.CosmoMeteor)
            .At(200.5f, StepIntoTheMiddle)
            .ShouldKillSomeone(ActionId.CosmoMeteorStack);

    [Test]
    public void StandingNearAFlareKills()
        => AlphaOmega(RegenHealer, P6Practice.CosmoMeteor)
            .At(200.5f, StepIntoTheMiddle)
            .ShouldKillSomeone(ActionId.CosmoMeteorFlare);

    [Test]
    public void MagicNumberRunsOutWithoutTheHealerLimitBreak()
        => AlphaOmega(RegenHealer, P6Practice.MagicNumberOne)
            .FreezeAt(206f)
            .ShouldKill(TheEnvironment, PerRole.All);

    private static void StepIntoTheMiddle(ScenarioProbe p)
    {
        var state = ((TopP6AlphaOmegaScenario)p.Game.ActiveScenario!).LastState!;
        var walker = PerRole.All.First(r => !state.FlareTargets.Contains(r) && r != state.StackTarget);
        var member = p.Member(walker)!;
        member.StopMoving();
        member.SetPosition(new Vector3(0f, 0f, 0f));
    }
}
