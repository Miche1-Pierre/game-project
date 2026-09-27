using System;
using UnityEngine;

namespace Movers
{
    public enum AudioChannel { Master, Music, Sfx, Voice, Ambience, Ui }

    // The player's volume settings, one per channel, 0..1. Every sound the game makes reads
    // Gain(channel) every frame it plays (AudioDirector), so a slider moves what is already
    // playing, not only the next sound.
    //
    // Kept in PlayerPrefs under the keys the options screen already uses (vol.master,
    // vol.music, vol.effects, vol.voices), plus vol.ambience and vol.ui: a per-machine
    // convenience, no save system (CLAUDE.md section 5). Read lazily on first use, on the main
    // thread, because PlayerPrefs cannot be touched from anywhere else.
    public static class GameAudio
    {
        static readonly float[] Defaults = { 1f, 0.6f, 1f, 1f, 0.7f, 0.8f };
        static readonly string[] Keys = { "vol.master", "vol.music", "vol.effects", "vol.voices", "vol.ambience", "vol.ui" };
        const int ChannelCount = 6;

        static readonly float[] volumes = new float[ChannelCount];
        static bool loaded;

        // A slider moved. Listeners that cache a gain (none do today) refresh on this.
        public static event Action<AudioChannel, float> Changed;

        public static float GetVolume(AudioChannel channel)
        {
            int i = (int)channel;
            if (i < 0 || i >= ChannelCount) return 1f;
            Load();
            return volumes[i];
        }

        public static void SetVolume(AudioChannel channel, float volume01)
        {
            int i = (int)channel;
            if (i < 0 || i >= ChannelCount) return;
            Load();
            float v = Mathf.Clamp01(float.IsNaN(volume01) ? 0f : volume01);
            if (Mathf.Approximately(volumes[i], v)) return;
            volumes[i] = v;
            PlayerPrefs.SetFloat(Keys[i], v);
            try { Changed?.Invoke(channel, v); }
            catch (Exception e) { Debug.LogException(e); }
        }

        // The gain a source on this channel plays at: its channel times the master.
        public static float Gain(AudioChannel channel)
        {
            Load();
            int i = (int)channel;
            if (i <= 0 || i >= ChannelCount) return volumes[0];
            return volumes[0] * volumes[i];
        }

        // Writes PlayerPrefs to disk. The options screen calls it when it closes; the game
        // also saves when it quits.
        public static void Save() { PlayerPrefs.Save(); }

        public static void ResetToDefaults()
        {
            for (int i = 0; i < ChannelCount; i++) SetVolume((AudioChannel)i, Defaults[i]);
        }

        // Re-reads PlayerPrefs, for a settings screen that wrote them directly.
        public static void Reload()
        {
            loaded = false;
            Load();
        }

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            for (int i = 0; i < ChannelCount; i++)
                volumes[i] = Mathf.Clamp01(PlayerPrefs.GetFloat(Keys[i], Defaults[i]));
        }

        // Domain reload off (the playtest CLI): PlayerPrefs are the truth, read them again.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            loaded = false;
            Changed = null;
        }
    }
}
