using UselessPlayerInfo.Objects;

namespace UselessPlayerInfo.Windows.SettingsTabs;

public sealed class JobsTab : ISettingsTab
{
    private readonly Configuration mutable;

    public JobsTab(Configuration mutable)
    {
        this.mutable = mutable;
    }

    public string Name => "Job Levels###tab-jobs";

    public void Draw(bool changed)
    {
        SettingsWidgets.Checkbox("Hide jobs you haven't unlocked", null, mutable.HideLockedJobs, v => mutable.HideLockedJobs = v);
        SettingsWidgets.Checkbox("Show experience bars", "When off, experience shows as plain text.", mutable.ShowExperienceBars, v => mutable.ShowExperienceBars = v);
        SettingsWidgets.Checkbox("Open on your current job's role", "When off, the window opens on the tab you last used.", mutable.OpenCurrentRoleTab, v => mutable.OpenCurrentRoleTab = v);
    }
}
