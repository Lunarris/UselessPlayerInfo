using UselessPlayerInfo.Objects;

namespace UselessPlayerInfo.Windows.SettingsTabs;

public sealed class HousingTab : ISettingsTab
{
    private readonly Configuration mutable;

    public HousingTab(Configuration mutable)
    {
        this.mutable = mutable;
    }

    public string Name => "Housing Notes###tab-housing";

    public void Draw(bool changed)
    {
        SettingsWidgets.SortCombo("Sort by", "world, district, ward, then plot.", mutable.HousingSort, v => mutable.HousingSort = v);
        SettingsWidgets.Checkbox("Ask before removing", "When off, hold Ctrl+Shift to click remove.", mutable.ConfirmHousingRemove, v => mutable.ConfirmHousingRemove = v);
        SettingsWidgets.Checkbox("Show notes in the table", "When off, notes show when you hover the name.", mutable.ShowHousingNotes, v => mutable.ShowHousingNotes = v);
    }
}
