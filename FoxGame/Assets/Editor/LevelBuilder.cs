using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 3단계: 4x4 타일맵(구멍 포함) 생성, 여우 이동 컴포넌트 추가, 카메라 배치
// 메뉴: Tools > Fox > 3단계 타일맵
// 배치: Unity.exe -batchmode -projectPath . -executeMethod LevelBuilder.Run -quit
public static class LevelBuilder
{
    const string ScenePath = "Assets/Scenes/FoxGame.unity";
    const string PrefabPath = "Assets/Prefabs/Fox.prefab";
    const string MatDir = "Assets/Materials";

    [MenuItem("Tools/Fox/3단계 타일맵")]
    public static void Run()
    {
        FoxSetup.ConfigureModelImport(); // 여우 크기(FoxSetup.FoxScale) 반영
        SetupFoxPrefab();
        BuildScene();
        VerifyWalk();
        Debug.Log("[LevelBuilder] 완료");
    }

    // ── 여우 프리팹에 이동 컴포넌트 추가 ─────────────────────────────
    static void SetupFoxPrefab()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        var cc = root.GetComponent<CharacterController>();
        if (cc == null) cc = root.AddComponent<CharacterController>();
        float s = FoxSetup.FoxScale; // 원본 여우: 폭 0.3, 높이 0.6, 길이 1.05m
        cc.center = new Vector3(0f, 0.3f * s, 0f);
        cc.height = 0.6f * s;
        cc.radius = 0.2f * s;
        cc.skinWidth = 0.02f;
        cc.stepOffset = 0.1f;
        cc.minMoveDistance = 0f; // 기본 0.001이면 fps가 아주 높을 때 한 프레임 이동량이 무시되어 여우가 멈춘다
        if (root.GetComponent<FoxController>() == null) root.AddComponent<FoxController>();
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    // ── 씬 구성 ─────────────────────────────────────────────────────
    static void BuildScene()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        DestroyIfExists("TempGround");
        DestroyIfExists("TileMap");

        var grassA = MakeMaterial("Tile_GrassA", new Color(0.49f, 0.78f, 0.33f));
        var grassB = MakeMaterial("Tile_GrassB", new Color(0.42f, 0.70f, 0.29f));
        var dirt = MakeMaterial("Tile_Dirt", new Color(0.55f, 0.36f, 0.21f));

        var map = new GameObject("TileMap").transform;
        const float size = LevelLayout.TileSize - 0.04f; // 타일 사이 얇은 틈
        for (int z = 0; z < LevelLayout.Depth; z++)
        {
            for (int x = 0; x < LevelLayout.Width; x++)
            {
                if (LevelLayout.IsHole(x, z)) continue;
                var tile = new GameObject($"Tile_{x}_{z}").transform;
                tile.SetParent(map);
                tile.position = LevelLayout.TileCenter(x, z);
                MakeBlock("Grass", tile, new Vector3(0f, -0.1f, 0f), new Vector3(size, 0.2f, size),
                          (x + z) % 2 == 0 ? grassA : grassB);
                MakeBlock("Dirt", tile, new Vector3(0f, -0.6f, 0f), new Vector3(size, 0.8f, size), dirt);
                GameObjectUtility.SetStaticEditorFlags(tile.gameObject, StaticEditorFlags.BatchingStatic);
            }
        }

        // 카메라: 맵 남쪽 위에서 내려다보는 고정 쿼터뷰 (W = +Z)
        var cam = Camera.main;
        cam.transform.position = new Vector3(0f, 8.5f, -8.5f);
        cam.transform.LookAt(new Vector3(0f, -0.6f, 0.2f));
        cam.fieldOfView = 45f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.56f, 0.78f, 0.95f);

        var light = Object.FindFirstObjectByType<Light>();
        light.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
        light.shadows = LightShadows.Soft;

        // 여우를 시작 타일에 배치하고 카메라 연결
        var fox = GameObject.Find("Fox");
        fox.transform.SetPositionAndRotation(LevelLayout.StartPosition() + Vector3.up * 0.05f, Quaternion.identity);
        var so = new SerializedObject(fox.GetComponent<FoxController>());
        so.FindProperty("cameraTransform").objectReferenceValue = cam.transform;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Capture(cam, "Captures/step3_map.png");
    }

    static void DestroyIfExists(string name)
    {
        var go = GameObject.Find(name);
        if (go != null) Object.DestroyImmediate(go);
    }

    static Material MakeMaterial(string name, Color color)
    {
        Directory.CreateDirectory(MatDir);
        string path = $"{MatDir}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Smoothness", 0.1f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void MakeBlock(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    // ── 걷기 검증: 입력을 시뮬레이션해 바닥/구멍/맵 밖 판정 확인 ─────────
    public static void VerifyWalk()
    {
        Simulate("위(W)로 1.8초: (0,3) 타일 도착, 바닥 위", new Vector2(0, 1), 1.8f, expectFall: false);
        Simulate("오른쪽(D)으로 1.5초: (2,0) 구멍에 낙하", new Vector2(1, 0), 1.5f, expectFall: true);
        Simulate("위(W)로 3초: 맵 밖으로 낙하", new Vector2(0, 1), 3f, expectFall: true);
        Simulate("오른쪽 위 대각선 1초: (1,1) 부근, 바닥 위", new Vector2(1, 1), 1f, expectFall: false);
    }

    static void Simulate(string label, Vector2 input, float seconds, bool expectFall)
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single); // 매번 저장된 씬에서 새로 시작
        Physics.SyncTransforms();
        var fox = Object.FindFirstObjectByType<FoxController>();
        const float dt = 0.02f;
        for (int i = 0; i < 10; i++) { fox.Tick(Vector2.zero, false, dt); Physics.SyncTransforms(); } // 착지
        for (float t = 0; t < seconds; t += dt) { fox.Tick(input, false, dt); Physics.SyncTransforms(); }
        for (int i = 0; i < 50; i++) { fox.Tick(Vector2.zero, false, dt); Physics.SyncTransforms(); } // 1초 대기

        Vector3 p = fox.transform.position;
        bool fell = p.y < -1f;
        string result = fell == expectFall ? "통과" : "실패";
        Debug.Log($"[LevelBuilder] 검증 {result} | {label} | 위치 ({p.x:F2}, {p.y:F2}, {p.z:F2}), 바라보는 방향 {fox.transform.forward:F2}");
    }

    static void Capture(Camera cam, string path)
    {
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
