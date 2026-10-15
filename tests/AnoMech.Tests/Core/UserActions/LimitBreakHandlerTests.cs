using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using AnoMech.Core.UserActions;
using AnoMech.Scenarios.Umad.P3LimitCut;

namespace AnoMech.Tests;

// The debug bot in the player's seat presses its LB3 through the fake client, as a strat would
// in game: the press is gated on the gauge, a bar roots it, and resolving spends the gauge, lands
// the action's row and tells the scenario where it was aimed.
public class LimitBreakHandlerTests
{
    private const uint PulseOfLife = 208;
    private const uint LandWaker = 4240;
    private const ushort LandWakerStatus = 863;
    private const float PressAt = 3f;

    private sealed class Press
    {
        public readonly List<(uint ActionId, LimitBreakAim Aim, float Time)> Landed = [];
        public bool RefusedWhileCasting, HeldWhileCasting, LimitBreakingSeen, RefusedAfterSpend, RowAfterLanding;
        public float CastSeconds;
        public ScenarioRun Run = null!;
    }

    private static Press PressAsDebugBot(PartyRole seat, ushort? rowStatus)
    {
        var press = new Press();
        press.Run = ScenarioRun.Execute(typeof(UmadP3LimitCutScenario), 0, 1, new ScenarioRunOptions
        {
            PlayerRole = seat,
            StopAt = PressAt + 5f,
            WriteArtifactsOnFailure = false,
            Probe = probe =>
            {
                var player = probe.World.Party.Player!;
                var self = (ulong)player.GameObjectId;
                if (probe.Crossed(PressAt))
                {
                    press.CastSeconds = Natives.Data.Action(LimitBreakHandler.ActionId(player.ClassJob, 3))!.CastSeconds;
                    probe.World.Party.LimitBreak.Landed += (actionId, aim) => press.Landed.Add((actionId, aim, probe.Time));
                    Assert.That(probe.World.Party.LimitBreak.FilledBars, Is.EqualTo(3), "the scenario grants a full gauge");
                    Assert.That(Natives.PlayerInput.PressLimitBreakThree(self, null), Is.True, "the client accepts the press");
                }
                else if (probe.Time > PressAt && probe.Time < PressAt + press.CastSeconds - 0.1f)
                {
                    press.LimitBreakingSeen |= player.IsLimitBreaking;
                    press.HeldWhileCasting |= player.AnimationLock;
                    press.RefusedWhileCasting |= !Natives.PlayerInput.PressLimitBreakThree(self, null);
                }
                else if (press.Landed.Count == 1 && probe.Time > press.Landed[0].Time + 0.5f && !press.RefusedAfterSpend)
                {
                    press.RowAfterLanding = rowStatus is { } status && player.HasStatus(status);
                    press.RefusedAfterSpend = !Natives.PlayerInput.PressLimitBreakThree(self, null);
                }
            },
        });
        Assert.That(press.Run.Failure, Is.Null, press.Run.ToString());
        Assert.That(press.Landed, Has.Count.EqualTo(1));
        Assert.That(press.Landed[0].Aim.Location, Is.Null, "nothing here is ground-targeted");
        Assert.That(press.RefusedAfterSpend, Is.True, "the spent gauge refuses the next press");
        return press;
    }

    [Test]
    public void HealerLimitBreakThreeCastsThenSpendsTheGauge()
    {
        var press = PressAsDebugBot(PartyRole.RegenHealer, null);
        Assert.That(press.Landed[0].ActionId, Is.EqualTo(PulseOfLife));
        Assert.That(press.CastSeconds, Is.EqualTo(2f));
        Assert.That(press.LimitBreakingSeen, Is.True, "the cast reads as a limit break");
        Assert.That(press.HeldWhileCasting, Is.True, "the debug bot is rooted through its cast");
        Assert.That(press.RefusedWhileCasting, Is.True, "a second press during the cast is refused");
        Assert.That(press.Landed[0].Time, Is.EqualTo(PressAt + press.CastSeconds).Within(ScenarioRun.FrameSeconds * 2));
    }

    [Test]
    public void TankLimitBreakThreeIsInstantAndLandsItsRowWithTheModuleOff()
    {
        var press = PressAsDebugBot(PartyRole.MainTank, LandWakerStatus);
        Assert.That(press.Landed[0].ActionId, Is.EqualTo(LandWaker));
        Assert.That(press.CastSeconds, Is.Zero);
        Assert.That(press.Landed[0].Time, Is.EqualTo(PressAt).Within(ScenarioRun.FrameSeconds * 2));
        Assert.That(press.RowAfterLanding, Is.True, "the tank LB3's own row lands without JobActionHandler");
    }
}
