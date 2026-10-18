using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios;

namespace AnoMech.Multiplayer;

public sealed partial class MultiplayerManager
{
    // ---- Debug: bot-controlled peer replay ----------------------------------

    // Polled from Tick until it succeeds: LastState isn't set on the first tick after Start.
    private void TrySendAiReplayState()
    {
        if (TryResolveScenario() is not IMultiplayerReplayable replayable) return;
        if (replayable.BuildReplayStateMessage() is not { } msg) return;
        aiReplayStateSent = true;
        DiagnosticLog.Info("[Multiplayer] Host: broadcasting AiReplayState for this run.");
        _ = Send(msg);
    }

    private bool IsValidAiIndex(IScenario scenario) =>
        Session.SelectedAi >= 0 && Session.SelectedAi < scenario.AiStrats.Count;

    // Needs both the host's replay message and our own run, which arrive in either order, so
    // Dispatch and OnPeerStartResolved both call it.
    private void TryStartDebugBotReplay()
    {
        if (!debugBotControlled || debugBotReplayStarted) return;
        // peerEnteredInstance outlives the run; the AI drives the real character.
        if (!PeerInRun || !peerRunStarted || Plugin.Game.ActiveScenario is null) return;
        if (MyClaimedRole is not { } myRole) return;
        if (pendingGenericReplayState is not { } msg) return;
        if (TryResolveScenario() is not IMultiplayerReplayable replayable) return;
        var world = Plugin.Game.World;

        debugBotReplayStarted = true;
        if (!IsValidAiIndex(replayable))
        {
            DiagnosticLog.Warn($"[Multiplayer] Peer: SelectedAi {Session.SelectedAi} is out of range for {replayable.Name} ({replayable.AiStrats.Count} strats) -- skipping debug-bot replay.");
            return;
        }
        DiagnosticLog.Info($"[Multiplayer] Peer: starting debug-bot replay for {myRole}.");
        var shadowState = world.Events.FromRunStart(() => replayable.StartReplay(msg, Session.SelectedAi, myRole, world));
        if (shadowState != null)
        {
            debugShadowStateGeneric = shadowState;
            DebugBotControl.Enabled = true;
            foreach (var move in earlyPlannedMoves) FollowPlannedMove(move);
        }
        earlyPlannedMoves.Clear();
    }

    // ---- The host's plan, walked by a peer's debug bot (IMultiplayerReplayable.PeersFollowHostPlan)

    // Host: the Ai's moves since the last send.
    private readonly List<PlannedMoveState> plannedMoves = new();
    // Peer: moves that came before the debug-bot replay was up to walk them.
    private readonly List<PlannedMoveState> earlyPlannedMoves = new();
    private AiManager? followAi;

    private void SendPlannedMoves()
    {
        if (plannedMoves.Count == 0) return;
        _ = Send(new PlannedMovesMessage(plannedMoves.ToList()));
        plannedMoves.Clear();
    }

    private void OnPlannedMovesReceived(PlannedMovesMessage msg)
    {
        if (!debugBotControlled || !PeerInRun || MyClaimedRole is not { } myRole) return;
        var mine = NetGuard.Cap(msg.Moves, NetGuard.MaxPlannedMovesPerMessage).Where(m => m.Role == myRole).ToList();
        if (debugShadowStateGeneric == null)
        {
            if (earlyPlannedMoves.Count + mine.Count <= NetGuard.MaxQueuedMessages) earlyPlannedMoves.AddRange(mine);
            return;
        }
        foreach (var move in mine) FollowPlannedMove(move);
    }

    private void FollowPlannedMove(PlannedMoveState move)
    {
        var world = Plugin.Game.World;
        if (!NetGuard.TryPosition(move.X, 0f, move.Z, out var spot)) return;
        followAi ??= new AiManager(world);
        followAi.Follow(new PlannedMove(move.Role, NetGuard.Clamp(move.Time, 0f, 3600f), new Vector2(spot.X, spot.Z),
            NetGuard.Clamp(move.Jitter, 0f, 5f), move.Sprint, NetGuard.Clamp(move.Delay, 0f, 60f)));
    }

    // Every frame: the bot reads this client's world, so the handles and the obstacles it steers
    // around follow the host's actors as they reach this client.
    private void TickDebugBotReplay(float deltaSeconds)
    {
        if (debugShadowStateGeneric is not { } shadow || replay is null || TryResolveScenario() is not IMultiplayerReplayable replayable) return;
        var world = Plugin.Game.World;
        var enemies = replay.Enemies;
        replayable.RefreshLiveHandles(shadow, enemies);
        world.Obstacles.Clear();
        replayable.RebuildPeerObstacles(world.Obstacles, enemies, replay.Props, world.Party.Player);
        if (deltaSeconds > 0f) replayable.TickReplay(shadow, deltaSeconds);
    }

    // Clears the run's replay state, not the sticky debugBotControlled toggle.
    private void StopDebugBotReplay()
    {
        if (debugBotReplayStarted) DiagnosticLog.Info("[Multiplayer] Peer: stopping debug-bot replay.");
        DebugBotControl.Enabled = false;
        pendingGenericReplayState = null;
        debugShadowStateGeneric = null;
        debugBotReplayStarted = false;
        earlyPlannedMoves.Clear();
        followAi = null;
    }

}
