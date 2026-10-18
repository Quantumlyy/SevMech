using System;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Top.TopConstants;

namespace AnoMech.Scenarios.Top;

public static class TopBoss
{
    // Radians a second: Final Omega swings the half turn from south to its tank in about half a
    // second.
    public const float FinalOmegaTurnSpeed = MathF.Tau;

    private const float FinalOmegaAutoAttackReach = 15.5f;

    // A boss faces its main tank whenever no cast or action holds it, turning at `radiansPerSecond`.
    public static void TurnToMainTank(SimParty party, SimEnemy? boss, float radiansPerSecond, float delta)
    {
        if (boss is not { IsActive: true, AnimationLock: false }) return;
        if (party.Get(PartyRole.MainTank) is not { } tank || !tank.IsAlive()) return;
        var toTank = tank.Position - boss.Position;
        if (toTank.X * toTank.X + toTank.Z * toTank.Z < 1e-6f) return;
        var rotation = MathUtil.StepRotation(boss.Rotation, MathF.Atan2(toTank.X, toTank.Z), radiansPerSecond * delta);
        // Already facing it: a rotation set again would go to the client, and to peers, every frame.
        if (MathF.Abs(rotation - boss.Rotation) > 1e-4f) boss.SetRotation(rotation);
    }

    // Final Omega's auto-attack on its main target, which it only reaches from its 12.5y hitbox
    // plus melee range. It swings facing the tank, with any of its three swings; one that falls in
    // a cast's hold is left out, since the real one doesn't turn the boss there.
    public static void FinalOmegaAutoAttack(SimWorld world, SimEnemy? omega, Rng rng)
    {
        if (omega is not { IsCasting: false, AnimationLock: false } || world.Party.Get(PartyRole.MainTank) is not { } tank || !tank.IsAlive()) return;
        if (Vector3.Distance(omega.Position, tank.Position) > FinalOmegaAutoAttackReach) return;
        omega.SetTarget(tank, follow: false);
        omega.Face(tank.Position);
        omega.Cast(ActionId.AutoAttackP3, tank, animationLock: 0.1f, animationVariation: (byte)rng.NextInt(3));
    }
}
