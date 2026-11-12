using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Lumina.Excel.Sheets;
using UselessPlayerInfo.Functions;
using UselessPlayerInfo.Objects;

namespace UselessPlayerInfo.Windows;

public class SpotListWindow : ThemedWindow
{
    private readonly Plugin plugin;
    private readonly ConfirmPopup confirmPopup = new("ConfirmSpotRemovePopup");
    private readonly EditPopup editPopup = new("EditSpotPopup");
    private string newName = "";
    private string importCode = "";
    private string importName = "";
    private string? importError;
    private int? pendingRemoveIndex;

    public SpotListWindow(Plugin plugin)
        : base("Useless Saved Spots", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(460, 300),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        this.plugin = plugin;
    }

    protected override void DrawContents()
    {
        using var child = ImRaii.Child("SavedSpotsWindow", Vector2.Zero, true);
        if (!child.Success)
        {
            return;
        }

        DrawAddRow();
        ImGui.Separator();
        ImGui.Spacing();

        var footerHeight = ImGui.GetFrameHeightWithSpacing() + ImGui.GetStyle().ItemSpacing.Y;
        using (var tableChild = ImRaii.Child("SavedSpotsTableRegion", new Vector2(0, -footerHeight)))
        {
            if (tableChild.Success)
            {
                DrawTable();
            }
        }

        ImGui.Separator();
        DrawImportRow();

        if (confirmPopup.Draw() is { } removeIndex && removeIndex < plugin.Configuration.WorldAreas.Count)
        {
            plugin.Configuration.WorldAreas.RemoveAt(removeIndex);
            plugin.Configuration.Save();
        }

        if (editPopup.Draw() is { } edit && edit.Index < plugin.Configuration.WorldAreas.Count)
        {
            var spot = plugin.Configuration.WorldAreas[edit.Index];
            spot.Name = edit.Name;
            spot.Notes = edit.Notes;
            plugin.Configuration.Save();
        }
    }

    private WorldArea? FindSaved(WorldArea spot)
        => plugin.Configuration.WorldAreas.FirstOrDefault(saved =>
            saved.TerritoryId == spot.TerritoryId && saved.MapId == spot.MapId
            && MathF.Abs(saved.X - spot.X) < 0.15f && MathF.Abs(saved.Y - spot.Y) < 0.15f);

    private void DrawAddRow()
    {
        if (OpenWorld.GetCurrentSpot() is not { } current)
        {
            Widgets.WrappedColored(Widgets.ErrorColor, "Spots can't be saved inside houses, apartments or rooms.");
            return;
        }

        ImGui.SetNextItemWidth(200f);
        ImGui.InputTextWithHint("##NewSpotName", "Name this spot...", ref newName, 64);

        ImGui.SameLine();

        using (ImRaii.Disabled(string.IsNullOrWhiteSpace(newName)))
        {
            if (ImGui.Button("Add Current Spot") && TrySaveCurrent(newName, true, out _))
            {
                newName = "";
            }
        }

        if (FindSaved(current) is { } existing)
        {
            Widgets.WrappedColored(Widgets.WarningColor, $"Already saved as \"{existing.Name}\".");
        }
    }

    private void DrawImportRow()
    {
        if (Widgets.IconButton(FontAwesomeIcon.FileImport, "Import a spot from a code"))
        {
            importCode = "";
            importName = "";
            importError = null;
            ImGui.OpenPopup("ImportSpotPopup");
        }

        using var popup = ImRaii.Popup("ImportSpotPopup");
        if (!popup.Success)
        {
            return;
        }

        ImGui.SetNextItemWidth(260f);
        if (ImGui.InputTextWithHint("##ImportSpotCode", "Paste a spot code...", ref importCode, 512))
        {
            importError = null;

            if (string.IsNullOrWhiteSpace(importName) && WorldArea.TryParseCode(importCode, out var parsed) && parsed != null)
            {
                importName = parsed.Name;
            }
        }

        ImGui.SetNextItemWidth(260f);
        ImGui.InputTextWithHint("##ImportSpotName", "Name this spot...", ref importName, 64);

        if (WorldArea.TryParseCode(importCode, out var preview) && preview != null && FindSaved(preview) is { } existing)
        {
            Widgets.WrappedColored(Widgets.WarningColor, $"Already saved as \"{existing.Name}\".");
        }

        var canImport = !string.IsNullOrWhiteSpace(importCode) && !string.IsNullOrWhiteSpace(importName);

        using (ImRaii.Disabled(!canImport))
        {
            if (ImGui.Button("Import"))
            {
                ImportSpot();
                if (importError == null)
                {
                    ImGui.CloseCurrentPopup();
                }
            }
        }

        if (importError != null)
        {
            Widgets.WrappedColored(Widgets.ErrorColor, importError);
        }
    }

    private void ImportSpot()
    {
        if (!WorldArea.TryParseCode(importCode, out var spot) || spot == null)
        {
            importError = "Invalid Code.";
            return;
        }

        if (!Plugin.DataManager.GetExcelSheet<TerritoryType>().TryGetRow(spot.TerritoryId, out _)
            || !Plugin.DataManager.GetExcelSheet<Map>().TryGetRow(spot.MapId, out _))
        {
            importError = "The code references an unknown zone or map.";
            return;
        }

        spot.Name = importName.Trim();
        plugin.Configuration.WorldAreas.Add(spot);
        plugin.Configuration.Save();

        importCode = "";
        importName = "";
        importError = null;
    }

    private void DrawTable()
    {
        var spots = plugin.Configuration.WorldAreas;

        if (spots.Count == 0)
        {
            ImGui.TextDisabled("No saved spots yet.");
            return;
        }

        if (!ImGui.BeginTable("SavedSpotsTable", 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders | ImGuiTableFlags.SizingStretchProp))
        {
            return;
        }

        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch, 0);
        ImGui.TableSetupColumn("Location", ImGuiTableColumnFlags.WidthStretch, 0);
        var actionsWidth = Widgets.IconButtonsWidth(FontAwesomeIcon.Copy, FontAwesomeIcon.MapMarkedAlt, FontAwesomeIcon.Pen, FontAwesomeIcon.Trash);
        ImGui.TableSetupColumn("##Actions", ImGuiTableColumnFlags.WidthFixed, actionsWidth, 0);
        ImGui.TableHeadersRow();

        var confirm = plugin.Configuration.ConfirmSpotRemove;

        foreach (var i in DisplayOrder(spots).ToList())
        {
            var spot = spots[i];
            using var id = ImRaii.PushId(i);

            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.TextWrapped(spot.Name);
            if (!string.IsNullOrEmpty(spot.Notes) && ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(spot.Notes);
            }

            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.TextWrapped(OpenWorld.Describe(spot, plugin.Configuration.SpotDecimals));

            ImGui.TableNextColumn();
            if (Widgets.IconButton(FontAwesomeIcon.Copy, "Copy shareable code"))
            {
                ImGui.SetClipboardText(spot.ToCode());
            }

            ImGui.SameLine();
            if (Widgets.IconButton(FontAwesomeIcon.MapMarkedAlt, "Show on map"))
            {
                OpenWorld.ShowOnMap(spot);
            }

            ImGui.SameLine();
            if (Widgets.IconButton(FontAwesomeIcon.Pen, "Rename or add notes"))
            {
                editPopup.Open(i, spot.Name, spot.Notes);
            }

            ImGui.SameLine();
            if (Widgets.RemoveButton(confirm))
            {
                if (confirm)
                {
                    confirmPopup.Open(i, $"Remove \"{spot.Name}\"?");
                }
                else
                {
                    pendingRemoveIndex = i;
                }
            }
        }

        ImGui.EndTable();

        if (pendingRemoveIndex is { } index)
        {
            spots.RemoveAt(index);
            plugin.Configuration.Save();
            pendingRemoveIndex = null;
        }
    }

    private IEnumerable<int> DisplayOrder(List<WorldArea> spots)
    {
        var indexes = Enumerable.Range(0, spots.Count);
        return plugin.Configuration.SpotSort switch
        {
            ListSort.Name => indexes.OrderBy(i => spots[i].Name, StringComparer.CurrentCultureIgnoreCase),
            ListSort.Location => indexes.OrderBy(i => OpenWorld.ZoneName(spots[i]), StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(i => spots[i].X).ThenBy(i => spots[i].Y),
            _ => indexes,
        };
    }

    public bool TrySaveCurrent(string name, bool allowDuplicate, out string message)
    {
        var spot = OpenWorld.GetCurrentSpot();
        if (spot == null)
        {
            message = "Spots can't be saved inside houses, apartments or rooms.";
            return false;
        }

        if (!allowDuplicate && FindSaved(spot) is { } existing)
        {
            message = $"This spot is already saved as \"{existing.Name}\".";
            return false;
        }

        spot.Name = string.IsNullOrWhiteSpace(name) ? OpenWorld.Describe(spot) : name.Trim();
        plugin.Configuration.WorldAreas.Add(spot);
        plugin.Configuration.Save();
        message = $"Saved \"{spot.Name}\" ({OpenWorld.Describe(spot)}).";
        return true;
    }
}
