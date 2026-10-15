using System.Linq;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AnoMech.Core.UserActions;

// Sends the party the server's limit break ActorControls: one as the cast starts, one when it
// resolves, which empties the gauge and tells the scenario where it landed. A cast interrupted
// before the slidecast window never resolves, so it costs nothing. What the limit break does is
// an ordinary JobActions row. Runs with the module off too: the gauge is the scenario's, so a
// press always spends it.
internal sealed class LimitBreakHandler : IUserActionHandler
{
    private const uint LimitBreakCategory = 9;

    public bool RunsWhenDisabled => true;

    public static bool IsLimitBreak(uint actionId)
        => Natives.Data.Action(actionId)?.ActionCategory == LimitBreakCategory;

    // level 1-3; 0 when the job has none at that level.
    public static uint ActionId(uint classJob, int level)
    {
        if (Natives.Data.ClassJob(classJob) is not { } job) return 0;
        return level switch
        {
            1 => job.LimitBreak1,
            2 => job.LimitBreak2,
            3 => job.LimitBreak3,
            _ => 0,
        };
    }

    private static void StartCast(SimParty party, SimCharacter user, uint actionId)
    {
        foreach (var member in party.AllMembers()) member.ActorControl.LimitBreakCast(actionId, member == user);
    }

    // The client's handler empties the HUD gauge; Spend only keeps FilledBars in step.
    internal static void Resolve(SimParty party, SimCharacter user, uint actionId, LimitBreakAim aim)
    {
        var group = Group((user as ISimPartyMember)?.Role, party.LimitBreak.FilledBars);
        foreach (var member in party.AllMembers()) member.ActorControl.LimitBreakResolve(actionId, member == user, group);
        party.LimitBreak.Spend();
        party.LimitBreak.Land(actionId, aim);
    }

    // The local player's own, as the client fired it. JobActionHandler applies its row only with
    // the module on, so it lands here when off; a peer's host learns of it from the report.
    internal static void ResolveOwn(SimWorld world, SimPlayer player, uint actionId, ulong targetId, LimitBreakPress press)
    {
        if (!Natives.UserActions.Enabled) JobActions.ApplyEffects(player, actionId, targetId);
        var aim = AimOf(world, player, press);
        Resolve(world.Party, player, actionId, aim);
        DiagnosticLog.Info($"[LimitBreak] {Natives.Data.ActionName(actionId)} ({actionId}) resolved -- the gauge is spent; aimed from "
                           + $"({aim.Origin.X:F2},{aim.Origin.Z:F2}) heading {aim.Heading:F3}{(aim.Location is { } at ? $" at ({at.X:F2},{at.Z:F2})" : "")}.");
        Plugin.Multiplayer?.ReportLimitBreak(actionId, aim);
    }

    // Toward the enemy the press was used on, else the way the user faces; the ground point of
    // an area-targeted one goes scenario-local.
    internal static LimitBreakAim AimOf(SimWorld world, SimCharacter user, LimitBreakPress press)
    {
        var aim = user.Placement();
        if (world.Children.OfType<SimEnemy>().FirstOrDefault(e => e.IsActive && (ulong)e.GameObjectId == press.TargetId) is { } target)
            aim = aim.Face(target.Position);
        var location = press.WorldLocation is { } global ? world.Coordinates.ToLocal(global) : (Vector3?)null;
        return new LimitBreakAim(aim.Position, aim.Rotation, location);
    }

    // The server's 0x48 p3. Only tank LB1-3, physical-DPS LB3 and magic LB3 were observed; the
    // non-tank LB1/LB2 values are UNVERIFIED.
    private static uint Group(PartyRole? role, int level) => role switch
    {
        PartyRole.MainTank or PartyRole.OffTank => level == 1 ? 18u : 37u,
        PartyRole.RegenHealer or PartyRole.ShieldHealer or PartyRole.CasterDps => 80u,
        _ => 36u,
    };

    public void OnCastStart(ActionType actionType, uint actionId)
    {
        if (actionType != ActionType.Action || !IsLimitBreak(actionId)) return;
        if (Plugin.Game?.World.Party is not { Player: { } player } party) return;
        StartCast(party, player, actionId);
    }

    public void OnAction(ActionType actionType, uint actionId, ulong targetId)
    {
        if (actionType != ActionType.Action || !IsLimitBreak(actionId)) return;
        if (Plugin.Game?.World is not { Party.Player: { } player } world) return;
        ResolveOwn(world, player, actionId, targetId, Natives.PlayerInput.LastLimitBreakPress);
    }
}
