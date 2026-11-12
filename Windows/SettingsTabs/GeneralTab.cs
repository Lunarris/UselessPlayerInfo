using UselessPlayerInfo.Objects;

namespace UselessPlayerInfo.Windows.SettingsTabs;

public sealed class GeneralTab : ISettingsTab
{
    private readonly Configuration mutable;

    public GeneralTab(Configuration mutable)
    {
        this.mutable = mutable;
    }

    public string Name => "General###tab-general";

    public void Draw(bool changed)
    {
        SettingsWidgets.Checkbox("Lock window position and size", "Applies to every window except this one.", mutable.LockWindows, v => mutable.LockWindows = v);
        SettingsWidgets.Checkbox("Close windows with Esc", null, mutable.CloseWithEscape, v => mutable.CloseWithEscape = v);

        SettingsWidgets.Heading("Hide windows");
        SettingsWidgets.Checkbox("During cutscenes", null, mutable.HideInCutscenes, v => mutable.HideInCutscenes = v);
        SettingsWidgets.Checkbox("In GPose", null, mutable.HideInGpose, v => mutable.HideInGpose = v);
        SettingsWidgets.Checkbox("In combat", null, mutable.HideInCombat, v => mutable.HideInCombat = v);
    }
}
