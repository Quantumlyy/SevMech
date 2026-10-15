using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Map;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Top.TopConstants;

namespace AnoMech.Scenarios.Top;

public sealed class TopZone : IZone
{
    public static readonly TopZone Instance = new();
    public static readonly Phase P1 = new(Instance, "P1", 77, BgmId.TopP1);
    public static readonly Phase P2 = new(Instance, "P2", 78, BgmId.TopP2,
        arenaMapEffectOverrides: new Dictionary<byte, ushort> { [0x0A] = 1 });
    // Starts on P2's kill: P2's sky and track, until the scenario brings in P3's.
    public static readonly Phase P3Intermission = new(Instance, "P3", 78, BgmId.TopP2,
        arenaMapEffectOverrides: new Dictionary<byte, ushort> { [0x01] = 1, [0x0A] = 1 });
    public static readonly Phase P3 = new(Instance, "P3", 79, BgmId.TopP3);
    // Starts on P3's last moments, so it opens in P3's sky and arena, and P3's track plays on.
    public static readonly Phase P4 = new(Instance, "P4", 79, BgmId.TopP3);
    public static readonly Phase P5 = new(Instance, "P5", 174, BgmId.TopP5);
    // The arena edge is down through P5's last cutscene; Alpha Omega raises it as it begins.
    public static readonly Phase P6 = new(Instance, "P6", 175, BgmId.TopP6, init: RaiseArenaEdge,
        arenaMapEffectOverrides: new Dictionary<byte, ushort> { [0x00] = 4 });
    // A start past Alpha Omega's opening, with the arena edge already up.
    public static readonly Phase P6Continued = new(Instance, "P6", 175, BgmId.TopP6);

    // Cosmo Meteor's arena change. Nothing turns it back before the territory reloads.
    public const byte CosmoMeteorArenaSlot = 0x15;

    public string Name => "The Omega Protocol";
    public uint TerritoryId => 1122;
    public Vector3 Origin => new(100f, 0f, 100f);

    public IReadOnlyList<WaymarkLayout> WaymarkPresets { get; } =
        [new WaymarkLayout("Ring", Core.Game.WaymarkPresets.Ring(13.63f))];

    public EntranceBarrier EntranceBarrier => new(2007457, 0x91D196, 0x91D193, new Vector3(5f, 0f, 15f));

    public IReadOnlyList<ushort> ArenaMapEffectSnapshot { get; } =
        [1, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4];

    public void Run(SimWorld world) => world.EnforceArenaBoundary(Geometry.ArenaRadius);

    private static void RaiseArenaEdge(SimWorld world) => world.Events.Add(0.04f, () => world.Map.AddEffect(0x00020001, 0x00));
}
