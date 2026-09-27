using UnityEngine;

namespace Movers
{
    // What left and right do on each options row of the title screen. The same settings as
    // the in-game pause menu's options (UICORE's HudPauseMenu), stored in the same places:
    // volumes in GameAudio, the rest in GameSettings (PlayerPrefs), so a change here is what
    // the game starts with.
    public static class MenuOptions
    {
        // True when something changed.
        public static bool Change(MainMenuModel.OptionRow row, int delta)
        {
            switch (row)
            {
                case MainMenuModel.OptionRow.Master: return Volume(AudioChannel.Master, delta);
                case MainMenuModel.OptionRow.Music: return Volume(AudioChannel.Music, delta);
                case MainMenuModel.OptionRow.Sfx: return Volume(AudioChannel.Sfx, delta);
                case MainMenuModel.OptionRow.Voice: return Volume(AudioChannel.Voice, delta);
                case MainMenuModel.OptionRow.Ambience: return Volume(AudioChannel.Ambience, delta);
                case MainMenuModel.OptionRow.Ui: return Volume(AudioChannel.Ui, delta);
                case MainMenuModel.OptionRow.Sensitivity:
                    GameSettings.LookSensitivity = Mathf.Round((GameSettings.LookSensitivity + 0.1f * delta) * 10f) / 10f;
                    return true;
                case MainMenuModel.OptionRow.InvertY:
                    GameSettings.InvertY = !GameSettings.InvertY;
                    return true;
                case MainMenuModel.OptionRow.Language:
                    GameSettings.Language = GameSettings.Language == Language.French ? Language.English : Language.French;
                    return true;
                case MainMenuModel.OptionRow.Layout:
                    GameSettings.Layout = GameSettings.Layout == SplitLayout.SideBySide ? SplitLayout.Stacked : SplitLayout.SideBySide;
                    return true;
                case MainMenuModel.OptionRow.Hints:
                    GameSettings.HintsShown = !GameSettings.HintsShown;
                    return true;
            }
            return false;
        }

        static bool Volume(AudioChannel channel, int delta)
        {
            float before = GameAudio.GetVolume(channel);
            float v = Mathf.Clamp01(Mathf.Round((before + 0.1f * delta) * 10f) / 10f);
            if (Mathf.Approximately(v, before)) return false;
            GameAudio.SetVolume(channel, v);
            return true;
        }
    }
}
