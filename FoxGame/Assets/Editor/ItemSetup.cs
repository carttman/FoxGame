using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// 5단계: 체리 아이템 5개 배치, 먹기 판정, 개수 HUD
// 메뉴: Tools > Fox > 5단계 아이템
// 배치: Unity.exe -batchmode -projectPath . -executeMethod ItemSetup.Run -quit
public static class ItemSetup
{
    const string ScenePath = "Assets/Scenes/FoxGame.unity";
    const string FoxPrefabPath = "Assets/Prefabs/Fox.prefab";
    const string CherryModelPath = "Assets/Models/cherry_voxel.fbx";
    const string CherryPrefabPath = "Assets/Prefabs/Cherry.prefab";
    const string IconPath = "Assets/UI/cherry_icon.png";

    public const float GroundItemHeight = 0.55f;  // 바닥 아이템 중심 높이
    public const float FloatingItemHeight = 1.8f; // 구멍 위 아이템: 점프 시 여우 몸(발 1.5m + 키 0.9m)이 지나는 높이
    const float Dt = 0.02f;

    [MenuItem("Tools/Fox/5단계 아이템")]
    public static void Run()
    {
        ConfigureImports();
        CreateCherryPrefab();
        SetupFoxPrefab();
        BuildScene();
        Verify();
        Debug.Log("[ItemSetup] 완료");
    }

    // ── 에셋 ────────────────────────────────────────────────────────
    static void ConfigureImports()
    {
        AssetDatabase.Refresh();

        var model = (ModelImporter)AssetImporter.GetAtPath(CherryModelPath);
        model.globalScale = 1f;
        model.useFileScale = true;
        model.importCameras = false;
        model.importLights = false;
        model.importAnimation = false;
        model.animationType = ModelImporterAnimationType.None;
        model.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        model.SaveAndReimport();

        const string matDir = "Assets/Models/Materials";
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(CherryModelPath))
        {
            if (asset is not Material) continue;
            string dst = $"{matDir}/{asset.name}.mat";
            if (!File.Exists(dst)) AssetDatabase.ExtractAsset(asset, dst);
        }
        AssetDatabase.WriteImportSettingsIfDirty(CherryModelPath);
        AssetDatabase.ImportAsset(CherryModelPath, ImportAssetOptions.ForceUpdate);

        var icon = (TextureImporter)AssetImporter.GetAtPath(IconPath);
        icon.textureType = TextureImporterType.Sprite;
        icon.spriteImportMode = SpriteImportMode.Single; // 기본값 Multiple이면 스프라이트가 비어 있다
        icon.alphaIsTransparency = true;
        icon.mipmapEnabled = false;
        icon.maxTextureSize = 256;
        icon.SaveAndReimport();
    }

    static void CreateCherryPrefab()
    {
        var root = new GameObject("Cherry");
        var col = root.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.45f;

        var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CherryModelPath));
        model.name = "Model";
        model.transform.SetParent(root.transform, false);
        model.transform.localScale = Vector3.one * 1.3f; // 전체 화면에서 잘 보이도록

        var item = root.AddComponent<Item>();
        var so = new SerializedObject(item);
        so.FindProperty("model").objectReferenceValue = model.transform;
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, CherryPrefabPath);
        Object.DestroyImmediate(root);
    }

    static void SetupFoxPrefab()
    {
        var root = PrefabUtility.LoadPrefabContents(FoxPrefabPath);
        if (root.GetComponent<ItemCollector>() == null) root.AddComponent<ItemCollector>();
        PrefabUtility.SaveAsPrefabAsset(root, FoxPrefabPath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    // ── 씬 ─────────────────────────────────────────────────────────
    static void BuildScene()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        // 아이템만 다시 배치한다. GameManager와 HUD는 이후 단계(결과 화면 등)가 덧붙이므로 있으면 유지
        var oldItems = GameObject.Find("Items");
        if (oldItems != null) Object.DestroyImmediate(oldItems);

        // 아이템: LevelLayout의 I(바닥 위), *(구멍 위 공중)
        var items = new GameObject("Items").transform;
        var cherry = AssetDatabase.LoadAssetAtPath<GameObject>(CherryPrefabPath);
        for (int z = 0; z < LevelLayout.Depth; z++)
        {
            for (int x = 0; x < LevelLayout.Width; x++)
            {
                char c = LevelLayout.Cell(x, z);
                if (c != 'I' && c != '*') continue;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(cherry, items);
                go.name = $"Cherry_{x}_{z}";
                float h = c == '*' ? FloatingItemHeight : GroundItemHeight;
                go.transform.position = LevelLayout.TileCenter(x, z) + Vector3.up * h;
                if (c == '*') MarkFloating(go.transform);
            }
        }

        if (Object.FindFirstObjectByType<GameManager>() == null) new GameObject("GameManager").AddComponent<GameManager>();
        if (GameObject.Find("HUD") == null) BuildHUD();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    // 쿼터뷰에서는 공중 체리가 뒤쪽 타일 위에 놓인 것처럼 보인다.
    // 그림자를 끄고, 구멍 아래에서 체리까지 빛기둥을 세워 구멍 위에 떠 있음을 보여 준다.
    static void MarkFloating(Transform cherry)
    {
        foreach (var r in cherry.GetComponentsInChildren<MeshRenderer>())
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        const float bottom = -1.5f, top = 1.45f; // 월드 높이
        var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beam.name = "LightBeam";
        Object.DestroyImmediate(beam.GetComponent<Collider>());
        beam.transform.SetParent(cherry, false);
        beam.transform.localPosition = new Vector3(0f, (bottom + top) * 0.5f - cherry.position.y, 0f);
        beam.transform.localScale = new Vector3(0.35f, (top - bottom) * 0.5f, 0.35f); // 실린더 기본 높이 2
        var mr = beam.GetComponent<MeshRenderer>();
        mr.sharedMaterial = MakeTransparentMaterial("Assets/Materials/LightBeam.mat", new Color(1f, 0.86f, 0.35f, 0.3f));
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    static Material MakeTransparentMaterial(string path, Color color)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        mat.SetColor("_BaseColor", color);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void BuildHUD()
    {
        var hudGo = new GameObject("HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        var canvas = hudGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera; // 카메라 캡처에도 찍히도록
        canvas.worldCamera = Camera.main;
        canvas.planeDistance = 1f;
        var scaler = hudGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // 반투명 둥근 패널
        var panel = NewUI("CounterPanel", hudGo.transform);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0f, 1f);
        panel.anchoredPosition = new Vector2(32f, -32f);
        panel.sizeDelta = new Vector2(280f, 96f);
        var bg = panel.gameObject.AddComponent<Image>();
        bg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.12f, 0.1f, 0.09f, 0.55f);

        var icon = NewUI("Icon", panel);
        icon.anchorMin = icon.anchorMax = icon.pivot = new Vector2(0f, 0.5f);
        icon.anchoredPosition = new Vector2(14f, 0f);
        icon.sizeDelta = new Vector2(80f, 80f);
        var img = icon.gameObject.AddComponent<Image>();
        img.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
        img.preserveAspect = true;

        var label = NewUI("Count", panel);
        label.anchorMin = new Vector2(0f, 0f);
        label.anchorMax = new Vector2(1f, 1f);
        label.offsetMin = new Vector2(104f, 0f);
        label.offsetMax = new Vector2(-12f, 0f);
        var text = label.gameObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 52;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleLeft;
        text.color = Color.white;
        text.text = "0 / 5";
        var shadow = label.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
        shadow.effectDistance = new Vector2(2f, -2f);

        var hud = hudGo.AddComponent<GameHUD>();
        var so = new SerializedObject(hud);
        so.FindProperty("countText").objectReferenceValue = text;
        so.FindProperty("counterPanel").objectReferenceValue = panel;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static RectTransform NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    // ── 검증 ────────────────────────────────────────────────────────
    class Sim
    {
        public FoxController fox;
        FoxAnimator anim;
        ItemCollector collector;
        public GameManager game;
        public GameHUD hud;
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
            s.game.Init();
            s.hud.Bind(s.game);
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
            foreach (var it in items) if (it.gameObject.activeSelf) it.Tick(Dt);
        }

        public void Wait(float seconds)
        {
            for (float t = 0; t < seconds; t += Dt) Step(Vector2.zero);
        }

        // 목표 지점(xz)까지 이동. jumpWhen이 참이 되는 순간 한 번 점프한다.
        public void MoveTo(float x, float z, System.Func<Vector3, bool> jumpWhen = null, float timeout = 8f)
        {
            bool jumped = false;
            for (float t = 0; t < timeout; t += Dt)
            {
                var d = new Vector2(x - Pos.x, z - Pos.z);
                if (d.magnitude < 0.08f && fox.IsGrounded) break;
                bool press = jumpWhen != null && !jumped && jumpWhen(Pos);
                if (press) jumped = true;
                Step(d.magnitude < 0.08f ? Vector2.zero : d.normalized, press);
                if (Pos.y < -2f) break;
            }
        }
    }

    static void Check(string label, bool ok, string detail)
    {
        string line = $"[ItemSetup] 검증 {(ok ? "통과" : "실패")} | {label} | {detail}";
        if (ok) Debug.Log(line); else Debug.LogError(line);
    }

    public static void Verify()
    {
        // 1) 배치: 5개, 공중 1개
        {
            var s = Sim.Start();
            var all = Object.FindObjectsByType<Item>(FindObjectsSortMode.None);
            int floating = 0;
            foreach (var it in all) if (it.transform.position.y > 1f) floating++;
            Check("아이템 배치", s.game.Total == 5 && floating == 1, $"전체 {s.game.Total}개, 공중 {floating}개, HUD \"{s.hud.Text}\"");
        }

        // 2) 전체 코스: 점프 2번 포함해 5개 모두 먹기
        {
            var s = Sim.Start();
            s.MoveTo(3f, -3f, p => p.x >= -0.35f);  // (2,0) 구멍 점프 → (3,0) 체리
            int afterFirst = s.game.Collected;
            s.MoveTo(3f, -1f);                      // (3,1) 체리
            s.MoveTo(3f, 3f);                       // (3,3) 체리
            s.MoveTo(3f, -1f);
            s.MoveTo(-3f, -1f);
            s.MoveTo(-3f, 3f);                      // (0,3) 체리
            s.MoveTo(-3f, 1f);
            int beforeJump = s.game.Collected;
            s.MoveTo(1f, 1f, p => p.x >= -2.35f);   // (1,2) 구멍 위 공중 체리 → (2,2) 착지
            s.Wait(0.5f);
            bool ok = afterFirst == 1 && beforeJump == 4 && s.game.Collected == 5 && s.Pos.y > -0.1f;
            Check("전체 코스 5개 수집", ok, $"첫 체리 후 {afterFirst}, 공중 체리 전 {beforeJump}, 최종 {s.game.Collected}/{s.game.Total}, HUD \"{s.hud.Text}\", 여우 {s.Pos:F2}");
            CaptureMap("Captures/step5_all_collected.png");
        }

        // 3) 공중 체리는 걸어서(점프 없이) 구멍 가장자리에 서도 못 먹음
        {
            var s = Sim.Start();
            s.MoveTo(-3f, 1f);
            s.MoveTo(-2.3f, 1f);
            s.Wait(1f);
            Check("공중 체리는 점프해야 먹음", s.game.Collected == 0 && s.Pos.y > -0.1f, $"가장자리 {s.Pos:F2}에서 먹은 개수 {s.game.Collected}");
        }

        // 4) 먹힌 체리는 연출 후 사라지고 두 번 세지 않음
        {
            var s = Sim.Start();
            s.MoveTo(-3f, 1f);
            s.MoveTo(-3f, 3f);    // (0,3) 체리
            s.Wait(0.2f);
            s.MoveTo(-3f, 2.5f);  // 다시 같은 자리 왕복
            s.MoveTo(-3f, 3f);
            s.Wait(0.5f);
            var cherry = GameObject.Find("Items").transform.Find("Cherry_0_3").gameObject;
            Check("중복 수집 없음 + 사라짐", s.game.Collected == 1 && !cherry.activeSelf, $"먹은 개수 {s.game.Collected}, 체리 활성 {cherry.activeSelf}");
        }

        // 시작 화면 캡처
        Sim.Start();
        CaptureMap("Captures/step5_start.png");
        CaptureCherryCloseUp("Captures/step5_cherry.png");
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    // ── 캡처 ────────────────────────────────────────────────────────
    static void CaptureMap(string path)
    {
        Canvas.ForceUpdateCanvases();
        Capture(Camera.main, path, 1200, 800);
    }

    static void CaptureCherryCloseUp(string path)
    {
        var target = GameObject.Find("Items").transform.Find("Cherry_3_1");
        var hud = GameObject.Find("HUD");
        hud.SetActive(false);
        var go = new GameObject("CaptureCam");
        var cam = go.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.56f, 0.78f, 0.95f);
        cam.fieldOfView = 30f;
        go.transform.position = target.position + new Vector3(-1.2f, 1.0f, -2.6f);
        go.transform.LookAt(target.position + Vector3.down * 0.15f);
        Capture(cam, path, 800, 600);
        Object.DestroyImmediate(go);
        hud.SetActive(true);
    }

    static void Capture(Camera cam, string path, int w, int h)
    {
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
