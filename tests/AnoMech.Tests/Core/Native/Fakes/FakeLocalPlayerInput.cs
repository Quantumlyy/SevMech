using System;
using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using AnoMech.Core.UserActions;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AnoMech.Tests;

// Nobody at the keyboard, unless a test holds a movement key. The debug bot's LB3 press runs as
// the client runs it: refused unless the gauge is full, then the cast, then what UserActions does
// as the bar completes.
internal sealed class FakeLocalPlayerInput(FakeCharacter player) : ILocalPlayerInput
{
    public event Action<ActionType, uint, ulong>? ActionExecuted { add { } remove { } }

    public bool MovementInputActive { get; set; }
    public bool IsJumping => false;
    public bool IsAutoAttacking => false;
    public bool PollActionUsed() => false;
    public bool PollCancelCast() => false;
    public IReadOnlyCollection<(uint ActionId, ActionType Type)> RecentActions => [];

    public Vector2? SteerDirection { get; private set; }
    public float SteerSpeed { get; private set; }

    public void Steer(Vector2? direction, float speed)
    {
        SteerDirection = direction is { } d && d.LengthSquared() > 0f ? Vector2.Normalize(d) : null;
        SteerSpeed = speed;
    }

    public LimitBreakPress LastLimitBreakPress { get; private set; }

    public bool PressLimitBreakThree(ulong targetId, Vector3? worldLocation)
    {
        if (player.IsCasting || Plugin.Game?.World is not { } world) return false;
        var actionId = LimitBreakHandler.ActionId(player.ClassJob, 3);
        if (actionId == 0 || world.Party.LimitBreak.FilledBars < 3) return false;
        var castSeconds = Natives.Data.Action(actionId)?.CastSeconds ?? 0f;
        player.IsCasting = castSeconds > 0f;
        player.CastActionId = actionId;
        player.CurrentCastTime = 0f;
        player.TotalCastTime = castSeconds;
        LastLimitBreakPress = new LimitBreakPress(targetId, worldLocation);
        world.Events.Add(castSeconds, () => Resolve(world, actionId, targetId));
        return true;
    }

    // JobActionHandler's row with the module on; LimitBreakHandler applies it itself when off.
    private void Resolve(SimWorld world, uint actionId, ulong targetId)
    {
        if (world.Party.Player is not { } self || !self.IsAlive()) return;
        if (Natives.UserActions.Enabled) JobActions.ApplyEffects(self, actionId, targetId);
        LimitBreakHandler.ResolveOwn(world, self, actionId, targetId, LastLimitBreakPress);
    }

    public void Dispose() { }
}
