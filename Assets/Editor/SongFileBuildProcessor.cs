// 빌드가 끝나면 런타임에서 직접 읽을 음악·패턴 원본 파일을 플레이어 데이터 폴더에 복사한다.
// Unity 에셋 포함만으로는 System.IO 경로 접근을 보장할 수 없어 별도 패키징을 수행한다.

using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// These files are opened with System.IO at runtime, so Unity's imported
// assets alone are not sufficient. Preserve the layout used by the loaders.
public sealed class SongFileBuildProcessor : IPostprocessBuildWithReport
{
    public int callbackOrder { get { return 0; } }

    public void OnPostprocessBuild(BuildReport report)
    {
        string output = Path.GetFullPath(report.summary.outputPath);
        string dataPath;
        switch (report.summary.platform)
        {
            case BuildTarget.StandaloneWindows:
            case BuildTarget.StandaloneWindows64:
            case BuildTarget.StandaloneLinux64:
                dataPath = Path.Combine(Path.GetDirectoryName(output),
                    Path.GetFileNameWithoutExtension(output) + "_Data");
                break;
            case BuildTarget.StandaloneOSX:
                dataPath = Path.Combine(output, "Contents", "Resources", "Data");
                break;
            default:
                return;
        }

        try
        {
            if (!Directory.Exists(dataPath))
                throw new DirectoryNotFoundException("Player data folder not found: " + dataPath);

            int count = CopyFolder(Application.dataPath, dataPath, "Patterns");
            count += CopyFolder(Application.dataPath, dataPath, "Musics/wav");
            Debug.Log("Song files packaged: " + count + " files in " + dataPath);
        }
        catch (Exception exception)
        {
            throw new BuildFailedException("Could not package song files: " + exception.Message);
        }
    }

    // 하위 경로를 유지해 원본 파일을 복사하고 Unity 메타 파일과 임시 잠금 파일은 제외한다.
    private static int CopyFolder(string sourceRoot, string destinationRoot, string relativeFolder)
    {
        string source = Path.Combine(sourceRoot, relativeFolder);
        if (!Directory.Exists(source))
            throw new DirectoryNotFoundException("Song source folder not found: " + source);

        int count = 0;
        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) ||
                Path.GetFileName(file).StartsWith("~$", StringComparison.Ordinal))
                continue;

            string relativeFile = file.Substring(source.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string destination = Path.Combine(destinationRoot, relativeFolder, relativeFile);
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            File.Copy(file, destination, true);
            count++;
        }
        return count;
    }
}
