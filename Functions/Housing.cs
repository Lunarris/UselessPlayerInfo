using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;
using UselessPlayerInfo.Objects;

namespace UselessPlayerInfo.Functions;

internal static class Housing
{
    private const sbyte ApartmentMainDivisionPlot = -128;
    private const sbyte ApartmentSubdivisionPlot = -127;

    public static unsafe string? GetLocationSuffix()
    {
        var housingManager = HousingManager.Instance();
        if (housingManager == null || !(housingManager->IsInside() || housingManager->IsOutside()))
        {
            return null;
        }

        var ward = housingManager->GetCurrentWard() + 1;
        var division = housingManager->GetCurrentDivision();
        var plot = housingManager->GetCurrentPlot();
        var room = housingManager->GetCurrentRoom();

        var suffix = division == 2 ? $"Ward {ward} (Subdivision)" : $"Ward {ward}";

        if (plot is ApartmentMainDivisionPlot or ApartmentSubdivisionPlot)
        {
            if (room > 0)
            {
                suffix += $", Room {room}";
            }
        }
        else if (plot >= 0)
        {
            suffix += $", Plot {plot + 1}";
        }

        return suffix;
    }

    public static unsafe bool IsInsideHousing()
    {
        var housingManager = HousingManager.Instance();
        return housingManager != null && housingManager->IsInside();
    }

    public static unsafe uint? GetOriginalHouseTerritoryTypeId()
    {
        var housingManager = HousingManager.Instance();
        if (housingManager == null || !housingManager->IsInside())
        {
            return null;
        }

        return HousingManager.GetOriginalHouseTerritoryTypeId();
    }

    private const uint HousingWardIntendedUse = 13;
    private static Dictionary<string, uint>? wardsByArtFolder;

    public static uint ToWardTerritory(uint territoryId)
    {
        var sheet = Plugin.DataManager.GetExcelSheet<TerritoryType>();
        if (!sheet.TryGetRow(territoryId, out var row) || row.TerritoryIntendedUse.RowId == HousingWardIntendedUse)
        {
            return territoryId;
        }

        if (wardsByArtFolder == null)
        {
            wardsByArtFolder = new Dictionary<string, uint>();
            foreach (var ward in sheet.Where(t => t.TerritoryIntendedUse.RowId == HousingWardIntendedUse))
            {
                if (ArtFolder(ward.Bg.ToString()) is { } folder)
                {
                    wardsByArtFolder.TryAdd(folder, ward.RowId);
                }
            }
        }

        return ArtFolder(row.Bg.ToString()) is { } interiorFolder && wardsByArtFolder.TryGetValue(interiorFolder, out var wardId)
            ? wardId
            : territoryId;
    }

    private static string? ArtFolder(string bg)
    {
        var parts = bg.Split('/');
        return parts.Length >= 2 ? $"{parts[0]}/{parts[1]}" : null;
    }

    public static HousingLocation? GetCurrentLocation()
    {
        if (!CanSaveCurrentLocation() || GetCurrentHousingCoords() is not { } coords)
        {
            return null;
        }

        return new HousingLocation
        {
            TerritoryId = ToWardTerritory(GetOriginalHouseTerritoryTypeId() ?? Plugin.ClientState.TerritoryType),
            WorldId = Plugin.ObjectTable.LocalPlayer?.CurrentWorld.RowId ?? 0,
            Ward = coords.Ward,
            Plot = coords.Plot,
            Room = coords.Room,
        };
    }

    public static string Describe(HousingLocation loc)
    {
        if (!Plugin.DataManager.GetExcelSheet<TerritoryType>().TryGetRow(loc.TerritoryId, out var territoryRow))
        {
            return "Unknown location";
        }

        var name = territoryRow.PlaceName.Value.Name.ToString();

        if (loc.WorldId != 0 && Plugin.DataManager.GetExcelSheet<World>().TryGetRow(loc.WorldId, out var worldRow))
        {
            name += $" ({worldRow.Name})";
        }

        var coords = DescribeCoords(loc.Ward, loc.Plot, loc.Room);

        return coords == null ? name : $"{name}, {coords}";
    }

    public static unsafe (int Ward, int Plot, int Room)? GetCurrentHousingCoords()
    {
        var housingManager = HousingManager.Instance();
        if (housingManager == null || !(housingManager->IsInside() || housingManager->IsOutside()))
        {
            return null;
        }

        var ward = housingManager->GetCurrentWard() + 1;
        var plot = housingManager->GetCurrentPlot();
        var room = housingManager->GetCurrentRoom();

        return (ward, plot, room);
    }

    public static unsafe bool CanSaveCurrentLocation()
    {
        var housingManager = HousingManager.Instance();
        if (housingManager == null || !(housingManager->IsInside() || housingManager->IsOutside()))
        {
            return false;
        }

        var plot = housingManager->GetCurrentPlot();
        var room = housingManager->GetCurrentRoom();

        if (plot is ApartmentMainDivisionPlot or ApartmentSubdivisionPlot)
        {
            return room > 0;
        }

        return plot >= 0;
    }

    private const int MainApartmentMarker = 60;
    private const int SubdivisionApartmentMarker = 61;

    public static bool CanShowOnMap(HousingLocation location)
        => GetMapMarker(location) != null;

    public static bool ShowOnMap(HousingLocation location)
    {
        if (GetMapMarker(location) is not { } marker)
        {
            return false;
        }

        var map = marker.Map.Value;
        var coords = MapUtil.WorldToMap(new Vector2(marker.X, marker.Z), map);
        return Plugin.GameGui.OpenMapWithMapLink(new MapLinkPayload(location.TerritoryId, map.RowId, coords.X, coords.Y));
    }

    private static HousingMapMarkerInfo? GetMapMarker(HousingLocation location)
    {
        var index = location.Plot switch
        {
            ApartmentMainDivisionPlot => MainApartmentMarker,
            ApartmentSubdivisionPlot => SubdivisionApartmentMarker,
            >= 0 and < MainApartmentMarker => location.Plot,
            _ => -1,
        };

        if (index < 0 || !Plugin.DataManager.GetSubrowExcelSheet<HousingMapMarkerInfo>().TryGetRow(location.TerritoryId, out var markers)
                      || index >= markers.Count)
        {
            return null;
        }

        return markers[index];
    }

    public static string? DescribeCoords(int ward, int plot, int room)
    {
        if (ward <= 0)
        {
            return null;
        }

        var suffix = $"Ward {ward}";

        if (plot is ApartmentMainDivisionPlot or ApartmentSubdivisionPlot)
        {
            if (room > 0)
            {
                suffix += $", Room {room}";
            }
        }
        else if (plot >= 0)
        {
            suffix += $", Plot {plot + 1}";
        }

        return suffix;
    }
}
