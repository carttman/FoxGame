using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 8단계(추가): 무너지는 바닥 — 여우가 지나간 타일은 흔들리다가 1초 뒤에 떨어진다.
// 타일에 CrumblingTile, TileMap에 TileCrumbler를 붙이고 정적 배칭을 끈다 (정적 배칭된 물체는 빌드에서 못 움직임).
// 메뉴: Tools > Fox > 8단계 무너지는 바닥
// 배치: Unity.exe -batchmode -projectPath . -executeMethod CrumbleSetup.Run -quit
public static class CrumbleSetup
{
    const string ScenePath = "Assets/Scenes/FoxGame.unity";
    const float Dt = 0.02f;

    [MenuItem("Tools/Fox/8단계 무너지는 바닥")]
    public static void Run()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        ApplyTo(GameObject.Find("TileMap").transform);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Verify();
        Debug.Log("[CrumbleSetup] 완료");
    }

    // LevelBuilder(3단계)가 타일맵을 새로 만들 때도 호출한다
    public static void ApplyTo(Transform map)
    {
        foreach (Transform tile in map)
        {
            var parts = tile.name.Split('_'); // Tile_x_z
            if (parts.Length != 3 || !int.TryParse(parts[1], out int x) || !int.TryParse(parts[2], out int z)) continue;
            GameObjectUtility.SetStaticEditorFlags(tile.gameObject, 0);
            foreach (Transform child in tile) GameObjectUtility.SetStaticEditorFlags(child.gameObject, 0);
            var ct = tile.GetComponent<CrumblingTile>();
            if (ct == null) ct = tile.gameObject.AddComponent<CrumblingTile>();
            ct.Init(new Vector2Int(x, z));
            EditorUtility.SetDirty(ct);
        }

        var crumbler = map.GetComponent<TileCrumbler>();
        if (crumbler == null) crumbler = map.gameObject.AddComponent<TileCrumbler>();
        var so = new SerializedObject(crumbler);
        so.FindProperty("fox").objectReferenceValue = Object.FindFirstObjectByType<FoxController>();
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ── 검증 ────────────────────────────────────────────────────────
    class Sim
    {
        public FoxController fox;
        FoxAnimator anim;
        ItemCollector collector;
        public GameManager game;
        public TileCrumbler crumbler;
        Item[] items;

        public static Sim Start()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Physics.SyncTransforms();
            var s = new Sim { fox = Object.FindFirstObjectByType<FoxController>() };
            s.anim = s.fox.GetComponent<FoxAnimator>();
            s.collector = s.fox.GetComponent<ItemCollector>();
            s.game = Object.FindFirstObjectByType<GameManager>();
            s.crumbler = Object.FindFirstObjectByType<TileCrumbler>();
            s.game.Init();
            Object.FindFirstObjectByType<GameHUD>().Bind(s.game); // 캡처에 먹은 개수가 보이게
            s.crumbler.Init();
            s.items = Object.FindObjectsByType<Item>(FindObjectsSortMode.None);
            s.Wait(0.2f);
            return s;
        }

        public Vector3 Pos => fox.transform.position;
        public CrumblingTile Tile(int x, int z) => crumbler.GetTile(x, z);

        public void Step(Vector2 input, bool jump = false)
        {
            fox.Tick(input, jump, Dt);
            Physics.SyncTransforms();
            anim.Tick(Dt);
            collector.Tick();
            game.Tick(Dt);
            crumbler.Tick(Dt);
            Physics.SyncTransforms();
            foreach (var it in items) if (it.gameObject.activeSelf) it.Tick(Dt);
        }

        public void Wait(float seconds)
        {
            for (float t = 0; t < seconds; t += Dt) Step(Vector2.zero);
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

        public int FallenCount()
        {
            int n = 0;
            foreach (var t in crumbler.GetComponentsInChildren<CrumblingTile>(true)) if (!t.IsSolid) n++;
            return n;
        }
    }

    static void Check(string label, bool ok, string detail)
    {
        string line = $"[CrumbleSetup] 검증 {(ok ? "통과" : "실패")} | {label} | {detail}";
        if (ok) Debug.Log(line); else Debug.LogError(line);
    }

    public static void Verify()
    {
        // 1) 가만히 서 있거나 제자리 점프하면 발밑 타일은 그대로
        {
            var s = Sim.Start();
            s.Wait(2f);
            s.Step(Vector2.zero, true);
            s.Wait(1.2f);
            s.Step(Vector2.zero, true);
            s.Wait(1.5f);
            var start = s.Tile(0, 0);
            Check("제자리(대기·점프)에서는 안 무너짐", start.State == TileState.Solid && s.Pos.y > -0.1f,
                  $"시작 타일 {start.State}, 여우 {s.Pos:F2}");
        }

        // 2) 다른 타일로 옮기면 떠난 타일이 흔들리고, 정확히 1초 뒤 떨어진 다음 사라짐
        {
            var s = Sim.Start();
            var start = s.Tile(0, 0);
            float t = 0f;
            while (start.State == TileState.Solid && t < 3f) { s.Step(new Vector2(0, 1)); t += Dt; }
            float shakeMax = 0f, shakeY = 0f, shakeTime = 0f;
            while (start.State == TileState.Shaking && shakeTime < 3f)
            {
                s.Step(shakeTime < 0.4f ? new Vector2(0, 1) : Vector2.zero); // 경계에서 조금 더 들어가 멈춤
                shakeTime += Dt;
                if (start.State == TileState.Shaking)
                {
                    shakeMax = Mathf.Max(shakeMax, start.ShakeOffset.magnitude);
                    shakeY = Mathf.Max(shakeY, Mathf.Abs(start.ShakeOffset.y));
                }
            }
            bool noCollider = true;
            foreach (var c in start.GetComponentsInChildren<Collider>()) noCollider &= !c.enabled;
            Check("떠난 타일 흔들림", shakeMax > 0.03f && shakeY < 0.001f, $"최대 흔들림 {shakeMax:F3}m, 높이 변화 {shakeY:F4}m");
            Check("1초 뒤 떨어짐", Mathf.Abs(shakeTime - start.FallDelay) <= Dt * 1.5f && start.State == TileState.Falling && noCollider,
                  $"흔들린 시간 {shakeTime:F2}초, 상태 {start.State}, 발판 판정 {(noCollider ? "꺼짐" : "남음")}");
            s.Wait(1f);
            float y = start.transform.position.y;
            s.Wait(1.5f);
            Check("떨어진 뒤 사라짐 + 여우는 안전", y < -3f && !start.gameObject.activeSelf && s.game.State == GameState.Playing && s.Pos.y > -0.1f,
                  $"1초 낙하 후 높이 {y:F1}m, 활성 {start.gameObject.activeSelf}, 상태 {s.game.State}, 여우 {s.Pos:F2}");
        }

        // 3) 흔들리는 타일로 되돌아가 서 있으면 함께 떨어져 게임 오버
        {
            var s = Sim.Start();
            s.MoveTo(-3f, -1f);   // (0,1)로
            s.MoveTo(-3f, -3f);   // 흔들리는 시작 타일로 복귀
            float t = 0f;
            while (s.game.State == GameState.Playing && t < 4f) { s.Step(Vector2.zero); t += Dt; }
            Check("흔들리는 타일로 돌아가면 낙하", s.game.State == GameState.GameOver, $"상태 {s.game.State}, 여우 {s.Pos:F2}");
        }

        // 4) 무너지는 바닥 규칙으로 전체 코스 클리어 (점프 2번)
        {
            var s = Sim.Start();
            s.MoveTo(-3f, 3f);                             // (0,1)(0,2) 지나 (0,3) 체리
            s.MoveTo(-1f, 3f);                             // (1,3)
            s.MoveTo(-1f, -1f, p => p.z <= 2.35f);         // (1,2) 구멍 위 공중 체리 → (1,1) 착지
            s.MoveTo(3f, -1f);                             // (2,1) 지나 (3,1) 체리
            s.MoveTo(3f, -3f);                             // (3,0) 체리
            s.Wait(0.3f);
            var mid = s.Tile(3, 1);
            string midState = mid.State.ToString();
            CaptureMap("Captures/step8_crumble.png");      // (3,1)이 흔들리는 중, 지나온 길은 사라짐
            s.MoveTo(3f, 3f, p => p.z >= -2.35f);          // 무너지는 (3,1)을 점프로 넘어 (3,2) → (3,3) 체리
            s.Wait(1f);
            bool ok = s.game.State == GameState.Cleared && s.game.Collected == 5 && s.Pos.y > -0.1f;
            Check("무너지는 바닥으로 전체 코스 클리어", ok,
                  $"상태 {s.game.State}, 체리 {s.game.Collected}/{s.game.Total}, 캡처 때 (3,1) {midState}, 무너진 타일 {s.FallenCount()}개, 여우 {s.Pos:F2}");
        }

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

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
