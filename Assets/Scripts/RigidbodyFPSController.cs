using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class RigidbodyFPSController : MonoBehaviour
{

    [Header("Refs")]
    public Camera playerCam;
    public InputActionAsset actionsAsset;
    public string actionMapName = "Player";

    // Input Action   
    InputAction moveAction, lookAction, jumpAction, sprintAction, crouchAction;

    [Header ("Look")]
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





    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
