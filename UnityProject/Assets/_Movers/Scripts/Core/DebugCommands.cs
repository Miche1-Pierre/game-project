using System;
using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // Debug keys, registered by the system that owns them and dispatched by SliceDebug, so no
    // system reads F-keys itself and two systems cannot silently fight over one key.
    // F-keys and the numeric keypad only: every letter and top-row digit is gameplay. The map of who owns which key is in
    // 03_TECHNICAL/SLICE_ARCHITECTURE.md ("Debug keys"); F12 shows the live list in game.
    public static class DebugCommands
    {
        public struct Command
        {
            public KeyCode key;
            public bool shift;
            public string label;
            public Action run;
            public string owner;
        }

        static readonly List<Command> commands = new List<Command>();

        public static IReadOnlyList<Command> All => commands;

        public static void Register(KeyCode key, bool shift, string label, Action run, string owner)
        {
            if (run == null) return;
            for (int i = commands.Count - 1; i >= 0; i--)
            {
                var c = commands[i];
                if (c.key == key && c.shift == shift)
                {
                    if (c.owner != owner)
                        Debug.LogWarning("DebugCommands: " + (shift ? "Shift+" : "") + key + " was " + c.owner + "'s '" + c.label + "', now " + owner + "'s '" + label + "'");
                    commands.RemoveAt(i);
                }
            }
            commands.Add(new Command { key = key, shift = shift, label = label, run = run, owner = owner });
        }

        public static void Unregister(string owner)
        {
            commands.RemoveAll(c => c.owner == owner);
        }

        // A short message on screen for two seconds (SliceDebug draws it).
        public static void Toast(string text)
        {
            lastToast = text;
            lastToastTime = Time.unscaledTime;
        }

        internal static string lastToast;
        internal static float lastToastTime = -99f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            commands.Clear();
            lastToast = null;
            lastToastTime = -99f;
        }
    }
}
