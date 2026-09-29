using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 2단계: 여우 FBX 임포트 설정, 프리팹, 게임 씬 생성
// 메뉴: Tools > Fox > 2단계 셋업
// 배치: Unity.exe -batchmode -projectPath . -executeMethod FoxSetup.Run -quit
public static class FoxSetup
{
    const string ModelPath = "Assets/Models/fox_voxel.fbx";
    const string PrefabPath = "Assets/Prefabs/Fox.prefab";
    const string ScenePath = "Assets/Scenes/FoxGame.unity";
    public const float FoxScale = 1.5f; // 2m 타일에 맞춘 여우 크기 (길이 약 1.6m)

    [MenuItem("Tools/Fox/2단계 셋업")]
    public static void Run()
    {
        ConfigureModelImport();
        if (File.Exists(PrefabPath) || File.Exists(ScenePath))
        {
            // 이후 단계에서 만든 프리팹 컴포넌트와 씬 구성을 덮어쓰지 않는다
            Debug.Log("[FoxSetup] 프리팹/씬이 이미 있어 임포트 설정만 갱신했습니다.");
            AssetDatabase.SaveAssets();
            return;
        }
        CreateFoxPrefab();
        CreateScene();
        AssetDatabase.SaveAssets();
        Debug.Log("[FoxSetup] 완료");
    }

    public static void ConfigureModelImport()
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
        importer.globalScale = FoxScale;
        importer.useFileScale = true;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importAnimation = false;
        importer.animationType = ModelImporterAnimationType.None; // 부위 Transform을 스크립트로 회전
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        importer.SaveAndReimport();

        // 머티리얼을 파일로 꺼내 두어 색을 에디터에서 바로 고칠 수 있게 한다
        const string matDir = "Assets/Models/Materials";
        Directory.CreateDirectory(matDir);
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(ModelPath))
        {
            if (asset is not Material) continue;
            string dst = $"{matDir}/{asset.name}.mat";
            if (File.Exists(dst)) continue;
            string err = AssetDatabase.ExtractAsset(asset, dst);
            if (!string.IsNullOrEmpty(err)) Debug.LogWarning($"[FoxSetup] 머티리얼 추출 실패 {asset.name}: {err}");
        }
        AssetDatabase.WriteImportSettingsIfDirty(ModelPath);
        AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceUpdate);
    }

    static void CreateFoxPrefab()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        var fox = (GameObject)PrefabUtility.InstantiatePrefab(model);
        fox.name = "Fox";
        PrefabUtility.SaveAsPrefabAsset(fox, PrefabPath);
        Object.DestroyImmediate(fox);
    }

    static void CreateScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // 임시 바닥 (3단계에서 4x4 타일맵으로 교체)
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "TempGround";
        ground.transform.localScale = new Vector3(2f, 0.2f, 2f);
        ground.transform.position = new Vector3(0f, -0.1f, 0f);

        var fox = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
        fox.transform.position = Vector3.zero;
        LogFoxInfo(fox);

        var cam = Camera.main;
        cam.transform.position = new Vector3(-1.8f, 1.6f, 2.2f);
        cam.transform.LookAt(new Vector3(0f, 0.3f, 0f));
        cam.fieldOfView = 40f;

        var light = Object.FindFirstObjectByType<Light>();
        light.transform.rotation = Quaternion.Euler(50f, 150f, 0f);

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

        CaptureCamera(cam, "Assets/../Captures/step2_fox.png");
    }

    static void LogFoxInfo(GameObject fox)
    {
        var b = new Bounds(fox.transform.position, Vector3.zero);
        foreach (var r in fox.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
        Debug.Log($"[FoxSetup] 루트 회전 {fox.transform.localEulerAngles}, 크기 {b.size}, 바닥 y {b.min.y:F3}");
        foreach (Transform c in fox.transform)
            Debug.Log($"[FoxSetup]  - {c.name} pos {c.localPosition} rot {c.localEulerAngles} scale {c.localScale}");
    }

    static void CaptureCamera(Camera cam, string path)
    {
        const int w = 800, h = 600;
        var rt = new RenderTexture(w, h, 24);
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
        Debug.Log($"[FoxSetup] 캡처 저장: {path}");
    }
}
