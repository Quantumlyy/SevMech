using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios.Top.P4BlueScreen;

namespace AnoMech.Tests;

public class SimNpcTests
{
    // The party outlives a run; a peer replays whatever PlayedActionSeq the next run's first
    // snapshot carries, so a run's end has to take the bot's last action with it.
    [Test]
    public void RunEndForgetsTheBotsPlayedAction()
    {
        SimPartyNpc? melee = null;
        var seqDuringRun = 0;
        var seqAfterRunEnd = -1;
        var run = ScenarioRun.Execute(typeof(TopP4BlueScreenScenario), 0, 1, new ScenarioRunOptions
        {
            PlayerRole = PartyRole.MainTank,
            WriteArtifactsOnFailure = false,
            StopAt = 16f,
            Overrides = o =>
            {
                var overrides = (TopP4BlueScreenStateOverrides)o;
                overrides.MeleeLimitBreakTiming = LimitBreakTiming.PhaseStart;
                overrides.MeleeLimitBreakBy = PartyRole.MeleeDpsA;
            },
            Probe = p =>
            {
                if (p.Crossed(15f))
                {
                    melee = p.World.Party.Get(PartyRole.MeleeDpsA) as SimPartyNpc;
                    seqDuringRun = melee?.PlayedActionSeq ?? 0;
                    p.World.Party.EndRun();
                    seqAfterRunEnd = melee?.PlayedActionSeq ?? -1;
                }
            },
        });

        Assert.That(run.Failure, Is.Null, run.ToString());
        Assert.That(melee, Is.Not.Null);
        Assert.That(seqDuringRun, Is.GreaterThan(0), "the melee bot played its limit break");
        Assert.That(seqAfterRunEnd, Is.Zero);
        Assert.That(melee!.PlayedActionId, Is.Zero);
    }
}
