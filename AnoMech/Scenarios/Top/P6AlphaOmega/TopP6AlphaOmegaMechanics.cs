using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.EnemyActions;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Top.TopConstants;
using static AnoMech.Scenarios.Top.P6AlphaOmega.TopP6AlphaOmegaConstants;
using Actions = AnoMech.Scenarios.Top.TopActions;

namespace AnoMech.Scenarios.Top.P6AlphaOmega;

// Alpha Omega and its gimmick helpers, and how their hits land.
public sealed class TopP6AlphaOmegaMechanics(SimWorld world)
{
    private const string Tag = "TopP6AlphaOmega";
    private static readonly Placement Centre = new(Vector3.Zero, 0f);
    private readonly SimParty party = world.Party;

    public SimEnemy? Boss { get; private set; }
    public TopHelpers? Helpers { get; private set; }

    private bool tracksMainTank;
    private bool holdsHeadingIntoNextCast;
    private bool facesNorth;

    public void SpawnBoss(bool targetable)
    {
        Boss = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.AlphaOmega,
            NameId: BNpcNameId.AlphaOmega,
            Level: Level,
            Targetable: targetable,
            EnemyList: EnemyListMode.Always,
            Placement: Centre,
            MaxHealth: BossMaxHealth));
    }

    public void SpawnHelpers() => Helpers = new TopHelpers(world, BNpcNameId.AlphaOmega, 32, Tag);

    public void TargetMainTank()
    {
        tracksMainTank = true;
        if (party.Get(PartyRole.MainTank) is { } tank) Boss?.SetTarget(tank, follow: false);
    }

    // Alpha Omega turns with its main tank between its own casts.
    public void FaceTarget()
    {
        if (!tracksMainTank || holdsHeadingIntoNextCast || facesNorth) return;
        if (Boss is not { IsCasting: false, AnimationLock: false } b || !b.IsAlive()) return;
        if (party.Get(PartyRole.MainTank) is { } tank && tank.IsAlive()) b.Face(tank.Position);
    }

    public void FaceNorthFromNowOn()
    {
        facesNorth = true;
        Boss?.SetRotation(MathF.PI);
    }

    // Alpha Omega's own casts, and its helpers', play on the caster itself.
    public void BossCast(EnemyAction action)
    {
        holdsHeadingIntoNextCast = false;
        Boss?.Cast(action);
    }

    public void BossCast(uint actionId, float animationLock)
    {
        holdsHeadingIntoNextCast = false;
        Boss?.Cast(actionId, animationLock: animationLock);
    }

    public SimEnemy? HelperCast(Placement at, EnemyAction action)
    {
        if (Helpers?.Next(at) is not { } helper) return null;
        helper.Cast(action);
        return helper;
    }

    public void ApplyDynamis()
    {
        Boss?.AddStatus(StatusId.CodeMi);
        foreach (var member in party.ActiveMembers().ToList())
        {
            member.RemoveStatus(StatusId.QuickeningDynamis);
            member.AddStatus(StatusId.BrilliantDynamis);
        }
    }

    public void BossAuto() => BossCast(ActionId.AlphaOmegaAutoAttack, animationLock: 1.1f);

    public void FlashGale()
    {
        if (Boss is not { } boss) return;
        var targets = new List<SimCharacter>();
        if (party.Get(PartyRole.MainTank) is { } tank && tank.IsAlive()) targets.Add(tank);
        if (party.Find.Farest(boss.Position) is { } far) targets.Add(far);
        foreach (var target in targets)
            Helpers?.Next(boss.Placement())?.Cast(Actions.FlashGale, target);
    }

    // The outer pinwheel: each line starts 15y off centre along an edge and sweeps the arena.
    private static readonly (Vector3 Origin, float Rotation, Vector3 Step)[] OuterLines =
    [
        (new(-20f, 0f, 15f), MathF.PI / 2, new(0f, 0f, -1f)),
        (new(15f, 0f, 20f), MathF.PI, new(-1f, 0f, 0f)),
        (new(20f, 0f, -15f), -MathF.PI / 2, new(0f, 0f, 1f)),
        (new(-15f, 0f, -20f), 0f, new(1f, 0f, 0f)),
    ];

    // The centre cross: each line splits and runs out to both edges.
    private static readonly (Vector3 Origin, float Rotation, Vector3 Step)[] CrossLines =
    [
        (new(-20f, 0f, 0f), MathF.PI / 2, new(0f, 0f, 1f)),
        (new(0f, 0f, -20f), 0f, new(1f, 0f, 0f)),
    ];

    // The rest explosions land every 2s after a line's first; the first moves it 7.5y, the rest 5y.
    private static readonly float[] ArrowSteps = [10.06f, 12.07f, 14.07f, 16.08f, 18.08f, 20.09f, 22.10f];

    public void CosmoArrow(ArrowPattern pattern)
    {
        BossCast(Actions.CosmoArrow);
        holdsHeadingIntoNextCast = true;
        var (leading, trailing) = pattern.InFirst ? (CrossLines, OuterLines) : (OuterLines, CrossLines);
        StartArrowLines(leading);
        world.Events.Add(2.01f, () => StartArrowLines(trailing));
        ScheduleArrowRest(leading, pattern.InFirst, firstStep: 0);
        ScheduleArrowRest(trailing, !pattern.InFirst, firstStep: 1);
    }

    private void StartArrowLines((Vector3 Origin, float Rotation, Vector3 Step)[] lines)
    {
        foreach (var line in lines)
        {
            var placement = new Placement(line.Origin, line.Rotation);
            HelperCast(placement, Actions.CosmoArrowOmen);
            HelperCast(placement, Actions.Inhale);
        }
    }

    private void ScheduleArrowRest((Vector3 Origin, float Rotation, Vector3 Step)[] lines, bool cross, int firstStep)
    {
        var steps = cross ? 3 : 6;
        for (var k = 1; k <= steps; k++)
        {
            var offset = 7.5f + 5f * (k - 1);
            var at = ArrowSteps[firstStep + k - 1];
            foreach (var line in lines)
            {
                if (cross)
                {
                    ScheduleArrowRestAt(at, new Placement(line.Origin + line.Step * offset, line.Rotation));
                    ScheduleArrowRestAt(at, new Placement(line.Origin - line.Step * offset, line.Rotation));
                }
                else
                    ScheduleArrowRestAt(at, new Placement(line.Origin + line.Step * offset, line.Rotation));
            }
        }
    }

    private void ScheduleArrowRestAt(float at, Placement placement)
        => world.Events.Add(at, () => HelperCast(placement, Actions.CosmoArrowLine));

    // Eight proteans from the centre in two waves of four, then a wild charge the whole party
    // lines up in, a tank at its head.
    public void WaveCannon(IReadOnlyList<PartyRole> proteanFirstWave, PartyRole wildChargeTarget, EnemyAction wildCharge)
    {
        BossCast(Actions.AlphaOmegaWaveCannon);
        world.Events.Add(3.03f, () => Proteans(proteanFirstWave));
        world.Events.Add(5.035f, () => Proteans(PerRole.All.Except(proteanFirstWave).ToList()));
        world.Events.Add(11.36f, () => WildCharge(wildChargeTarget, wildCharge));
    }

    private void Proteans(IReadOnlyList<PartyRole> roles)
    {
        foreach (var role in roles)
            if (party.Get(role) is { } target && target.IsAlive())
                Helpers?.Next(Centre)?.Cast(Actions.WaveCannonProtean, target);
    }

    private void WildCharge(PartyRole targetRole, EnemyAction wildCharge)
    {
        if (Boss is not { } boss) return;
        var target = party.Get(targetRole) is { } t && t.IsAlive() ? t : party.ActiveMembers().FirstOrDefault();
        if (target == null) return;
        boss.SetRotation(boss.Placement().Face(target.Position).Rotation);
        boss.Cast(wildCharge, target);
    }
}
