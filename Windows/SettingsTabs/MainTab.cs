using Dalamud.Interface.Utility.Raii;
using UselessPlayerInfo.Objects;

namespace UselessPlayerInfo.Windows.SettingsTabs;

public sealed class MainTab : ISettingsTab
{
    private readonly Configuration mutable;

    public MainTab(Configuration mutable)
    {
        this.mutable = mutable;
    }

    public string Name => "Main Window###tab-main";

    public void Draw(bool changed)
    {
        SettingsWidgets.Heading("Show");
        SettingsWidgets.Checkbox("Location", null, mutable.ShowLocation, v => mutable.ShowLocation = v);
        SettingsWidgets.Checkbox("Weather", null, mutable.ShowWeather, v => mutable.ShowWeather = v);
        SettingsWidgets.Checkbox("Forecast", null, mutable.ShowForecast, v => mutable.ShowForecast = v);

        if (mutable.ShowForecast)
        {
            using (ImRaii.PushIndent())
            {
                SettingsWidgets.Slider("Periods", "How many upcoming weather changes to list.", mutable.ForecastCount, 1, 5, v => mutable.ForecastCount = v);
            }
        }

        SettingsWidgets.Checkbox("Window buttons", "The Housing Notes, Saved Spots and Peeper List buttons. Their commands still work when hidden.", mutable.ShowNavigation, v => mutable.ShowNavigation = v);
    }
}
