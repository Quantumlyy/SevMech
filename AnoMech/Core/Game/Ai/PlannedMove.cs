using System.Numerics;
using AnoMech.Core.Game.Party;

namespace AnoMech.Core.Game.Ai;

// One seat's move as the Ai issued it: the spot before the jitter, and how it leaves. Time is
// the schedule it was issued at, as the Ai's log names it. A peer whose debug bot walks the
// host's plan repeats it (AiManager.Follow).
public readonly record struct PlannedMove(PartyRole Role, float Time, Vector2 Spot, float Jitter, bool Sprint, float Delay);
