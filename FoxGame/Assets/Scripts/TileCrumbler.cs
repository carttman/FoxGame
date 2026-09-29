using System.Collections.Generic;
using UnityEngine;

// 여우가 서 있는 타일을 추적하다가, 다른 타일에 올라서면 방금 떠난 타일을 무너뜨린다.
// 제자리 점프·가만히 서 있기는 타일을 떠난 것이 아니다. TileMap 오브젝트에 붙는다.
public class TileCrumbler : MonoBehaviour
{
    [SerializeField] FoxController fox;

    readonly Dictionary<Vector2Int, CrumblingTile> tiles = new Dictionary<Vector2Int, CrumblingTile>();
    CrumblingTile current;

    public CrumblingTile Current => current;

    void Awake() => Init();

    // 에디터 검증에서는 Awake가 불리지 않으므로 직접 호출한다
    public void Init()
    {
        if (fox == null) fox = FindFirstObjectByType<FoxController>();
        tiles.Clear();
        foreach (var t in GetComponentsInChildren<CrumblingTile>(true)) tiles[t.Cell] = t;
        current = null;
    }

    public CrumblingTile GetTile(int x, int z) => tiles.TryGetValue(new Vector2Int(x, z), out var t) ? t : null;

    void Update() => Tick(Time.deltaTime);

    public void Tick(float dt)
    {
        var game = GameManager.Current;
        bool playing = game == null || game.State == GameState.Playing;

        if (playing && fox != null && fox.IsGrounded)
        {
            var under = TileAt(fox.transform.position);
            if (under != null && under != current)
            {
                if (current != null) current.Crumble();
                current = under;
            }
        }

        foreach (var t in tiles.Values) t.Tick(dt);
    }

    // 여우 중심 아래의 밟을 수 있는 타일 (구멍·맵 밖·이미 떨어지는 타일이면 null)
    CrumblingTile TileAt(Vector3 p)
    {
        Vector3 origin = LevelLayout.TileCenter(0, 0);
        int x = Mathf.RoundToInt((p.x - origin.x) / LevelLayout.TileSize);
        int z = Mathf.RoundToInt((p.z - origin.z) / LevelLayout.TileSize);
        var t = GetTile(x, z);
        return t != null && t.IsSolid ? t : null;
    }
}
