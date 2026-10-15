using System;
using System.Linq;
using AnoMech.Core.EnemyActions;
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

    // -- P3 Monitors --

    // -- P4 Blue Screen --

    // -- P6 Alpha Omega --
    // Cosmo Memory and Magic Number: only a tank LB3 or an invuln makes them survivable.
    private static readonly VulnSpec SparedByTankLimitBreak = P6AlphaOmega.TopP6AlphaOmegaConstants.TankLimitBreakStatusIds
        .Concat(P6AlphaOmega.TopP6AlphaOmegaConstants.InvulnStatusIds)
        .Aggregate(VulnSpec.None, (spec, statusId) => spec.ProtectedBy(statusId));

    public static readonly EnemyAction CosmoMemory = new(ActionId.CosmoMemory)
    {
        Cast = new() { AnimationLock = 4.1f },
        Timing = new() { DamageDelay = 1.96f },
        Effects = [Damage(SparedByTankLimitBreak, Lethal)],
        DeathExplanation = "no tank limit break",
    };

    // Its debuff runs out without a healer LB3; the scenario watches for that.
    public static readonly EnemyAction MagicNumber = new(ActionId.MagicNumber)
    {
        Cast = new() { AnimationLock = 5.1f },
        Timing = new() { DamageDelay = 1.96f },
        Effects = [Damage(SparedByTankLimitBreak, Lethal), ApplyStatus(StatusId.MagicNumber, 6f, delayToEffectResult: true)],
        DeathExplanation = "no tank limit break",
    };

    // On the main target and the farthest player; a tank hit that needs no mitigation of its own.
    public static readonly EnemyAction FlashGale = new(ActionId.FlashGale)
    {
        Cast = new() { AnimationLock = 1.1f },
        Timing = new() { DamageDelay = 0.62f },
        Effects = [Damage(MagicVulns, TankBuster), ApplyStatus(StatusId.MagicVulnerabilityUp, 1.96f)],
    };

    public static readonly EnemyAction CosmoDive = new(ActionId.CosmoDive)
    {
        Cast = new() { AnimationLock = 9.1f },
    };

    // On the two nearest the boss; requiredMitigation is the share of the hit a human tank's own
    // mitigation must cover.
    public static EnemyAction CosmoDiveBuster(float requiredMitigation) => new(ActionId.CosmoDive_7BA7)
    {
        Cast = new() { AnimationLock = 1.1f },
        Timing = new() { DamageDelay = 0.62f },
        Effects = [Damage(MagicVulns, TankBuster.MinMit(requiredMitigation)), ApplyStatus(StatusId.MagicVulnerabilityUp, 1.96f)],
    };

    public static readonly EnemyAction CosmoDiveStack = new(ActionId.CosmoDive_7BA8)
    {
        Cast = new() { AnimationLock = 1.1f },
        Timing = new() { DamageDelay = 0.62f },
        Effects = [Damage(MagicVulns, split: Stack(6)), ApplyStatus(StatusId.MagicVulnerabilityUp, 1.96f)],
    };

    public static readonly EnemyAction UnlimitedWaveCannon = new(ActionId.UnlimitedWaveCannon)
    {
        Cast = new() { AnimationLock = 5.1f },
    };

    // The exaflares and the puddles take their hits with the effect, 0.29s after the bar, which is
    // the margin the puddle run is cut to. An exaflare line's first explosion is the cast's own;
    // the rest are UnlimitedWaveCannonExaflare.
    public static readonly EnemyAction UnlimitedWaveCannonLine = new(ActionId.WaveCannon_7BAD)
    {
        Cast = new() { AnimationLock = 1.1f },
        Timing = new() { ResolveSnapshotOffset = 0.29f },
        Effects = [Damage(MagicVulns, Lethal)],
    };

    public static readonly EnemyAction UnlimitedWaveCannonExaflare = new(ActionId.WaveCannon_7BAE)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects = [Damage(MagicVulns, Lethal)],
    };

    public static readonly EnemyAction UnlimitedWaveCannonPuddle = new(ActionId.WaveCannon_7BAF)
    {
        Cast = new() { AnimationLock = 1.1f },
        Timing = new() { ResolveSnapshotOffset = 0.29f },
        Effects = [Damage(MagicVulns, Lethal)],
    };

    // The whole party lines up in it; the front two take it as a tankbuster, frontRequiredMitigation
    // as CosmoDiveBuster's.
    public static EnemyAction AlphaOmegaWildCharge(float frontRequiredMitigation) => new(ActionId.WaveCannonWildCharge)
    {
        Cast = new() { AnimationLock = 4.1f },
        Timing = new() { DamageDelay = 0.78f },
        Effects = [Damage(MagicVulns, split: WildCharge(front: 2, min: 8, frontHit: TankBuster.MinMit(frontRequiredMitigation)))],
    };

    public static readonly EnemyAction CosmoMeteor = new(ActionId.CosmoMeteor)
    {
        Cast = new() { AnimationLock = 3.1f },
    };

    public static readonly EnemyAction CosmoMeteorPuddle = new(ActionId.CosmoMeteorPuddle)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects = [Damage(MagicVulns, Lethal)],
    };

    public static readonly EnemyAction CosmoMeteorSpread = new(ActionId.CosmoMeteorSpread)
    {
        Cast = new() { AnimationLock = 1.1f },
        Timing = new() { DamageDelay = 0.62f },
        Effects = [Damage(MagicVulns), ApplyStatus(StatusId.MagicVulnerabilityUp, 1.96f)],
    };

    public static readonly EnemyAction CosmoMeteorStack = new(ActionId.CosmoMeteorStack)
    {
        Cast = new() { AnimationLock = 1.1f },
        Timing = new() { DamageDelay = 0.62f },
        Effects = [Damage(MagicVulns, split: Stack(5))],
    };

    // Reaches the whole arena, falling off with distance; its holder takes nothing from it.
    public static readonly EnemyAction CosmoMeteorFlare = new(ActionId.CosmoMeteorFlare)
    {
        Cast = new() { AnimationLock = 1.1f },
        Timing = new() { DamageDelay = 0.62f },
        Effects = [OnOthers(Damage(MagicVulns, split: Falloff(P6AlphaOmega.TopP6AlphaOmegaConstants.FlareLethalRange)))],
    };

    public static readonly EnemyAction CosmoMeteorEnrage = new(ActionId.CosmoMeteorEnrage)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects = [Damage(MagicVulns, Lethal)],
        DeathExplanation = "a comet or meteor left standing",
    };

    // Reports 9,999,999 on everyone; the result that would carry it out comes long after the boss
    // has died.
    public static readonly EnemyAction RunMi = new(ActionId.RunMi)
    {
        Cast = new() { AnimationLock = 16.1f },
        Timing = new() { DamageDelay = 0.3f },
        Effects = [ShowHit(9_999_999)],
    };

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
