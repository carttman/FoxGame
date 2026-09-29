using System.IO;
using System;
using UnityEditor;
using UnityEditor.Build;
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

        // 압축은 끈다: GitHub Pages 등 서버가 gzip으로 보내 주고, 켜면 서버 설정이나 JS 압축 해제(느림)가 필요하다
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
        PlayerSettings.WebGL.decompressionFallback = false;

        // 용량 줄이기 (이슈 #12)
        FontSubsetter.Run();                                                              // 한글 폰트 10MB → 쓰는 글자만
        PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.High); // 안 쓰는 C# 코드 제거
        SetCodeOptimization("DiskSizeLTO");                                              // wasm을 크기 우선 + LTO로

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

    // UnityEditor.WebGL.UserBuildSettings.codeOptimization은 WebGL 모듈에만 있어서 리플렉션으로 설정한다
    // (모듈이 없는 PC에서도 이 스크립트가 컴파일되게)
    static void SetCodeOptimization(string value)
    {
        var settings = Type.GetType("UnityEditor.WebGL.UserBuildSettings, UnityEditor.WebGL.Extensions");
        var prop = settings?.GetProperty("codeOptimization");
        if (prop == null)
        {
            Debug.LogWarning("[WebGLBuild] 코드 최적화 설정을 찾지 못해 기본값으로 빌드합니다");
            return;
        }
        prop.SetValue(null, Enum.Parse(prop.PropertyType, value));
        Debug.Log($"[WebGLBuild] 코드 최적화 = {prop.GetValue(null)}");
    }
}
