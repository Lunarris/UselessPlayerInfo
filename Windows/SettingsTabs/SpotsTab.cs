using UselessPlayerInfo.Objects;

namespace UselessPlayerInfo.Windows.SettingsTabs;

public sealed class SpotsTab : ISettingsTab
{
    private readonly Configuration mutable;

    public SpotsTab(Configuration mutable)
    {
        this.mutable = mutable;
    }

    public string Name => "Saved Spots###tab-spots";

    public void Draw(bool changed)
    {
        SettingsWidgets.SortCombo("Sort by", "zone, then coordinates.", mutable.SpotSort, v => mutable.SpotSort = v);
        SettingsWidgets.Checkbox("Ask before removing", "When off, hold Ctrl+Shift to click remove.", mutable.ConfirmSpotRemove, v => mutable.ConfirmSpotRemove = v);
        SettingsWidgets.Slider("Coordinate decimals", "1 matches the game's map.", mutable.SpotDecimals, 1, 2, v => mutable.SpotDecimals = v);
    }
}
