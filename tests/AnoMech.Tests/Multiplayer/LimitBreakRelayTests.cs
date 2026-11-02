using System.Globalization;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.UserActions;
using AnoMech.Scenarios;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AnoMech.Tests;

// A peer's limit break as the party sees it: its cast's control on every seat from the host, its
// resolve as the host's answer to the peer's report, grouped by the gauge as it stood, and the one
// gauge spent once that is announced.
public class LimitBreakRelayTests
{
    private static readonly PlayerSeat Host = new(PartyRole.MeleeDpsA, BotControlled: false);
    private static readonly PlayerSeat[] Peers = [new(PartyRole.MainTank, BotControlled: false), new(PartyRole.CasterDps, BotControlled: false)];
    private const uint LimitBreakCastCategory = 0x47, LimitBreakResolveCategory = 0x48;
    private const uint TankLevelOneGroup = 18;

    [Test]
    public void APeerTanksLevelOneLimitBreakResolvesInItsOwnGroupAndSpendsTheGauge()
    {
        var tank = PartyRole.MainTank;
        var lb1 = LimitBreakHandler.ActionId(PartyPresets.Standard[(int)tank].ClassJob, 1);
        Assert.That(lb1, Is.Not.Zero);
        var run = Play(
            (0.5, 0, c => c.Game.World.Party.LimitBreak.Set(1f)),
            (1.0, 1, c => c.Fake.UserActions.Act(new PlayerAction(PlayerActionStep.Resolved, ActionType.Action, lb1, 0))),
            (1.0, 1, c => c.Multiplayer.ReportLimitBreak(lb1, new LimitBreakAim(c.Game.World.Party.Player!.Position, 0f))));

        var resolves = Controls(run.Host, LimitBreakResolveCategory, lb1);
        Assert.That(resolves.Select(r => r.Actor).Distinct().Count(), Is.EqualTo(8), "the host announces the resolve on every seat");
        Assert.That(resolves.Select(r => r.Args[2]).Distinct(), Is.EqualTo(new[] { TankLevelOneGroup }), "grouped by the gauge as it stood");
        Assert.That(run.Host.Game.World.Party.LimitBreak.FilledBars, Is.Zero, "the host's gauge is spent");
        Assert.That(Controls(run.Peers[0], LimitBreakResolveCategory, lb1).Count(r => r.Actor == tank.ToString()), Is.EqualTo(1),
            "the presser sees its own resolve once, from the host");
        Assert.That(Controls(run.Peers[1], LimitBreakResolveCategory, lb1).Select(r => r.Actor).Distinct().Count(), Is.EqualTo(8));
    }

    [Test]
    public void APeersLimitBreakCastShowsItsControlOnEverySeatButItsOwnThere()
    {
        var caster = PartyRole.CasterDps;
        var lb3 = LimitBreakHandler.ActionId(PartyPresets.Standard[(int)caster].ClassJob, 3);
        Assert.That(lb3, Is.Not.Zero);
        var run = Play((1.0, 2, c => c.Fake.UserActions.Act(new PlayerAction(PlayerActionStep.CastStarted, ActionType.Action, lb3, 0, 4.5f))));

        foreach (var client in new[] { run.Host, run.Peers[0] })
            Assert.That(Controls(client, LimitBreakCastCategory, lb3).Select(c => c.Actor).Distinct().Count(), Is.EqualTo(8), $"{client.Name} shows the cast on every seat");
        var shown = Controls(run.Peers[1], LimitBreakCastCategory, lb3).Select(c => c.Actor).Distinct().ToList();
        Assert.That(shown, Has.Count.EqualTo(7).And.No.Member(caster.ToString()), "the presser's own game showed its own already");
    }

    private static MultiplayerRun Play(params (double At, int Client, Action<TestClient> Input)[] inputs)
    {
        var scenario = ScenarioCatalog.Create().Scenarios.First(s => s.SupportsMultiplayer).GetType();
        var run = MultiplayerRun.Play(scenario, 0, 1, Host, Peers, immortal: true, inputs: inputs);
        Assert.That(run.Failure, Is.Null);
        Assert.That(run.InputsLeft, Is.Zero, "inputs the fight ended before");
        return run;
    }

    private sealed record Control(string Actor, uint[] Args);

    // The ActorControls of one category a client's game received that name the action, by the
    // actor's name at the time.
    private static List<Control> Controls(TestClient client, uint category, uint actionId)
        => client.Fake.Timeline.Rows
            .Where(r => r["kind"] is "ac" or "ac_self" && r["id"] == $"0x{category:X}")
            .Select(r => new Control(
                r.GetValueOrDefault("actor") is { Length: > 0 } hex ? client.NameAt(uint.Parse(hex, NumberStyles.HexNumber), double.Parse(r["t"], CultureInfo.InvariantCulture)) : "nobody",
                r.GetValueOrDefault("p", "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(a => Convert.ToUInt32(a, 16)).ToArray()))
            .Where(c => c.Args.Length > 0 && c.Args[0] == actionId)
            .ToList();
}
