using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Top.P1ProgramLoop;
using static AnoMech.Core.Game.Party.PartyRole;

namespace AnoMech.Tests;

// In Line 1 MT OT, 2 RH SH, 3 M1 M2, 4 C R; M1 and M2 hold the first tethers; the first tower set
// is N E clockwise-shifted. Under the LPDU priority (M1 M2 MT OT R C H1 H2) the first of each
// pair is group 1, which takes the tower and drop spot first clockwise from north:
//   28.5  MT soaks N (3,-12) and OT E (12,3); M1's tether blasts at S (0,15), M2's at W (-15,0).
public class TopP1ProgramLoopLpduAiTests
{
    private const float FirstSet = 28.3f;
    private const float SoakTolerance = 3f;
    private const float DropTolerance = 2f;

    private static readonly int Lpdu = new TopP1ProgramLoopScenario().AiStrats
        .Select((ai, index) => (ai, index))
        .Single(s => s.ai.Name == "LPDU")
        .index;

    [Test]
    public void GroupOneTakesTheClockwiseFirstTowerAndDropSpot()
    {
        var positions = new Dictionary<PartyRole, Vector2>();
        var options = new ScenarioRunOptions
        {
            PlayerRole = MainTank,
            Overrides = o =>
            {
                var overrides = (TopP1ProgramLoopStateOverrides)o;
                overrides.Number[MainTank] = 1;
                overrides.Number[OffTank] = 1;
                overrides.Number[RegenHealer] = 2;
                overrides.Number[ShieldHealer] = 2;
                overrides.Number[MeleeDpsA] = 3;
                overrides.Number[MeleeDpsB] = 3;
                overrides.Number[CasterDps] = 4;
                overrides.Number[PhysRangedDps] = 4;
                overrides.Towers = [new(0, 1, 1), new(2, 3, 1), new(0, 2, -1), new(1, 3, -1)];
                overrides.FirstTetherHolders = [MeleeDpsA, MeleeDpsB];
            },
            Probe = p =>
            {
                if (!p.Crossed(FirstSet)) return;
                foreach (var role in PerRole.All)
                    if (p.Member(role) is { } member)
                        positions[role] = new Vector2(member.Position.X, member.Position.Z);
            },
        };
        var run = ScenarioRun.Execute(typeof(TopP1ProgramLoopScenario), Lpdu, seed: 1, options);
        Assert.That(run.Passed, Is.True, run.ToString);
        ExpectAt(positions, MainTank, new(3, -12), SoakTolerance);
        ExpectAt(positions, OffTank, new(12, 3), SoakTolerance);
        ExpectAt(positions, MeleeDpsA, new(0, 15), DropTolerance);
        ExpectAt(positions, MeleeDpsB, new(-15, 0), DropTolerance);
    }

    private static void ExpectAt(IReadOnlyDictionary<PartyRole, Vector2> positions, PartyRole role, Vector2 expected, float tolerance)
    {
        Assert.That(positions.ContainsKey(role), Is.True, $"{role} was not captured.");
        var actual = positions[role];
        Assert.That(Vector2.Distance(actual, expected), Is.LessThan(tolerance),
            $"{role} stood at ({actual.X:F1},{actual.Y:F1}), expected ({expected.X:F1},{expected.Y:F1}).");
    }
}
