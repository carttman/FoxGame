using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 4단계: walk / jump 애니메이션(FoxAnimator)과 발밑 그림자(BlobShadow)를 여우 프리팹에 추가하고 검증
// 메뉴: Tools > Fox > 4단계 애니메이션
// 배치: Unity.exe -batchmode -projectPath . -executeMethod AnimationSetup.Run -quit
public static class AnimationSetup
{
    const string ScenePath = "Assets/Scenes/FoxGame.unity";
    const string PrefabPath = "Assets/Prefabs/Fox.prefab";
    const string ShadowMatPath = "Assets/Materials/BlobShadow.mat";
    const float Dt = 0.02f;

    [MenuItem("Tools/Fox/4단계 애니메이션")]
    public static void Run()
    {
        SetupPrefab();
        Verify();
        CapturePoses();
        Debug.Log("[AnimationSetup] 완료");
    }

    // ── 프리팹 구성 ─────────────────────────────────────────────────
    static void SetupPrefab()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        // 구멍 1칸(2m)을 여유 있게 넘도록 이동/점프 값을 맞춘다 (체공 0.8초 × 3.5m/s = 2.8m)
        var ctrl = new SerializedObject(root.GetComponent<FoxController>());
        ctrl.FindProperty("moveSpeed").floatValue = 3.5f;
        ctrl.FindProperty("gravity").floatValue = -20f;
        ctrl.FindProperty("jumpSpeed").floatValue = 8f;
        ctrl.ApplyModifiedPropertiesWithoutUndo();

        if (root.GetComponent<FoxAnimator>() == null) root.AddComponent<FoxAnimator>();

        var blob = root.GetComponent<BlobShadow>();
        if (blob == null) blob = root.AddComponent<BlobShadow>();

        var shadow = root.transform.Find("BlobShadow");
        if (shadow == null)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "BlobShadow";
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(root.transform, false);
            shadow = quad.transform;
        }
        var mr = shadow.GetComponent<MeshRenderer>();
        mr.sharedMaterial = MakeShadowMaterial();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        shadow.localPosition = new Vector3(0f, 0.01f, 0f);
        shadow.localRotation = Quaternion.Euler(90f, 0f, 0f);

        var so = new SerializedObject(blob);
        so.FindProperty("shadow").objectReferenceValue = shadow;
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    static Material MakeShadowMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(ShadowMatPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(mat, ShadowMatPath);
        }
        // URP 반투명 설정
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        mat.SetColor("_BaseColor", new Color(0f, 0f, 0f, 0.35f));
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        return mat;
    }

    // ── 검증 ────────────────────────────────────────────────────────
    class Sim
    {
        public FoxController fox;
        public FoxAnimator anim;
        public BlobShadow blob;
        public float maxY;

        public static Sim Start()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Physics.SyncTransforms();
            var s = new Sim { fox = Object.FindFirstObjectByType<FoxController>() };
            s.anim = s.fox.GetComponent<FoxAnimator>();
            s.blob = s.fox.GetComponent<BlobShadow>();
            s.Steps(Vector2.zero, 0.2f); // 착지
            return s;
        }

        public void Step(Vector2 input, bool jump = false)
        {
            fox.Tick(input, jump, Dt);
            Physics.SyncTransforms();
            anim.Tick(Dt);
            blob.Tick();
            maxY = Mathf.Max(maxY, fox.transform.position.y);
        }

        public void Steps(Vector2 input, float seconds)
        {
            for (float t = 0; t < seconds; t += Dt) Step(input);
        }

        // input 방향으로 가다가 cond가 참이 되면 점프, 착지하면 멈춤
        public bool JumpWhen(Vector2 input, System.Func<Vector3, bool> cond, float timeout = 5f)
        {
            bool jumped = false, airborne = false;
            for (float t = 0; t < timeout; t += Dt)
            {
                bool press = !jumped && cond(fox.transform.position);
                if (press) jumped = true;
                Step(input, press);
                if (jumped && !fox.IsGrounded) airborne = true;
                if (airborne && fox.IsGrounded) break;
                if (fox.transform.position.y < -2f) break;
            }
            Steps(Vector2.zero, 0.5f);
            return airborne;
        }

        public Vector3 Pos => fox.transform.position;
    }

    static void Check(string label, bool ok, string detail)
    {
        string line = $"[AnimationSetup] 검증 {(ok ? "통과" : "실패")} | {label} | {detail}";
        if (ok) Debug.Log(line); else Debug.LogError(line);
    }

    static string P(Vector3 p) => $"({p.x:F2}, {p.y:F2}, {p.z:F2})";

    public static void Verify()
    {
        // 1) 제자리 점프: 떠올랐다가 같은 자리에 착지
        {
            var s = Sim.Start();
            Vector3 start = s.Pos;
            s.Step(Vector2.zero, true);
            s.Steps(Vector2.zero, 1.5f);
            bool ok = s.maxY > 1.2f && Mathf.Abs(s.Pos.y - start.y) < 0.1f && s.fox.IsGrounded;
            Check("제자리 점프", ok, $"최고 높이 {s.maxY:F2}m, 착지 위치 {P(s.Pos)}");
        }

        // 2) (1,0) → (2,0) 구멍 → (3,0) 점프로 건너기
        {
            var s = Sim.Start();
            bool air = s.JumpWhen(new Vector2(1, 0), p => p.x >= -0.35f);
            bool ok = air && s.Pos.y > -0.1f && s.Pos.x > 2f && s.Pos.x < 4f;
            Check("구멍 (2,0) 점프로 건너기", ok, $"착지 위치 {P(s.Pos)}, 최고 높이 {s.maxY:F2}m");
        }

        // 3) (0,2) → (1,2) 구멍 → (2,2): 5단계 공중 아이템 높이 참고용으로 구멍 중앙 통과 높이 기록
        {
            var s = Sim.Start();
            s.Steps(new Vector2(0, 1), 4f / 3.5f); // (0,2) 타일 중앙 근처까지
            float yAtCenter = float.NaN;
            bool jumped = false, air = false;
            for (float t = 0; t < 5f; t += Dt)
            {
                bool press = !jumped && s.Pos.x >= -2.35f;
                if (press) jumped = true;
                s.Step(new Vector2(1, 0), press);
                if (float.IsNaN(yAtCenter) && s.Pos.x >= -1f) yAtCenter = s.Pos.y;
                if (jumped && !s.fox.IsGrounded) air = true;
                if (air && s.fox.IsGrounded) break;
            }
            s.Steps(Vector2.zero, 0.5f);
            bool ok = air && s.Pos.y > -0.1f && s.Pos.x > 0f && s.Pos.x < 2f;
            Check("구멍 (1,2) 점프로 건너기", ok, $"착지 위치 {P(s.Pos)}, 구멍 중앙 통과 높이 {yAtCenter:F2}m");
        }

        // 4) 점프 없이 걸어가면 구멍에 빠짐 (3단계 동작 유지)
        {
            var s = Sim.Start();
            s.Steps(new Vector2(1, 0), 1.5f);
            s.Steps(Vector2.zero, 1f);
            Check("점프 안 하면 구멍에 낙하", s.Pos.y < -1f, $"위치 {P(s.Pos)}");
        }

        // 5) walk 애니메이션: 걷는 중 대각선 다리가 반대로 움직이고, 멈추면 제자리로
        {
            var s = Sim.Start();
            float maxSwing = 0f, minProduct = 0f;
            for (float t = 0; t < 1f; t += Dt)
            {
                s.Step(new Vector2(0, 1));
                float fl = s.anim.GetLegAngle("Leg_FL"), fr = s.anim.GetLegAngle("Leg_FR");
                maxSwing = Mathf.Max(maxSwing, Mathf.Abs(fl));
                minProduct = Mathf.Min(minProduct, fl * fr);
            }
            bool trot = maxSwing > 20f && minProduct < 0f;
            s.Steps(Vector2.zero, 1f);
            float rest = Mathf.Abs(s.anim.GetLegAngle("Leg_FL"));
            Check("walk: 다리 교차 스윙", trot, $"최대 스윙 {maxSwing:F1}°, 앞다리 좌우 반대 방향 {(minProduct < 0 ? "예" : "아니오")}");
            Check("walk → idle 복귀", rest < 2f, $"멈춘 뒤 앞다리 각도 {rest:F1}°");
        }

        // 6) jump 애니메이션: 올라갈 때 앞다리 앞으로, 뒷다리 뒤로
        {
            var s = Sim.Start();
            s.Step(Vector2.zero, true);
            s.Steps(Vector2.zero, 0.15f);
            float fl = s.anim.GetLegAngle("Leg_FL"), bl = s.anim.GetLegAngle("Leg_BL");
            Check("jump: 상승 포즈", fl < -20f && bl > 10f, $"앞다리 {fl:F1}°, 뒷다리 {bl:F1}° (음수=앞)");
        }

        // 7) 발밑 그림자: 바닥 위에서는 보이고, 구멍 위에서는 숨김
        {
            var s = Sim.Start();
            var shadow = s.fox.transform.Find("BlobShadow").gameObject;
            bool onGround = shadow.activeSelf;
            s.Steps(new Vector2(1, 0), 1.2f); // (2,0) 구멍 위로
            bool overHole = shadow.activeSelf;
            Check("발밑 그림자", onGround && !overHole, $"바닥 위 {(onGround ? "보임" : "안 보임")}, 구멍 위 {(overHole ? "보임" : "숨김")}");
        }
    }

    // ── 포즈 캡처 ───────────────────────────────────────────────────
    static void CapturePoses()
    {
        // 걷기 중간 포즈
        var s = Sim.Start();
        s.Steps(new Vector2(1, 0), 0.5f);
        s.Steps(new Vector2(1, 0), 0.06f);
        CaptureCloseUp(s.fox.transform, "Captures/step4_walk.png");

        // 점프 상승 포즈 (구멍 (2,0)을 건너는 중)
        s = Sim.Start();
        bool jumped = false;
        for (float t = 0; t < 3f; t += Dt)
        {
            bool press = !jumped && s.Pos.x >= -0.35f;
            if (press) jumped = true;
            s.Step(new Vector2(1, 0), press);
            if (jumped && s.fox.VerticalVelocity < 3f) break;
        }
        CaptureCloseUp(s.fox.transform, "Captures/step4_jump.png");

        // 전체 맵에서 점프 장면
        Capture(Camera.main, "Captures/step4_map_jump.png", 1200, 800);
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single); // 저장하지 않고 원상태로
    }

    static void CaptureCloseUp(Transform fox, string path)
    {
        var go = new GameObject("CaptureCam");
        var cam = go.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.56f, 0.78f, 0.95f);
        cam.fieldOfView = 30f;
        // 여우의 옆면(왼쪽)에서 약간 앞쪽 위
        Vector3 target = fox.position + Vector3.up * 0.45f;
        go.transform.position = target - fox.right * 3.2f + fox.forward * 1.2f + Vector3.up * 0.9f;
        go.transform.LookAt(target);
        Capture(cam, path, 800, 600);
        Object.DestroyImmediate(go);
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
