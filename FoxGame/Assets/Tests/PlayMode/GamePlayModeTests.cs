using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// 실제 Play 모드에서 가상 키보드로 조작해 게임 흐름과 재시작(7단계)을 확인한다.
// 실행: Window > General > Test Runner > PlayMode > Run All
public class GamePlayModeTests
{
    const string SceneName = "FoxGame";

    Keyboard keyboard;
    InputSettings.BackgroundBehavior prevBackground;
#if UNITY_EDITOR
    InputSettings.EditorInputBehaviorInPlayMode prevEditorBehavior;
#endif

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        // 배치 모드처럼 창에 포커스가 없어도 가상 키보드 입력이 들어오게 한다
        prevBackground = InputSystem.settings.backgroundBehavior;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
        prevEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
        keyboard = InputSystem.AddDevice<Keyboard>("TestKeyboard");
        Application.targetFrameRate = 60; // 배치 모드의 수천 fps 대신 실제 플레이와 비슷한 조건

        SceneManager.LoadScene(SceneName);
        yield return null;
        yield return new WaitForSeconds(0.3f); // 여우 착지
    }

    [TearDown]
    public void TearDown()
    {
        if (keyboard != null) InputSystem.RemoveDevice(keyboard);
        InputSystem.settings.backgroundBehavior = prevBackground;
#if UNITY_EDITOR
        InputSystem.settings.editorInputBehaviorInPlayMode = prevEditorBehavior;
#endif
    }

    // ── 도우미 ──────────────────────────────────────────────────────
    static GameManager Game => UnityEngine.Object.FindFirstObjectByType<GameManager>();
    static FoxController Fox => UnityEngine.Object.FindFirstObjectByType<FoxController>();
    static ResultScreen Result => UnityEngine.Object.FindFirstObjectByType<ResultScreen>();

    IEnumerator Hold(Key key, float seconds)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
        yield return new WaitForSeconds(seconds);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return null;
    }

    IEnumerator Press(Key key)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
        yield return null;
        yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return null;
    }

    static IEnumerator WaitFor(Func<bool> cond, float timeout, string what)
    {
        float t = 0f;
        while (!cond())
        {
            if (t > timeout) Assert.Fail($"{timeout}초 안에 일어나지 않음: {what}");
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    // 재시작 뒤 새 씬의 GameManager가 생길 때까지 기다린다
    static IEnumerator WaitForReload(GameManager old)
    {
        yield return WaitFor(() => { var g = Game; return g != null && g != old; }, 3f, "씬 다시 불러오기");
        yield return new WaitForSeconds(0.3f);
    }

    static void AssertFreshGame(string when)
    {
        var start = LevelLayout.StartPosition();
        var p = Fox.transform.position;
        Assert.AreEqual(GameState.Playing, Game.State, $"{when}: 상태");
        Assert.AreEqual(5, Game.Total, $"{when}: 전체 체리");
        Assert.AreEqual(0, Game.Collected, $"{when}: 먹은 체리");
        Assert.IsFalse(Result.IsVisible, $"{when}: 결과 화면이 숨겨져야 함");
        Assert.Less(new Vector2(p.x - start.x, p.z - start.z).magnitude, 0.1f, $"{when}: 여우가 시작 위치에 있어야 함 {p}");
        Assert.IsTrue(Fox.InputEnabled, $"{when}: 조작 가능해야 함");
    }

    // ── 테스트 ──────────────────────────────────────────────────────
    [UnityTest]
    public IEnumerator 시작_상태()
    {
        AssertFreshGame("시작");
        yield break;
    }

    [UnityTest]
    public IEnumerator 키보드_이동과_점프()
    {
        Vector3 start = Fox.transform.position;
        yield return Hold(Key.W, 0.6f);
        Assert.Greater(Fox.transform.position.z - start.z, 1.5f, "W로 앞(+Z)으로 이동");

        float y0 = Fox.transform.position.y;
        yield return Press(Key.Space);
        yield return new WaitForSeconds(0.25f);
        Assert.Greater(Fox.transform.position.y - y0, 0.8f, "Space로 점프");
        yield return WaitFor(() => Fox.IsGrounded, 2f, "착지");
    }

    [UnityTest]
    public IEnumerator 구멍_낙하_게임오버_후_R키_재시작()
    {
        yield return Hold(Key.D, 1.4f); // (2,0) 구멍으로
        yield return WaitFor(() => Game.State == GameState.GameOver, 4f, "게임 오버");
        yield return new WaitForSeconds(0.5f);
        Assert.IsTrue(Result.IsVisible, "결과 화면 표시");
        Assert.AreEqual("GAME OVER", Result.Title);
        Assert.AreEqual(Result.RetryButton.gameObject, EventSystem.current.currentSelectedGameObject, "다시 하기 버튼이 선택돼 있어야 함");

        var old = Game;
        yield return Press(Key.R);
        yield return WaitForReload(old);
        AssertFreshGame("R 키 재시작 후");
    }

    [UnityTest]
    public IEnumerator 게임오버_후_버튼_재시작()
    {
        yield return Hold(Key.D, 1.4f);
        yield return WaitFor(() => Game.State == GameState.GameOver, 4f, "게임 오버");

        var old = Game;
        Assert.IsTrue(Result.RetryButton.interactable);
        Result.RetryButton.onClick.Invoke();
        yield return WaitForReload(old);
        AssertFreshGame("버튼 재시작 후");
    }

    [UnityTest]
    public IEnumerator 게임오버_후_Enter로_선택된_버튼_누르기()
    {
        yield return Hold(Key.D, 1.4f);
        yield return WaitFor(() => Game.State == GameState.GameOver, 4f, "게임 오버");
        yield return new WaitForSeconds(0.5f);

        var old = Game;
        yield return Press(Key.Enter);
        yield return WaitForReload(old);
        AssertFreshGame("Enter 재시작 후");
    }

    [UnityTest]
    public IEnumerator 플레이_중_R키_재시작()
    {
        yield return Hold(Key.W, 0.5f);
        Assert.AreEqual(GameState.Playing, Game.State);

        var old = Game;
        yield return Press(Key.R);
        yield return WaitForReload(old);
        AssertFreshGame("플레이 중 R 후");
    }

    [UnityTest]
    public IEnumerator 클리어_후_재시작하면_체리_복구()
    {
        foreach (var item in UnityEngine.Object.FindObjectsByType<Item>(FindObjectsSortMode.None)) item.Collect();
        Assert.AreEqual(GameState.Cleared, Game.State);
        yield return new WaitForSeconds(0.6f);
        Assert.AreEqual("CLEAR!", Result.Title);
        Assert.AreEqual(0, UnityEngine.Object.FindObjectsByType<Item>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length, "먹은 체리는 사라짐");

        var old = Game;
        yield return Press(Key.R);
        yield return WaitForReload(old);
        AssertFreshGame("클리어 후 R");
    }
}
