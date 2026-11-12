using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using UselessPlayerInfo.Objects;

namespace UselessPlayerInfo.Windows.SettingsTabs;

public sealed class PeepersTab : ISettingsTab
{
    private readonly Configuration mutable;

    public PeepersTab(Configuration mutable)
    {
        this.mutable = mutable;
    }

    public string Name => "Peeper List###tab-peepers";

    public void Draw(bool changed)
    {
        SettingsWidgets.Checkbox("24-hour time", null, mutable.PeeperUse24Hour, v => mutable.PeeperUse24Hour = v);

        SettingsWidgets.Heading("Ignore");
        SettingsWidgets.Checkbox("Party and alliance members", null, mutable.PeeperIgnoreParty, v => mutable.PeeperIgnoreParty = v);
        SettingsWidgets.Checkbox("Friends", null, mutable.PeeperIgnoreFriends, v => mutable.PeeperIgnoreFriends = v);

        SettingsWidgets.Heading("When someone starts targeting you");
        SettingsWidgets.Checkbox("Play a sound", null, mutable.PeeperAlertSound, v => mutable.PeeperAlertSound = v);

        if (mutable.PeeperAlertSound)
        {
            using (ImRaii.PushIndent())
            {
                SettingsWidgets.Slider("Sound", "The same sounds as <se.1> to <se.16> in chat.", mutable.PeeperSoundEffect, 1, 16, v => mutable.PeeperSoundEffect = v);
                ImGui.SameLine();
                if (ImGui.SmallButton("Test"))
                {
                    PeeperList.PlaySound(mutable.PeeperSoundEffect);
                }
            }
        }

        SettingsWidgets.Checkbox("Print a chat message", "Only you can see it.", mutable.PeeperAlertChat, v => mutable.PeeperAlertChat = v);
        SettingsWidgets.Checkbox("Open the list", "Opens without taking focus from the game.", mutable.PeeperAutoOpen, v => mutable.PeeperAutoOpen = v);

        SettingsWidgets.Heading("List");
        SettingsWidgets.Slider("Maximum players", null, mutable.PeeperMaxEntries, 10, 500, v => mutable.PeeperMaxEntries = v);
        SettingsWidgets.Checkbox("Clear when changing zones", "When off, it only clears on logout.", mutable.PeeperClearOnZoneChange, v => mutable.PeeperClearOnZoneChange = v);
    }
}
