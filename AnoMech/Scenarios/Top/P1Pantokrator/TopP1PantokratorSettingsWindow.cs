using AnoMech.Core.Game.Party;
using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios.Top.P1Pantokrator;

public sealed class TopP1PantokratorSettingsWindow
{
    public TopP1PantokratorStateOverrides Overrides { get; } = new();

    private PartyRole editingSeat = PartyRole.MainTank;

    private static readonly string[] FlameLabels =
        ["Random", "N", "NNE", "ENE", "E", "ESE", "SSE", "S", "SSW", "WSW", "W", "WNW", "NNW"];
    private static readonly string[] TurnLabels = ["Random", "Clockwise", "Counter-clockwise"];
    private static readonly string[] NumberLabels = ["Random", "1", "2", "3", "4"];

    // Solo, the player's own pick sits in this panel; a host assigns seats from the Multiplayer
    // window.
    public void Draw()
    {
        var solo = !PerRole.SeatsActive;
        if (ImGui.Button("Auto"))
        {
            Overrides.FlameStart = null;
            Overrides.Clockwise = null;
            if (solo) Overrides.Number.Mine = null;
        }
        if (SettingsGrid.Begin("##p1pantokrator"))
        {
            SettingsGrid.Row("First flamethrowers:");
            var start = Overrides.FlameStart is { } s ? s + 1 : 0;
            SettingsGrid.ItemWidth(140);
            if (ImGui.Combo("##p1pkflame", ref start, FlameLabels, FlameLabels.Length))
                Overrides.FlameStart = start == 0 ? null : start - 1;

            SettingsGrid.Row("Flamethrowers turn:");
            var turn = Overrides.Clockwise is { } cw ? (cw ? 1 : 2) : 0;
            SettingsGrid.ItemWidth(140);
            if (ImGui.Combo("##p1pkturn", ref turn, TurnLabels, TurnLabels.Length))
                Overrides.Clockwise = turn == 0 ? null : turn == 1;

            if (solo) DrawPlayerRows();
            SettingsGrid.End();
        }
    }

    public void DrawPerPlayer()
    {
        if (ImGui.Button("Auto")) Overrides.Number.Clear();
        if (SettingsGrid.Begin("##p1pantokratorplayers"))
        {
            editingSeat = SettingsGrid.SeatRow("##pkseat", editingSeat);
            DrawPlayerRows();
            SettingsGrid.ForcedRecapRow("In Line set:", Overrides.Number);
            SettingsGrid.End();
        }
        SettingsGrid.ConflictRows(Overrides.Validate());
    }

    private void DrawPlayerRows()
    {
        SettingsGrid.PlayerRow("In Line:");
        var number = Overrides.Number.Effective(editingSeat) ?? 0;
        SettingsGrid.ItemWidth(140);
        if (ImGui.Combo("##pknumber", ref number, NumberLabels, NumberLabels.Length))
            Overrides.Number.Set(editingSeat, number == 0 ? null : number);
    }
}
