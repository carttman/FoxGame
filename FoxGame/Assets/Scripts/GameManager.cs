using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public enum GameState { Playing, GameOver, Cleared }

// 게임 진행 상태: 아이템 개수 집계, 낙하 = 게임 오버, 전부 수집 = 클리어, R 키 = 재시작
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // 플레이 중에는 Instance, 에디터 검증처럼 Awake가 불리지 않을 때는 씬에서 찾는다
    public static GameManager Current => Instance != null ? Instance : FindFirstObjectByType<GameManager>();

    [SerializeField] FoxController fox;
    [SerializeField] float fallLimitY = -5f; // 여우가 이 높이 아래로 떨어지면 게임 오버

    public int Total { get; private set; }
    public int Collected { get; private set; }
    public GameState State { get; private set; } = GameState.Playing;
    public float ElapsedTime { get; private set; }

    public event Action<int, int> ItemCountChanged; // (먹은 개수, 전체 개수)
    public event Action AllItemsCollected;
    public event Action<GameState> StateChanged;

    InputAction restartAction;
    bool restarting;

    void Awake()
    {
        Instance = this;

        restartAction = new InputAction("Restart", InputActionType.Button);
        restartAction.AddBinding("<Keyboard>/r");
        restartAction.AddBinding("<Gamepad>/start");

        Init();
    }

    void OnEnable() => restartAction?.Enable();
    void OnDisable() => restartAction?.Disable();

    void OnDestroy()
    {
        restartAction?.Dispose();
        if (Instance == this) Instance = null;
    }

    public void Init()
    {
        Total = FindObjectsByType<Item>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;
        Collected = 0;
        ElapsedTime = 0f;
        State = GameState.Playing;
        if (fox != null) fox.InputEnabled = true;
        ItemCountChanged?.Invoke(Collected, Total);
    }

    void Update()
    {
        if (restartAction.WasPressedThisFrame())
        {
            Restart();
            return;
        }
        Tick(Time.deltaTime);
    }

    public void Tick(float dt)
    {
        if (State != GameState.Playing) return;
        ElapsedTime += dt;
        if (fox != null && fox.transform.position.y < fallLimitY) EndGame(GameState.GameOver);
    }

    public void OnItemCollected(Item item)
    {
        if (State != GameState.Playing) return;
        Collected++;
        ItemCountChanged?.Invoke(Collected, Total);
        if (Collected >= Total)
        {
            AllItemsCollected?.Invoke();
            EndGame(GameState.Cleared);
        }
    }

    // 현재 씬을 다시 불러와 처음 상태로 되돌린다 (게임 중·결과 화면 어디서든)
    public void Restart()
    {
        if (restarting) return; // R 키와 버튼이 같은 프레임에 눌려도 한 번만
        restarting = true;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void EndGame(GameState result)
    {
        State = result;
        if (fox != null) fox.InputEnabled = false; // 결과 화면에서는 조작 불가
        StateChanged?.Invoke(result);
    }
}
