// Unity 명령행에서 호출하는 Windows 64비트 빌드 진입점이다.
// 활성화된 빌드 씬을 사용하고 Direct3D 11을 지정하며 실패 시 예외로 빌드 오류를 알린다.

#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine.Rendering;

public static class CommandLineBuild
{
    // Unity의 -executeMethod 인자로 호출한다. -buildOutput이 없으면 기본 출력 경로를 사용한다.
    public static void BuildWindows64()
    {
        string outputPath = GetArgumentValue("-buildOutput");
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            outputPath = "Builds/PatternTestMerge/DanceGamePatternTest.exe";
        }

        outputPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);

        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,
            new[] { GraphicsDeviceType.Direct3D11 });

        string[] scenes = Array.ConvertAll(
            Array.FindAll(EditorBuildSettings.scenes, scene => scene.enabled),
            scene => scene.path);

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outputPath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new Exception($"Build failed: {report.summary.result}");
        }
    }

    private static string GetArgumentValue(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name)
            {
                return args[i + 1];
            }
        }

        return string.Empty;
    }
}
#endif
