using UnityEngine;

// 여우 부위(몸통/머리/다리4/꼬리) Transform을 회전시켜 idle / walk / jump 포즈를 만든다.
// 리깅 없이 FoxController 상태(속도, 착지 여부, 수직 속도)만 보고 움직인다.
[RequireComponent(typeof(FoxController))]
public class FoxAnimator : MonoBehaviour
{
    [Header("Walk")]
    [SerializeField] float strideLength = 0.9f;   // 다리 한 주기에 나아가는 거리(m)
    [SerializeField] float legSwing = 35f;
    [SerializeField] float bodyBob = 0.03f;
    [SerializeField] float headNod = 5f;
    [SerializeField] float tailWag = 15f;

    [Header("Jump")]
    [SerializeField] float jumpBodyPitch = 12f;
    [SerializeField] float landingDip = 0.08f;

    [Header("Blend")]
    [SerializeField] float blendSpeed = 10f;

    class Part
    {
        public Transform t;
        public Vector3 basePos;
        public Quaternion baseRot;
    }

    FoxController fox;
    Part body, head, tail, legFL, legFR, legBL, legBR;

    float walkWeight, airWeight, landT, phase, clock;
    bool wasGrounded = true;

    void Awake() => Init();

    void Init()
    {
        if (fox != null) return;
        fox = GetComponent<FoxController>();
        body = Find("Body"); head = Find("Head"); tail = Find("Tail");
        legFL = Find("Leg_FL"); legFR = Find("Leg_FR"); legBL = Find("Leg_BL"); legBR = Find("Leg_BR");
    }

    Part Find(string name)
    {
        var t = transform.Find(name);
        if (t == null) Debug.LogError($"[FoxAnimator] 부위를 찾을 수 없음: {name}", this);
        return new Part { t = t, basePos = t.localPosition, baseRot = t.localRotation };
    }

    void LateUpdate() => Tick(Time.deltaTime);

    public void Tick(float dt)
    {
        Init();
        clock += dt;

        bool grounded = fox.IsGrounded;
        if (grounded && !wasGrounded)
            landT = Mathf.Clamp01(-fox.VerticalVelocity / 12f); // 세게 떨어질수록 크게 움츠림
        wasGrounded = grounded;
        landT = Mathf.MoveTowards(landT, 0f, dt * 4f);

        float k = 1f - Mathf.Exp(-blendSpeed * dt);
        float speed01 = Mathf.Clamp01(fox.HorizontalSpeed / fox.MoveSpeed);
        walkWeight = Mathf.Lerp(walkWeight, grounded ? speed01 : 0f, k);
        airWeight = Mathf.Lerp(airWeight, grounded ? 0f : 1f, 1f - Mathf.Exp(-blendSpeed * 2f * dt));

        // 이동 거리에 맞춰 다리 위상을 진행시켜 발이 미끄러져 보이지 않게 한다
        phase += fox.HorizontalSpeed * dt / strideLength * Mathf.PI * 2f;
        float s = Mathf.Sin(phase);

        // ── 다리 (X 회전 음수 = 앞으로) ──
        // 걷기: 대각선 다리가 함께 움직이는 트롯 (FL+BR / FR+BL)
        float walkA = -s * legSwing * walkWeight;
        float walkB = s * legSwing * walkWeight;

        // 점프: 올라갈 때는 앞다리 앞으로·뒷다리 뒤로 쭉 뻗고, 내려올 때는 착지 준비
        float rise = Mathf.Clamp01(fox.VerticalVelocity / fox.JumpSpeed * 0.5f + 0.5f);
        float airFront = Mathf.Lerp(-25f, -55f, rise);
        float airBack = Mathf.Lerp(-10f, 45f, rise);

        float front = airWeight;
        float fl = Mathf.Lerp(walkA, airFront, front);
        float fr = Mathf.Lerp(walkB, airFront, front);
        float bl = Mathf.Lerp(walkB, airBack, front);
        float br = Mathf.Lerp(walkA, airBack, front);

        // ── 몸 전체 기울기와 위아래 흔들림 ──
        float pitch = -jumpBodyPitch * (rise * 2f - 1f) * airWeight; // 올라갈 때 머리를 들고, 내려올 때 숙인다
        float bob = Mathf.Abs(Mathf.Cos(phase)) * bodyBob * walkWeight
                  + Mathf.Sin(clock * 2.5f) * 0.006f * (1f - walkWeight) * (1f - airWeight); // 숨쉬기
        float dip = -landingDip * Mathf.Sin(landT * Mathf.PI * 0.5f);

        var bodyRot = Quaternion.Euler(pitch, 0f, 0f);
        Vector3 pivot = body.basePos;
        var upper = new Vector3(0f, bob + dip, 0f); // 다리는 땅에 붙어 있도록 위쪽 부위만 내린다
        var legs = new Vector3(0f, bob * 0.5f, 0f);

        Apply(body, bodyRot, pivot, upper, Quaternion.identity);
        Apply(head, bodyRot, pivot, upper, Quaternion.Euler(Mathf.Sin(phase * 2f) * headNod * walkWeight - 8f * landT, 0f, 0f));

        float tailUp = 10f * walkWeight + 30f * airWeight;
        float tailYaw = Mathf.Sin(phase) * tailWag * walkWeight + Mathf.Sin(clock * 3f) * 10f * (1f - walkWeight) * (1f - airWeight);
        Apply(tail, bodyRot, pivot, upper, Quaternion.Euler(tailUp, tailYaw, 0f)); // X 회전 양수 = 꼬리 위로

        Apply(legFL, bodyRot, pivot, legs, Quaternion.Euler(fl, 0f, 0f));
        Apply(legFR, bodyRot, pivot, legs, Quaternion.Euler(fr, 0f, 0f));
        Apply(legBL, bodyRot, pivot, legs, Quaternion.Euler(bl, 0f, 0f));
        Apply(legBR, bodyRot, pivot, legs, Quaternion.Euler(br, 0f, 0f));
    }

    // 몸 전체 기울기(bodyRot)를 몸통 중심(pivot) 기준으로 모든 부위에 똑같이 적용해 부위가 떨어지지 않게 한다
    static void Apply(Part p, Quaternion bodyRot, Vector3 pivot, Vector3 offset, Quaternion local)
    {
        p.t.localPosition = pivot + bodyRot * (p.basePos - pivot) + offset;
        p.t.localRotation = bodyRot * p.baseRot * local;
    }

    public float GetLegAngle(string legName)
    {
        Init();
        var t = transform.Find(legName);
        float x = t.localEulerAngles.x;
        return x > 180f ? x - 360f : x;
    }
}
