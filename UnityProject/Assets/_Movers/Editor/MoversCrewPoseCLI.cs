#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Movers.EditorTools
{
    // Gives the crew a pose for empty hands. Idempotent: running it again changes nothing.
    //
    // Until 2026-09-25 AC_Crew had one state, the carry idle, so a player with nothing in his
    // hands stood with his arms out in front of him. This imports the relaxed clip exactly the
    // way the carry clip is imported, makes it the controller's default state, switches to the
    // carry pose on the "Carrying" bool, and puts CrewPose, which sets that bool from
    // PlayerGrab, on the four crew prefabs. No scene is touched: maps pick it up through the
    // prefabs.
    //
    //   Unity.exe -batchmode -projectPath C:\dev\game-project\UnityProject \
    //     -executeMethod Movers.EditorTools.MoversCrewPoseCLI.Setup -logFile pose.log
    //
    // No -quit needed, it exits by itself. The clip comes from
    // tools/blender/author_carry_clip.py --pose relaxed.
    public static class MoversCrewPoseCLI
    {
        const string Dir = "Assets/_Movers/Generated/Characters/";
        const string CarryClipPath = Dir + "Anim_Carry_Idle.fbx";
        const string RelaxedClipPath = Dir + "Anim_Relaxed_Idle.fbx";
        const string ControllerPath = Dir + "AC_Crew.controller";
        const string CarryState = "Carry_Idle";
        const string RelaxedState = "Relaxed_Idle";
        const string Param = "Carrying";
        const float BlendSeconds = 0.15f;

        static readonly string[] Prefabs =
        {
            Dir + "PF_Crew_01_Red.prefab", Dir + "PF_Crew_02_Blue.prefab",
            Dir + "PF_Crew_03_Yellow.prefab", Dir + "PF_Crew_04_Green.prefab",
        };

        [MenuItem("The Movers/Crew Pose Setup")]
        public static void Setup()
        {
            bool ok = ImportRelaxedClip() && WireController() && AddToPrefabs();
            AssetDatabase.SaveAssets();
            Debug.Log(ok ? "[Pose] VERDICT: crew pose set up" : "[Pose] VERDICT: failures above");
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 2);
        }

        // Same humanoid mapping, same loop and root settings as the carry clip; only the name
        // differs. Copied field by field from the carry importer rather than typed in, so the
        // two clips cannot drift apart.
        static bool ImportRelaxedClip()
        {
            var carry = AssetImporter.GetAtPath(CarryClipPath) as ModelImporter;
            var relaxed = AssetImporter.GetAtPath(RelaxedClipPath) as ModelImporter;
            if (carry == null || relaxed == null)
            {
                Debug.LogError("[Pose] missing model importer at " + (carry == null ? CarryClipPath : RelaxedClipPath));
                return false;
            }
            var template = carry.clipAnimations.FirstOrDefault();
            var take = relaxed.defaultClipAnimations.FirstOrDefault();
            if (template == null || take == null)
            {
                Debug.LogError("[Pose] no clip to copy from " + CarryClipPath + " or no take in " + RelaxedClipPath);
                return false;
            }

            relaxed.animationType = carry.animationType;
            relaxed.avatarSetup = carry.avatarSetup;
            relaxed.humanDescription = carry.humanDescription;
            relaxed.materialImportMode = carry.materialImportMode;
            relaxed.importAnimation = true;

            take.name = RelaxedState;
            take.loopTime = template.loopTime;
            take.loopPose = template.loopPose;
            take.cycleOffset = template.cycleOffset;
            take.keepOriginalOrientation = template.keepOriginalOrientation;
            take.keepOriginalPositionY = template.keepOriginalPositionY;
            take.keepOriginalPositionXZ = template.keepOriginalPositionXZ;
            take.heightFromFeet = template.heightFromFeet;
            take.lockRootRotation = template.lockRootRotation;
            take.lockRootHeightY = template.lockRootHeightY;
            take.lockRootPositionXZ = template.lockRootPositionXZ;
            take.mirror = template.mirror;
            relaxed.clipAnimations = new[] { take };
            relaxed.SaveAndReimport();

            var clip = LoadClip(RelaxedClipPath, RelaxedState);
            bool ok = clip != null && clip.isLooping && clip.isHumanMotion;
            Debug.Log("[Pose] " + (ok ? "OK    " : "FAIL  ") + "relaxed clip imported as a looping humanoid clip ("
                      + (clip == null ? "missing" : clip.length.ToString("F2") + " s") + ")");
            return ok;
        }

        // Relaxed is the default; the carry pose while "Carrying" is true, and back when it is not.
        static bool WireController()
        {
            var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            var clip = LoadClip(RelaxedClipPath, RelaxedState);
            if (ac == null || clip == null)
            {
                Debug.LogError("[Pose] missing " + (ac == null ? ControllerPath : RelaxedState + " clip"));
                return false;
            }
            if (!ac.parameters.Any(p => p.name == Param)) ac.AddParameter(Param, AnimatorControllerParameterType.Bool);

            var sm = ac.layers[0].stateMachine;
            var carry = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == CarryState);
            if (carry == null)
            {
                Debug.LogError("[Pose] " + ControllerPath + " has no " + CarryState + " state");
                return false;
            }
            var relaxed = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == RelaxedState)
                          ?? sm.AddState(RelaxedState, new Vector3(300f, 200f, 0f));
            relaxed.motion = clip;
            sm.defaultState = relaxed;
            Link(relaxed, carry, AnimatorConditionMode.If);
            Link(carry, relaxed, AnimatorConditionMode.IfNot);
            EditorUtility.SetDirty(ac);
            Debug.Log("[Pose] OK    " + ControllerPath + ": " + RelaxedState + " is the default, " + CarryState
                      + " while " + Param + " is true, " + BlendSeconds + " s blends");
            return true;
        }

        static void Link(AnimatorState from, AnimatorState to, AnimatorConditionMode mode)
        {
            var t = from.transitions.FirstOrDefault(x => x.destinationState == to) ?? from.AddTransition(to);
            t.hasExitTime = false;
            t.hasFixedDuration = true;
            t.duration = BlendSeconds;
            t.conditions = new[] { new AnimatorCondition { mode = mode, parameter = Param, threshold = 0f } };
        }

        static bool AddToPrefabs()
        {
            bool ok = true;
            foreach (var path in Prefabs)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var animator = root.GetComponentInChildren<Animator>();
                    if (animator == null)
                    {
                        Debug.LogError("[Pose] no Animator in " + path);
                        ok = false;
                        continue;
                    }
                    if (animator.GetComponent<CrewPose>() != null)
                    {
                        Debug.Log("[Pose] OK    " + path + " already has CrewPose");
                        continue;
                    }
                    animator.gameObject.AddComponent<CrewPose>();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    Debug.Log("[Pose] OK    CrewPose added to " + path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            return ok;
        }

        static AnimationClip LoadClip(string path, string name)
        {
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .FirstOrDefault(c => c.name == name);
        }
    }
}
#endif
