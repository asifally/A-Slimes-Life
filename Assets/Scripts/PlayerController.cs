using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 9.0f;
    public float accelleration = 13.0f;
    public float deccelleration = 16.0f;
    public float velPow = 0.96f;
    private float _horizontalInput;
    [SerializeField] bool _isFacingRight;

    [Header("Jumping")]
    public float jumpForce = 13.0f;
    public float coyoteTime = 0.15f; // Higher the value, the longer the player can jump after leaving the ground
    public float coyoteTimeCounter;
    public float jumpBufferTime = 0.1f; // Higher the value, the longer the amount of time allowed between button press and actual jump
    public float jumpBufferCounter;
    [Range(0.0f, 1.0f)]
    public float jumpCutMultiplier = 0.4f;
    public float gravityScale = 10.0f;
    public float fallGravityMultiplier = 2f;

    [Header("Dashing")]
    [SerializeField] bool _isDashing;
    [SerializeField] bool _canDash = true;
    [SerializeField] float _dashSpeed = 18.0f;
    [SerializeField] float _dashTime = 0.5f;
    private Vector2 _dashDir;
    TrailRenderer dashTrail;

    [Header("Wall Climbing")]
    [SerializeField] Transform WallCheck;
    [SerializeField] bool _isTouchingWall = false;
    [SerializeField] bool _isWallClimbing;
    [SerializeField] bool _canWallClimb = true;
    [SerializeField] float _wallCheckDistance = 0.52f;
    [SerializeField] float _wallGrabTime = 10.0f;
    [SerializeField] float _climbUpSpeed = 4.5f;
    [SerializeField] float _climbDownSpeed = -9.0f;
    [SerializeField] float _verticalInput;

    [Header("Checks")]
    [SerializeField] bool _isGrounded;
    
    private Rigidbody2D playerRb;
    private CircleCollider2D playerColl;
    [SerializeField] LayerMask jumpableLayer;

    // Inputs
    PlayerInputActions controls;
    InputAction dash;
    
    private void Awake() {
        controls = new PlayerInputActions();
    }

    private void OnEnable() {
        dash = controls.PlayerActions.Dash;
        dash.Enable();
        // dash.performed += HandlePlayerDashInput;
    }

    private void OnDisable() {
        dash.Disable();
    }

    // Start is called before the first frame update
    void Start()
    {
        playerRb = GetComponent<Rigidbody2D>();
        playerColl = GetComponent<CircleCollider2D>();
        dashTrail = GetComponent<TrailRenderer>();
    }

    // Update is called once per frame
    void Update()
    {
        _horizontalInput = Input.GetAxis("Horizontal");
        _verticalInput = Input.GetAxis("Vertical");
        CheckPlayerSurroundings();
        UpdatePlayerTimers();
        HandlePlayerJump();
        HandlePlayerDashInput();
        HandlePlayerDashReset();
        HandlePlayerWallClimbing();
        UpdatePlayerGravity();
    }

    private void FixedUpdate() 
    {
        UpdatePlayerMovement();
    }

    #region Timers
    private void UpdatePlayerTimers()
    {
        if (_isGrounded)
        {
            coyoteTimeCounter = coyoteTime;
        }
        else {
            coyoteTimeCounter -= Time.deltaTime;
        }

        if (Input.GetButtonDown("Jump"))
        {
            jumpBufferCounter = jumpBufferTime;
        }
        else{
            jumpBufferCounter -= Time.deltaTime;
        }
    }
    #endregion

    #region Movement
    private void UpdatePlayerMovement()
    {
        // Horizontal Movement
        float targetSpeed = _horizontalInput * moveSpeed;

        float speedDiff = targetSpeed - playerRb.velocity.x;

        float a = (Mathf.Abs(targetSpeed) > 0.01f) ? accelleration: deccelleration;

        float movement = Mathf.Pow(Mathf.Abs(speedDiff) * a, velPow) * Mathf.Sign(speedDiff);

        playerRb.AddForce(movement * Vector2.right);

        CheckPlayerMovementDirection();

        // Movement on wall
        if (_isWallClimbing)
        {
            StartCoroutine("StopPlayerWallGrabbing");

            if (_verticalInput > 0.0f)
            {
                playerRb.velocity = new Vector2(playerRb.velocity.x, _climbUpSpeed);
            }
            else if (_verticalInput < 0.0f)
            {
                playerRb.velocity = new Vector2(playerRb.velocity.x, _climbDownSpeed);
            }
            else {
                playerRb.velocity = new Vector2(playerRb.velocity.x, 0.0f);
            }
        }

    }

    private void CheckPlayerMovementDirection()
    {
        if (_horizontalInput > 0.01f && !_isFacingRight)
        {
            Flip();
        }
        else if (_horizontalInput < 0.01f && _isFacingRight)
        {
            Flip();
        }
    }

    private void Flip()
    {
        transform.Rotate(0.0f, 180.0f, 0.0f);
        _isFacingRight = !_isFacingRight;
    }
    #endregion

    #region Jump
    private void HandlePlayerJump()
    {
        if (coyoteTimeCounter > 0f && jumpBufferCounter > 0f)
        {
            playerRb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
            jumpBufferCounter = 0;
        }

        // Jump Height Variation
        if (Input.GetButtonUp("Jump") && playerRb.velocity.y > 0f)
        {
            playerRb.AddForce(Vector2.down * playerRb.velocity.y * (1 - jumpCutMultiplier), ForceMode2D.Impulse);
            coyoteTimeCounter = 0;
        }
        
    }  
    #endregion

    #region Dash
    private void HandlePlayerDashInput()
    {
        // TODO: Need to add right trigger
        if (Input.GetKeyDown(KeyCode.LeftShift) && _canDash)
        {
            _isDashing = true;
            _canDash = false;
            dashTrail.emitting = true;
            _dashDir = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

            // If no input, default direction
            if (_dashDir == Vector2.zero)
            {
                if (Mathf.Abs(playerRb.velocity.x) > 0 | Mathf.Abs(playerRb.velocity.y) > 0)
                {
                    _dashDir = new Vector2(playerRb.velocity.x, playerRb.velocity.y);
                }
                else {
                    _dashDir = new Vector2(transform.localScale.x, 0); // TODO: Need to set default direction based on player heading
                }
            }

            StartCoroutine("StopDashing");
        }

        if (_isDashing)
        {
            playerRb.velocity = _dashDir.normalized * _dashSpeed;
            // playerRb.AddForce(_dashDir * _dashSpeed);
            return;
        }
    }

    private void HandlePlayerDashReset()
    {
        if (_isGrounded)
        {
            _canDash = true;
        }
    }

    private IEnumerator StopDashing()
    {
        yield return new WaitForSeconds(_dashTime);
        dashTrail.emitting = false;
        _isDashing = false;
    }
    #endregion

    #region Friction
    #endregion

    #region Wall Climbing
    private void HandlePlayerWallClimbing()
    {
        if (_isTouchingWall && Input.GetMouseButton(1) && !_isGrounded && _canWallClimb)
        {
            _isWallClimbing = true;
        }
        else
        {
            _isWallClimbing = false;
        }

        if (_isGrounded)
        {
            _canWallClimb = true;
        }
    }

    private IEnumerator StopPlayerWallGrabbing()
    {
        yield return new WaitForSeconds(_wallGrabTime);
        _isWallClimbing = false;
        _canWallClimb = false;
    }
    #endregion

    #region Gravity
    private void UpdatePlayerGravity()
    {
        if (playerRb.velocity.y < 0)
        {
            playerRb.gravityScale = gravityScale * fallGravityMultiplier;
        }
        else
        {
            playerRb.gravityScale = gravityScale;
        }
    } 
    #endregion

    private void CheckPlayerSurroundings()
    {
        _isGrounded = Physics2D.CircleCast(playerColl.bounds.center, playerColl.radius, Vector2.down, 0.1f, jumpableLayer);
        _isTouchingWall = Physics2D.Raycast(WallCheck.position, Vector2.right, _wallCheckDistance, jumpableLayer);
    }

    private void OnDrawGizmos() {
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(WallCheck.position, new Vector2(WallCheck.position.x + _wallCheckDistance, WallCheck.position.y));
    }
}
