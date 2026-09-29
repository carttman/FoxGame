using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// 웹 미리보기용 WebGL 빌드 (결과: FoxGame/Builds/WebGL, Git에는 올리지 않음)
// 압축을 끄면 서버 설정 없이 아무 정적 서버(GitHub Pages, python -m http.server 등)에서 바로 열린다.
// 메뉴: Tools > Fox > WebGL 빌드
// 배치 모드: Unity -batchmode -quit -projectPath FoxGame -executeMethod WebGLBuild.Run
public static class WebGLBuild
{
    const string ScenePath = "Assets/Scenes/FoxGame.unity";
    const string OutputDir = "Builds/WebGL";

    [MenuItem("Tools/Fox/WebGL 빌드")]
    public static void Run()
    {
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
        {
            const string msg = "WebGL 모듈이 없습니다. Unity Hub > Installs > 6000.3.10f1 > Add modules > WebGL Build Support 설치 후 에디터를 다시 여세요.";
            Debug.LogError("[WebGLBuild] " + msg);
            if (!Application.isBatchMode) EditorUtility.DisplayDialog("WebGL 빌드", msg, "확인");
            return;
        }

        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
        PlayerSettings.WebGL.decompressionFallback = false;

        var options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = OutputDir,
            target = BuildTarget.WebGL,
            targetGroup = BuildTargetGroup.WebGL,
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        var summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            string full = Path.GetFullPath(OutputDir);
            Debug.Log($"[WebGLBuild] 완료 | {full} | {summary.totalSize / (1024f * 1024f):F1}MB | {summary.totalTime.TotalSeconds:F0}초");
            if (!Application.isBatchMode) EditorUtility.RevealInFinder(Path.Combine(full, "index.html"));
        }
        else
        {
            Debug.LogError($"[WebGLBuild] 실패 | {summary.result} | 오류 {summary.totalErrors}건");
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
