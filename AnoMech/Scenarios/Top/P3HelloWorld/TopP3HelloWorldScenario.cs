using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.EnemyActions;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using AnoMech.Multiplayer;
using static AnoMech.Scenarios.Top.TopConstants;
using Actions = AnoMech.Scenarios.Top.TopActions;

namespace AnoMech.Scenarios.Top.P3HelloWorld;

public sealed class TopP3HelloWorldScenario : IMultiplayerReplayable
{
    public string Name => "Hello World";
    public IPhase Phase => TopZone.P3;
    public bool SupportsMultiplayer => true;
    public float BgmSecondsAtStart => 21.29f;
    public void DrawSettings() => settingsWindow.Draw();
    public bool HasPerPlayerSettings => true;
    public void DrawPerPlayerSettings() => settingsWindow.DrawPerPlayer();
    public object SettingsOverrides => settingsWindow.Overrides;
    public IReadOnlyList<string> SettingsConflicts => settingsWindow.Overrides.Validate().Problems;
    private readonly TopP3HelloWorldSettingsWindow settingsWindow = new();

    public IReadOnlyList<IScenarioAi> AiStrats => [new TopP3HelloWorldAi()];

    private static readonly (ushort Id, string Key)[] GimmickTimelines =
    [
        (10720, "mon_sp/gimmick/z3oz_boss_gimmick08"),
        (6751, "mon_sp/gimmick/z3of_boss_gimmick13"),
        (10721, "mon_sp/gimmick/z3oz_boss_gimmick09"),
        (6753, "mon_sp/gimmick/z3of_boss_gimmick14"),
        (6744, "mon_sp/gimmick/z3of_boss_gimmick09"),
        (6746, "mon_sp/gimmick/z3of_boss_gimmick10"),
        (6750, "mon_sp/gimmick/z3of_boss_gimmick12"),
        (10719, "mon_sp/gimmick/z3oz_boss_gimmick07"),
        (6748, "mon_sp/gimmick/z3of_boss_gimmick11"),
        (6755, "mon_sp/gimmick/z3of_boss_gimmick15"),
    ];

    // A rot's explosion, a tower debuff's wipe and a tether's break hits land this long after the
    // status that sets them off drops.
    private const float ExpiryHitDelay = 0.09f;
    // The server first measures a new tether this long after it goes live, so a pair already in
    // place breaks it no sooner.
    private const float TetherFirstCheck = 0.13f;
    private const float PatchResolveDelay = 10f;

    private SimWorld world = null!;
    private SimParty party = null!;
    private TopP3HelloWorldState state = null!;

    public TopP3HelloWorldState? LastState { get; private set; }

    private sealed record Rot(SimCharacter Holder, RotColor Color);
    private sealed record TowerDebuff(SimCharacter Holder, RotColor Color);

    private SimEnemy? omega;
    private readonly List<Rot> rots = [];
    private readonly List<TowerDebuff> towerDebuffs = [];
    private TopHelpers? towerHelpers;
    private TopHelpers? burstHelpers;
    private readonly EnemyActionCast?[] towerCasts = new EnemyActionCast?[4];
    private readonly List<SimTether> prepTethers = [];
    private readonly List<SimTether> localTethers = [];
    private readonly List<SimTether> remoteTethers = [];
    private float tethersMeasuredFrom;
    private List<SimCharacter> stackHolders = [];
    private List<SimCharacter> defamationHolders = [];
    private int resolvedPatch = -1;
    private bool wiped;
    private (uint ActionId, string Reason, float At)? loopWipe;

    private const float LoopStarts = 11.00f;
    private const float LoopEnds = 108.45f;

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        party = worldParam.Party;
        state = new TopP3HelloWorldState(world.Rng, party, settingsWindow.Overrides);
        LastState = state;
        rots.Clear();
        towerDebuffs.Clear();
        towerHelpers = null;
        burstHelpers = null;
        Array.Clear(towerCasts);
        prepTethers.Clear();
        localTethers.Clear();
        remoteTethers.Clear();
        tethersMeasuredFrom = 0f;
        stackHolders = [];
        defamationHolders = [];
        resolvedPatch = -1;
        wiped = false;
        loopWipe = null;
        if (Plugin.Game is { } game)
        {
            game.PartyMemberKilled -= WipeForADeath;
            game.PartyMemberKilled += WipeForADeath;
        }
        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<TopP3HelloWorldState>)AiStrats[idx]).Run(state, world);

        world.Events.Add(0.1f, SpawnOmega);
        world.Events.Add(0.1f, SpawnHelpers);
        world.Events.Add(2.91f, () => world.Map.BattleTalk(BNpcNameId.OmegaFinal, BattleTalkId.HelloWorld, 6000));
        world.Events.Add(3.00f, CastHelloWorld);
        world.Events.Add(7.96f, ApplyCodeSmells);
        world.Events.Add(8.01f, () => world.Map.SetWeather(176, 1f));
        world.Events.Add(8.01f, ApplyTetherCodeSmells);
        world.Events.Add(11.00f, ActivateBugs);
        world.Events.Add(11.00f, () => ShowPrepTethers(0));

        world.Events.Add(22.16f, () => CastPatch(0));
        world.Events.Add(30.99f, () => ActivateTethers(0));
        world.Events.Add(31.97f, () => ShowPrepTethers(1));
        world.Events.Add(34.95f, ExpireLatentDefects);
        world.Events.Add(40.89f, CheckTethersExpired);

        world.Events.Add(43.21f, () => CastPatch(1));
        world.Events.Add(52.00f, () => ActivateTethers(1));
        world.Events.Add(52.98f, () => ShowPrepTethers(2));
        world.Events.Add(61.90f, CheckTethersExpired);

        world.Events.Add(64.26f, () => CastPatch(2));
        world.Events.Add(73.00f, () => ActivateTethers(2));
        world.Events.Add(73.98f, () => ShowPrepTethers(3));
        world.Events.Add(76.95f, ExpireLatentSynchronizationBugs);
        world.Events.Add(82.90f, CheckTethersExpired);

        world.Events.Add(85.30f, () => CastPatch(3));
        world.Events.Add(93.99f, () => ActivateTethers(3));
        world.Events.Add(103.89f, CheckTethersExpired);

        world.Events.Add(108.45f, CastCriticalError);
        world.Events.Add(116.43f, () => world.Map.SetWeather(79, 1f));
        foreach (var t in AutoAttacks)
            world.Events.Add(t, AutoAttack);
    }

    // Omega's auto-attacks on its main target between its own casts.
    private static readonly float[] AutoAttacks =
    [
        11.07f, 14.10f, 17.13f, 20.16f, 32.22f, 35.26f, 38.28f, 41.31f, 53.38f, 56.41f,
        59.43f, 62.46f, 74.53f, 77.56f, 80.59f, 83.62f, 98.73f, 101.76f, 104.78f, 107.81f,
    ];

    private void AutoAttack() => TopBoss.FinalOmegaAutoAttack(world, omega, state.Rng);

    public void Tick(float delta, float elapsed)
    {
        TopBoss.TurnToMainTank(party, omega, TopBoss.FinalOmegaTurnSpeed, delta);
        TickTethers(remoteTethers, tether => tether.StretchGt(Geometry.HwTetherBreakDistance));
        TickTethers(localTethers, tether => tether.StretchLt(Geometry.HwTetherBreakDistance));
        DebugExpiredBugs();
        ExplodeExpiredRots();
        SpreadRots();
        CheckTowerDebuffs();
        WipeForTheLoop();
    }

    public void RunInstanceEvents(SimWorld instanceWorld) => Natives.TimelinePreload.Preload(GimmickTimelines, "TopP3HelloWorld");

    private void SpawnOmega()
    {
        omega = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.OmegaFinal,
            NameId: BNpcNameId.OmegaFinal,
            Level: Level,
            Targetable: true,
            Placement: new Placement(Vector3.Zero, MathF.PI)));
    }

    // Omega keeps the main tank as its target and turns to face it before each cast.
    private void FaceMainTank()
    {
        if (omega is not { IsActive: true } boss || party.Get(PartyRole.MainTank) is not { } tank || !tank.IsAlive()) return;
        boss.SetTarget(tank, follow: false);
        boss.Face(tank.Position);
    }

    private void CastHelloWorld()
    {
        FaceMainTank();
        omega?.Cast(Actions.HelloWorld);
    }

    private void CastCriticalError()
    {
        FaceMainTank();
        omega?.Cast(Actions.CriticalError);
    }

    private void SpawnHelpers()
    {
        towerHelpers = new TopHelpers(world, BNpcNameId.OmegaFinal, 4, "TopP3HelloWorld");
        burstHelpers = new TopHelpers(world, BNpcNameId.OmegaFinal, 16, "TopP3HelloWorld");
    }

    private IEnumerable<SimCharacter> Members(HelloWorldRole role, int patch)
        => state.WithRole(role, patch).Select(party.Get).OfType<SimCharacter>();

    private HelloWorldRole? RoleOf(SimCharacter member, int patch)
        => member is ISimPartyMember { Role: var role } ? state.RoleIn(role, patch) : null;

    private static string Who(SimCharacter member) => member is ISimPartyMember { Role: var role } ? role.ToString() : "someone";

    // Whatever goes wrong wipes the party on the spot, in the name of the mechanic it broke: what
    // the real fight does later with a missed pass or an unshared stack can't be followed
    // faithfully, and a wipe there is clearer than one at the next patch.
    private void Wipe(uint actionId, string reason)
    {
        if (wiped) return;
        wiped = true;
        DiagnosticLog.Info($"[TopP3HelloWorld] Wipe: {reason}.");
        world.Announce($"Hello World wipe: {reason}.");
        party.WipeAllPlayers(actionId, $"Hello World wipe: {reason}");
    }

    // The loop needs every player for every patch. Put off to the next frame, so a raidwide's deaths
    // each keep their own reason, and a tether whose end just died fails by its own hit.
    private void WipeForADeath(PartyRole role, string cause, uint? actionId)
    {
        if (wiped || loopWipe is not null || Plugin.Game is not { } game || game.ActiveScenario != this) return;
        var now = world.Events.Elapsed;
        if (now < LoopStarts || now > LoopEnds) return;
        loopWipe = (actionId ?? SimCharacterDeathExtensions.Environment, $"{role} died ({cause}), and the loop can't go on without them", now);
    }

    private void WipeForTheLoop()
    {
        if (loopWipe is not { } pending || world.Events.Elapsed <= pending.At) return;
        loopWipe = null;
        if (!party.IsAlive) wiped = true;
        else Wipe(pending.ActionId, pending.Reason);
    }

    // Everyone must share a stack before Latent Synchronization Bug runs out; the first Christmas
    // pair must take a defamation before Latent Defect does.
    private void ApplyCodeSmells()
    {
        party.ForEachActive(member => member.AddStatus(StatusId.HWNeedStack, 69f));
        foreach (var member in Members(HelloWorldRole.Local, 0)) member.AddStatus(StatusId.HWNeedDefamation, 27f);
        foreach (var member in Members(HelloWorldRole.Defamation, 0))
        {
            member.AddStatus(StatusId.HWPrepDefamation, 3f);
            member.AddStatus(state.DefamationColor.PrepStatusId, 3f);
        }
        foreach (var member in Members(HelloWorldRole.Stack, 0))
        {
            member.AddStatus(StatusId.HWPrepStack, 3f);
            member.AddStatus(state.StackColor.PrepStatusId, 3f);
        }
    }

    // A tether's code smell counts down to the patch whose tether it becomes.
    private void ApplyTetherCodeSmells()
    {
        foreach (var role in PerRole.All)
        {
            if (party.Get(role) is not { } member) continue;
            for (var patch = 0; patch < 4; patch++)
            {
                var statusId = state.RoleIn(role, patch) switch
                {
                    HelloWorldRole.Local => StatusId.HWPrepLocalTether,
                    HelloWorldRole.Remote => StatusId.HWPrepRemoteTether,
                    _ => (ushort)0,
                };
                if (statusId != 0) member.AddStatus(statusId, 23f + 21f * patch);
            }
        }
    }

    private void ActivateBugs()
    {
        defamationHolders = Members(HelloWorldRole.Defamation, 0).ToList();
        stackHolders = Members(HelloWorldRole.Stack, 0).ToList();
        foreach (var member in defamationHolders)
        {
            member.AddStatus(StatusId.HWDefamation, 21f);
            GiveRot(member, state.DefamationColor);
        }
        foreach (var member in stackHolders)
        {
            member.AddStatus(StatusId.HWStack, 21f);
            GiveRot(member, state.StackColor);
        }
    }

    private void ShowPrepTethers(int patch)
    {
        var locals = Members(HelloWorldRole.Local, patch).ToArray();
        var remotes = Members(HelloWorldRole.Remote, patch).ToArray();
        if (locals.Length == 2) prepTethers.Add(world.Tether(locals[0], locals[1], TetherId.HWPrepLocal));
        if (remotes.Length == 2) prepTethers.Add(world.Tether(remotes[0], remotes[1], TetherId.HWPrepRemote));
    }

    // The towers' bars run beside Omega's; each tower's hit lands on whoever stands in it. The
    // patch resolves from the same moment as the towers' casts, so it reads their hits whatever
    // the frame length.
    private void CastPatch(int patch)
    {
        FaceMainTank();
        omega?.Cast(ActionId.LatentDefect, animationLock: 4.1f);
        var towers = state.Towers[patch];
        for (var i = 0; i < towerCasts.Length; i++)
        {
            var tower = new Placement(towers.Position(i), 0f).Face(Vector3.Zero);
            towerCasts[i] = towerHelpers?.At(i, tower)?.Cast(towers.ColorOf(i).Tower);
        }
        world.Events.Add(PatchResolveDelay, () => ResolvePatch(patch));
    }

    private void ActivateTethers(int patch)
    {
        foreach (var tether in prepTethers.Where(tether => Members(HelloWorldRole.Local, patch).Concat(Members(HelloWorldRole.Remote, patch)).Contains(tether.A)).ToList())
        {
            tether.Despawn();
            prepTethers.Remove(tether);
        }
        var locals = Members(HelloWorldRole.Local, patch).ToArray();
        var remotes = Members(HelloWorldRole.Remote, patch).ToArray();
        if (locals.Length == 2) localTethers.Add(world.Tether(locals[0], locals[1], TetherId.HWLocal, 10f, StatusId.HWLocalTether));
        if (remotes.Length == 2) remoteTethers.Add(world.Tether(remotes[0], remotes[1], TetherId.HWRemote, 10f, StatusId.HWRemoteTether));
        tethersMeasuredFrom = world.Events.Elapsed + TetherFirstCheck;
    }

    // Just after the towers' hits, which leave no vulnerability; the stack and defamation hits
    // leave one, so a player caught by both of those dies.
    private void ResolvePatch(int patch)
    {
        var towers = state.Towers[patch];
        resolvedPatch = patch;
        for (var i = 0; i < towerCasts.Length; i++)
            foreach (var soak in towerCasts[i]?.Hits ?? [])
                if (!soak.Killed) towerDebuffs.Add(new TowerDebuff(soak.Who, towers.ColorOf(i)));
        CheckWhoTheBugsReach(patch);

        var nextStacks = new List<SimCharacter>();
        foreach (var holder in stackHolders.Where(holder => holder.IsAlive()))
        {
            holder.RemoveStatus(StatusId.HWStack);
            GiveDebugger(holder, StatusId.HWImmuneStack);
            if (burstHelpers?.Next(holder.Placement())?.Cast(Actions.CriticalSynchronizationBug, holder) is not { } stack) continue;
            foreach (var hit in stack.Hits.Where(hit => !hit.Killed && hit.Who != holder))
                PassBug(hit.Who, StatusId.HWImmuneStack, StatusId.HWStack, nextStacks);
        }

        var nextDefamations = new List<SimCharacter>();
        foreach (var holder in defamationHolders.Where(holder => holder.IsAlive()))
        {
            holder.RemoveStatus(StatusId.HWDefamation);
            GiveDebugger(holder, StatusId.HWImmuneDefamation);
            if (burstHelpers?.Next(holder.Placement())?.Cast(Actions.CriticalOverflowBug, holder) is not { } defamation) continue;
            foreach (var hit in defamation.Hits.Where(hit => !hit.Killed && hit.Who != holder))
                PassBug(hit.Who, StatusId.HWImmuneDefamation, StatusId.HWDefamation, nextDefamations);
        }

        stackHolders = nextStacks;
        defamationHolders = nextDefamations;
    }

    // Each stack is shared by its holder and one blue tether, each defamation taken by one Christmas
    // tether; in the last patch every tether stacks and no one takes a defamation.
    private void CheckWhoTheBugsReach(int patch)
    {
        var lastPatch = patch == 3;
        var stacksTaken = new Dictionary<SimCharacter, int>();
        foreach (var holder in stackHolders.Where(holder => holder.IsAlive()))
        {
            var reached = party.Find.InsideActionAoe(ActionId.CriticalSynchronizationBug, holder.Placement()).Where(hit => hit != holder).ToList();
            if (reached.Count == 0) Wipe(ActionId.CriticalSynchronizationBug, $"no one shared {Who(holder)}'s stack");
            foreach (var hit in reached)
            {
                var role = RoleOf(hit, patch);
                if (role != HelloWorldRole.Remote && !(lastPatch && role == HelloWorldRole.Local))
                    Wipe(ActionId.CriticalSynchronizationBug, $"{Who(holder)}'s stack reached {Who(hit)}");
                stacksTaken[hit] = stacksTaken.GetValueOrDefault(hit) + 1;
            }
        }
        if (!lastPatch)
            foreach (var remote in Members(HelloWorldRole.Remote, patch).Where(member => member.IsAlive()))
            {
                var taken = stacksTaken.GetValueOrDefault(remote);
                if (taken == 0) Wipe(ActionId.CriticalSynchronizationBug, $"{Who(remote)} didn't take a stack");
                else if (taken > 1) Wipe(ActionId.CriticalSynchronizationBug, $"{Who(remote)} took both stacks");
            }

        var defamationsTaken = new Dictionary<SimCharacter, int>();
        foreach (var holder in defamationHolders.Where(holder => holder.IsAlive()))
            foreach (var hit in party.Find.InsideActionAoe(ActionId.CriticalOverflowBug, holder.Placement()).Where(hit => hit != holder))
            {
                if (lastPatch || RoleOf(hit, patch) != HelloWorldRole.Local)
                    Wipe(ActionId.CriticalOverflowBug, $"{Who(holder)}'s defamation reached {Who(hit)}");
                defamationsTaken[hit] = defamationsTaken.GetValueOrDefault(hit) + 1;
            }
        if (lastPatch) return;
        foreach (var local in Members(HelloWorldRole.Local, patch).Where(member => member.IsAlive()))
        {
            var taken = defamationsTaken.GetValueOrDefault(local);
            if (taken == 0) Wipe(ActionId.CriticalOverflowBug, $"{Who(local)} didn't take a defamation");
            else if (taken > 1) Wipe(ActionId.CriticalOverflowBug, $"{Who(local)} took both defamations");
        }
    }

    // A bug's debugger comes on the moment the bug runs out, a beat before the bug's own hit.
    private void DebugExpiredBugs()
    {
        foreach (var holder in stackHolders.Where(holder => holder.IsAlive() && !holder.HasStatus(StatusId.HWStack)))
            GiveDebugger(holder, StatusId.HWImmuneStack);
        foreach (var holder in defamationHolders.Where(holder => holder.IsAlive() && !holder.HasStatus(StatusId.HWDefamation)))
            GiveDebugger(holder, StatusId.HWImmuneDefamation);
    }

    private static void GiveDebugger(SimCharacter holder, ushort debuggerStatusId)
    {
        if (!holder.HasStatus(debuggerStatusId)) holder.AddStatus(debuggerStatusId);
    }

    // A debugger turns the bug away once and is spent doing it.
    private static void PassBug(SimCharacter hit, ushort debuggerStatusId, ushort bugStatusId, List<SimCharacter> nextHolders)
    {
        if (hit.HasStatus(debuggerStatusId))
        {
            hit.RemoveStatus(debuggerStatusId);
            return;
        }
        if (nextHolders.Contains(hit)) return;
        hit.AddStatus(bugStatusId, 21f);
        nextHolders.Add(hit);
    }

    private void GiveRot(SimCharacter member, RotColor color)
    {
        member.AddStatus(color.RotStatusId, 27f);
        rots.Add(new Rot(member, color));
    }

    // Anyone who touches a rot catches their own copy, unless that colour's debugger or a copy of
    // it is already on them.
    private void SpreadRots()
    {
        foreach (var rot in rots.Where(rot => rot.Holder.IsAlive()).ToList())
            foreach (var other in party.Find.InsideCircle(rot.Holder.Position, Geometry.HwRotPassRadius))
            {
                if (other == rot.Holder || other.HasStatus(rot.Color.RotStatusId) || other.HasStatus(rot.Color.DebuggerStatusId)) continue;
                if (!MeantToTake(other, rot.Color))
                    Wipe(rot.Color.Explosion.ActionId, $"{Who(rot.Holder)}'s {rot.Color} rot passed to {Who(other)}");
                GiveRot(other, rot.Color);
            }
    }

    private bool MeantToTake(SimCharacter member, RotColor color)
    {
        if (resolvedPatch is < 0 or >= 3) return false;
        var taker = color == state.DefamationColor ? HelloWorldRole.Local : HelloWorldRole.Remote;
        return RoleOf(member, resolvedPatch) == taker;
    }

    // A rot holder's rot runs out after its patch; by then both tether players meant to take that
    // colour must have it.
    private void CheckRotsTaken(Rot expired)
    {
        if (resolvedPatch is < 0 or >= 3) return;
        var holderRole = RoleOf(expired.Holder, resolvedPatch);
        var taker = holderRole switch
        {
            HelloWorldRole.Defamation when expired.Color == state.DefamationColor => HelloWorldRole.Local,
            HelloWorldRole.Stack when expired.Color == state.StackColor => HelloWorldRole.Remote,
            _ => (HelloWorldRole?)null,
        };
        if (taker is not { } takers) return;
        foreach (var member in Members(takers, resolvedPatch).Where(member => member.IsAlive() && !member.HasStatus(expired.Color.RotStatusId)))
            Wipe(expired.Color.Explosion.ActionId, $"{Who(member)} never took a {expired.Color} rot before {Who(expired.Holder)}'s ran out");
    }

    // The holder gets its debugger as the rot runs out; the explosion follows, and it also clears
    // their own tower debuff of that colour.
    private void ExplodeExpiredRots()
    {
        foreach (var rot in rots.Where(rot => !rot.Holder.HasStatus(rot.Color.RotStatusId)).ToList())
        {
            rots.Remove(rot);
            if (!rot.Holder.IsAlive()) continue;
            CheckRotsTaken(rot);
            rot.Holder.AddStatus(rot.Color.DebuggerStatusId);
            world.Events.Add(ExpiryHitDelay, () => Explode(rot));
        }
    }

    private void Explode(Rot rot)
    {
        var holder = rot.Holder;
        if (!holder.IsAlive()) return;
        burstHelpers?.Next(holder.Placement())?.Cast(rot.Color.Explosion, holder);
        if (towerDebuffs.FirstOrDefault(debuff => debuff.Holder == holder && debuff.Color == rot.Color) is { } cleansed)
        {
            towerDebuffs.Remove(cleansed);
            holder.RemoveStatus(rot.Color.TowerStatusId);
        }
    }

    private void CheckTowerDebuffs()
    {
        foreach (var debuff in towerDebuffs.Where(debuff => !debuff.Holder.HasStatus(debuff.Color.TowerStatusId)).ToList())
        {
            towerDebuffs.Remove(debuff);
            if (!debuff.Holder.IsAlive()) continue;
            world.Events.Add(ExpiryHitDelay, () => burstHelpers?.Next(debuff.Holder.Placement())?.Cast(debuff.Color.TowerExpired, debuff.Holder));
        }
    }

    private void TickTethers(List<SimTether> tethers, Predicate<SimTether> breaks)
    {
        foreach (var tether in tethers.Where(SimTether.IsAnyDead).ToList())
        {
            tethers.Remove(tether);
            FailTether(tether);
        }
        if (world.Events.Elapsed < tethersMeasuredFrom) return;
        foreach (var tether in tethers.Where(tether => breaks(tether)).ToList())
        {
            tethers.Remove(tether);
            BreakTether(tether);
        }
    }

    // The regression drops as the tether breaks; the hits follow, a raidwide from each end, each
    // leaving a stack of a short Magic Vulnerability Up and Thrice-come Ruin, so a second break
    // before they fall off is lethal.
    private void BreakTether(SimTether tether)
    {
        if (tether.Resolved || tether.A is not { } a || tether.B is not { } b) return;
        tether.Resolved = true;
        tether.Despawn();
        Placement[] ends = [a.Placement(), b.Placement()];
        world.Events.Add(ExpiryHitDelay, () =>
        {
            foreach (var end in ends) burstHelpers?.Next(end)?.Cast(Actions.HwTetherBreak);
        });
    }

    private void FailTether(SimTether tether)
    {
        if (tether.Resolved) return;
        tether.Resolved = true;
        tether.Despawn();
        if (wiped) return;
        // Its hit is the wipe; the loop's own must not get in first.
        wiped = true;
        var reason = $"Hello World wipe: {(tether.A is { } a ? Who(a) : "?")} and {(tether.B is { } b ? Who(b) : "?")}'s {TetherName(tether)} didn't break in time";
        world.Announce($"{reason}.");
        if (burstHelpers?.Next(tether.A?.Placement() ?? default)?.Cast(Actions.HwTetherFail) is null)
            party.WipeAllPlayers(ActionId.HwTetherFail, reason);
    }

    private string TetherName(SimTether tether) => localTethers.Contains(tether) ? "Christmas tether" : remoteTethers.Contains(tether) ? "blue tether" : "tether";

    private void CheckTethersExpired()
    {
        foreach (var tether in localTethers.Concat(remoteTethers).ToList()) FailTether(tether);
        localTethers.Clear();
        remoteTethers.Clear();
    }

    private void ExpireLatentDefects() => ExpireNeed(StatusId.HWNeedDefamation, Actions.LatentDefectExpired);

    private void ExpireLatentSynchronizationBugs() => ExpireNeed(StatusId.HWNeedStack, Actions.LatentSynchronizationDefect);

    private void ExpireNeed(ushort statusId, EnemyAction expired)
    {
        foreach (var member in party.ActiveMembers().Where(member => member.HasStatus(statusId)).ToList())
        {
            member.RemoveStatus(statusId);
            if (burstHelpers?.Next(member.Placement())?.Cast(expired, member) is null)
                Wipe(expired.ActionId, $"{Who(member)}'s {Natives.Data.ActionName(expired.ActionId)} ran out");
        }
    }

    public MpMessage? BuildReplayStateMessage()
        => LastState is { } s ? new TopP3HelloWorldAiReplayStateMessage(
            s.Pairs.List, s.DefamationColor == RotColor.Red,
            s.Towers.Select(towers => towers.Intercardinal).ToArray(), s.Towers.Select(towers => towers.RedStart).ToArray())
        : null;

    public bool PeersFollowHostPlan => true;

    public object? StartReplay(MpMessage message, int aiIndex, PartyRole myRole, SimWorld replayWorld)
    {
        if (message is not TopP3HelloWorldAiReplayStateMessage msg) return null;
        return TopP3HelloWorldState.FromNetworkReplay(replayWorld.Party, msg.Pairs, msg.DefamationIsRed, msg.Intercardinal, msg.RedStart);
    }
}
