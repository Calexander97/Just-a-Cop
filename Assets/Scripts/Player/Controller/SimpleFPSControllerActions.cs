using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SocialPlatforms;

[RequireComponent(typeof(CharacterController))]
public class SimpleFPSControllerActions : MonoBehaviour
{
    // References
    [Header("Refs")]
    public Camera playerCam;

    // Input System
    [Header("Input (Input System)")]
    public InputActionAsset actionsAsset;     
    public string actionMapName = "Player";
    InputAction moveAction, lookAction, jumpAction, sprintAction, crouchAction;
 

    [Header("Camera Height")]
    public float eyeLerpSpeed = 12f;          // how fast the camera moves to new height
    float camDefaultLocalY, camSlideLocalY, camDiveLocalY, camTargetLocalY, camCrouchLocalY ;                   // camera Y when standing

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

 
    // Sprint/Crouch
    [Header("Sprint/Crouch")]
    public float sprintMultiplier = 1.35f; // speed boost when sprinting
    public float crouchHeight = 0.5f;      // crouch (used only while sliding start press)
    public float minSlideSpeed = 2.0f;     // need this ground speed to be allowed to slide
    public float minDiveSpeed = 2.0f;     // need this horizontal speed to be allowed to dive


    // Slide
    [Header("Slide")]
    public float slideStartBoost = 1.25f;
    public float slideFriction = 6f;
    public float slideDuration = 0.7f;
    public float slideHeight = 1.2f;

    // Dive
    [Header("Dive")]  
    public float diveForce = 12f;
    public float diveUpVelocity = 4.8f;
    public float diveDuration = 0.35f;
    public float diveHeight = 1.2f;
    public bool diveEndsOnlyOnLanding = true;

    // Landing behavior
    bool wasGrounded;
    bool showProneThisFrame; // for 1-frame prone camera at landing



    // Runtime state
    CharacterController cc;
    float yaw, pitch;
    Vector3 velocity, slideVel, diveVel;
    float defaultHeight;
    Vector3 defaultCenter;
    bool isSliding, isDiving, isCrouching;
    bool raiseCameraNextFrame;
    float slideTimer, diveTimer;

    // Stored Inputs
    Vector2 lookInput, moveInput;
    bool prevCrouchHeld, prevSprintHeld, prevJumpHeld;

    // Call input data/camera data, set initial camera heights
    void Awake()
    {
        cc = GetComponent<CharacterController>();
        if (!playerCam) playerCam = GetComponentInChildren<Camera>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        defaultHeight = cc.height;
        defaultCenter = cc.center;

        wasGrounded = true;
        camDefaultLocalY = playerCam ? playerCam.transform.localPosition.y : 0f;

        // Camera should follow the capsule height change by the same amount
        camSlideLocalY = camDefaultLocalY - (defaultHeight - slideHeight);
        camDiveLocalY = camDefaultLocalY - (defaultHeight - diveHeight);
        camCrouchLocalY = camDefaultLocalY - (defaultHeight - crouchHeight);
        camTargetLocalY = camDefaultLocalY;
    }

    // Called when enabled, bind to Input Actions if an asset is assigned
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
        sprintAction = map.FindAction("Sprint", true);
        crouchAction = map.FindAction("Crouch", true);

        moveAction.performed += c => moveInput = c.ReadValue<Vector2>();
        moveAction.canceled += _ => moveInput = Vector2.zero;

        lookAction.performed += c => lookInput = c.ReadValue<Vector2>();
        lookAction.canceled += _ => lookInput = Vector2.zero;
       
        map.Enable();
    }

    // Called when disabled, disable actions if we were using the asset
    void OnDisable()
    {
        if (actionsAsset != null) actionsAsset.Disable();
    }

    // Per-frame loop: look, move, and move camera to target height
    void Update()
    {
        HandleLook();
        HandleMove();

        bool groundedNow = cc.isGrounded;

        // If player landed while diving, show camera at dive height for 1 frame
        showProneThisFrame = false;
        if (groundedNow && !wasGrounded && isDiving)
        {
            camTargetLocalY = camDiveLocalY;   // one frame at prone height
            showProneThisFrame = true;
        }
        wasGrounded = groundedNow;

        if (raiseCameraNextFrame && cc.isGrounded && !isDiving && !isSliding && !isCrouching)
        {
            camTargetLocalY = camDefaultLocalY;
            raiseCameraNextFrame = false;
        }

        // After we set camTargetLocalY for prone frame, EndDive() will run (or already ran)
        // and TryRestoreCapsule() will restore standing + cam to default next frame.

        if (playerCam)
        {
            var lp = playerCam.transform.localPosition;
            lp.y = Mathf.Lerp(lp.y, camTargetLocalY, eyeLerpSpeed * Time.deltaTime);
            playerCam.transform.localPosition = lp;
        }

    }

    // Camera look (yaw on body, pitch on camera)
    void HandleLook()
    {
        // Use actions if present, else raw mouse delta
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

    // Movement state: Walk/air control, slide, dive, jump, gravity
    void HandleMove()
    {
        bool grounded = cc.isGrounded;
        if (grounded && velocity.y < 0f) velocity.y = -2f;

        bool sprintHeld = Held(sprintAction);
        bool crouchHeld = Held(crouchAction);
        bool crouchDownEdge = EdgeDown(crouchAction, ref prevCrouchHeld);
        bool jumpDownEdge = EdgeDown(jumpAction, ref prevJumpHeld);

        // Crouch (hold to stay low; not during slide/dive)
        if (!isSliding && !isDiving)
        {
            if (crouchHeld && !isCrouching)
                StartCrouch();
            else if (!crouchHeld && isCrouching)
                TryEndCrouch();  // stand if headroom
        }

        // Sprint speed
        float topSpeed = moveSpeed * (sprintHeld ? sprintMultiplier : 1f);

        // Read move (actions or KB fallback)
        Vector2 mv = (actionsAsset != null && moveAction != null)
            ? moveInput
            : ReadKBMoveFallback();

        mv = Vector2.ClampMagnitude(mv, 1f);

        // Convert local input to world direction based on player yaw
        Vector3 wishDir = transform.TransformDirection(new Vector3(mv.x, 0f, mv.y));

        // Horizontal velocity target

        Vector3 targetHVel = Vector3.Lerp(
            Horizontal(velocity),
            wishDir * topSpeed,
           (grounded ? 1f : airControl) * acceleration * Time.deltaTime
        );


        float hSpeed = Horizontal(velocity).magnitude;
        float intendedHSpeed = Horizontal(targetHVel).magnitude;

        // Slide: must be grounded, sprinting, press crouch, have some speed
        bool wantSlide = grounded && sprintHeld && crouchDownEdge && hSpeed > minSlideSpeed;

        // Dive: sprinting + jump, some intended speed (use targetHVel so player can dive from standstill if pressing W)
        bool wantDive = sprintHeld && jumpDownEdge && intendedHSpeed > minDiveSpeed;

        if (!isSliding && !isDiving && wantSlide) StartSlide();
        if (!isDiving && !isSliding && wantDive) StartDive();


        // State horizontal velocity
        if (isDiving)
        {
            // Commitment timer 
            if (diveTimer > 0f) diveTimer -= Time.deltaTime;

            // Maintain forward burst based on recorded diveVel, lightly taper after commitment
            Vector3 hv = (diveTimer > 0f)
                ? diveVel
                : Vector3.Lerp(diveVel, Vector3.zero, 0.12f * Time.deltaTime);

            velocity.x = hv.x;
            velocity.z = hv.z;

            // End: either timer or preferably landing after timer
            if (diveEndsOnlyOnLanding)
            {
                if (diveTimer <= 0f && grounded) EndDive();
            }
            else
            {
                if (diveTimer <= 0f) EndDive();
            }
        }
        else if (isSliding)
        {
            // Slide friction toward zero
            slideTimer -= Time.deltaTime;
            slideVel = Vector3.MoveTowards(slideVel, Vector3.zero, slideFriction * Time.deltaTime);
            velocity.x = slideVel.x; velocity.z = slideVel.z;

            // End slide when timer runs out or we leave ground
            if (slideTimer <= 0f || !grounded) EndSlide();
        }
        else
        {
            // Normal locomotion
            velocity.x = targetHVel.x; velocity.z = targetHVel.z;
        }

        // Ground jump (disabled while sliding/diving)
        if (!isSliding && !isDiving && grounded && jumpDownEdge)
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

        // Gravity + move
        velocity.y += gravity * Time.deltaTime;
        cc.Move(velocity * Time.deltaTime);
    }

    void StartCrouch()
    {
        isCrouching = true;
        SetCapsuleHeight(crouchHeight);
        camTargetLocalY = camCrouchLocalY;
    }

    void TryEndCrouch()
    {
        if (CanStandUp())
        {
            isCrouching = false;
            SetCapsuleHeight(defaultHeight);
            camTargetLocalY = camDefaultLocalY;
        }
        // else stay crouched; will retry when player releases under headroom
    }

    // Enter slide: lower player, set camera height, kick forward momentum
    void StartSlide()
    {
        isSliding = true; slideTimer = slideDuration;
        SetCapsuleHeight(slideHeight);                       
        camTargetLocalY = camSlideLocalY;   

        Vector3 hv = Horizontal(velocity);
        if (hv.sqrMagnitude < 0.01f) hv = transform.forward * moveSpeed;
        slideVel = hv.normalized * Mathf.Max(hv.magnitude * slideStartBoost, moveSpeed * 1.1f);
        velocity.y = -2f;
    }

    // Exit slide, restore standing
    void EndSlide()
    {
        isSliding = false;
        if (Held(crouchAction)) StartCrouch();
        TryRestoreCapsule();
    }

    void StartDive()
    {
        isDiving = true; 
        diveTimer = diveDuration;

        SetCapsuleHeight(diveHeight);
        camTargetLocalY = camDiveLocalY; // camera matches dive height

        // Momentum direction from current horizontal velocity (fallback to forward)
        Vector3 hvNow = Horizontal(velocity);
        Vector3 dir = hvNow.sqrMagnitude > 0.01f ? hvNow.normalized : transform.forward;


        // Strong forward burst in that direction
        diveVel = dir * diveForce;

        // Upward hop for a visible arc (overrides tiny bias)
        velocity.y = Mathf.Max(velocity.y, diveUpVelocity);
    }

    void EndDive()
    {
        isDiving = false;

        velocity.x = diveVel.x * 0.6f;
        velocity.z = diveVel.z * 0.6f;

        TryRestoreCapsule();         // will also set camTargetLocalY to default (unless blocked)

        // If crouch is held, stay crouched; otherwise schedule a raise next frame
        if (Held(crouchAction)) StartCrouch();
        else raiseCameraNextFrame = true;
    }

    // Try to stand up
    void TryRestoreCapsule()
    {
        if (CanStandUp())
        {
            SetCapsuleHeight(defaultHeight);         
            if (!showProneThisFrame && isCrouching)
                camTargetLocalY = camDefaultLocalY;  
        }
        else
        {
            Invoke(nameof(TryRestoreCapsule), 0.1f);
        }
    }
    // Check if the standing capsule would fit (no objects above head)
    bool CanStandUp()
    {
        float radius = cc.radius;

        // Standing capsule endpoints in world space using default height/center
        Vector3 p1 = transform.position + Vector3.up * (defaultCenter.y - defaultHeight * 0.5f + radius); 
        Vector3 p2 = transform.position + Vector3.up * (defaultCenter.y + defaultHeight * 0.5f - radius); 

        int mask = ~(1 << gameObject.layer);
        return !Physics.CheckCapsule(p1, p2, radius, mask, QueryTriggerInteraction.Ignore);
    }

    // Strip vertical component
    static Vector3 Horizontal(Vector3 v) => new Vector3(v.x, 0f, v.z);

    // Keyboard fallback if no Input Actions asset is assigned
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
    // Change CharacterController height while keeping feet position stable
    void SetCapsuleHeight(float newHeight)
    {
        // World Y of capsule bottom stays fixed so we don't sink/rise
        float bottomWorldY = transform.position.y + (cc.center.y - cc.height * 0.5f);
        float topWorldY = bottomWorldY + newHeight;

        cc.height = newHeight;

        // Recenter capsule around the same bottom/top in local space
        float newCenterY = (bottomWorldY + topWorldY) * 0.5f - transform.position.y;
        cc.center = new Vector3(defaultCenter.x, newCenterY, defaultCenter.z);
    }

    bool Held(InputAction a)
    {
        return a != null && a.ReadValue<float>() > 0.5f;
    }

    bool EdgeDown(InputAction a, ref bool prev)
    {
        bool cur = a != null && a.ReadValue<float>() > 0.5f;
        bool down = cur && !prev;
        prev = cur;
        return down;
    }
}



