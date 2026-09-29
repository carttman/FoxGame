using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

// 7단계: 재시작 (R 키 / 결과 화면 [다시 하기] 버튼)
// 결과 화면(버튼 포함)과 EventSystem은 ResultSetup이 만들고, 여기서는 연결 상태를 확인한다.
// 실제 재시작 동작은 Play 모드 테스트(Assets/Tests/PlayMode)로 검증한다.
// 메뉴: Tools > Fox > 7단계 재시작
public static class RestartSetup
{
    const string ScenePath = "Assets/Scenes/FoxGame.unity";

    [MenuItem("Tools/Fox/7단계 재시작")]
    public static void Run()
    {
        ResultSetup.Run();
        Check();
        Debug.Log("[RestartSetup] 완료 - Play 모드 테스트: Window > General > Test Runner > PlayMode > Run All");
    }

    static void Check()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var es = Object.FindFirstObjectByType<EventSystem>();
        bool module = es != null && es.GetComponent<InputSystemUIInputModule>() != null;
        Log("EventSystem + 새 Input System UI 모듈", module, es == null ? "없음" : es.name);

        var result = Object.FindFirstObjectByType<ResultScreen>();
        bool button = result != null && result.RetryButton != null && result.RetryButton.interactable;
        Log("결과 화면 [다시 하기] 버튼", button, button ? result.RetryButton.name : "없음");

        var scenes = EditorBuildSettings.scenes;
        bool inBuild = scenes.Length > 0 && scenes[0].path == ScenePath && scenes[0].enabled;
        Log("빌드 씬 등록 (재시작 시 다시 불러올 씬)", inBuild, scenes.Length > 0 ? scenes[0].path : "없음");
    }

    static void Log(string label, bool ok, string detail)
    {
        string line = $"[RestartSetup] 검증 {(ok ? "통과" : "실패")} | {label} | {detail}";
        if (ok) Debug.Log(line); else Debug.LogError(line);
    }
}
