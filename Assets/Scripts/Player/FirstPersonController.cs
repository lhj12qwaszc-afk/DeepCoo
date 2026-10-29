using UnityEngine;
using UnityEngine.InputSystem;

// First-person movement: WASD to move, mouse to look, Shift to sprint,
// C / Left Ctrl to crouch, F to toggle the flashlight, Esc to free the cursor.
[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    // How loud the player is right now, for the monster's hearing later.
    public enum NoiseLevel { Silent, Quiet, Normal, Loud }

    [Header("References")]
    public Transform cameraRoot;
    public Light flashlight;

    [Header("Movement (m/s)")]
    public float walkSpeed = 2.5f;
    public float sprintSpeed = 5f;
    public float crouchSpeed = 1.2f;
    public float gravity = -20f;

    [Header("Look")]
    public float mouseSensitivity = 0.1f;
    public float maxLookAngle = 85f;

    [Header("Height (m)")]
    public float standHeight = 1.8f;
    public float crouchHeight = 1.0f;
    public float standEyeHeight = 1.6f;
    public float crouchEyeHeight = 0.85f;
    public float crouchTransitionSpeed = 8f;

    public NoiseLevel CurrentNoise { get; private set; }
    public bool IsCrouching { get; private set; }

    CharacterController controller;
    float pitch;
    float verticalVelocity;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        controller.height = standHeight;
        controller.center = new Vector3(0f, standHeight / 2f, 0f);
    }

    void OnEnable() => LockCursor(true);

    void Update()
    {
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        if (keyboard == null || mouse == null)
            return;

        if (keyboard.escapeKey.wasPressedThisFrame)
            LockCursor(false);
        else if (mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
            LockCursor(true);

        if (Cursor.lockState == CursorLockMode.Locked)
            Look(mouse.delta.ReadValue());

        if (keyboard.fKey.wasPressedThisFrame && flashlight != null)
            flashlight.enabled = !flashlight.enabled;

        bool wantsCrouch = keyboard.cKey.isPressed || keyboard.leftCtrlKey.isPressed;
        IsCrouching = wantsCrouch || (IsCrouching && !HasHeadroom());
        UpdateHeight();

        Move(keyboard);
    }

    void Look(Vector2 delta)
    {
        transform.Rotate(0f, delta.x * mouseSensitivity, 0f);
        pitch = Mathf.Clamp(pitch - delta.y * mouseSensitivity, -maxLookAngle, maxLookAngle);
        cameraRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    void Move(Keyboard keyboard)
    {
        var input = Vector2.zero;
        if (keyboard.wKey.isPressed) input.y += 1f;
        if (keyboard.sKey.isPressed) input.y -= 1f;
        if (keyboard.dKey.isPressed) input.x += 1f;
        if (keyboard.aKey.isPressed) input.x -= 1f;
        input = Vector2.ClampMagnitude(input, 1f);

        bool moving = input.sqrMagnitude > 0.01f;
        bool sprinting = moving && !IsCrouching && keyboard.leftShiftKey.isPressed && input.y > 0f;
        float speed = IsCrouching ? crouchSpeed : sprinting ? sprintSpeed : walkSpeed;

        if (!moving) CurrentNoise = NoiseLevel.Silent;
        else if (IsCrouching) CurrentNoise = NoiseLevel.Quiet;
        else if (sprinting) CurrentNoise = NoiseLevel.Loud;
        else CurrentNoise = NoiseLevel.Normal;

        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;
        verticalVelocity += gravity * Time.deltaTime;

        var velocity = (transform.right * input.x + transform.forward * input.y) * speed;
        velocity.y = verticalVelocity;
        controller.Move(velocity * Time.deltaTime);
    }

    void UpdateHeight()
    {
        float targetHeight = IsCrouching ? crouchHeight : standHeight;
        float targetEye = IsCrouching ? crouchEyeHeight : standEyeHeight;
        float t = crouchTransitionSpeed * Time.deltaTime;

        controller.height = Mathf.Lerp(controller.height, targetHeight, t);
        controller.center = new Vector3(0f, controller.height / 2f, 0f);

        var eye = cameraRoot.localPosition;
        eye.y = Mathf.Lerp(eye.y, targetEye, t);
        cameraRoot.localPosition = eye;
    }

    bool HasHeadroom()
    {
        float radius = controller.radius * 0.9f;
        var origin = transform.position + Vector3.up * (controller.height - radius);
        float distance = standHeight - controller.height + 0.05f;
        return !Physics.SphereCast(origin, radius, Vector3.up, out _, distance, ~0, QueryTriggerInteraction.Ignore);
    }

    static void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
