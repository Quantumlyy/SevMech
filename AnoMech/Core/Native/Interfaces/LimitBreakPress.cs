using System.Numerics;

namespace AnoMech.Core.Native.Interfaces;

// The local player's last limit break as the client fired it: what it was used on, and the
// world-space ground point of an area-targeted one.
public readonly record struct LimitBreakPress(ulong TargetId, Vector3? WorldLocation = null);
