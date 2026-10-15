using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Event;

namespace AnoMech.Core.Native.Implementations.Pointers;

internal unsafe class EventFrameworkPointers
{
    // Where the BattleTalk packet lands: shows the text box and plays the voice through the
    // handler's own BattleTalk. speakerKind is the speaker's ObjectKind, which picks the sheet
    // speakerNameId is read from (2 = BNpcName). Nullable so a patch that moves it costs the
    // lines, not the plugin.
    [Signature("40 53 55 56 48 81 EC C0 00 00 00 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 B0 00 00 00 F7 05 ?? ?? ?? ?? FD FF FF FF", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text)]
    public static ProcessBattleTalkDelegate? ProcessBattleTalk { get; private set; }

    public delegate void ProcessBattleTalkDelegate(
        EventFramework* thisPtr, EventId handlerId, ulong speakerEntityId, byte speakerKind, uint speakerNameId,
        uint textId, uint durationMs, uint unknown1, uint unknown2, uint* textArgs, byte textArgCount);

    public static void Initialize()
    {
        Plugin.GameInterop.InitializeFromAttributes(new EventFrameworkPointers());
    }
}
