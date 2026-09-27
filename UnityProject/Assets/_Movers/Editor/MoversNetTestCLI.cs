#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Movers.EditorTools
{
    // Loopback run 2 (NETCODE_SLICE 14): the editor hosts, a build joins. RunHost enters Play on
    // the title scene, hosts by direct IP on 7777 with -netautostart semantics, and runs the
    // "host" NetTestBot script; the build client is started by hand:
    //
    //   Build/Movers.exe -screen-fullscreen 0 -screen-width 960 -screen-height 540 \
    //     -logFile Logs/client_player.log -netjoin 127.0.0.1 -netautostart -netbot client \
    //     -netlog Logs/net_client.log -netdumpids Logs/ids_client.txt \
    //     -netbotpeerlog <UnityProject>/Logs/net_editor_host.log
    //
    // Results are the NETTEST lines of Logs/net_editor_host.log; the editor's id dump is
    // Logs/ids_editor.txt, to diff against the build's. Play ends by itself once the bot is done.
    // It refuses to start in Play or with unsaved scenes: it never saves or discards for you.
    public static class MoversNetTestCLI
    {
        const double DeadlineSeconds = 600;

        static bool launched;
        static System.DateTime startedAt;
        static SceneAsset savedStartScene;
        static bool savedOptionsEnabled;
        static EnterPlayModeOptions savedOptions;
        static bool savedRunInBackground, touchedRunInBackground;

        static string LogsDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs"));
        public static string HostLogPath => Path.Combine(LogsDir, "net_editor_host.log");
        public static string IdsPath => Path.Combine(LogsDir, "ids_editor.txt");

        [MenuItem("The Movers/Net Test Host (editor hosts, a build joins)")]
        public static void RunHost()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[NetTest] already in Play mode, nothing done.");
                return;
            }
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                var scene = EditorSceneManager.GetSceneAt(i);
                if (!scene.isDirty) continue;
                Debug.LogWarning("[NetTest] unsaved changes in " + scene.path + ": save or discard them first, nothing done.");
                return;
            }

            // The domain must survive entering Play, or this callback is lost with it.
            savedOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            savedOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;
            savedStartScene = EditorSceneManager.playModeStartScene;
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SceneFlow.MenuScenePath);

            launched = false;
            touchedRunInBackground = false;
            startedAt = System.DateTime.UtcNow;
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }

        static void Tick()
        {
            if ((System.DateTime.UtcNow - startedAt).TotalSeconds > DeadlineSeconds)
            {
                Debug.LogError("[NetTest] deadline reached, the bot never finished.");
                Finish();
                return;
            }
            if (!EditorApplication.isPlaying)
            {
                if (launched) Finish();
                return;
            }
            if (!launched)
            {
                // After entering Play: the statics reset at SubsystemRegistration would wipe these.
                launched = true;
                savedRunInBackground = Application.runInBackground;
                touchedRunInBackground = true;
                Application.runInBackground = true;   // an unfocused editor must keep pumping the transport
                NetSession.SetTestOptions(HostLogPath, "host", IdsPath, true);
                NetSession.HostDirect(NetSession.DefaultPort);
                NetTestBot.Launch("host");
                Debug.Log("[NetTest] hosting on " + NetSession.DefaultPort + ", start the build client now. Log: " + HostLogPath);
                return;
            }
            if (NetTestBot.Finished) Finish();
        }

        static void Finish()
        {
            EditorApplication.update -= Tick;
            Debug.Log("[NetTest] done: pass=" + NetTestBot.PassCount + " fail=" + NetTestBot.FailCount + ", log " + HostLogPath);
            if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
            EditorSettings.enterPlayModeOptionsEnabled = savedOptionsEnabled;
            EditorSettings.enterPlayModeOptions = savedOptions;
            EditorSceneManager.playModeStartScene = savedStartScene;
            if (touchedRunInBackground)
            {
                Application.runInBackground = savedRunInBackground;
                touchedRunInBackground = false;
            }
            if (Application.isBatchMode) EditorApplication.Exit(NetTestBot.FailCount == 0 && NetTestBot.Finished ? 0 : 2);
        }
    }
}
#endif
