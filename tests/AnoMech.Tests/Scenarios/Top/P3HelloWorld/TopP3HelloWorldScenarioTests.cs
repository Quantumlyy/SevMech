using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Top.P3HelloWorld;
using static AnoMech.Core.Game.Party.PartyRole;
using static AnoMech.Scenarios.Top.TopConstants;
using static AnoMech.Tests.NegativeRun;

namespace AnoMech.Tests;

// Every roll is pinned: the defamations carry the red rot, and every patch's towers stand on the
// cardinals, red at N and E, blue at S and W. First-patch roles: MT OT defamations, RH SH blue
// tethers, M1 M2 stacks, R C Christmas tethers. The AI puts the defamation holders in the N and E
// towers (r 15.5), the stack holders in the S and W towers (r 12.5 at 200° and 250°), the blue
// tethers beside the stacks and the Christmas tethers flanking the defamations. The first patch's
// towers land at 32.14, its stacks and defamations at 32.16, and its rots run out at 38.0.
public class TopP3HelloWorldScenarioTests
{
    private static readonly Vector2 OutsideArena = new(0, 20.5f);

    private static NegativeRun<TopP3HelloWorldScenario> Loop(PartyRole player)
        => Negative<TopP3HelloWorldScenario>(player)
            .Overrides<TopP3HelloWorldStateOverrides>(o =>
            {
                o.DefamationColor = DefamationRot.Red;
                o.Towers = [new(false, 0), new(false, 0), new(false, 0), new(false, 0)];
                o.Role[MainTank] = HelloWorldRole.Defamation;
                o.Role[OffTank] = HelloWorldRole.Defamation;
                o.Role[RegenHealer] = HelloWorldRole.Remote;
                o.Role[ShieldHealer] = HelloWorldRole.Remote;
                o.Role[MeleeDpsA] = HelloWorldRole.Stack;
                o.Role[MeleeDpsB] = HelloWorldRole.Stack;
                o.Role[PhysRangedDps] = HelloWorldRole.Local;
                o.Role[CasterDps] = HelloWorldRole.Local;
            });

    [Test]
    public void DiesWalkingIntoWall()
        => Loop(RegenHealer)
            .TeleportAt(5f, to: OutsideArena)
            .ShouldKill(TheEnvironment, RegenHealer);

    // M1 leaves its W tower for a spot towards the centre, out of both defamations' 20y; its blue
    // tether still finds it there to share the stack.
    [Test]
    public void UnsoakedTowerWipes()
        => Loop(MeleeDpsA)
            .TeleportAt(25f, to: new(-8, 4))
            .ShouldKill(ActionId.LatentPerformanceDefectUnsoaked, PerRole.All);

    // RH leaves its stack holder for the NE edge just before the patch lands.
    [Test]
    public void StackTakenAloneWipes()
        => Loop(RegenHealer)
            .TeleportAt(31.9f, to: new(12, -12))
            .ShouldKill(ActionId.CriticalSynchronizationBug, PerRole.All);

    // Within 20y of both defamation holders.
    [Test]
    public void TakingBothDefamationsWipes()
        => Loop(PhysRangedDps)
            .TeleportAt(31.9f, to: new(8, -8))
            .ShouldKill(ActionId.CriticalOverflowBug, PerRole.All);

    // RH leaves SH's side the moment the stacks land, while everyone still carries the patch's
    // Magic Vulnerability Up.
    [Test]
    public void BreakingTheBlueTetherWhileVulnerableWipes()
        => Loop(RegenHealer)
            .TeleportAt(32.2f, to: new(0, 0))
            .ShouldKill(ActionId.HwTetherBreak, PerRole.All);

    // R takes its defamation, then never steps onto the holder's rot.
    [Test]
    public void RotNotTakenBeforeItRunsOutWipes()
        => Loop(PhysRangedDps)
            .TeleportAt(32.3f, to: new(0, 0))
            .ShouldKill(ActionId.CriticalUnderflowBug, PerRole.All);

    // Off the arena with the Christmas tether still up.
    [Test]
    public void DyingWithATetherFailsIt()
        => Loop(PhysRangedDps)
            .TeleportAt(33f, to: OutsideArena)
            .ShouldKill(TheEnvironment, PhysRangedDps)
            .ShouldKill(ActionId.HwTetherFail, AllBut(PhysRangedDps));

    // 3y inside the stack holder standing at 200°, carrying its own red rot but not the blue one.
    [Test]
    public void BystanderInRotExplosionDies()
        => Loop(PhysRangedDps)
            .TeleportAt(37.5f, to: new(-3.25f, 8.93f))
            .ShouldKill(ActionId.CriticalPerformanceBug, PhysRangedDps);
}
