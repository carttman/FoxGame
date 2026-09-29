using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Microsoft Store용 Windows 빌드 (64비트 데스크톱). 결과: FoxGame/Builds/Windows/FoxGame.exe (Git에는 올리지 않음)
// 스토어 제출용 MSIX 패키지는 이 빌드를 Store/make-msix.ps1로 포장해서 만든다.
// 메뉴: Tools > Fox > Windows 스토어 빌드
// 배치 모드: Unity -batchmode -quit -projectPath FoxGame -executeMethod WindowsStoreBuild.Run
public static class WindowsStoreBuild
{
    const string ScenePath = "Assets/Scenes/FoxGame.unity";
    const string OutputDir = "Builds/Windows";
    const string IconPath = "Assets/UI/app_icon.png"; // Art/app_icon_transparent.png (Codex가 그린 아이콘, 모서리 투명)

    [MenuItem("Tools/Fox/Windows 스토어 빌드")]
    public static void Run()
    {
        FontSubsetter.Run(); // 결과 화면 한글 폰트 (WebGL과 같은 서브셋)
        ApplyIcon();

        string exe = Path.Combine(OutputDir, PlayerSettings.productName + ".exe");
        var options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = exe,
            target = BuildTarget.StandaloneWindows64,
            targetGroup = BuildTargetGroup.Standalone,
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        var summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            string full = Path.GetFullPath(OutputDir);
            Debug.Log($"[WindowsStoreBuild] 완료 | {full} | {summary.totalSize / (1024f * 1024f):F1}MB | {summary.totalTime.TotalSeconds:F0}초");
            Debug.Log("[WindowsStoreBuild] 다음: powershell -ExecutionPolicy Bypass -File Store/make-msix.ps1 (저장소 루트에서)");
            if (!Application.isBatchMode) EditorUtility.RevealInFinder(Path.GetFullPath(exe));
        }
        else
        {
            Debug.LogError($"[WindowsStoreBuild] 실패 | {summary.result} | 오류 {summary.totalErrors}건");
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }

    // 실행 파일·창 아이콘: 기본 아이콘 한 장을 넣으면 Unity가 필요한 크기로 줄여 쓴다
    static void ApplyIcon()
    {
        var importer = AssetImporter.GetAtPath(IconPath) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError($"[WindowsStoreBuild] 아이콘이 없습니다: {IconPath}");
            return;
        }
        if (!importer.alphaIsTransparency || importer.textureCompression != TextureImporterCompression.Uncompressed || importer.mipmapEnabled)
        {
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 1024;
            importer.SaveAndReimport();
        }
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
        PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
        Debug.Log($"[WindowsStoreBuild] 아이콘 = {IconPath} ({icon.width}x{icon.height})");
    }
}
