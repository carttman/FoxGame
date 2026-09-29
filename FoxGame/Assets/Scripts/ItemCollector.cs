using UnityEngine;

// 여우 캡슐과 겹치는 아이템을 먹는다.
// 트리거 이벤트 대신 직접 겹침 검사를 해서 에디터 검증에서도 같은 결과가 나온다.
[RequireComponent(typeof(CharacterController))]
public class ItemCollector : MonoBehaviour
{
    readonly Collider[] hits = new Collider[8];
    CharacterController controller;

    void LateUpdate() => Tick();

    public void Tick()
    {
        if (controller == null) controller = GetComponent<CharacterController>();

        Vector3 center = transform.TransformPoint(controller.center);
        float half = Mathf.Max(0f, controller.height * 0.5f - controller.radius);
        Vector3 p1 = center + Vector3.up * half;
        Vector3 p2 = center - Vector3.up * half;

        int n = Physics.OverlapCapsuleNonAlloc(p1, p2, controller.radius, hits, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < n; i++)
        {
            var item = hits[i].GetComponentInParent<Item>();
            if (item != null) item.Collect();
        }
    }
}
