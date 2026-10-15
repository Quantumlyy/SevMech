using System;
using System.Collections.Generic;
using System.Linq;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.UI.Arrays;
using FFXIVClientStructs.FFXIV.Component.GUI;
using GroupPartyMember = FFXIVClientStructs.FFXIV.Client.Game.Group.PartyMember;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.Native.Implementations.Interop;
using AnoMech.Core.Native.Implementations.Pointers;

namespace AnoMech.Core.Native.Implementations;

// Mirrors the sim party into MainGroup the way the server does: an UpdateParty packet whenever
// the roster changes. The _PartyList addon only renders members when MemberCount > 0, and
// resolves each slot's BattleChara by EntityId (registered in CharacterManager; scenarios are
// inn-gated upstream) for status icons and timer text.
//
// The firewall keeps real UpdateParty packets out during a sim, so the pre-sim party captured
// at the first send is what Clear restores.
internal sealed unsafe class PartyHud : IPartyHud
{
    // Doppels have no ContentId; this top byte never appears on a real one.
    private const ulong DoppelContentIdTag = 0xFF00000000000000UL;
    private const long SimPartyId = 0xFFFFFFFF;

    private const int MaxSlots = 8;
    private const string AddonName = "_PartyList";
    // PartyListStringArray: 6 header strings, then 23 per member, the slot number glyph first.
    private const int MemberStringsStart = 6;
    private const int StringsPerMember = 23;

    private readonly List<(uint EntityId, byte ClassJob, byte Level)> sentRoster = new();
    private readonly List<(uint EntityId, byte ClassJob, byte Level)> roster = new();
    private bool ownsRoster;
    private UpdatePartyPacket realParty;

    // Top to bottom; null leaves the rows where the game puts them.
    private IReadOnlyList<PartyRole>? displayOrder;
    private readonly Dictionary<uint, PartyRole> roleByEntity = new();

    public PartyHud()
    {
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PreRequestedUpdate, AddonName, OnPreRequestedUpdate);
    }

    public void Dispose()
    {
        Plugin.AddonLifecycle.UnregisterListener(AddonEvent.PreRequestedUpdate, AddonName, OnPreRequestedUpdate);
    }

    public void SetDisplayOrder(IReadOnlyList<PartyRole>? order) => displayOrder = order;

    public void Refresh(SimParty party)
    {
        if (party.AllMembers().Count() < 2) return;

        var gm = GroupManager.Instance();
        if (gm == null) return;

        roleByEntity.Clear();
        foreach (var (role, member) in party.FilledSlots())
        {
            var bc = member.BattleCharaPtr();
            if (bc != null) roleByEntity[((GameObject*)bc)->EntityId] = role;
        }

        var members = OrderedMembers(party);
        roster.Clear();
        foreach (var p in members)
        {
            var bc = (BattleChara*)p;
            roster.Add((((GameObject*)bc)->EntityId, bc->ClassJob, bc->Level));
        }

        if (!ownsRoster || !roster.SequenceEqual(sentRoster))
        {
            if (!ownsRoster)
            {
                realParty = SnapshotPacket(ref gm->MainGroup);
                ownsRoster = true;
            }
            SendRoster(members, realParty);
            sentRoster.Clear();
            sentRoster.AddRange(roster);
        }
    }

    public void Clear()
    {
        displayOrder = null;
        roleByEntity.Clear();
        if (!ownsRoster) return;
        ownsRoster = false;
        sentRoster.Clear();
        var packet = realParty;
        PacketDispatcherPointers.HandleUpdatePartyPacket(0, &packet, 0);
        realParty = default;
    }

    // The player first, then the bots in role order: the slot order the player sees.
    private static List<nint> OrderedMembers(SimParty party)
    {
        var list = new List<nint>();
        foreach (var member in party.AllMembers().OrderBy(m => m is SimNpc ? 1 : 0))
        {
            var bc = member.BattleCharaPtr();
            if (bc != null) list.Add((nint)bc);
        }
        return list;
    }

    private void SendRoster(List<nint> members, in UpdatePartyPacket real)
    {
        var packet = new UpdatePartyPacket();
        var entries = (UpdatePartyPacketMember*)&packet;
        var count = Math.Min(members.Count, UpdatePartyPacket.Capacity);
        for (var i = 0; i < count; i++)
            WriteEntry(&entries[i], (BattleChara*)members[i]);
        packet.MemberCount = (byte)count;
        packet.LeaderIndex = 0;
        packet.PartyId = real.PartyId != 0 ? real.PartyId : SimPartyId;
        packet.PartyId_2 = real.PartyId_2 != 0 ? real.PartyId_2 : SimPartyId;
        PacketDispatcherPointers.HandleUpdatePartyPacket(0, &packet, 0);
    }

    private static void WriteEntry(UpdatePartyPacketMember* entry, BattleChara* bc)
    {
        var obj = (GameObject*)bc;
        // The local player keeps its real ids: AgentReadyCheck and other party lookups match on them.
        entry->ContentId = bc->ContentId != 0 ? bc->ContentId : DoppelContentIdTag | obj->EntityId;
        entry->AccountId = bc->AccountId != 0 ? bc->AccountId : DoppelContentIdTag | obj->EntityId;
        entry->EntityId = obj->EntityId;
        entry->PetEntityId = 0xE0000000;
        entry->CompanionEntityId = 0xE0000000;
        entry->CurrentHP = bc->Health;
        entry->MaxHP = bc->MaxHealth;
        entry->CurrentMP = (ushort)bc->Mana;
        entry->MaxMP = (ushort)bc->MaxMana;
        entry->HomeWorld = bc->HomeWorld;
        entry->TerritoryType = (ushort)Plugin.ClientState.TerritoryType;
        entry->Valid = 1;
        entry->ClassJob = bc->ClassJob;
        entry->Sex = bc->DrawData.CustomizeData.Sex;
        entry->Level = bc->Level;
        for (var i = 0; i < UpdatePartyPacketMember.NameLength - 1; i++)
        {
            var b = obj->Name[i];
            entry->Name[i] = b;
            if (b == 0) break;
        }
    }

    // The packet the server would send for the party as it stands; all zero when solo, which is
    // what the server sends on leaving a party.
    private static UpdatePartyPacket SnapshotPacket(ref GroupManager.Group grp)
    {
        var packet = new UpdatePartyPacket();
        if (grp.MemberCount == 0) return packet;
        var entries = (UpdatePartyPacketMember*)&packet;
        var count = Math.Min((int)grp.MemberCount, UpdatePartyPacket.Capacity);
        for (var i = 0; i < count; i++)
            SnapshotEntry(&entries[i], ref grp.PartyMembers[i]);
        packet.MemberCount = (byte)count;
        packet.LeaderIndex = (byte)grp.PartyLeaderIndex;
        packet.PartyId = grp.PartyId;
        packet.PartyId_2 = grp.PartyId_2;
        return packet;
    }

    private static void SnapshotEntry(UpdatePartyPacketMember* entry, ref GroupPartyMember slot)
    {
        entry->AccountId = slot.AccountId;
        entry->ContentId = slot.ContentId;
        entry->EntityId = slot.EntityId;
        entry->PetEntityId = slot.PetEntityId;
        entry->CompanionEntityId = slot.CompanionEntityId;
        entry->CurrentHP = slot.CurrentHP;
        entry->MaxHP = slot.MaxHP;
        entry->CurrentMP = slot.CurrentMP;
        entry->MaxMP = slot.MaxMP;
        entry->HomeWorld = slot.HomeWorld;
        entry->TerritoryType = slot.TerritoryType;
        entry->Valid = 1;
        entry->ClassJob = slot.ClassJob;
        entry->Sex = slot.Sex;
        entry->Level = slot.Level;
        entry->SyncedLevel = (byte)((slot.Flags & 0x8) != 0 ? slot.Level : 0);
        for (var i = 0; i < UpdatePartyPacketMember.NameLength - 1; i++)
        {
            var b = slot.Name[i];
            entry->Name[i] = b;
            if (b == 0) break;
        }
        var statuses = (UpdatePartyPacketStatus*)((byte*)entry + UpdatePartyPacketMember.StatusesOffset);
        for (var i = 0; i < UpdatePartyPacketMember.StatusCount; i++)
        {
            ref var s = ref slot.StatusManager.Status[i];
            statuses[i].StatusId = s.StatusId;
            statuses[i].Param = s.Param;
            statuses[i].RemainingTime = s.RemainingTime;
            statuses[i].SourceEntityId = s.SourceObject.ObjectId;
        }
    }

    // Each member's block keeps its place in the arrays and the addon draws it at DisplayRow,
    // which is how the game lays out a dragged party list. Moving DisplayRow (and the slot
    // number with it) reorders the rows; clicks stay on the block, so targeting is unaffected.
    private void OnPreRequestedUpdate(AddonEvent type, AddonArgs args)
    {
        if (displayOrder is not { } order || roleByEntity.Count == 0) return;
        if (args is not AddonRequestedUpdateArgs reqArgs) return;
        var numArrays = (NumberArrayData**)reqArgs.NumberArrayData;
        var strArrays = (StringArrayData**)reqArgs.StringArrayData;
        if (numArrays == null || strArrays == null) return;
        var numArr = numArrays[(int)NumberArrayType.PartyList];
        var strArr = strArrays[(int)StringArrayType.PartyList];
        if (numArr == null || strArr == null) return;
        var list = (PartyListNumberArray*)numArr->IntArray;
        var count = Math.Clamp(list->PartyListCount, 0, MaxSlots);
        if (count < 2) return;

        // A row the order doesn't name keeps its relative place below the ordered ones.
        Span<int> rank = stackalloc int[MaxSlots];
        Span<int> oldRow = stackalloc int[MaxSlots];
        for (var block = 0; block < count; block++)
        {
            ref var member = ref list->PartyMembers[block];
            oldRow[block] = member.DisplayRow;
            var index = roleByEntity.TryGetValue(member.EntityId, out var role) ? IndexOf(order, role) : -1;
            rank[block] = index >= 0 ? index : MaxSlots + member.DisplayRow;
        }

        Span<int> rows = stackalloc int[MaxSlots];
        oldRow[..count].CopyTo(rows);
        rows[..count].Sort();
        Span<int> byRank = stackalloc int[MaxSlots];
        for (var i = 0; i < count; i++) byRank[i] = i;
        for (var i = 1; i < count; i++)
            for (var j = i; j > 0 && rank[byRank[j]] < rank[byRank[j - 1]]; j--)
                (byRank[j], byRank[j - 1]) = (byRank[j - 1], byRank[j]);

        var glyphs = new byte[MaxSlots][];
        for (var block = 0; block < count; block++)
            glyphs[block] = ReadString(strArr, GlyphIndex(block));
        for (var k = 0; k < count; k++)
        {
            var block = byRank[k];
            var newRow = rows[k];
            if (newRow == oldRow[block]) continue;
            list->PartyMembers[block].DisplayRow = newRow;
            var source = oldRow[..count].IndexOf(newRow);
            if (source >= 0) WriteString(strArr, GlyphIndex(block), glyphs[source]);
        }
    }

    private static int IndexOf(IReadOnlyList<PartyRole> order, PartyRole role)
    {
        for (var i = 0; i < order.Count; i++)
            if (order[i] == role) return i;
        return -1;
    }

    private static int GlyphIndex(int block) => MemberStringsStart + block * StringsPerMember;

    private static byte[] ReadString(StringArrayData* strArr, int index)
    {
        byte* text = strArr->StringArray[index];
        if (text == null) return [0];
        var length = 0;
        while (text[length] != 0) length++;
        var bytes = new byte[length + 1];
        new ReadOnlySpan<byte>(text, length).CopyTo(bytes);
        return bytes;
    }

    private static void WriteString(StringArrayData* strArr, int index, byte[] bytes)
    {
        fixed (byte* text = bytes) strArr->SetValue(index, text, managed: true);
    }
}
