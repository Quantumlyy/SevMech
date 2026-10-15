using AnoMech.Core.Game.Party;
using AnoMech.Scenarios.Top.P3Monitors;
using static AnoMech.Core.Game.Party.PartyRole;
using static AnoMech.Scenarios.Top.TopConstants;
using static AnoMech.Tests.NegativeRun;

namespace AnoMech.Tests;

// Omega's monitor fires east and M1, OT and SH carry the players'; which way each of those faces is
// the run's own roll, but the AI turns them so every cleave lands the same. The layout, north to
// south: RH (-1.4,-18.3), M1 (9.5,-8.4), C (-8,-12), MT (-16.5,-4.2), OT (9.5,8.4), M2 (-16.8,5),
// SH (-9.9,11.6), R (-1.7,18.3). At 20.07 Omega's monitor takes M1 and OT, M1's fires north at RH
// and C, OT's south at SH and R, SH's west at MT and M2. A Left boss mirrors it all in x.
public class TopP3MonitorsScenarioTests
{
    private static NegativeRun<TopP3MonitorsScenario> Monitors(PartyRole player, MonitorCleave bossSide = MonitorCleave.Right)
        => Negative<TopP3MonitorsScenario>(player)
            .Overrides<TopP3MonitorsStateOverrides>(o =>
            {
                o.BossSide = bossSide;
                o.Monitor[MeleeDpsA] = true;
                o.Monitor[OffTank] = true;
                o.Monitor[ShieldHealer] = true;
            });

    // Still north of M1, so its monitor takes RH and C as before, now inside each other's spread.
    [Test]
    public void OverlappingSpreadsKillBoth()
        => Monitors(RegenHealer)
            .TeleportAt(19.9f, to: new(-4, -12))
            .ShouldKill(ActionId.OversampledWaveCannonAoe, RegenHealer, CasterDps);

    // On Omega's side without leaving M1's: hit by both monitors.
    [TestCase(MonitorCleave.Right)]
    [TestCase(MonitorCleave.Left)]
    public void StandingOnOmegasSideDies(MonitorCleave bossSide)
        => Monitors(RegenHealer, bossSide)
            .TeleportAt(19.9f, to: new(bossSide == MonitorCleave.Left ? -9 : 9, -17))
            .ShouldKill(ActionId.OversampledWaveCannonAoe, RegenHealer);

    // Inside M1's spread from Omega's monitor, and RH's own spread lands on M1.
    [Test]
    public void SpreadingOntoOmegasTargetKillsBoth()
        => Monitors(RegenHealer)
            .TeleportAt(19.9f, to: new(6, -12))
            .ShouldKill(ActionId.OversampledWaveCannonAoe, RegenHealer, MeleeDpsA);

    // Still OT's target and still cleaving west at MT and M2, but close enough to M2 to share spreads.
    [Test]
    public void MonitorInsideItsOwnTargetsSpreadKillsBoth()
        => Monitors(ShieldHealer)
            .TeleportAt(19.9f, to: new(-13, 9))
            .ShouldKill(ActionId.OversampledWaveCannonAoe, ShieldHealer, MeleeDpsB);
}
