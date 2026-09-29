using UnityEngine;

// 수집 아이템(체리): 제자리에서 돌며 둥실거리고, 먹으면 튀어 오르며 사라진다.
// 판정은 루트의 트리거 콜라이더, 연출은 자식 모델(model)만 움직인다.
public class Item : MonoBehaviour
{
    [SerializeField] Transform model;
    [SerializeField] float spinSpeed = 90f;     // 초당 회전 각도
    [SerializeField] float bobHeight = 0.08f;
    [SerializeField] float bobSpeed = 2f;
    [SerializeField] float collectDuration = 0.3f;

    public bool IsCollected { get; private set; }

    float clock, collectT;
    Vector3 modelBasePos, modelBaseScale;
    bool initialized;

    void Init()
    {
        if (initialized) return;
        initialized = true;
        modelBasePos = model.localPosition;
        modelBaseScale = model.localScale;
        clock = transform.position.x + transform.position.z; // 아이템마다 둥실거리는 박자를 다르게
    }

    void Update() => Tick(Time.deltaTime);

    public void Tick(float dt)
    {
        Init();
        clock += dt;
        model.localRotation = Quaternion.Euler(0f, clock * spinSpeed, 0f);

        if (!IsCollected)
        {
            model.localPosition = modelBasePos + Vector3.up * Mathf.Sin(clock * bobSpeed) * bobHeight;
            return;
        }

        // 먹힘 연출: 살짝 커지며 솟아오른 뒤 작아지며 사라짐
        collectT += dt / collectDuration;
        float grow = collectT < 0.3f ? Mathf.Lerp(1f, 1.4f, collectT / 0.3f) : Mathf.Lerp(1.4f, 0f, (collectT - 0.3f) / 0.7f);
        model.localScale = modelBaseScale * grow;
        model.localPosition = modelBasePos + Vector3.up * collectT * 0.6f;
        model.localRotation = Quaternion.Euler(0f, clock * spinSpeed * 6f, 0f);
        if (collectT >= 1f) gameObject.SetActive(false);
    }

    public void Collect()
    {
        if (IsCollected) return;
        Init();
        IsCollected = true;
        GetComponent<Collider>().enabled = false;
        GameManager.Current?.OnItemCollected(this);
    }
}
