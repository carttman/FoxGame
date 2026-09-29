using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// 6단계: 낙하 = 게임 오버, 전부 수집 = 클리어, 결과 화면
// 메뉴: Tools > Fox > 6단계 게임 결과
// 배치: Unity.exe -batchmode -projectPath . -executeMethod ResultSetup.Run -quit
public static class ResultSetup
{
    const string ScenePath = "Assets/Scenes/FoxGame.unity";
    const float Dt = 0.02f;

    [MenuItem("Tools/Fox/6단계 게임 결과")]
    public static void Run()
    {
        BuildScene();
        Verify();
        Debug.Log("[ResultSetup] 완료");
    }

    // ── 씬 ─────────────────────────────────────────────────────────
    static void BuildScene()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var gm = Object.FindFirstObjectByType<GameManager>();
        var so = new SerializedObject(gm);
        so.FindProperty("fox").objectReferenceValue = Object.FindFirstObjectByType<FoxController>();
        so.ApplyModifiedPropertiesWithoutUndo();

        var hud = GameObject.Find("HUD");
        var old = hud.transform.Find("ResultOverlay");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var oldComp = hud.GetComponent<ResultScreen>();
        if (oldComp != null) Object.DestroyImmediate(oldComp);

        // 화면 전체를 덮는 어두운 배경
        var overlay = NewUI("ResultOverlay", hud.transform);
        Stretch(overlay);
        overlay.gameObject.AddComponent<Image>().color = new Color(0.05f, 0.04f, 0.08f, 0.6f);
        var group = overlay.gameObject.AddComponent<CanvasGroup>();

        // 가운데 패널 (위에서부터 제목 → 내용 → 다시 하기 버튼 → 안내)
        var panel = NewUI("Panel", overlay);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(900f, 640f);
        var bg = panel.gameObject.AddComponent<Image>();
        bg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.14f, 0.12f, 0.11f, 0.92f);

        var title = MakeText("Title", panel, 140, FontStyle.Bold, top: 30f, height: 170f);
        var titleOutline = title.gameObject.AddComponent<Outline>();
        titleOutline.effectColor = new Color(0f, 0f, 0f, 0.6f);
        titleOutline.effectDistance = new Vector2(4f, -4f);

        var detail = MakeText("Detail", panel, 48, FontStyle.Normal, top: 210f, height: 150f);
        detail.color = new Color(0.95f, 0.92f, 0.88f);
        detail.lineSpacing = 1.2f;

        var (retry, retryLabel) = MakeButton("RetryButton", panel, top: 395f, size: new Vector2(380f, 110f));

        var hint = MakeText("Hint", panel, 30, FontStyle.Normal, top: 540f, height: 60f);
        hint.color = new Color(0.8f, 0.76f, 0.7f);
        hint.text = "R 키를 눌러도 다시 시작해요";

        var screen = hud.AddComponent<ResultScreen>();
        var sso = new SerializedObject(screen);
        sso.FindProperty("group").objectReferenceValue = group;
        sso.FindProperty("panel").objectReferenceValue = panel;
        sso.FindProperty("title").objectReferenceValue = title;
        sso.FindProperty("detail").objectReferenceValue = detail;
        sso.FindProperty("retryButton").objectReferenceValue = retry;
        sso.FindProperty("retryLabel").objectReferenceValue = retryLabel;
        sso.FindProperty("hint").objectReferenceValue = hint;
        sso.ApplyModifiedPropertiesWithoutUndo();

        EnsureEventSystem();

        overlay.gameObject.SetActive(false); // 게임 중에는 숨김

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    // 패널 위쪽에서 top만큼 내려온 곳에 가로 전체 폭, height 높이의 글자 띠
    static Text MakeText(string name, RectTransform parent, int size, FontStyle style, float top, float height)
    {
        var rt = NewUI(name, parent);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(24f, -top - height);
        rt.offsetMax = new Vector2(-24f, -top);
        var t = rt.gameObject.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = size;
        t.fontStyle = style;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    static (Button, Text) MakeButton(string name, RectTransform parent, float top, Vector2 size)
    {
        var rt = NewUI(name, parent);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -top);
        rt.sizeDelta = size;
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        img.type = Image.Type.Sliced;
        img.color = Color.white;

        var button = rt.gameObject.AddComponent<Button>();
        var colors = button.colors;
        colors.normalColor = new Color(0.95f, 0.55f, 0.18f);      // 여우 주황
        colors.highlightedColor = new Color(1f, 0.66f, 0.3f);
        colors.selectedColor = new Color(1f, 0.66f, 0.3f);
        colors.pressedColor = new Color(0.8f, 0.42f, 0.1f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        var label = MakeText("Label", rt, 52, FontStyle.Bold, top: 0f, height: size.y);
        label.text = "다시 하기";
        return (button, label);
    }

    static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;
        var go = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
        var module = go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        module.AssignDefaultActions(); // 새 Input System용 UI 입력 (마우스, Enter/Space, 게임패드)
    }

    static RectTransform NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    // ── 검증 ────────────────────────────────────────────────────────
    class Sim
    {
        public FoxController fox;
        FoxAnimator anim;
        ItemCollector collector;
        public GameManager game;
        public GameHUD hud;
        public ResultScreen result;
        Item[] items;

        public static Sim Start()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Physics.SyncTransforms();
            var s = new Sim { fox = Object.FindFirstObjectByType<FoxController>() };
            s.anim = s.fox.GetComponent<FoxAnimator>();
            s.collector = s.fox.GetComponent<ItemCollector>();
            s.game = Object.FindFirstObjectByType<GameManager>();
            s.hud = Object.FindFirstObjectByType<GameHUD>();
            s.result = Object.FindFirstObjectByType<ResultScreen>();
            s.game.Init();
            s.hud.Bind(s.game);
            s.result.Bind(s.game);
            s.items = Object.FindObjectsByType<Item>(FindObjectsSortMode.None);
            s.Wait(0.2f);
            return s;
        }

        public Vector3 Pos => fox.transform.position;

        public void Step(Vector2 input, bool jump = false)
        {
            fox.Tick(input, jump, Dt);
            Physics.SyncTransforms();
            anim.Tick(Dt);
            collector.Tick();
            game.Tick(Dt);
            result.Tick(Dt);
            foreach (var it in items) if (it.gameObject.activeSelf) it.Tick(Dt);
        }

        public void Wait(float seconds)
        {
            for (float t = 0; t < seconds; t += Dt) Step(Vector2.zero);
        }

        public void Hold(Vector2 input, float seconds)
        {
            for (float t = 0; t < seconds; t += Dt) Step(input);
        }

        public void MoveTo(float x, float z, System.Func<Vector3, bool> jumpWhen = null, float timeout = 8f)
        {
            bool jumped = false;
            for (float t = 0; t < timeout; t += Dt)
            {
                var d = new Vector2(x - Pos.x, z - Pos.z);
                if (d.magnitude < 0.08f && fox.IsGrounded) break;
                if (!fox.InputEnabled) break;
                bool press = jumpWhen != null && !jumped && jumpWhen(Pos);
                if (press) jumped = true;
                Step(d.magnitude < 0.08f ? Vector2.zero : d.normalized, press);
                if (Pos.y < -2f) break;
            }
        }

        public void CollectAll()
        {
            MoveTo(3f, -3f, p => p.x >= -0.35f);
            MoveTo(3f, -1f);
            MoveTo(3f, 3f);
            MoveTo(3f, -1f);
            MoveTo(-3f, -1f);
            MoveTo(-3f, 3f);
            MoveTo(-3f, 1f);
            MoveTo(1f, 1f, p => p.x >= -2.35f);
            Wait(0.8f);
        }
    }

    static void Check(string label, bool ok, string detail)
    {
        string line = $"[ResultSetup] 검증 {(ok ? "통과" : "실패")} | {label} | {detail}";
        if (ok) Debug.Log(line); else Debug.LogError(line);
    }

    static string Oneline(string s) => s.Replace("\n", " / ");

    public static void Verify()
    {
        // 1) 평소에는 결과 화면이 뜨지 않음
        {
            var s = Sim.Start();
            s.Hold(new Vector2(0, 1), 1.5f);
            s.Hold(new Vector2(1, 0), 0.5f);
            s.Wait(1f);
            Check("플레이 중 유지", s.game.State == GameState.Playing && !s.result.IsVisible,
                  $"상태 {s.game.State}, 결과 화면 {(s.result.IsVisible ? "보임" : "숨김")}, 경과 {s.game.ElapsedTime:F1}초");
        }

        // 2) 구멍에 빠지면 게임 오버
        {
            var s = Sim.Start();
            s.Hold(new Vector2(1, 0), 1.5f);
            float t = 0f;
            while (s.game.State == GameState.Playing && t < 3f) { s.Step(Vector2.zero); t += Dt; }
            s.Wait(0.6f);
            bool ok = s.game.State == GameState.GameOver && s.result.IsVisible && s.result.Title == "GAME OVER";
            Check("구멍 낙하 → 게임 오버", ok, $"상태 {s.game.State}, 제목 \"{s.result.Title}\", 내용 \"{Oneline(s.result.Detail)}\"");
            CaptureMap("Captures/step6_gameover.png");
        }

        // 3) 맵 밖으로 나가도 게임 오버, 먹은 개수 표시
        {
            var s = Sim.Start();
            s.MoveTo(-3f, 1f);
            s.MoveTo(-3f, 3f);        // (0,3) 체리 1개
            s.Hold(new Vector2(0, 1), 2f); // 북쪽 끝 밖으로
            s.Wait(2f);
            bool ok = s.game.State == GameState.GameOver && s.result.Detail.Contains("1 / 5");
            Check("맵 밖 낙하 → 게임 오버", ok, $"상태 {s.game.State}, 내용 \"{Oneline(s.result.Detail)}\"");
        }

        // 4) 5개 모두 먹으면 클리어
        {
            var s = Sim.Start();
            s.CollectAll();
            // 마지막 체리는 공중 체리라 점프 도중 클리어된다 → 입력이 막혀도 건너편에 착지해야 함
            bool ok = s.game.State == GameState.Cleared && s.result.IsVisible && s.result.Title == "CLEAR!" && s.Pos.y > -0.1f;
            Check("전부 수집 → 클리어", ok, $"상태 {s.game.State}, 제목 \"{s.result.Title}\", 내용 \"{Oneline(s.result.Detail)}\", 여우 {s.Pos:F2}");

            // 5) 클리어 후에는 조작이 막히고 상태가 바뀌지 않음
            Vector3 before = s.Pos;
            s.Hold(new Vector2(1, 0), 1.5f);   // 입력해도
            s.Step(Vector2.zero, true);        // 점프해도
            s.Wait(1f);
            float moved = Vector3.Distance(before, s.Pos);
            Check("클리어 후 조작 잠금", moved < 0.05f && s.game.State == GameState.Cleared,
                  $"이동 거리 {moved:F3}m, 상태 {s.game.State}");
            CaptureMap("Captures/step6_clear.png");
        }

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    // ── 캡처 ────────────────────────────────────────────────────────
    static void CaptureMap(string path)
    {
        Canvas.ForceUpdateCanvases();
        var cam = Camera.main;
        const int w = 1200, h = 800;
        var rt = new RenderTexture(w, h, 24) { antiAliasing = 4 };
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        cam.targetTexture = null;
        RenderTexture.active = null;
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
    }
}
