using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Top.P1Pantokrator;
using static AnoMech.Core.Game.Party.PartyRole;

namespace AnoMech.Tests;

// Every number lands twice in one light party: 1 MT M1 and 3 R RH in group 1, 2 OT SH and 4 M2 C
// in group 2. The lower priority of each pair flexes (T > M > R > H), so M1 and RH cross south
// while SH and C cross north. The first cones point E and W, so group 1 stays north.
public class TopP1PantokratorAiTests
{
    private const float FormedUp = 13f;

    [Test]
    public void TheLowerPriorityOfASharedNumberFlexes()
    {
        var positions = new Dictionary<PartyRole, Vector2>();
        var options = new ScenarioRunOptions
        {
            PlayerRole = MainTank,
            Overrides = o =>
            {
                var overrides = (TopP1PantokratorStateOverrides)o;
                overrides.Number[MainTank] = 1;
                overrides.Number[MeleeDpsA] = 1;
                overrides.Number[OffTank] = 2;
                overrides.Number[ShieldHealer] = 2;
                overrides.Number[PhysRangedDps] = 3;
                overrides.Number[RegenHealer] = 3;
                overrides.Number[MeleeDpsB] = 4;
                overrides.Number[CasterDps] = 4;
                overrides.FlameStart = 3;
                overrides.Clockwise = true;
            },
            Probe = p =>
            {
                if (!p.Crossed(FormedUp)) return;
                foreach (var role in PerRole.All)
                    if (p.Member(role) is { } member)
                        positions[role] = new Vector2(member.Position.X, member.Position.Z);
            },
        };
        var run = ScenarioRun.Execute(typeof(TopP1PantokratorScenario), 0, seed: 1, options);
        Assert.That(run.Passed, Is.True, run.ToString);
        foreach (var role in new[] { MainTank, PhysRangedDps, ShieldHealer, CasterDps })
            Assert.That(positions[role].Y, Is.LessThan(-4f), $"{role} should stand north, stood at ({positions[role].X:F1},{positions[role].Y:F1}).");
        foreach (var role in new[] { OffTank, MeleeDpsB, MeleeDpsA, RegenHealer })
            Assert.That(positions[role].Y, Is.GreaterThan(4f), $"{role} should stand south, stood at ({positions[role].X:F1},{positions[role].Y:F1}).");
    }
}
