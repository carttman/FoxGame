using UnityEngine;

public enum TileState { Solid, Shaking, Falling, Gone }

// 무너지는 바닥 타일: Crumble()이 불리면 흔들리다가 fallDelay초 뒤에 떨어진다.
// 매 프레임 처리는 TileCrumbler가 Tick으로 호출한다 (에디터 검증에서도 같은 경로).
public class CrumblingTile : MonoBehaviour
{
    [SerializeField] Vector2Int cell;          // 맵 좌표 (LevelLayout 기준)
    [SerializeField] float fallDelay = 1f;     // 흔들림 시작 → 떨어지기까지
    [SerializeField] float shakeStart = 0.03f; // 흔들림 폭 (m), 떨어질 때가 가까울수록 커진다
    [SerializeField] float shakeEnd = 0.08f;
    [SerializeField] float gravity = -20f;
    [SerializeField] float removeAfter = 2f;   // 떨어지기 시작하고 이 시간 뒤 비활성화

    Vector3 basePosition;
    Quaternion baseRotation;
    Collider[] colliders;
    float timer;
    float fallSpeed;
    Vector3 tumbleAxis;

    public Vector2Int Cell => cell;
    public TileState State { get; private set; } = TileState.Solid;
    public float FallDelay => fallDelay;
    public Vector3 ShakeOffset => State == TileState.Shaking ? transform.position - basePosition : Vector3.zero;
    public bool IsSolid => State == TileState.Solid || State == TileState.Shaking; // 밟을 수 있는 상태

    public void Init(Vector2Int c) => cell = c; // 셋업 스크립트용

    void Awake() => Cache();

    void Cache()
    {
        if (colliders != null) return;
        basePosition = transform.position;
        baseRotation = transform.rotation;
        colliders = GetComponentsInChildren<Collider>();
        // 타일마다 다른 방향으로 기울며 떨어지게 (좌표 기반이라 항상 같은 결과)
        tumbleAxis = new Vector3(Mathf.Sin(cell.x * 2.1f + cell.y * 1.3f), 0f, Mathf.Cos(cell.x * 1.7f - cell.y * 2.3f)).normalized;
    }

    // 여우가 이 타일을 떠났을 때 한 번 호출된다
    public void Crumble()
    {
        Cache();
        if (State != TileState.Solid) return;
        State = TileState.Shaking;
        timer = 0f;
    }

    public void Tick(float dt)
    {
        if (State == TileState.Solid || State == TileState.Gone) return;
        timer += dt;

        if (State == TileState.Shaking)
        {
            if (timer < fallDelay)
            {
                // 좌우로 덜덜 떨기: 서로 다른 주파수 두 개를 섞어 불규칙하게 (높이는 그대로라 위에 선 여우가 튀지 않음)
                float k = timer / fallDelay;
                float amp = Mathf.Lerp(shakeStart, shakeEnd, k * k);
                float ox = Mathf.Sin(timer * 71f) * 0.6f + Mathf.Sin(timer * 113f + 1.3f) * 0.4f;
                float oz = Mathf.Sin(timer * 83f + 2.1f) * 0.6f + Mathf.Sin(timer * 127f) * 0.4f;
                transform.position = basePosition + new Vector3(ox, 0f, oz) * amp;
                return;
            }
            // 떨어지기 시작: 발판 판정을 없애 위에 있던 여우도 함께 떨어진다
            transform.position = basePosition;
            foreach (var c in colliders) c.enabled = false;
            State = TileState.Falling;
            timer = 0f;
            fallSpeed = 0f;
        }

        // Falling
        fallSpeed += gravity * dt;
        transform.position += Vector3.up * (fallSpeed * dt);
        transform.rotation = Quaternion.AngleAxis(timer * timer * 40f, tumbleAxis) * baseRotation;
        if (timer >= removeAfter)
        {
            State = TileState.Gone;
            gameObject.SetActive(false);
        }
    }
}
