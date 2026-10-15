using System;
using System.Collections.Generic;
using System.Linq;
using AnoMech.Core.EnemyActions;
using AnoMech.Core.SimObjects;
using static AnoMech.Core.EnemyActions.Distribution;
using static AnoMech.Core.EnemyActions.EnemyActionEffects;
using static AnoMech.Core.EnemyActions.Severity;
using static AnoMech.Scenarios.Top.TopConstants;

namespace AnoMech.Scenarios.Top;

public static class TopActions
{
    private static readonly VulnSpec MagicVulns = VulnSpec.None
        .VulnerableTo(StatusId.MagicVulnerabilityUp)
        .VulnerableTo(StatusId.VulnerabilityUp)
        .VulnerableTo(StatusId.MagicVulnerabilityUpMini, minStacks: 2);


    internal static readonly RuinSpec ComeRuin = new((2, StatusId.TwiceComeRuin), (3, StatusId.TriceComeRuin));

    // -- Sword/shield/leg/staff body forms --
    public static readonly EnemyAction EfficientBladework = new(ActionId.EfficientBladework)
    {
        Cast = new() { OmenDelay = Duration.OmegaAttackOmenDelay, AnimationLock = 3.1f },
        Timing = new() { DamageDelay = 0.9f },
        Effects = [Damage(Lethal)],
    };

    public static readonly EnemyAction BeyondStrength = new(ActionId.BeyondStrength)
    {
        Cast = new() { OmenDelay = Duration.OmegaAttackOmenDelay, AnimationLock = 3.1f },
        Area = new() { Size = Geometry.BeyondStrengthSafeRadius },
        Effects = [Damage(Lethal)],
    };

    public static readonly EnemyAction OptimizedBlizzardIII = new(ActionId.OptimizedBlizzardIII)
    {
        Cast = new() { OmenDelay = Duration.OmegaAttackOmenDelay, AnimationLock = 3.1f },
        Timing = new() { DamageDelay = 1.14f },
        Effects = [Damage(MagicVulns, Lethal)],
    };

    // Visual only: the two helpers' SuperliminalSteelL/R deal the damage.
    public static readonly EnemyAction SuperliminalSteel = new(ActionId.SuperliminalSteel)
    {
        Cast = new() { AnimationLock = 3.1f },
    };

    public static readonly EnemyAction SuperliminalSteelL = SuperliminalSteelSide(ActionId.SuperliminalSteelOmenL);
    public static readonly EnemyAction SuperliminalSteelR = SuperliminalSteelSide(ActionId.SuperliminalSteelOmenR);

    private static EnemyAction SuperliminalSteelSide(uint actionId) => new(actionId)
    {
        Cast = new() { OmenDelay = Duration.OmegaAttackOmenDelay, AnimationLock = 1.1f },
        Effects = [Damage(MagicVulns, Lethal)],
    };

    public static readonly EnemyAction BeyondDefense = new(ActionId.BeyondDefenseAOE)
    {
        Effects =
        [
            OnOthers(Damage(Lethal)),
            OnTarget(Damage()),
            OnTarget(ApplyRuin(ComeRuin, 2, 6.96f)),
        ],
    };

    public static readonly EnemyAction PilePitch = new(ActionId.PilePitch)
    {
        Effects = [Damage(MagicVulns, split: Stack(3)), ApplyRuin(ComeRuin, 2, 6.96f)],
    };

    public static readonly EnemyAction Discharger = new(ActionId.Discharger)
    {
        Cast = new() { AnimationLock = 3.1f },
        Timing = new() { DamageDelay = 0.77f },
        Effects = [Knockback(KnockbackId.Discharger, knockbackDelay: 0.73f)],
    };

    public static readonly EnemyAction OptimizedFireIII = new(ActionId.OptimizedFireIII)
    {
        Cast = new() { AnimationLock = 1.1f },
        Timing = new() { DamageDelay = 0.62f },
        Effects = [Damage(MagicVulns), ApplyStatus(StatusId.MagicVulnerabilityUp, 1.96f)],
    };

    // -- Wave Cannon / Oversampled / Diffuse --
    public static readonly EnemyAction OversampledWaveCannon = new(ActionId.OversampledWaveCannonAoe)
    {
        Cast = new() { AnimationLock = 1.1f },
        Timing = new() { DamageDelay = 0.64f },
        Effects =
        [
            Damage(MagicVulns),
            ApplyRuin(ComeRuin, 2, 6.96f),
            ApplyStatus(StatusId.MagicVulnerabilityUp, 4.96f),
        ],
    };

    public static readonly EnemyAction WaveCannon = new(ActionId.WaveCannonAoe)
    {
        Cast = new() { AnimationLock = 1.1f },
        Timing = new() { DamageDelay = 0.66f },
        Effects = [Damage(MagicVulns), ApplyStatus(StatusId.MagicVulnerabilityUp, 1.96f)],
    };

    public static readonly EnemyAction DiffuseWaveCannon = new(ActionId.OmegaDiffuseWaveCannonAOE)
    {
        Cast = new() { AnimationLock = 1.1f },
        Area = new() { Size = MathF.PI / 3f },
        Effects = [Damage(MagicVulns, Lethal)],
    };

    // -- Omega-specific --
    public static readonly EnemyAction RunMiOmegaVersion = new(ActionId.RunMiOmegaVersion)
    {
        Cast = new() { AnimationLock = 3.1f },
        Timing = new() { DamageDelay = 1.16f },
        Effects = [Damage(MagicVulns)],
    };

    public static readonly EnemyAction Blaster = new(ActionId.OmegaBlasterAoe)
    {
        Cast = new() { AnimationLock = 1.1f },
        Timing = new() { DamageDelay = 0.64f },
        Effects =
        [
            Damage(MagicVulns),
            ApplyRuin(ComeRuin, 2, 10.96f),
            ApplyStatus(StatusId.MagicVulnerabilityUp, 4.96f),
            ApplyStatus(StatusId.HPPenalty, 3f, delayToEffectResult: true),
        ],
    };

    // -- Towers --
    public static readonly EnemyAction StorageViolationSolo = StorageViolation(ActionId.StorageViolationSolo, 0.63f);
    public static readonly EnemyAction StorageViolationPair = StorageViolation(ActionId.StorageViolationPair, 0.66f);

    private static EnemyAction StorageViolation(uint actionId, float damageDelay) => new(actionId)
    {
        Cast = new() { AnimationLock = 1.1f },
        Timing = new() { DamageDelay = damageDelay },
        Effects =
        [
            Damage(MagicVulns),
            ApplyRuin(ComeRuin, 2, 10.96f),
            RemoveStatus(StatusId.Looper),
        ],
    };

    public static readonly EnemyAction StorageViolationObliteration = new(ActionId.StorageViolationObliteration)
    {
        Cast = new() { AnimationLock = 1.1f },
        Timing = new() { DamageDelay = 0.67f },
        Effects = [Damage(MagicVulns, Lethal)],
        DeathExplanation = "tower unfilled",
    };

    // -- Hyper Pulse --
    public static readonly EnemyAction HyperPulseSigma = new(ActionId.HyperPulseSigma)
    {
        Cast = new() { AnimationLock = 2.1f },
        Timing = new() { DamageDelay = 0.48f },
        Effects = [Damage(MagicVulns), ApplyStatus(StatusId.MagicVulnerabilityUp, 4.96f)],
    };

    public static readonly EnemyAction HyperPulseCharging = new(ActionId.HyperPulseDeltaCharging)
    {
        Effects = [Damage(MagicVulns, Lethal)],
    };

    public static readonly EnemyAction HyperPulseShoot = new(ActionId.HyperPulseDeltaShoot)
    {
        Effects = [Damage(MagicVulns, Lethal)],
    };

    // -- Delta-specific --
    public static readonly EnemyAction RunMiDeltaVersion = new(ActionId.RunMiDeltaVersion)
    {
        Effects = [Damage(MagicVulns)],
    };

    public static readonly EnemyAction DeltaExplosion = new(ActionId.DeltaExplosion)
    {
        Effects = [Damage(MagicVulns, Lethal)],
    };

    public static readonly EnemyAction DeltaUnmitigatedExplosion = new(ActionId.DeltaUnmitigatedExplosion)
    {
        Effects = [Damage(MagicVulns, Lethal)],
    };

    public static readonly EnemyAction SwivelCannonLeft = SwivelCannon(ActionId.SwivelCannonL, MathF.PI / 2);
    public static readonly EnemyAction SwivelCannonRight = SwivelCannon(ActionId.SwivelCannonR, -MathF.PI / 2);

    private static EnemyAction SwivelCannon(uint actionId, float rotation) => new(actionId)
    {
        Cast = new() { OmenDelay = 8.5f, Rotation = rotation },
        Area = new() { Size = Geometry.SwivelCannonHalfAngle },
        Effects = [Damage(MagicVulns, Lethal)],
    };

    public static readonly EnemyAction HwTetherBreak = new(ActionId.HwTetherBreak)
    {
        Effects =
        [
            Damage(MagicVulns),
            ApplyRuin(ComeRuin, 3, Duration.HwTetherBreakStack),
            ApplyStatus(StatusId.MagicVulnerabilityUpMini, Duration.HwTetherBreakStack),
        ],
    };

    public static readonly EnemyAction HwTetherFail = new(ActionId.HwTetherFail)
    {
        Effects = [Damage(MagicVulns, Lethal)],
    };

    // -- Sigma-specific --
    public static readonly EnemyAction RunMiSigmaVersion = new(ActionId.RunMiSigmaVersion)
    {
        Cast = new() { AnimationLock = 3.1f },
        Timing = new() { DamageDelay = 1.03f },
        Effects = [Damage(MagicVulns)],
    };

    public static readonly EnemyAction RearLasersCharging = new(ActionId.RearLasersCharging)
    {
        Effects = [Damage(MagicVulns, Lethal)],
    };

    public static readonly EnemyAction RearLasersShoot = new(ActionId.RearLasersShoot)
    {
        Effects = [Damage(MagicVulns, Lethal)],
    };

    // -- Hello World --
    // Above the soaks, whose FollowUp captures it at initialization.
    public static readonly EnemyAction HelloWorldFail = new(ActionId.HelloWorldFail)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects = [Damage(MagicVulns, Lethal)],
        DeathExplanation = "Failed Hello World mechanic",
    };

    public static readonly EnemyAction HelloNearWorld = HelloWorldSoak(ActionId.HelloNearWorld, 0.63f);
    public static readonly EnemyAction HelloNearWorldJump = HelloWorldSoak(ActionId.HelloNearWorldJump, 0.61f);
    public static readonly EnemyAction HelloDistantWorld = HelloWorldSoak(ActionId.HelloDistantWorld, 0.63f);
    public static readonly EnemyAction HelloDistantWorldJump = HelloWorldSoak(ActionId.HelloDistantWorldJump, 0.61f);

    private static EnemyAction HelloWorldSoak(uint actionId, float damageDelay) => new(actionId)
    {
        Cast = new() { AnimationLock = 1.1f },
        Timing = new() { DamageDelay = damageDelay },
        Effects =
        [
            Damage(MagicVulns),
            ApplyStatusOrOverload(StatusId.QuickeningDynamis, maxStacks: 3),
            ApplyStatus(StatusId.MagicVulnerabilityUp, 4.96f),
            FollowUp(HelloWorldFail, when: ctx => ctx.Hits.Count != 1 || ctx.Hits.Any(ctx.IsKilled)),
        ],
    };

    // -- Optical Unit --
    public static readonly EnemyAction OpticalLaser = new(ActionId.OpticalLaser)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects = [Damage(MagicVulns, Lethal)],
    };

    // -- P1 Program Loop --

    // -- P3 Intermission --
    // P2's enrage, this far into its bar by the scenario's first frames.
    public static readonly EnemyAction LaserShower = new(ActionId.LaserShower)
    {
        Cast = new() { ElapsedAtStart = 54.94f },
    };

    public static readonly EnemyAction ColossalBlow = new(ActionId.ColossalBlow)
    {
        Cast = new() { AnimationLock = 2.1f },
        Effects = [Damage(MagicVulns, Lethal)],
    };

    public static readonly EnemyAction WaveRepeater1 = new(ActionId.WaveRepeater1)
    {
        Cast = new() { AnimationLock = 2.1f },
        Effects = [Damage(MagicVulns, Lethal)],
    };

    public static readonly EnemyAction WaveRepeater2 = WaveRepeaterRing(ActionId.WaveRepeater2, 6f);
    public static readonly EnemyAction WaveRepeater3 = WaveRepeaterRing(ActionId.WaveRepeater3, 12f);
    public static readonly EnemyAction WaveRepeater4 = WaveRepeaterRing(ActionId.WaveRepeater4, 18f);

    private static EnemyAction WaveRepeaterRing(uint actionId, float inner) => new(actionId)
    {
        Cast = new() { AnimationLock = 2.1f },
        Area = new() { Size = inner },
        Effects = [Damage(MagicVulns, Lethal)],
    };

    public static readonly EnemyAction SniperCannon = new(ActionId.SniperCannon)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects =
        [
            OnTarget(Damage(MagicVulns)),
            OnOthers(Damage(Lethal)),
            ApplyStatus(StatusId.MagicVulnerabilityUp, 1.96f),
        ],
    };

    public static readonly EnemyAction HighPoweredSniperCannon = new(ActionId.HighPoweredSniperCannon)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects = [Damage(MagicVulns, split: Stack(2)), ApplyStatus(StatusId.MagicVulnerabilityUp, 1.96f)],
    };

    // A dead holder's debuff going off on someone at random. UNVERIFIED: no pull lost a holder early.
    public static readonly EnemyAction SniperCannonJump = SniperCannonJumpOf(ActionId.SniperCannon);
    public static readonly EnemyAction HighPoweredSniperCannonJump = SniperCannonJumpOf(ActionId.HighPoweredSniperCannon);

    private static EnemyAction SniperCannonJumpOf(uint actionId) => new(actionId)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects = [Damage(MagicVulns, Lethal)],
    };

    // -- P3 Hello World --
    public static readonly EnemyAction HelloWorld = new(ActionId.HelloWorld)
    {
        Cast = new() { AnimationLock = 3.1f },
        Timing = new() { DamageDelay = 0.6f },
        Effects = [Damage(MagicVulns)],
    };

    public static readonly EnemyAction CriticalSynchronizationBug = new(ActionId.CriticalSynchronizationBug)
    {
        Cast = new() { AnimationLock = 1.1f },
        Timing = new() { DamageDelay = 0.6f },
        Effects =
        [
            Damage(MagicVulns, split: Stack(2)),
            ApplyStatus(StatusId.MagicVulnerabilityUp, 0.96f),
            RemoveStatus(StatusId.HWNeedStack),
        ],
    };

    public static readonly EnemyAction CriticalOverflowBug = new(ActionId.CriticalOverflowBug)
    {
        Cast = new() { AnimationLock = 1.1f },
        Timing = new() { DamageDelay = 0.6f },
        Effects =
        [
            Damage(MagicVulns),
            ApplyStatus(StatusId.MagicVulnerabilityUp, 0.96f),
            OnOthers(RemoveStatus(StatusId.HWNeedDefamation)),
        ],
    };

    // A rot running out: its holder takes the hit, anyone else inside dies.
    public static readonly EnemyAction CriticalUnderflowBug = RotExplosion(ActionId.CriticalUnderflowBug);
    public static readonly EnemyAction CriticalPerformanceBug = RotExplosion(ActionId.CriticalPerformanceBug);

    private static EnemyAction RotExplosion(uint actionId) => new(actionId)
    {
        Cast = new() { AnimationLock = 1.1f },
        Timing = new() { DamageDelay = 0.6f },
        Effects = [OnTarget(Damage(MagicVulns)), OnOthers(Damage(MagicVulns, Lethal))],
    };

    // A tower's hit leaves no vulnerability; its debuff is cleansed by that colour's rot running
    // out on the holder. It snapshots with its effect, not at the bar's end: the Christmas tether
    // broken as the last patch's tethers go live leaves a vulnerability that has only just run out.
    public static readonly EnemyAction CascadingLatentDefect =
        HelloWorldTower(ActionId.CascadingLatentDefect, ActionId.CascadingLatentDefectUnsoaked, StatusId.HWRedRot, StatusId.HWRedTower);
    public static readonly EnemyAction LatentPerformanceDefect =
        HelloWorldTower(ActionId.LatentPerformanceDefect, ActionId.LatentPerformanceDefectUnsoaked, StatusId.HWBlueRot, StatusId.HWBlueTower);

    private static EnemyAction HelloWorldTower(uint actionId, uint unsoakedActionId, ushort rotStatusId, ushort towerStatusId) => new(actionId)
    {
        Cast = new() { AnimationLock = 1.1f },
        Timing = new() { ResolveSnapshotOffset = 0.28f, DamageDelay = 0.63f },
        Effects =
        [
            Damage(MagicVulns),
            ApplyStatus(towerStatusId, 10f, delayToEffectResult: true),
            FollowUp(HelloWorldFailure(unsoakedActionId, "tower unsoaked"), when: ctx => ctx.Hits.Count == 0),
            FollowUp(HelloWorldFailure(unsoakedActionId, "tower soaked without its rot"), when: ctx => ctx.Hits.Any(soaker => !soaker.HasStatus(rotStatusId))),
        ],
    };

    public static readonly EnemyAction CascadingLatentDefectExpired =
        HelloWorldFailure(ActionId.CascadingLatentDefectExpired, "tower debuff ran out before its rot did");
    public static readonly EnemyAction LatentPerformanceDefectExpired =
        HelloWorldFailure(ActionId.LatentPerformanceDefectExpired, "tower debuff ran out before its rot did");

    // On whoever still carries the need when it runs out.
    public static readonly EnemyAction LatentDefectExpired =
        HelloWorldFailure(ActionId.LatentDefectExpired, "no defamation taken before Latent Defect ran out");
    public static readonly EnemyAction LatentSynchronizationDefect =
        HelloWorldFailure(ActionId.LatentSynchronizationDefect, "no stack shared before Latent Synchronization Bug ran out");

    private static EnemyAction HelloWorldFailure(uint actionId, string explanation) => new(actionId)
    {
        Cast = new() { AnimationLock = 1.1f },
        Timing = new() { DamageDelay = 0.6f },
        Effects = [Damage(MagicVulns, Lethal)],
        DeathExplanation = explanation,
    };

    // Anyone Critical Error finds without an Overflow Debugger gets a defamation that wipes the
    // party during Monitors.
    public static readonly EnemyAction CriticalError = new(ActionId.CriticalError)
    {
        Cast = new() { AnimationLock = 3.1f },
        Timing = new() { DamageDelay = 0.6f },
        Effects = [Damage(MagicVulns), ApplyStatus(StatusId.MagicVulnerabilityUp, 1f), new OverflowDebuggerCheck()],
    };

    private sealed class OverflowDebuggerCheck : IEnemyActionEffect
    {
        public void Apply(EnemyActionContext ctx, IReadOnlyList<SimCharacter> hits)
        {
            foreach (var target in hits)
            {
                if (ctx.IsKilled(target) || !target.IsAlive()) continue;
                if (target.HasStatus(StatusId.HWImmuneDefamation))
                {
                    target.RemoveStatus(StatusId.HWImmuneDefamation);
                    continue;
                }
                ctx.AddStatus(target, StatusId.HWDefamation, 21f);
                ctx.Kill(target, "no Overflow Debugger");
            }
        }
    }

    // -- P3 Monitors --

    // -- P4 Blue Screen --

    // -- P6 Alpha Omega --

    // -- P2 Party Synergy --
    public static readonly EnemyAction Spotlight = new(ActionId.Spotlight)
    {
        Cast = new() { AnimationLock = 1.1f },
        Timing = new() { DamageDelay = 0.6f },
        Effects = [Damage(MagicVulns, split: Stack(4)), ApplyStatus(StatusId.MagicVulnerabilityUp, 1.96f)],
    };

    // -- P6 Wave Cannon 2 --
    public static readonly EnemyAction CosmoArrow = new(ActionId.CosmoArrow)
    {
        Cast = new() { AnimationLock = 8.1f },
    };

    public static readonly EnemyAction Inhale = new(ActionId.Inhale)
    {
        Cast = new() { AnimationLock = 2.1f },
    };

    public static readonly EnemyAction CosmoArrowOmen = new(ActionId.CosmoArrowOmen)
    {
        Cast = new() { OmenDelay = 2.0f, AnimationLock = 2.1f },
        Effects = [Damage(MagicVulns, Lethal)],
    };

    public static readonly EnemyAction CosmoArrowLine = new(ActionId.CosmoArrowDamage)
    {
        Cast = new() { AnimationLock = 2.1f },
        Effects = [Damage(MagicVulns, Lethal)],
    };

    public static readonly EnemyAction AlphaOmegaWaveCannon = new(ActionId.WaveCannon_7BA9)
    {
        Cast = new() { AnimationLock = 0.2f },
    };

    public static readonly EnemyAction WaveCannonProtean = new(ActionId.WaveCannonProtean)
    {
        Cast = new() { AnimationLock = 1.1f },
        Timing = new() { DamageDelay = 0.64f },
        Effects = [Damage(MagicVulns), ApplyStatus(StatusId.MagicVulnerabilityUp, 2.5f)],
    };

    public static readonly EnemyAction WaveCannonWildCharge = new(ActionId.WaveCannonWildCharge)
    {
        Cast = new() { AnimationLock = 4.1f },
        Timing = new() { DamageDelay = 0.78f },
        Effects = [Damage(MagicVulns, split: WildCharge(front: 2, min: 8, frontHit: TankBuster))],
    };
}
