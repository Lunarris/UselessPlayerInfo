using System.Numerics;
using System.Reflection;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Lumina.Excel.Sheets;
using UselessPlayerInfo.Functions;

namespace UselessPlayerInfo.Windows;

public class MainWindow : ThemedWindow
{
    private const float IconSize = 24f;
    private const float ButtonHeight = 32f;
    private const float WeatherIconSize = 24f;
    private static readonly Vector4 SubtleColor = new(0.65f, 0.65f, 0.65f, 1f);
    private static readonly Vector4 LocationColor = new(0.95f, 0.80f, 0.45f, 1f);
    private static readonly Vector4 ErrorColor = new Vector4(0.90f, 0.30f, 0.30f, 1f);
    private readonly Plugin plugin;

    public MainWindow(Plugin plugin)
        : base("Useless Main Page", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.AlwaysAutoResize)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(300, 0),
            MaximumSize = new Vector2(300, float.MaxValue)
        };

        this.plugin = plugin;

        TitleBarButtons.Add(new TitleBarButton
        {
            Icon = FontAwesomeIcon.Cog,
            IconOffset = new Vector2(2, 1),
            ShowTooltip = () => ImGui.SetTooltip("Settings"),
            Click = _ => plugin.ToggleSettingsWindowUi(),
        });
    }

    public static void Dispose() { }

    protected override void DrawContents()
    {
        DrawPlayerSummary();

        var config = plugin.Configuration;
        if (config.ShowWeather || config.ShowForecast || config.ShowLocation)
        {
            DrawDivider();
            DrawLocationSummary();
        }

        if (config.ShowNavigation)
        {
            DrawDivider();
            DrawNavigation();
        }
    }

    private static void DrawDivider()
    {
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
    }
    private void DrawPlayerSummary()
    {
        var localPlayer = Plugin.ObjectTable.LocalPlayer;
        if (localPlayer == null)
        {
            WrappedColored(ErrorColor, "Log into a character to see player info.");
            return;
        }

        ImGui.SetWindowFontScale(1.2f);
        ImGui.TextWrapped(localPlayer.Name.TextValue);
        ImGui.SetWindowFontScale(1f);
        ImGui.Separator();
        ImGui.Spacing();
        
        if (localPlayer.ClassJob.IsValid)
        {
            using (ImRaii.Group())
            {
                DrawJobIcon(localPlayer.ClassJob.RowId, IconSize);
                ImGui.SameLine();
                ImGui.AlignTextToFramePadding();
                ImGui.TextUnformatted($"{localPlayer.ClassJob.Value.Abbreviation} - Level {localPlayer.Level}");
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                ImGui.SetTooltip("Open Job Levels");
            }

            if (ImGui.IsItemClicked())
            {
                plugin.ToggleJobWindowUi();
            }
        }
    }

    private void DrawLocationSummary()
    {
        var config = plugin.Configuration;
        var localPlayer = Plugin.ObjectTable.LocalPlayer;
        if (localPlayer != null)
        {
            var territoryId = Housing.GetOriginalHouseTerritoryTypeId() ?? Plugin.ClientState.TerritoryType;
            if (config.ShowLocation && Plugin.DataManager.GetExcelSheet<TerritoryType>().TryGetRow(territoryId, out var territoryRow))
            {
                var locationSuffix = Housing.GetLocationSuffix();
                var location = locationSuffix != null
                    ? $"{territoryRow.PlaceName.Value.Name}, {locationSuffix}"
                    : territoryRow.PlaceName.Value.Name.ToString();

                using (ImRaii.PushFont(UiBuilder.IconFont))
                {
                    ImGui.TextColored(LocationColor, FontAwesomeIcon.MapMarkerAlt.ToIconString());
                }

                ImGui.SameLine();
                WrappedColored(LocationColor, location);
            }

            if (config.ShowWeather)
            {
                DrawWeather();
            }

            if (config.ShowForecast)
            {
                DrawForecast(config.ForecastCount);
            }
        }
    }

    private void DrawNavigation()
    {
        var width = ImGui.GetContentRegionAvail().X;
        var buttonSize = new Vector2(width, ButtonHeight);

        if (ImGui.Button("Housing Notes", buttonSize))
        {
            plugin.ToggleHousingListWindowUI();
        }

        ImGui.Spacing();

        if (ImGui.Button("Saved Spots", buttonSize))
        {
            plugin.ToggleSpotListWindowUi();
        }

        ImGui.Spacing();

        if (ImGui.Button("Peeper List", buttonSize))
        {
            plugin.TogglePeeperListWindowUi();
        }
    }

    private static void DrawForecast(int count)
    {
        var forecast = WeatherHelper.GetForecast(WeatherHelper.CurrentWeatherTerritory(), count);
        var now = System.DateTimeOffset.UtcNow;

        for (var i = 0; i < forecast.Count; i++)
        {
            var (weather, start) = forecast[i];
            var icon = Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup((uint)weather.Icon)).GetWrapOrEmpty();
            ImGui.Image(icon.Handle, new Vector2(WeatherIconSize * 0.75f, WeatherIconSize * 0.75f));
            ImGui.SameLine();
            ImGui.TextColored(SubtleColor, $"{(i == 0 ? "Next" : "Then")}: {weather.Name} in {FormatDuration(start - now)}");
        }
    }

    private static string FormatDuration(System.TimeSpan time)
    {
        if (time.TotalHours >= 1)
        {
            return $"{(int)time.TotalHours}h {time.Minutes}m";
        }

        return time.TotalMinutes >= 1 ? $"{(int)time.TotalMinutes}m" : "<1m";
    }

    private static void WrappedColored(Vector4 color, string text)
    {
        using (ImRaii.PushColor(ImGuiCol.Text, color))
        {
            ImGui.TextWrapped(text);
        }
    }

    private static void DrawJobIcon(uint jobId, float size)
    {
        var icon = Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(Jobs.GetIconId(jobId))).GetWrapOrEmpty();
        ImGui.Image(icon.Handle, new Vector2(size, size));
    }

    private static void DrawWeather()
    {
        if (WeatherHelper.GetCurrentWeather() is not { } weatherRow)
        {
            ImGui.TextColored(SubtleColor, "Unknown weather.");
            return;
        }

        var icon = Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup((uint)weatherRow.Icon)).GetWrapOrEmpty();
        ImGui.Image(icon.Handle, new Vector2(WeatherIconSize, WeatherIconSize));
        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(weatherRow.Name.ToString());
    }
}
