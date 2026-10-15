using System;
using System.Collections.Generic;
using System.Linq;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Top.P1Pantokrator;

public sealed class TopP1PantokratorState
{
    public const int FlameBearings = 12;
    public const float FlameStepDegrees = 360f / FlameBearings;

    private readonly Rng rng = Rng.Detached;

    public Rng Rng => rng;

    // InLine[2k] and InLine[2k + 1] are In Line k + 1.
    public RoleList InLine { get; }
    // The first pair of cones points this many steps clockwise from north; each pair after it
    // turns one step on, clockwise or not.
    public int FlameStart { get; }
    public bool Clockwise { get; }
    // Wave Cannon Kyrios takes its marked players from the front of this, skipping the two the
    // cleaves have.
    public IReadOnlyList<PartyRole> LaserOrder { get; }

    public TopP1PantokratorState(Rng rng, SimParty party, TopP1PantokratorStateOverrides overrides)
    {
        this.rng = rng;
        InLine = new RoleListBuilder
        {
            Slots = overrides.Number.Resolve(party.PlayerRole)
                             .Where(r => r.Value is >= 1 and <= 4)
                             .ToDictionary(r => r.Role, r => new[] { 2 * r.Value - 2, 2 * r.Value - 1 }),
        }.Build(rng, party);
        FlameStart = overrides.FlameStart is { } start && start is >= 0 and < FlameBearings ? start : rng.NextInt(FlameBearings);
        Clockwise = overrides.Clockwise ?? rng.NextBool();
        LaserOrder = overrides.LaserOrder ?? rng.Shuffle(PerRole.All);
    }

    private TopP1PantokratorState(SimParty party, PartyRole[] inLine, int flameStart, bool clockwise, PartyRole[] laserOrder)
    {
        InLine = new RoleList(party, inLine);
        FlameStart = flameStart;
        Clockwise = clockwise;
        LaserOrder = laserOrder;
    }

    public static TopP1PantokratorState? FromNetworkReplay(SimParty party, PartyRole[]? inLine, int flameStart, bool clockwise, PartyRole[]? laserOrder)
    {
        if (inLine is not { Length: 8 } || inLine.Distinct().Count() != 8 || !inLine.All(Enum.IsDefined)
            || flameStart is < 0 or >= FlameBearings
            || laserOrder is not { Length: 8 } || laserOrder.Distinct().Count() != 8 || !laserOrder.All(Enum.IsDefined))
            return null;
        return new(party, inLine, flameStart, clockwise, laserOrder);
    }

    public int NumberOf(PartyRole role) => InLine.List.ToList().IndexOf(role) / 2 + 1;

    public IEnumerable<PartyRole> WithNumber(int number) => [InLine[2 * number - 2], InLine[2 * number - 1]];

    // Set k's missiles go to number k + 1; its line stacks go through number k + 3, wrapped.
    public static int SpreadNumber(int set) => set + 1;

    public static int StackNumber(int set) => (set + 2) % 4 + 1;

    // Compass bearing of one cone of the pair; the other points the opposite way.
    public float FlameBearing(int pair) => FlameStart * FlameStepDegrees + (Clockwise ? 1 : -1) * FlameStepDegrees * pair;
}
