using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class SimpleFPSControllerActions : MonoBehaviour
{
    // References
    [Header("Refs")]
    public Camera playerCam;

    // Input System
    [Header("Input (Input System)")]
    public InputActionAsset actionsAsset;     // optional; falls back to KB/M if null
    public string actionMapName = "Player";   // e.g., "Player" or "Gameplay"
    InputAction moveAction, lookAction, jumpAction, slideAction, diveAction;

    // Camera Height
    [Header("Camera Height")]
    public float eyeLerpSpeed = 12f; // how fast the camera eases to new height

    float camDefaultLocalY;  // remembered at Awake
    float camSlideLocalY;    // computed from heights
    float camDiveLocalY;     // computed from heights
    float camTargetLocalY;   // what we lerp toward

    // Look
    [Header("Look")]
    public float mouseSensitivity = 1.4f;
    public float minPitch = -80f;
    public float maxPitch = 80f;

    // Movement
    [Header("Movement")]
    public float moveSpeed = 7f;
    public float acceleration = 12f;
    [Range(0f, 1f)] public float airControl = 0.4f;
    public float gravity = -18f;
    public float jumpHeight = 1.2f;

    // Slide
    [Header("Slide")]
    public KeyCode slideFallbackKey = KeyCode.LeftControl;  // used if no asset
    public float slideStartBoost = 1.25f;
    public float slideFriction = 6f;
    public float slideDuration = 0.7f;
    public float slideHeight = 1.2f;

    // Dive
    [Header("Dive")]
    public KeyCode diveFallbackKey = KeyCode.LeftAlt;       // used if no asset
    public float diveForce = 12f;
    public float diveUpBias = 0.3f;
    public float diveDuration = 0.35f;
    public float diveHeight = 1.2f;

    // Debug HUD
    [Header("HUD")]
    public bool showSpeedometer = true;

    // Runtime state
    CharacterController cc;
    float yaw, pitch;
    Vector3 velocity, slideVel, diveVel;
    float defaultHeight;
    Vector3 defaultCenter;
    bool isSliding, isDiving;
    float slideTimer, diveTimer;

    // Cached inputs (updated each frame)
    Vector2 lookInput, moveInput;
    bool jumpPressed, slidePressed, divePressed;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        if (!playerCam) playerCam = GetComponentInChildren<Camera>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        defaultHeight = cc.height;
        defaultCenter = cc.center;

        camDefaultLocalY = playerCam ? playerCam.transform.localPosition.y : 0f;

        // Drop the camera by exactly how much the capsule shrinks for slide/dive:
        camSlideLocalY = camDefaultLocalY - (defaultHeight - slideHeight);
        camDiveLocalY = camDefaultLocalY - (defaultHeight - diveHeight);

        // Start with default target
        camTargetLocalY = camDefaultLocalY;
    }

    void OnEnable()
    {
        // Bind to Input Actions if provided
        if (actionsAsset == null) return;

        var map = !string.IsNullOrEmpty(actionMapName)
            ? actionsAsset.FindActionMap(actionMapName, false)
            : (actionsAsset.actionMaps.Count > 0 ? actionsAsset.actionMaps[0] : null);

        if (map == null)
        {
            Debug.LogError($"Input Action Map '{actionMapName}' not found.");
            return;
        }

        moveAction = map.FindAction("Move", true);
        lookAction = map.FindAction("Look", true);
        jumpAction = map.FindAction("Jump", true);
        slideAction = map.FindAction("Slide", true);
        diveAction = map.FindAction("Dive", true);

        moveAction.performed += c => moveInput = c.ReadValue<Vector2>();
        moveAction.canceled += _ => moveInput = Vector2.zero;

        lookAction.performed += c => lookInput = c.ReadValue<Vector2>();
        lookAction.canceled += _ => lookInput = Vector2.zero;

        jumpAction.performed += _ => jumpPressed = true;
        slideAction.performed += _ => slidePressed = true;
        diveAction.performed += _ => divePressed = true;

        map.Enable();
    }

    void OnDisable()
    {
        if (actionsAsset != null) actionsAsset.Disable();
    }

    void Update()
    {
        HandleLook();
        HandleMove();

        if (playerCam)
        {
            var lp = playerCam.transform.localPosition;
            lp.y = Mathf.Lerp(lp.y, camTargetLocalY, eyeLerpSpeed * Time.deltaTime);
            playerCam.transform.localPosition = lp;
        }

        // one-shot buttons
        jumpPressed = slidePressed = divePressed = false;
    }

    // Camera look (yaw on body, pitch on camera)
    void HandleLook()
    {
        // Use actions if present; else raw mouse delta
        Vector2 li = (actionsAsset != null && lookAction != null)
            ? lookInput
            : (Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero);

        float mx = li.x * mouseSensitivity * Time.deltaTime * 10f;
        float my = li.y * mouseSensitivity * Time.deltaTime * 10f;

        yaw += mx;
        pitch = Mathf.Clamp(pitch - my, minPitch, maxPitch);

        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        if (playerCam) playerCam.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    // Movement, slide, dive, jump, gravity
    void HandleMove()
    {
        bool grounded = cc.isGrounded;
        if (grounded && velocity.y < 0f) velocity.y = -2f;

        // Read move (actions or KB fallback)
        Vector2 mv = (actionsAsset != null && moveAction != null)
            ? moveInput
            : ReadKBMoveFallback();

        mv = Vector2.ClampMagnitude(mv, 1f);
        Vector3 wishDir = transform.TransformDirection(new Vector3(mv.x, 0f, mv.y));

        float control = grounded ? 1f : airControl;
        Vector3 targetHVel = Vector3.Lerp(
            Horizontal(velocity),
            wishDir * moveSpeed,
            control * acceleration * Time.deltaTime
        );

        // Slide start
        bool slideDown = slidePressed || (actionsAsset == null && Input.GetKeyDown(slideFallbackKey));
        if (!isSliding && !isDiving && grounded && slideDown && Horizontal(velocity).magnitude > 2f)
            StartSlide();

        // Dive start
        bool diveDown = divePressed || (actionsAsset == null && Input.GetKeyDown(diveFallbackKey));
        if (!isDiving && !isSliding && diveDown && Horizontal(targetHVel).magnitude > 0.1f)
            StartDive();

        // State horizontal velocity
        if (isDiving)
        {
            diveTimer -= Time.deltaTime;
            Vector3 hv = Vector3.Lerp(diveVel, Vector3.zero,
                (1f - Mathf.Clamp01(diveTimer / diveDuration)) * 0.15f);
            velocity.x = hv.x; velocity.z = hv.z;
            if (diveTimer <= 0f) EndDive();
        }
        else if (isSliding)
        {
            slideTimer -= Time.deltaTime;
            slideVel = Vector3.MoveTowards(slideVel, Vector3.zero, slideFriction * Time.deltaTime);
            velocity.x = slideVel.x; velocity.z = slideVel.z;
            if (slideTimer <= 0f || !grounded) EndSlide();
        }
        else
        {
            velocity.x = targetHVel.x; velocity.z = targetHVel.z;
        }

        // Jump (blocked during slide/dive)
        bool jumpDown = jumpPressed || (actionsAsset == null && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame);
        if (!isSliding && !isDiving && grounded && jumpDown)
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

        // Gravity + move
        velocity.y += gravity * Time.deltaTime;
        cc.Move(velocity * Time.deltaTime);
    }

    void StartSlide()
    {
        isSliding = true; slideTimer = slideDuration;
        SetCapsuleHeight(slideHeight);                       // <— use helper
        camTargetLocalY = camSlideLocalY;   // <<< lower camera

        Vector3 hv = Horizontal(velocity);
        if (hv.sqrMagnitude < 0.01f) hv = transform.forward * moveSpeed;
        slideVel = hv.normalized * Mathf.Max(hv.magnitude * slideStartBoost, moveSpeed * 1.1f);
        velocity.y = -2f;
    }

    void EndSlide()
    {
        isSliding = false;
        TryRestoreCapsule();
    }

    void StartDive()
    {
        isDiving = true; diveTimer = diveDuration;
        SetCapsuleHeight(diveHeight);                        // <— use helper

        diveVel = transform.forward * diveForce;
        velocity.y = Mathf.Max(velocity.y, Mathf.Abs(gravity) * Time.deltaTime * diveUpBias);
        camTargetLocalY = camDiveLocalY;    // <<< lower camera

        diveVel = transform.forward * diveForce;
        velocity.y = Mathf.Max(velocity.y, Mathf.Abs(gravity) * Time.deltaTime * diveUpBias);
    }

    void EndDive()
    {
        isDiving = false;
        velocity.x = diveVel.x * 0.6f;
        velocity.z = diveVel.z * 0.6f;
        TryRestoreCapsule();
    }

    // Try to stand up; retry if blocked
    void TryRestoreCapsule()
    {
        if (CanStandUp())
        {
            SetCapsuleHeight(defaultHeight);                 // <— use helper
            camTargetLocalY = camDefaultLocalY;  // <<< raise camera
        }
        else
        {
            Invoke(nameof(TryRestoreCapsule), 0.1f);
        }
    }

    bool CanStandUp()
    {
        float radius = cc.radius;

        // World-space centers of the STANDING capsule’s bottom/top spheres,
        // computed from the *default* height/center (what we want to return to).
        Vector3 p1 = transform.position + Vector3.up * (defaultCenter.y - defaultHeight * 0.5f + radius); // bottom sphere center
        Vector3 p2 = transform.position + Vector3.up * (defaultCenter.y + defaultHeight * 0.5f - radius); // top sphere center

        // Ignore the player's own layer so we don't self-hit (optional but useful)
        int mask = ~(1 << gameObject.layer);
        return !Physics.CheckCapsule(p1, p2, radius, mask, QueryTriggerInteraction.Ignore);
    }

    // Simple on-screen readout
    void DrawSpeedometer()
    {
        float hs = Horizontal(velocity).magnitude;
        GUI.Label(new Rect(10, 10, 260, 22), $"Speed: {hs:0.0} m/s");
        GUI.Label(new Rect(10, 30, 260, 22), $"State: {(isDiving ? "DIVE" : isSliding ? "SLIDE" : (cc.isGrounded ? "GROUNDED" : "AIR"))}");
    }

    // Helpers
    static Vector3 Horizontal(Vector3 v) => new Vector3(v.x, 0f, v.z);

    Vector2 ReadKBMoveFallback()
    {
        if (Keyboard.current == null) return Vector2.zero;
        float ix = 0f, iz = 0f;
        if (Keyboard.current.aKey.isPressed) ix -= 1f;
        if (Keyboard.current.dKey.isPressed) ix += 1f;
        if (Keyboard.current.sKey.isPressed) iz -= 1f;
        if (Keyboard.current.wKey.isPressed) iz += 1f;
        return new Vector2(ix, iz);
    }

    void SetCapsuleHeight(float newHeight)
    {
        // Keep the *bottom* of the capsule fixed so we don’t drift into floor/ceiling
        float bottomWorldY = transform.position.y + (cc.center.y - cc.height * 0.5f);
        float topWorldY = bottomWorldY + newHeight;

        cc.height = newHeight;

        // Recompute center.y in local space from new bottom/top
        float newCenterY = (bottomWorldY + topWorldY) * 0.5f - transform.position.y;
        cc.center = new Vector3(defaultCenter.x, newCenterY, defaultCenter.z);
    }
}



