using UnityEngine;

namespace Movers
{
    // What the loading screen's little stage is made of: a crew body, its running, tripping and
    // getting-up clips, a few trees and a fence for the scenery rushing by. One asset,
    // Assets/_Movers/UI/Resources/MoversLoadingStage.asset, loaded by name because the loading
    // screen is made at runtime and outlives every scene. The integration recipe fills it
    // (MENU integration/01_menu_assets.cs); run that again after the CHARACTERS clips arrive.
    //
    // Anything left empty has a fallback: no fall clips, the mover tumbles by code; no body, a
    // little wooden figure; no asset at all, the same, plus flat boxes for scenery.
    [CreateAssetMenu(menuName = "Movers/Loading Stage", fileName = "MoversLoadingStage")]
    public sealed class LoadingStageSettings : ScriptableObject
    {
        public const string ResourcePath = "MoversLoadingStage";

        [Tooltip("Crew bodies (PF_Crew_0x): one is picked per loading screen.")]
        public GameObject[] crewBodies = new GameObject[0];

        [Header("Clips (humanoid, in place)")]
        public AnimationClip run;           // Crew_Run
        public AnimationClip fall;          // Crew_Fall: tripped at a run, down, back up into the run
        public AnimationClip knockedDown;   // Crew_KnockedDown: thrown on the back
        public AnimationClip getUp;         // Crew_GetUp: up from the back

        [Header("Scenery")]
        public GameObject[] trees = new GameObject[0];
        public GameObject fence;
        [Tooltip("A Standard material: its shader draws everything the stage makes itself.")]
        public Material template;

        [Header("Timing")]
        [Tooltip("Playback speed of the fall and of the get-up (the game plays them a little faster).")]
        public float fallSpeed = 1.1f;
        public float knockedDownSpeed = 1.3f;
        public float getUpSpeed = 1.25f;
        [Tooltip("Run speed of Crew_Run at 1x, m/s: the scenery scrolls at this speed.")]
        public float runSpeed = 3.7f;

        static LoadingStageSettings current;

        public static LoadingStageSettings Current
        {
            get
            {
                if (current != null) return current;
                current = Resources.Load<LoadingStageSettings>(ResourcePath);
                if (current == null)
                {
                    current = CreateInstance<LoadingStageSettings>();
                    current.name = "LoadingStageSettings (defaults)";
                    current.hideFlags = HideFlags.DontSave;
                }
                return current;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            current = null;
        }
    }
}
