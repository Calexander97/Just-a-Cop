using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class RigidbodyFPSController : MonoBehaviour
{

    [Header("Refs")]
    public Camera playerCam;
    public InputActionAsset actionsAsset;
    public string actionMapName = "Player";
    string _lastAction;

    // DEBUG additions 
    Vector2 _lastMoveDir;
    const float _moveLogThreshold = 0.35f;
    bool _callbacksBound;
    

    // Input Action   
    InputAction moveAction, lookAction, jumpAction, sprintAction, crouchAction;

    [Header("Look")]
    public float mouseSensitivity = 1.4f;
    public float minPitch = -80f;
    public float maxPitch = 80f;

    [Header("Movement")]
    public float walkSpeed = 7f;
    public float sprintMultiplier = 1.35f;
    public float acceleration = 30f;    // how fast player approaches desired velocity
    public float maxGroundSlope = 50f;  // degrees
    public float jumpHeight = 1.2f;     // desired jump apex height

    [Header("Heights & Camera")]
    public float standingHeight = 1.8f;
    public float crouchHeight = 1.2f;
    public float slideHeight = 1.2f;
    public float diveHeight = 1.2f;
    public float eyeLerpSpeed = 12f;

    [Header("Slide")]
    public float minSlideSpeed = 2.0f;
    public float slideImpulse = 8f;
    public float slideDuration = 0.7f;
    public PhysicsMaterial slideMaterial;   // assign PhysMat_Slide
    public PhysicsMaterial defaultMaterial; // assign PhysMat_Default

    [Header("Dive")]
    public float minDiveSpeed = 2.0f;
    public float diveForwardImpulse = 12f;
    public float diveUpImpulse = 4.8f;
    public float diveDuration = 0.35f;
    public bool diveEndsOnlyOnLanding = true;

    [Header("Grounding")]
    public Transform groundProbe;       // optional; else uses transform.position
    public float groundCheckRadius = 0.2f;
    public float groundCheckDistance = 0.25f;
    public LayerMask groundMask = ~0;

    // Runtime
    Rigidbody rb;
    CapsuleCollider capsule;

    float yaw, pitch;
    Vector3 desiredVelXZ;     // target horizontal velocity
    bool grounded, wasGrounded;
    Vector3 groundNormal = Vector3.up;

    // Heights / camera
    float camDefaultLocalY, camCrouchLocalY, camSlideLocalY, camDiveLocalY, camTargetLocalY;

    // States
    bool isSliding, isDiving, isCrouching;
    float slideTimer, diveTimer;
    bool raiseCameraNextFrame;

    // Input edges
    bool prevCrouchHeld, prevJumpHeld;
    bool crouchHeldThisFrame;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        if (!playerCam) playerCam = GetComponentInChildren<Camera>();

        // Freeze tilt
        rb.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        // Camera height presets
        camDefaultLocalY = playerCam ? playerCam.transform.localPosition.y : 0f;
        camCrouchLocalY = camDefaultLocalY - (standingHeight - crouchHeight);
        camSlideLocalY = camDefaultLocalY - (standingHeight - slideHeight);
        camDiveLocalY = camDefaultLocalY - (standingHeight - diveHeight);
        camTargetLocalY = camDefaultLocalY;

        // Ensure collider height starts at standing
        SetCapsuleHeight(standingHeight);
    }

    void OnEnable()
    {
        if (actionsAsset == null) return;
        var map = actionsAsset.FindActionMap(actionMapName, false);
        if (map == null) { Debug.LogError($"ActionMap '{actionMapName}' not found"); return; }

        moveAction = map.FindAction("Move", true);
        lookAction = map.FindAction("Look", true);
        jumpAction = map.FindAction("Jump", true);
        sprintAction = map.FindAction("Sprint", true);
        crouchAction = map.FindAction("Crouch", true);

        map.Enable();

        //  DEBUG subscriptions (input-based logging) 
        if (!_callbacksBound)
        {
            if (sprintAction != null) { sprintAction.started += OnSprintStarted; sprintAction.canceled += OnSprintCanceled; }
            if (crouchAction != null) { crouchAction.started += OnCrouchStarted; crouchAction.canceled += OnCrouchCanceled; }
            if (jumpAction != null) { jumpAction.started += OnJumpStarted; }
            if (moveAction != null) { moveAction.performed += OnMovePerformed; }
            _callbacksBound = true;
        }
        // 

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void OnDisable()
    {
        if (actionsAsset != null) actionsAsset.Disable();

        //  DEBUG unsubscriptions 
        if (_callbacksBound)
        {
            if (sprintAction != null) { sprintAction.started -= OnSprintStarted; sprintAction.canceled -= OnSprintCanceled; }
            if (crouchAction != null) { crouchAction.started -= OnCrouchStarted; crouchAction.canceled -= OnCrouchCanceled; }
            if (jumpAction != null) { jumpAction.started -= OnJumpStarted; }
            if (moveAction != null) { moveAction.performed -= OnMovePerformed; }
            _callbacksBound = false;
        }
       
    }

    // Update is called once per frame
    void Update()
    {
        HandleLook();
        UpdateGrounding();

        // Camera easing
        if (playerCam)
        {
            var lp = playerCam.transform.localPosition;
            lp.y = Mathf.Lerp(lp.y, camTargetLocalY, eyeLerpSpeed * Time.deltaTime);
            playerCam.transform.localPosition = lp;
        }

        // Dive landing one-frame prone, then pop up next frame
        if (grounded && !wasGrounded && isDiving)
        {
            camTargetLocalY = camDiveLocalY;
            raiseCameraNextFrame = true;
        }
        else if (raiseCameraNextFrame && grounded && !isSliding && !isCrouching && !isDiving)
        {
            camTargetLocalY = camDefaultLocalY;
            raiseCameraNextFrame = false;
        }

        wasGrounded = grounded;
    }

    void FixedUpdate()
    {
        HandleMovePhysics();
    }

    // Look
    void HandleLook()
    {
        Vector2 look = lookAction != null ? lookAction.ReadValue<Vector2>() : Vector2.zero;
        yaw += look.x * mouseSensitivity * Time.deltaTime * 10f;
        pitch = Mathf.Clamp(pitch - look.y * mouseSensitivity * Time.deltaTime * 10f, minPitch, maxPitch);
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        if (playerCam) playerCam.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    // Ground Check
    void UpdateGrounding()
    {
        // Build a capsule matching the current CapsuleCollider in world space
        float r = capsule.radius - 0.01f; // tiny shrink to avoid self-hits
        Vector3 c = transform.position + capsule.center;
        Vector3 top = c + Vector3.up * (capsule.height * 0.5f - capsule.radius);
        Vector3 bottom = c - Vector3.up * (capsule.height * 0.5f - capsule.radius);

        // Ignore ONLY the player's own layer so any solid surface can be "ground"
        int notPlayerMask = ~(1 << gameObject.layer);

        grounded = Physics.CapsuleCast(
            top, bottom, r, Vector3.down,
            out RaycastHit hit,
            groundCheckDistance + 0.02f,
            notPlayerMask,
            QueryTriggerInteraction.Ignore
        );

        groundNormal = grounded ? hit.normal : Vector3.up;

        // Optional: reject very steep "ground"
        if (grounded)
        {
            float slopeAngle = Vector3.Angle(groundNormal, Vector3.up);
            if (slopeAngle > maxGroundSlope) grounded = false;
        }
    }


    // Movement (Physics)
    void HandleMovePhysics()
    {
        // Inputs
        Vector2 mv = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
        mv = Vector2.ClampMagnitude(mv, 1f);
        Vector3 wishDir = transform.TransformDirection(new Vector3(mv.x, 0f, mv.y));
        float topSpeed = walkSpeed * (IsHeld(sprintAction) ? sprintMultiplier : 1f);

        // Edge detections
        crouchHeldThisFrame = IsHeld(crouchAction);
        bool crouchDownEdge = EdgeDown(crouchAction, ref prevCrouchHeld);
        bool jumpDownEdge = EdgeDown(jumpAction, ref prevJumpHeld);

        // (Removed per-frame movement label spam)

        //Crouch (Hold)
        if (!isSliding && !isDiving)
        {
            if (crouchHeldThisFrame && !isCrouching) StartCrouch();
            else if (!crouchHeldThisFrame && isCrouching) TryEndCrouch();
        }

        // Derived actions: Slide (sprint + crouch tap while grounded and moving), Dive (sprint + jump tap)
        float horizSpeed = Horizontal(rb.linearVelocity).magnitude;
        bool wantSlide = grounded && IsHeld(sprintAction) && crouchDownEdge && horizSpeed > minSlideSpeed && !isSliding && !isDiving;
        bool wantDive = IsHeld(sprintAction) && jumpDownEdge && (Horizontal(rb.linearVelocity + wishDir * topSpeed).magnitude > minDiveSpeed) && !isDiving && !isSliding;

        if (wantSlide) StartSlide();
        if (wantDive) StartDive();

        // Per-state horizontal control
        Vector3 vel = rb.linearVelocity;

        if (isDiving)
        {
            if (diveTimer > 0f) diveTimer -= Time.fixedDeltaTime;

            // Keep horizontal component; let physics handle collisions/bounces
            // Clamp horizontal speed after the commit window ends
            if (diveTimer <= 0f)
            {
                Vector3 hv = Horizontal(vel);
                float clamp = Mathf.Max(hv.magnitude - 0.5f * Time.fixedDeltaTime, 0f);
                hv = hv.normalized * clamp;
                vel.x = hv.x; vel.z = hv.z;
            }

            // End condition
            if (diveEndsOnlyOnLanding)
            {
                if (diveTimer <= 0f && grounded) EndDive();
            }
            else if (diveTimer <= 0f) EndDive();
        }
        else if (isSliding)
        {
            slideTimer -= Time.fixedDeltaTime;
            // Add a bit of drag during slide for decay
            vel.x *= (1f - 0.02f);
            vel.z *= (1f - 0.02f);
            if (slideTimer <= 0f) EndSlide();
        }
        else
        {
            // Normal locomotion: accelerate toward desired horizontal velocity
            Vector3 currentXZ = Horizontal(vel);
            Vector3 desiredXZ = wishDir * topSpeed;

            float control = grounded ? 1f : 0.15f; // minimal air control: 15%
            Vector3 accelVec = (desiredXZ - currentXZ) * (acceleration * control);
            rb.AddForce(new Vector3(accelVec.x, 0f, accelVec.z), ForceMode.Acceleration);

            // Jump (only on ground and not sliding/diving)
            if (grounded && jumpDownEdge)
            {
                float upImpulse = Mathf.Sqrt(2f * Mathf.Abs(Physics.gravity.y) * jumpHeight);
                vel.y = upImpulse;
                Debug.Log("Action: JUMP");
            }
        }

        rb.linearVelocity = new Vector3(vel.x, vel.y, vel.z);
    }

    // Slide
    void StartSlide()
    {
        isSliding = true; slideTimer = slideDuration;

        SetCapsuleHeight(slideHeight);
        camTargetLocalY = camSlideLocalY;

        // lower friction during slide
        if (slideMaterial && capsule) capsule.material = slideMaterial;

        // Add a forward impulse for punch
        Vector3 forward = transform.forward;
        rb.AddForce(forward * slideImpulse, ForceMode.VelocityChange);
        LogActionIfChanged("SLIDE");
    }

    void EndSlide()
    {
        isSliding = false;

        // restore friction
        if (defaultMaterial && capsule) capsule.material = defaultMaterial;

        // Stay crouched if the key is still held; else stand up (if headroom)
        if (crouchHeldThisFrame) StartCrouch();
        else TryRestoreStanding();
        LogActionIfChanged("SLIDE END");
    }

    // Dive

    void StartDive()
    {
        isDiving = true; diveTimer = diveDuration;

        SetCapsuleHeight(diveHeight);
        camTargetLocalY = camDiveLocalY;

        // Heading from current momentum (fallback to forward)
        Vector3 hv = Horizontal(rb.linearVelocity);
        Vector3 dir = hv.sqrMagnitude > 0.01f ? hv.normalized : transform.forward;

        // Apply forward + up impulses
        rb.AddForce(dir * diveForwardImpulse, ForceMode.VelocityChange);
        rb.AddForce(Vector3.up * diveUpImpulse, ForceMode.VelocityChange);
        LogActionIfChanged("DIVE");
    }

    void EndDive()
    {
        isDiving = false;
        // After prone flash, stand unless crouch is held
        if (crouchHeldThisFrame) StartCrouch();
        else { TryRestoreStanding(); raiseCameraNextFrame = true; }
        LogActionIfChanged("DIVE END");
    }

    // Crouch
    void StartCrouch()
    {
        isCrouching = true;
        SetCapsuleHeight(crouchHeight);
        camTargetLocalY = camCrouchLocalY;
        LogActionIfChanged("CROUCH");

    }

    void TryEndCrouch()
    {
        if (CanStandUp())
        {
            isCrouching = false;
            SetCapsuleHeight(standingHeight);
            camTargetLocalY = camDefaultLocalY;
            LogActionIfChanged("STAND");
        }
        // else remain crouched until there is headroom
    }

    void TryRestoreStanding()
    {
        if (!isCrouching && CanStandUp())
        {
            SetCapsuleHeight(standingHeight);
            camTargetLocalY = camDefaultLocalY;
        }
        else if (!isCrouching)
        {
            Invoke(nameof(TryRestoreStanding), 0.1f);
        }
    }

    // Helpers
    static Vector3 Horizontal(Vector3 v) => new Vector3(v.x, 0f, v.z);

    bool IsHeld(InputAction a) => a != null && a.ReadValue<float>() > 0.5f;

    bool EdgeDown(InputAction a, ref bool prev)
    {
        bool cur = IsHeld(a);
        bool down = cur && !prev;
        prev = cur;
        return down;
    }

    void SetCapsuleHeight(float h)
    {
        // keep feet at same world Y (adjust center)
        float bottomWorldY = transform.position.y + capsule.center.y - capsule.height * 0.5f;
        capsule.height = h;
        float newCenterY = (bottomWorldY + h * 0.5f) - transform.position.y;
        capsule.center = new Vector3(capsule.center.x, newCenterY, capsule.center.z);
    }

    bool CanStandUp()
    {
        float radius = capsule.radius;
        Vector3 pos = transform.position;
        // Build a standing capsule in world space to test
        Vector3 p1 = pos + Vector3.up * ((standingHeight * 0.5f) - radius);
        Vector3 p2 = pos + Vector3.up * (radius);
        int notPlayerMask = ~(1 << gameObject.layer);
        return !Physics.CheckCapsule(p1, p2, radius, notPlayerMask, QueryTriggerInteraction.Ignore);
    }

    void LogActionIfChanged(string label)
    {
        if (label == null) return;
        if (label != _lastAction)
        {
            _lastAction = label;
            Debug.Log($"Action: {label}");
        }
    }

    // ===== DEBUG input handlers =====
    void OnSprintStarted(InputAction.CallbackContext _) => Debug.Log("INPUT: Sprint Down");
    void OnSprintCanceled(InputAction.CallbackContext _) => Debug.Log("INPUT: Sprint Up");
    void OnCrouchStarted(InputAction.CallbackContext _) => Debug.Log("INPUT: Crouch Down");
    void OnCrouchCanceled(InputAction.CallbackContext _) => Debug.Log("INPUT: Crouch Up");
    void OnJumpStarted(InputAction.CallbackContext _) => Debug.Log("INPUT: Jump Pressed");

    // Move: log direction changes only
    void OnMovePerformed(InputAction.CallbackContext ctx)
    {
        var v = ctx.ReadValue<Vector2>();
        if (v.magnitude < _moveLogThreshold)
        {
            if (_lastMoveDir.magnitude >= _moveLogThreshold)
                Debug.Log("INPUT: Move Neutral");
            _lastMoveDir = v;
            return;
        }

        string dir = Mathf.Abs(v.y) >= Mathf.Abs(v.x) ? (v.y > 0 ? "Forward" : "Back")
                                                      : (v.x > 0 ? "Right" : "Left");
        string lastDir = Mathf.Abs(_lastMoveDir.y) >= Mathf.Abs(_lastMoveDir.x) ? (_lastMoveDir.y > 0 ? "Forward" : "Back")
                                                                                : (_lastMoveDir.x > 0 ? "Right" : "Left");
        if (dir != lastDir)
            Debug.Log($"INPUT: Move {dir}");

        _lastMoveDir = v;
    }
}
