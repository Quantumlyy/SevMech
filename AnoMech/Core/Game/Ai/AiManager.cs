using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Core.Native.Interfaces;

namespace AnoMech.Core.Game.Ai;

// Drives slot-ordered party movement from a scenario's position functions.
// Owns jitter, run speed, and event scheduling. Position functions return an
// AiMove whose entries are scenario-local XZ coords — same space MoveTo
// consumes, so AiManager forwards them as-is. Eye-spawn flip and slot
// reordering are handled inside the AiMove before it reaches here.
public sealed class AiManager
{
    // Measured in-game.
    public const float RunSpeed = 6.5f;
    public const float SprintSpeed = 8.3f;
    private const float DefaultJitter = 0.3f;
    // Move's deadline math leaves zero margin, and a move needing speed within a hair of RunSpeed
    // arrives short. Only ever makes arrival earlier.
    private const float MoveDeadlineSafetyMargin = 0.25f;

    private readonly SimWorld world;

    public AiManager(SimWorld world)
    {
        this.world = world;
    }

    /// <summary>Schedule bots movement</summary> 
    /// <param name="arrivalTime">If not empty, this is expected arrival time for bots. They will sprint if there is not enough time to walk, and they will leave as late as possible otherwise</param>
    /// <param name="sprint">Ignored if arrivalTime is set. Sprint towards target instead of walking</param>
    public void Move(float time, Func<IAiMove> positions, float jitter = DefaultJitter, float? arrivalTime = null, bool sprint = false,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        world.Events.Add(time, () =>
        {
            var move = positions();
            for (int i = 0; i < 8; i++)
            {
                if (move[i] is not { } local) continue;
                var member = world.Party.Get(i);
                if (member == null || !member.IsAlive()) continue;
                var target = Jitter(new Vector3(local.X, 0f, local.Y), jitter);
                var dist = Vector2.Distance(new Vector2(member.Position.X, member.Position.Z), new Vector2(target.X, target.Z));
                var (sprinting, delay) = arrivalTime is { } arrival
                    ? PlanArrival(dist, arrival - time - MoveDeadlineSafetyMargin)
                    : (sprint, 0f);
                var planned = new PlannedMove((PartyRole)i, time, local, jitter, sprinting, delay);
                Issue(member, planned, target, file, line);
                world.ReportPlannedMove(planned);
            }
        }, file, line);
    }

    // A move as the host's Ai issued it to our seat (see SimWorld.MovePlanned).
    public void Follow(PlannedMove planned, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        if (world.Party.Get(planned.Role) is { } member)
            Issue(member, planned, Jitter(new Vector3(planned.Spot.X, 0f, planned.Spot.Y), planned.Jitter), file, line);
    }

    // target: the spot with this client's jitter on it.
    private void Issue(SimCharacter member, PlannedMove planned, Vector3 target, string file, int line)
    {
        var dist = Vector2.Distance(new Vector2(member.Position.X, member.Position.Z), new Vector2(target.X, target.Z));
        var speed = planned.Sprint ? SprintSpeed : RunSpeed;
        if (planned.Sprint) (member as ISimPartyMember)?.UseSprint(dist / SprintSpeed + 1f);
        // `for`: the spot before the jitter, which each client rolls on its own stream.
        AnoMech.Core.DiagnosticLog.Info($"[AiManager] Move@{planned.Time:F1}: {planned.Role} ({member.Position.X:F1},{member.Position.Z:F1}) -> ({target.X:F1},{target.Z:F1}) for ({planned.Spot.X:F2},{planned.Spot.Y:F2}) {dist:F1}y{(planned.Sprint ? " sprinting" : "")}{(planned.Delay > 0f ? $", leaving in {planned.Delay:F2}s" : "")}.");
        if (planned.Delay > 0f) world.Events.Add(planned.Delay, () => member.MoveTo(target, speed: speed), file, line);
        else member.MoveTo(target, speed: speed);
    }

    // Walk, leaving as late as still arrives in time; sprint now if walking can't make it.
    private static (bool Sprint, float Delay) PlanArrival(float dist, float available)
    {
        var slack = available - dist / RunSpeed;
        return slack >= 0f ? (false, slack) : (true, 0f);
    }

    public void Automarker(float time, Func<Dictionary<PartyRole, Sign>> mapping, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        world.Events.Add(time, () =>
        {
            world.Party.ClearMarks();
            var marks = mapping();
            AnoMech.Core.DiagnosticLog.Info($"[AiManager] Automarker@{time:F1}: [{string.Join(", ", marks.Select(kv => $"{kv.Key}={kv.Value}"))}].");
            foreach (var (role, sign) in marks)
                if (world.Party.Get(role) is { } member && member.IsAlive())
                    world.Party.Mark(sign, member);
        }, file, line);
    }

    private Vector3 Jitter(Vector3 target, float radius)
    {
        var rng = world.Stream("ai-jitter");
        var theta = rng.NextDouble() * 2.0 * Math.PI;
        var r = radius * MathF.Sqrt((float)rng.NextDouble());
        return new Vector3(
            target.X + r * MathF.Cos((float)theta),
            target.Y,
            target.Z + r * MathF.Sin((float)theta));
    }
}
