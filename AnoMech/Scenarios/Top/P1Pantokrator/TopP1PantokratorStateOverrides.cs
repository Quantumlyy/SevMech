using System.Collections.Generic;
using System.Linq;
using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios.Top.P1Pantokrator;

public sealed class TopP1PantokratorStateOverrides
{
    // --- Fight-wide: one roll the whole sim shares -------------------------------------
    // Where the first pair of cones points: 0 = north, each step 30 degrees clockwise; null = random.
    public int? FlameStart { get; set; }
    public bool? Clockwise { get; set; }

    // --- Per player: everyone has their own ---------------------------------------------
    // In Line 1..4; two seats carry each number.
    public PerRoleSetting<int> Number { get; set; } = new();

    // Headless runs pin the rest of the roll. Internal, so settings sync and the summary skip it.
    internal IReadOnlyList<PartyRole>? FirstLasers { get; set; }

    public SettingsConflicts Validate()
    {
        var conflicts = new SettingsConflicts();
        if (!PerRole.SeatsActive) return conflicts;

        foreach (var number in Enumerable.Range(1, 4))
            conflicts.AtMost(2, PerRole.All.Where(r => Number[r] == number).ToList(), $"In Line {number}");
        return conflicts;
    }
}
