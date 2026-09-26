using UnityEngine;

namespace Movers
{
    // The one owner of the OS cursor. There is one mouse and one cursor, so this cannot live on
    // a player: two controllers each locking and unlocking it was the old single-player habit.
    //
    // Same rules as before: locked from the start, Escape frees it, a click on the game takes
    // it back. Both are read from whichever player the keyboard drives, through its CrewInput,
    // so a gamepad player's Start never frees the mouse under the keyboard player. No player
    // on the keyboard: the cursor is left free.
    [DisallowMultipleComponent]
    public sealed class CursorLock : MonoBehaviour
    {
        public bool lockAtStart = true;

        void Start()
        {
            if (lockAtStart && KeyboardPlayer() != null) Cursor.lockState = CursorLockMode.Locked;
        }

        void Update()
        {
            var input = KeyboardPlayer();
            if (input == null)
            {
                if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
                return;
            }
            if (input.Down(CrewButton.Pause)) Cursor.lockState = CursorLockMode.None;
            else if (input.Down(CrewButton.Grab) && Cursor.lockState != CursorLockMode.Locked)
                Cursor.lockState = CursorLockMode.Locked;
        }

        static CrewInput KeyboardPlayer()
        {
            var roster = CrewRoster.All;
            for (int i = 0; i < roster.Count; i++)
            {
                var input = roster[i] != null ? roster[i].Input : null;
                if (input != null && input.Source is KeyboardMouseSource) return input;
            }
            return null;
        }
    }
}
