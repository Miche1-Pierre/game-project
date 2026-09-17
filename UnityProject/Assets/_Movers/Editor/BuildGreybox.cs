using UnityEditor;
using UnityEditor.Build.Reporting;
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
}
