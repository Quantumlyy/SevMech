using AnoMech.Core.Game.Party;
using AnoMech.Scenarios.Top.P4BlueScreen;
using static AnoMech.Core.Game.Party.PartyRole;
using static AnoMech.Scenarios.Top.TopConstants;
using static AnoMech.Tests.NegativeRun;

namespace AnoMech.Tests;

// One stack target per light party, so nobody flexes: west stacks at (-4.8,13.5), east at
// (4.8,13.5). The line-up: MT (-13.4,-5.5), C (-14.2,0.1), RH (-12.3,5.4), M1 (-9.9,9.9) and the
// mirror for OT, R, SH, M2.
//
//   23.72  the lingering lines are aimed at the line-up; they go off at 28.72.
//   24.25  the proteans go off through the line-up; everyone then runs to the stacks.
//   29.20  the two line stacks; everyone walks back along the ring at 14 y.
//   31.36  Wave Repeater's circle; rings at 33.76, 35.81, 37.87.
public class TopP4BlueScreenScenarioTests
{
    private static NegativeRun<TopP4BlueScreenScenario> BlueScreen(PartyRole player)
        => Negative<TopP4BlueScreenScenario>(player)
            .Overrides<TopP4BlueScreenStateOverrides>(o =>
            {
                o.FirstStacks = StackSplit.OnePerSide;
                o.SecondStacks = StackSplit.OnePerSide;
                o.ThirdStacks = StackSplit.OnePerSide;
            });

    // Behind C on its line from the centre: each inside the other's protean.
    [Test]
    public void OverlappingProteansKillBoth()
        => BlueScreen(RegenHealer)
            .TeleportAt(24f, to: new(-18f, 0.1f))
            .ShouldKill(ActionId.P4WaveCannonProtean, RegenHealer, CasterDps);

    // Still on the line-up spot the lingering line was aimed at.
    [Test]
    public void StayingInTheLingeringLineKills()
        => BlueScreen(RegenHealer)
            .FreezeAt(24.5f)
            .ShouldKill(ActionId.P4WaveCannonProteanAoe, RegenHealer);

    // North, away from both stacks: the east stack is one short, or OT's own line finds OT alone.
    [Test]
    public void StackShortOfFourKills()
        => BlueScreen(OffTank)
            .TeleportAt(29f, to: new(0f, -10f))
            .ShouldKillSomeone(ActionId.P4WaveCannonStack);

    [Test]
    public void DiesInWaveRepeaterCircle()
        => BlueScreen(RegenHealer)
            .TeleportAt(31f, to: new(0f, 3f))
            .ShouldKill(ActionId.WaveRepeater1, RegenHealer);

    [Test]
    public void DiesInWaveRepeaterRing()
        => BlueScreen(RegenHealer)
            .TeleportAt(33.5f, to: new(0f, 9f))
            .ShouldKill(ActionId.WaveRepeater2, RegenHealer);

    // The gauge as the run passes each time, with the melee LB3 timed as asked.
    private static Dictionary<float, float> GaugeBars(LimitBreakTiming timing, PartyRole player, PartyRole? by, params float[] times)
    {
        var bars = new Dictionary<float, float>();
        var run = ScenarioRun.Execute(typeof(TopP4BlueScreenScenario), 0, 1, new ScenarioRunOptions
        {
            PlayerRole = player,
            WriteArtifactsOnFailure = false,
            StopAt = times.Max() + 0.5f,
            Overrides = o =>
            {
                var overrides = (TopP4BlueScreenStateOverrides)o;
                overrides.MeleeLimitBreakTiming = timing;
                overrides.MeleeLimitBreakBy = by;
            },
            Probe = p =>
            {
                foreach (var time in times)
                    if (p.Crossed(time)) bars[time] = p.World.Party.LimitBreak.CurrentBars;
            },
        });
        Assert.That(run.Failure, Is.Null, run.ToString());
        Assert.That(run.Deaths, Is.Empty, run.ToString());
        return bars;
    }

    // The bot's LB3 lands at 13.75; the gauge ticks 220 every 3 s from 16.73, fourteen times by 56.
    [Test]
    public void MeleeBotLimitBreaksAsThePhaseOpens()
    {
        var bars = GaugeBars(LimitBreakTiming.PhaseStart, MainTank, null, 9.5f, 14.5f, 56f);
        Assert.Multiple(() =>
        {
            Assert.That(bars[9.5f], Is.EqualTo(3f));
            Assert.That(bars[14.5f], Is.EqualTo(0f));
            Assert.That(bars[56f], Is.EqualTo(0.308f).Within(0.001f));
        });
    }

    [Test]
    public void MeleeBotLimitBreaksDuringBlueScreen()
    {
        var bars = GaugeBars(LimitBreakTiming.BlueScreen, MainTank, null, 50f, 62f);
        Assert.Multiple(() =>
        {
            Assert.That(bars[50f], Is.EqualTo(3f));
            Assert.That(bars[62f], Is.EqualTo(0f));
        });
    }

    [Test]
    public void LimitBreakOffKeepsTheGauge()
        => Assert.That(GaugeBars(LimitBreakTiming.Off, MainTank, null, 62f)[62f], Is.EqualTo(3f));

    // The debug bot in the melee's seat presses through the client: its 4.5 s bar lands at 14.28.
    [Test]
    public void DebugBotMeleePressesItsOwn()
    {
        var bars = GaugeBars(LimitBreakTiming.PhaseStart, MeleeDpsA, MeleeDpsA, 9.5f, 15f);
        Assert.Multiple(() =>
        {
            Assert.That(bars[9.5f], Is.EqualTo(3f));
            Assert.That(bars[15f], Is.EqualTo(0f));
        });
    }
}
