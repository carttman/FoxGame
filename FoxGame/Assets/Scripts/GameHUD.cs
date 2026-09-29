using UnityEngine;
using UnityEngine.UI;

// 화면 왼쪽 위 아이템 카운터 "3 / 5". 먹을 때마다 살짝 튀는 연출.
public class GameHUD : MonoBehaviour
{
    [SerializeField] Text countText;
    [SerializeField] RectTransform counterPanel;
    [SerializeField] float punchScale = 1.25f;
    [SerializeField] float punchDuration = 0.25f;

    GameManager game;
    float punchT = 1f;

    void Start() => Bind(GameManager.Current);

    void OnDestroy()
    {
        if (game != null) game.ItemCountChanged -= OnCountChanged;
    }

    public void Bind(GameManager gm)
    {
        if (game != null) game.ItemCountChanged -= OnCountChanged;
        game = gm;
        if (game == null) return;
        game.ItemCountChanged += OnCountChanged;
        SetText(game.Collected, game.Total);
    }

    void OnCountChanged(int collected, int total)
    {
        SetText(collected, total);
        if (collected > 0) punchT = 0f;
    }

    void SetText(int collected, int total) => countText.text = $"{collected} / {total}";

    public string Text => countText.text;

    void Update()
    {
        if (punchT >= 1f) return;
        punchT = Mathf.Min(1f, punchT + Time.deltaTime / punchDuration);
        float s = 1f + (punchScale - 1f) * Mathf.Sin(punchT * Mathf.PI);
        counterPanel.localScale = Vector3.one * s;
    }
}
