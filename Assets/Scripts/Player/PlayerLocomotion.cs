using System.ComponentModel;
using UnityEngine;
using UnityEngine.InputSystem;

public enum PlayerMovementState { Idle, Walking, Sprinting, Crouching}

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class PlayerLocomotionScript : MonoBehaviour
{
    [Header("Unity Input Actions")]
    [SerializeField] private PlayerInputActions playerInput;
    [SerializeField] private InputAction lookAction;
    [SerializeField] private InputAction moveAction;
    [SerializeField] private InputAction jumpAction;
    [SerializeField] private InputAction sprintAction;

    [Header("Player Functionality")]
    [SerializeField] private bool canMove = true;
    [SerializeField] private bool canLook = true;
    [SerializeField] private bool canCrouch = true;
    [SerializeField] private bool canSprint = true;
    [SerializeField] private bool canJump = true;

    [Header("Movement State")]
    [SerializeField] private PlayerMovementState playerMovementState = PlayerMovementState.Idle;

    [Header("Movement Variables")]
    [SerializeField] private float walkSpeed = 8f;
    [SerializeField] private float sprintSpeed = 16f;
    [SerializeField] private float crouchSpeed = 4f;
    private float moveSpeed;

    [Header("Drag")]
    [SerializeField] private float groundDrag;

    [Header("Jump Variables")]
    [SerializeField] private float jumpForce;
    [SerializeField] private float jumpCooldown;
    [SerializeField] private float airMultiplier;
    bool readyToJump;

    [Header("Ground Check")]
    [SerializeField] private bool isGrounded;
    [SerializeField] private LayerMask groundMask;

    [Header("Camera")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float sensitivity;
    float xRotation;
    float yRotation;

    [Header("External Components")]
    [SerializeField] private Rigidbody rb;
    [SerializeField] private CapsuleCollider capsuleCollider;

    private Vector3 moveDirection;

    [Space(10)]
    [Header("Debug")]
    [SerializeField] private bool enableDebug;

    private void Awake()
    {
        playerInput = new PlayerInputActions();

        playerCamera = GetComponentInChildren<Camera>();

        rb = GetComponent<Rigidbody>();
        capsuleCollider = GetComponent<CapsuleCollider>();

        ToggleMouseLock(true);

        readyToJump = true;
    }

    private void OnEnable()
    {
        lookAction = playerInput.Player.Look;
        lookAction.Enable();

        moveAction = playerInput.Player.Move;
        moveAction.Enable();

        jumpAction = playerInput.Player.Jump;
        jumpAction.Enable();
        jumpAction.performed += Jump;

        sprintAction = playerInput.Player.Sprint;
        sprintAction.Enable();
        sprintAction.performed += StartSprint;
        sprintAction.canceled += StopSprint;
    }

    private void OnDisable()
    {
        lookAction.Disable();
        moveAction.Disable();
        jumpAction.Disable();
        sprintAction.Disable();
    }

    private void Update()
    {
        isGrounded = IsGrounded();
        if (isGrounded)
        {
            rb.linearDamping = groundDrag;
        }
        else
        {
            rb.linearDamping = 0;
        }

            SetStateInformation();

        if (canMove)
        {
            CalculateMoveDirection();
            CalculateLookDirection();
            SpeedControl();
        }
    }

    private void FixedUpdate()
    {
        if (canMove)
        {
            ApplyFinalMovements();
        }
    }

    private void SetStateInformation()
    {
        switch (playerMovementState)
        {
            case PlayerMovementState.Walking:
                moveSpeed = walkSpeed;
            break;

            case PlayerMovementState.Sprinting:
                moveSpeed = sprintSpeed;
            break;

            case PlayerMovementState.Crouching:
                moveSpeed = crouchSpeed;
            break;

            default:
                moveSpeed = walkSpeed;
            break;
        }
    }

    private void CalculateMoveDirection()
    {
        Vector2 moveInput = moveAction.ReadValue<Vector2>();
        moveDirection = transform.forward * moveInput.y + transform.right * moveInput.x;
    }

    private void CalculateLookDirection()
    {
        float mouseX = lookAction.ReadValue<Vector2>().x * Time.deltaTime * sensitivity;
        float mouseY = lookAction.ReadValue<Vector2>().y * Time.deltaTime * sensitivity;

        yRotation += mouseX;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -75f, 75f);
        
        playerCamera.transform.rotation = Quaternion.Euler(xRotation, yRotation, 0);
        transform.rotation = Quaternion.Euler(0f, yRotation, 0f);
    }

    private void Jump(InputAction.CallbackContext cxt)
    {
        if (!readyToJump || !isGrounded) return;

        readyToJump = false;

        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

        rb.AddForce(transform.up * jumpForce, ForceMode.Impulse);

        Debug.Log("Jumped!");

        Invoke(nameof(ResetJump), jumpCooldown);
    }

    private void StartSprint(InputAction.CallbackContext cxt)
    {
        playerMovementState = PlayerMovementState.Sprinting;
    }

    private void StopSprint(InputAction.CallbackContext cxt)
    {
        playerMovementState = PlayerMovementState.Walking;
    }

    private void ApplyFinalMovements()
    {
        if (isGrounded)
        {
            rb.AddForce(moveDirection.normalized * moveSpeed, ForceMode.Acceleration);
        }
        else if(!isGrounded)
        {
            rb.AddForce(moveDirection.normalized * moveSpeed * airMultiplier, ForceMode.Acceleration);
        }
    }


    #region - Public Helpers - 
    public bool IsGrounded()
    {
        //Assign the groundCheckDistance (internally using the capsuleCollider height halved by +0.01f to offset
        float groundCheckDistance = capsuleCollider.height * 0.51f;

        if(enableDebug) Debug.DrawRay(transform.position, Vector3.down, Color.red, groundCheckDistance); // Draw a debug ray of the ground check

        //Check if the ray hits something on the ground mask, if so return true, else return false
        if (Physics.Raycast(transform.position, Vector3.down, groundCheckDistance, groundMask))
        {
            return true;
        }

        return false;
    }

    private void ToggleMouseLock(bool state)
    {
        //If state is true (state) then hide the mouse and lock it to the screen otherwise (!state) unlock it and show it
        if (state)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
    #endregion

    #region - Internal Helpers - 
    private void SpeedControl()
    {
        Vector3 flatVel = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);

        if(flatVel.magnitude > moveSpeed)
        {
            Vector3 limitedVel = flatVel.normalized * moveSpeed;
            rb.linearVelocity = new Vector3(limitedVel.x, rb.linearVelocity.y, limitedVel.z);
        }
    }

    private void ResetJump()
    {
        readyToJump = true;
    }
    #endregion
}
