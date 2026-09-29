using UnityEngine;

// 여우 바로 아래 바닥에 사각 그림자를 붙인다. 점프할 때 착지 위치를 알려 준다.
public class BlobShadow : MonoBehaviour
{
    [SerializeField] Transform shadow;             // 반투명 Quad (자식)
    [SerializeField] Vector2 size = new Vector2(0.6f, 1.3f);
    [SerializeField] float maxDistance = 30f;
    [SerializeField] float shrinkHeight = 4f;      // 이 높이에서 그림자가 절반 크기

    void LateUpdate() => Tick();

    public void Tick()
    {
        if (shadow == null) return;

        // 캡슐 안쪽에서 시작하므로 여우 자신의 콜라이더는 맞지 않는다
        Vector3 origin = transform.position + Vector3.up * 0.3f;
        if (!Physics.Raycast(origin, Vector3.down, out var hit, maxDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            shadow.gameObject.SetActive(false); // 구멍 위나 맵 밖
            return;
        }

        shadow.gameObject.SetActive(true);
        float height = transform.position.y - hit.point.y;
        float scale = Mathf.Lerp(1f, 0.5f, Mathf.Clamp01(height / shrinkHeight));
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, hit.normal).normalized;

        shadow.SetPositionAndRotation(hit.point + hit.normal * 0.01f, Quaternion.LookRotation(-hit.normal, forward));
        shadow.localScale = new Vector3(size.x * scale, size.y * scale, 1f);
    }
}
