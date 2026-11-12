using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Utility;
using Lumina.Excel.Sheets;
using UselessPlayerInfo.Objects;

namespace UselessPlayerInfo.Functions;

internal static class OpenWorld
{
    public static bool CanSaveCurrentLocation()
        => Plugin.ObjectTable.LocalPlayer != null && !Housing.IsInsideHousing() && Plugin.ClientState.MapId != 0;

    public static WorldArea? GetCurrentSpot()
    {
        var localPlayer = Plugin.ObjectTable.LocalPlayer;
        if (localPlayer == null || !CanSaveCurrentLocation())
        {
            return null;
        }

        var coords = localPlayer.GetMapCoordinates(false);
        return new WorldArea
        {
            TerritoryId = Plugin.ClientState.TerritoryType,
            MapId = Plugin.ClientState.MapId,
            X = coords.X,
            Y = coords.Y,
        };
    }

    public static bool ShowOnMap(WorldArea spot)
        => Plugin.GameGui.OpenMapWithMapLink(new MapLinkPayload(spot.TerritoryId, spot.MapId, spot.X, spot.Y));

    public static string ZoneName(WorldArea spot)
        => Plugin.DataManager.GetExcelSheet<TerritoryType>().TryGetRow(spot.TerritoryId, out var territoryRow)
            ? territoryRow.PlaceName.Value.Name.ToString()
            : "Unknown zone";

    public static string Describe(WorldArea spot, int decimals = 1)
    {
        var format = decimals >= 2 ? "0.00" : "0.0";
        return $"{ZoneName(spot)} ({spot.X.ToString(format)}, {spot.Y.ToString(format)})";
    }
}
