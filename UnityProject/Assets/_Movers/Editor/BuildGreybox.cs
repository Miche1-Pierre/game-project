using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

// Batchmode build entry point for the greybox, driven from the command line.
public static class BuildGreybox
{
    public static void Windows64()
    {
        var opts = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/_Movers/Scenes/Tutorial_01.unity" },
            locationPathName = "Build/Movers.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(opts);
        BuildSummary s = report.summary;
        Debug.Log($"BUILD_RESULT={s.result} errors={s.totalErrors} warnings={s.totalWarnings} time={s.totalTime}");
        EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
    }

    // The build for the online loopback test (NETCODE_SLICE 14): the enabled Build Settings
    // scenes (MainMenu, Map01), a development build (debug keys, the direct-host option, NGO's
    // message-size checks), at UnityProject/Build/Movers.exe (gitignored). Run from the open
    // editor, outside Play: it never quits the editor, and it refuses rather than build a
    // scene with unsaved changes, since the id digest would then refuse the build anyway.
    [MenuItem("The Movers/Build Online Game (Windows)")]
    public static void NetClient()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("BUILD_RESULT=Refused reason=\"the editor is in Play mode\"");
            return;
        }
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            var scene = EditorSceneManager.GetSceneAt(i);
            if (!scene.isDirty) continue;
            Debug.LogError("BUILD_RESULT=Refused reason=\"unsaved changes in " + scene.path + "\"");
            return;
        }

        var scenes = new List<string>();
        foreach (var s in EditorBuildSettings.scenes)
            if (s.enabled && !string.IsNullOrEmpty(s.path)) scenes.Add(s.path);
        if (scenes.Count == 0)
        {
            Debug.LogError("BUILD_RESULT=Refused reason=\"no enabled scene in the Build Settings\"");
            return;
        }

        var opts = new BuildPlayerOptions
        {
            scenes = scenes.ToArray(),
            locationPathName = "Build/Movers.exe",   // relative to UnityProject/
            target = BuildTarget.StandaloneWindows64,
            targetGroup = BuildTargetGroup.Standalone,
            options = BuildOptions.Development
        };

        BuildReport report = BuildPipeline.BuildPlayer(opts);
        BuildSummary sum = report.summary;
        Debug.Log($"BUILD_RESULT={sum.result} errors={sum.totalErrors} warnings={sum.totalWarnings} time={sum.totalTime} " +
                  $"scenes={string.Join(",", scenes)} path={sum.outputPath}");
    }
}
