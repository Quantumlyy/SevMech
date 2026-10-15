using System;
using AnoMech.Core.Native.Interfaces;

namespace AnoMech.Core.Game.Party;

// The party's faked limit break gauge. Solo in the inn the real gauge is empty, so Set sends the
// local player the gauge's ActorControlSelf, for display only: the real values are saved once and
// sent back when the party despawns. The client gates presses on it; the request packet is eaten
// by the firewall.
public sealed class LimitBreakGauge
{
    private const ushort UnitsPerBar = 10000;
    private const byte Bars = 3;

    private readonly Func<ActorControl?> player;
    private ushort units;
    private LimitBreakBars? saved;

    internal LimitBreakGauge(Func<ActorControl?> player) => this.player = player;

    public int FilledBars => units / UnitsPerBar;

    public float CurrentBars => units / (float)UnitsPerBar;

    // A limit break as it resolves through the sim, and where it was aimed: the local player's
    // own (LimitBreakHandler) or a bot's (SimParty.UseLimitBreak). A scenario reacts to what it
    // did (a healer LB3's cleanse, where a caster LB3 came down).
    public event Action<uint, LimitBreakAim>? Landed;

    internal void Land(uint actionId, LimitBreakAim aim) => Landed?.Invoke(actionId, aim);

    // An unchanged value sends nothing: a peer mirrors the host's gauge from every snapshot.
    public void Set(float bars)
    {
        var next = (ushort)(Math.Clamp(bars, 0f, Bars) * UnitsPerBar);
        if (saved != null && next == units) return;
        units = next;
        saved ??= Natives.LimitBreak.Read();
        player()?.SetLimitBreakGauge(Bars, units, UnitsPerBar);
    }

    internal void Spend()
    {
        units = 0;
        player()?.SetLimitBreakGauge(Bars, units, UnitsPerBar);
    }

    internal void Restore()
    {
        units = 0;
        if (saved is not { } s) return;
        saved = null;
        player()?.SetLimitBreakGauge(s.BarCount, s.CurrentUnits, s.BarUnits);
    }
}
