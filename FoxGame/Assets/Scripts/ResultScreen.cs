using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 게임 오버 / 클리어 결과 화면: 배경이 어두워지고 제목이 튀어나온다. [다시 하기] 버튼으로 재시작.
public class ResultScreen : MonoBehaviour
{
    [SerializeField] CanvasGroup group;
    [SerializeField] RectTransform panel;
    [SerializeField] Text title;
    [SerializeField] Text detail;
    [SerializeField] Button retryButton;
    [SerializeField] Text retryLabel;
    [SerializeField] Text hint;
    [SerializeField] Color gameOverColor = new Color(1f, 0.42f, 0.32f);
    [SerializeField] Color clearColor = new Color(1f, 0.82f, 0.25f);
    [SerializeField] float showDuration = 0.45f;

    GameManager game;
    float showT = 1f;
    bool fontReady;

    public bool IsVisible => group.gameObject.activeSelf;
    public string Title => title.text;
    public string Detail => detail.text;
    public Button RetryButton => retryButton;

    void Start() => Bind(GameManager.Current);

    void OnDestroy()
    {
        if (game != null) game.StateChanged -= OnStateChanged;
    }

    public void Bind(GameManager gm)
    {
        if (game != null) game.StateChanged -= OnStateChanged;
        game = gm;
        group.gameObject.SetActive(false);
        retryButton.onClick.RemoveListener(OnRetry);
        retryButton.onClick.AddListener(OnRetry);
        if (game != null) game.StateChanged += OnStateChanged;
    }

    void OnRetry()
    {
        if (game != null) game.Restart();
    }

    void OnStateChanged(GameState state)
    {
        if (state == GameState.Playing)
        {
            group.gameObject.SetActive(false);
            return;
        }

        EnsureKoreanFont();
        bool cleared = state == GameState.Cleared;
        title.text = cleared ? "CLEAR!" : "GAME OVER";
        title.color = cleared ? clearColor : gameOverColor;
        detail.text = cleared
            ? $"체리 {game.Collected}개를 모두 모았어요!\n기록 {game.ElapsedTime:F1}초"
            : $"떨어졌어요...\n체리 {game.Collected} / {game.Total}";
        retryLabel.text = cleared ? "한 번 더!" : "다시 하기";

        group.gameObject.SetActive(true);
        showT = 0f;
        Tick(0f);

        // 버튼을 미리 선택해 두어 Enter / Space / 게임패드 A로도 누를 수 있게 한다
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(retryButton.gameObject);
    }

    // 기본 폰트에는 한글이 없어서 OS 폰트를 쓴다
    void EnsureKoreanFont()
    {
        if (fontReady) return;
        fontReady = true;
        var font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }, 48);
        if (font == null) return;
        detail.font = font;
        retryLabel.font = font;
        hint.font = font;
    }

    void Update() => Tick(Time.unscaledDeltaTime);

    public void Tick(float dt)
    {
        if (showT >= 1f) return;
        showT = Mathf.Min(1f, showT + dt / showDuration);
        group.alpha = Mathf.Clamp01(showT * 2f);
        // 살짝 넘쳤다가 제자리로 (ease-out-back)
        float t = showT - 1f;
        float s = 1f + 2.2f * t * t * t + 1.2f * t * t;
        panel.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, s);
    }
}
