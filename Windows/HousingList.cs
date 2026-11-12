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

public class HousingListWindow : ThemedWindow
{
    private readonly Plugin plugin;
    private readonly ConfirmPopup confirmPopup = new("ConfirmHousingRemovePopup");
    private readonly EditPopup editPopup = new("EditHousingPopup");
    private string newName = "";
    private string importCode = "";
    private string importName = "";
    private string? importError;
    private int? pendingRemoveIndex;

    public HousingListWindow(Plugin plugin)
        : base("Useless Housing List", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(480, 300),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        this.plugin = plugin;
    }

    public static void Dispose() { }

    protected override void DrawContents()
    {
        using var child = ImRaii.Child("SavedLocationsWindow", Vector2.Zero, true);
        if (!child.Success)
        {
            return;
        }

        DrawAddRow();
        ImGui.Separator();
        ImGui.Spacing();

        var footerHeight = ImGui.GetFrameHeightWithSpacing() + ImGui.GetStyle().ItemSpacing.Y;
        using (var tableChild = ImRaii.Child("SavedLocationsTableRegion", new Vector2(0, -footerHeight)))
        {
            if (tableChild.Success)
            {
                DrawTable();
            }
        }

        ImGui.Separator();
        DrawImportRow();

        if (confirmPopup.Draw() is { } removeIndex && removeIndex < plugin.Configuration.HousingLocations.Count)
        {
            plugin.Configuration.HousingLocations.RemoveAt(removeIndex);
            plugin.Configuration.Save();
        }

        if (editPopup.Draw() is { } edit && edit.Index < plugin.Configuration.HousingLocations.Count)
        {
            var location = plugin.Configuration.HousingLocations[edit.Index];
            location.Name = edit.Name;
            location.Notes = edit.Notes;
            plugin.Configuration.Save();
        }
    }

    private HousingLocation? FindSaved(HousingLocation address)
        => plugin.Configuration.HousingLocations.FirstOrDefault(saved => saved.SameAddress(address));

    private void DrawAddRow()
    {
        if (!Housing.CanSaveCurrentLocation())
        {
            Widgets.WrappedColored(Widgets.ErrorColor, "You must be on a plot or in an apartment/private room to save a location.");
            return;
        }

        ImGui.SetNextItemWidth(200f);
        ImGui.InputTextWithHint("##NewLocationName", "Name this location...", ref newName, 64);

        ImGui.SameLine();

        using (ImRaii.Disabled(string.IsNullOrWhiteSpace(newName)))
        {
            if (ImGui.Button("Add Current Location") && TrySaveCurrent(newName, true, out _))
            {
                newName = "";
            }
        }

        if (Housing.GetCurrentLocation() is { } current && FindSaved(current) is { } existing)
        {
            Widgets.WrappedColored(Widgets.WarningColor, $"Already saved as \"{existing.Name}\".");
        }
    }

    private void DrawImportRow()
    {
        if (Widgets.IconButton(FontAwesomeIcon.FileImport, "Import a location from a code"))
        {
            importCode = "";
            importName = "";
            importError = null;
            ImGui.OpenPopup("ImportLocationPopup");
        }

        using var popup = ImRaii.Popup("ImportLocationPopup");
        if (!popup.Success)
        {
            return;
        }

        ImGui.SetNextItemWidth(260f);
        if (ImGui.InputTextWithHint("##ImportCode", "Paste a location code...", ref importCode, 512))
        {
            importError = null;

            if (string.IsNullOrWhiteSpace(importName) && HousingLocation.TryParseCode(importCode, out var parsed) && parsed != null)
            {
                importName = parsed.Name;
            }
        }

        ImGui.SetNextItemWidth(260f);
        ImGui.InputTextWithHint("##ImportName", "Name this location...", ref importName, 64);

        if (HousingLocation.TryParseCode(importCode, out var preview) && preview != null && FindSaved(preview) is { } existing)
        {
            Widgets.WrappedColored(Widgets.WarningColor, $"Already saved as \"{existing.Name}\".");
        }

        var canImport = !string.IsNullOrWhiteSpace(importCode) && !string.IsNullOrWhiteSpace(importName);

        using (ImRaii.Disabled(!canImport))
        {
            if (ImGui.Button("Import"))
            {
                ImportLocation();
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

    private void ImportLocation()
    {
        if (!HousingLocation.TryParseCode(importCode, out var location) || location == null)
        {
            importError = "Invalid Code.";
            return;
        }

        if (!Plugin.DataManager.GetExcelSheet<TerritoryType>().TryGetRow(location.TerritoryId, out _))
        {
            importError = "The code currently references an unknown territory.";
            return;
        }

        if (location.WorldId != 0 && !Plugin.DataManager.GetExcelSheet<World>().TryGetRow(location.WorldId, out _))
        {
            importError = "The code currently references an unknown world.";
            return;
        }

        location.Name = importName.Trim();
        location.TerritoryId = Housing.ToWardTerritory(location.TerritoryId);
        plugin.Configuration.HousingLocations.Add(location);
        plugin.Configuration.Save();

        importCode = "";
        importName = "";
        importError = null;
    }

    private void DrawTable()
    {
        var locations = plugin.Configuration.HousingLocations;

        if (locations.Count == 0)
        {
            ImGui.TextDisabled("No saved locations yet.");
            return;
        }

        if (!ImGui.BeginTable("SavedLocationsTable", 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders | ImGuiTableFlags.SizingStretchProp))
        {
            return;
        }

        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch, 0);
        ImGui.TableSetupColumn("Location", ImGuiTableColumnFlags.WidthStretch, 0);
        var actionsWidth = Widgets.IconButtonsWidth(FontAwesomeIcon.Copy, FontAwesomeIcon.MapMarkedAlt, FontAwesomeIcon.Pen, FontAwesomeIcon.Trash);
        ImGui.TableSetupColumn("##Actions", ImGuiTableColumnFlags.WidthFixed, actionsWidth, 0);
        ImGui.TableHeadersRow();

        var confirm = plugin.Configuration.ConfirmHousingRemove;

        foreach (var i in DisplayOrder(locations).ToList())
        {
            var loc = locations[i];
            using var id = ImRaii.PushId(i);

            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.TextWrapped(loc.Name);
            if (!string.IsNullOrEmpty(loc.Notes))
            {
                if (plugin.Configuration.ShowHousingNotes)
                {
                    using (ImRaii.PushColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]))
                    {
                        ImGui.TextWrapped(loc.Notes);
                    }
                }
                else if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(loc.Notes);
                }
            }

            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.TextWrapped(Housing.Describe(loc));

            ImGui.TableNextColumn();
            if (Widgets.IconButton(FontAwesomeIcon.Copy, "Copy shareable code"))
            {
                ImGui.SetClipboardText(loc.ToCode());
            }

            ImGui.SameLine();
            using (ImRaii.Disabled(!Housing.CanShowOnMap(loc)))
            {
                if (Widgets.IconButton(FontAwesomeIcon.MapMarkedAlt, "Show on map"))
                {
                    Housing.ShowOnMap(loc);
                }
            }

            ImGui.SameLine();
            if (Widgets.IconButton(FontAwesomeIcon.Pen, "Rename or add notes"))
            {
                editPopup.Open(i, loc.Name, loc.Notes);
            }

            ImGui.SameLine();
            if (Widgets.RemoveButton(confirm))
            {
                if (confirm)
                {
                    confirmPopup.Open(i, $"Remove \"{loc.Name}\"?");
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
            locations.RemoveAt(index);
            plugin.Configuration.Save();
            pendingRemoveIndex = null;
        }
    }

    private IEnumerable<int> DisplayOrder(List<HousingLocation> locations)
    {
        var indexes = Enumerable.Range(0, locations.Count);
        return plugin.Configuration.HousingSort switch
        {
            ListSort.Name => indexes.OrderBy(i => locations[i].Name, StringComparer.CurrentCultureIgnoreCase),
            ListSort.Location => indexes.OrderBy(i => locations[i].WorldId).ThenBy(i => locations[i].TerritoryId)
                .ThenBy(i => locations[i].Ward).ThenBy(i => locations[i].Plot).ThenBy(i => locations[i].Room),
            _ => indexes,
        };
    }

    public bool TrySaveCurrent(string name, bool allowDuplicate, out string message)
    {
        var location = Housing.GetCurrentLocation();
        if (location == null)
        {
            message = "You must be on a plot or in an apartment/private room to save a location.";
            return false;
        }

        if (!allowDuplicate && FindSaved(location) is { } existing)
        {
            message = $"This location is already saved as \"{existing.Name}\".";
            return false;
        }

        location.Name = string.IsNullOrWhiteSpace(name) ? Housing.Describe(location) : name.Trim();
        plugin.Configuration.HousingLocations.Add(location);
        plugin.Configuration.Save();
        message = $"Saved \"{location.Name}\" ({Housing.Describe(location)}).";
        return true;
    }
}
