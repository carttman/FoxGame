using UnityEngine;
using UnityEngine.InputSystem;

// 여우 이동: 카메라 기준 자유 이동 + 중력 + 점프
[RequireComponent(typeof(CharacterController))]
public class FoxController : MonoBehaviour
{
    [SerializeField] float moveSpeed = 3.5f;
    [SerializeField] float turnSpeed = 720f;   // 초당 회전 각도
    [SerializeField] float gravity = -20f;
    [SerializeField] float jumpSpeed = 8f;     // 체공 약 0.8초, 수평 약 2.8m (구멍 1칸 = 2m)
    [SerializeField] float coyoteTime = 0.12f; // 발판을 벗어난 직후에도 점프 허용
    [SerializeField] float jumpBufferTime = 0.12f; // 착지 직전 입력을 기억
    [SerializeField] Transform cameraTransform;

    CharacterController controller;
    InputAction moveAction;
    InputAction jumpAction;
    float verticalVelocity;
    float lastGroundedTime = float.NegativeInfinity;
    float lastJumpPressedTime = float.NegativeInfinity;
    float clock;
    Vector3 carriedDir;

    public Vector3 Velocity { get; private set; }
    public float HorizontalSpeed { get; private set; }
    public float VerticalVelocity => verticalVelocity;
    public bool IsGrounded { get; private set; } = true;
    public float MoveSpeed => moveSpeed;
    public float JumpSpeed => jumpSpeed;
    public bool InputEnabled { get; set; } = true; // 게임이 끝나면 GameManager가 끈다 (중력은 계속 적용)

    CharacterController Controller
    {
        get
        {
            if (controller == null) controller = GetComponent<CharacterController>();
            return controller;
        }
    }

    void Awake()
    {
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;

        moveAction = new InputAction("Move", InputActionType.Value);
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
        moveAction.AddBinding("<Gamepad>/leftStick");

        jumpAction = new InputAction("Jump", InputActionType.Button);
        jumpAction.AddBinding("<Keyboard>/space");
        jumpAction.AddBinding("<Gamepad>/buttonSouth");
    }

    void OnEnable()
    {
        moveAction?.Enable();
        jumpAction?.Enable();
    }

    void OnDisable()
    {
        moveAction?.Disable();
        jumpAction?.Disable();
    }

    void OnDestroy()
    {
        moveAction?.Dispose();
        jumpAction?.Dispose();
    }

    void Update() => Tick(moveAction.ReadValue<Vector2>(), jumpAction.WasPressedThisFrame(), Time.deltaTime);

    // 입력 한 프레임 처리 (에디터 검증에서도 직접 호출)
    public void Tick(Vector2 input, bool jumpPressed, float dt)
    {
        clock += dt;
        Vector3 dir;
        if (InputEnabled)
        {
            dir = ToWorldDirection(input);
            carriedDir = dir;
        }
        else
        {
            // 조작이 막혀도 공중에서는 하던 이동을 이어 가 착지한다 (점프 중 클리어 시 구멍에 빠지지 않게)
            jumpPressed = false;
            if (Controller.isGrounded) carriedDir = Vector3.zero;
            dir = carriedDir;
        }
        if (jumpPressed) lastJumpPressedTime = clock;

        if (dir.sqrMagnitude > 0.0001f)
        {
            var target = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * dt);
        }

        if (Controller.isGrounded)
        {
            lastGroundedTime = clock;
            if (verticalVelocity < 0f) verticalVelocity = -2f;
        }

        bool canJump = clock - lastGroundedTime <= coyoteTime && verticalVelocity <= 0f;
        if (canJump && clock - lastJumpPressedTime <= jumpBufferTime)
        {
            verticalVelocity = jumpSpeed;
            lastGroundedTime = float.NegativeInfinity;
            lastJumpPressedTime = float.NegativeInfinity;
        }

        verticalVelocity += gravity * dt;

        Vector3 motion = dir * moveSpeed;
        motion.y = verticalVelocity;
        Vector3 before = transform.position;
        Controller.Move(motion * dt);

        Velocity = dt > 0f ? (transform.position - before) / dt : Vector3.zero;
        HorizontalSpeed = new Vector3(Velocity.x, 0f, Velocity.z).magnitude;
        IsGrounded = Controller.isGrounded; // 착지한 프레임에는 VerticalVelocity에 낙하 속도가 남아 있다
    }

    Vector3 ToWorldDirection(Vector2 input)
    {
        input = Vector2.ClampMagnitude(input, 1f);
        Vector3 forward = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
        forward.y = 0f;
        forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        return forward * input.y + right * input.x;
    }
}
