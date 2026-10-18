using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Fighter controller. One instance per player; each instance only tracks its own state and
/// talks to the other fighter through <see cref="opponent"/>.
///
/// Split across partial files:
///   PlayerMove.cs         setup, per-frame update, movement, jump/dash/projectile
///   PlayerMove.Combat.cs  attacks, blocks, damage and healing
///   PlayerMove.Rounds.cs  knockouts, round wins, card draft, applying upgrades
/// </summary>
public partial class PlayerMove : MonoBehaviour
{
    // Public field names are unchanged so the existing scene and prefab wiring still works.
    [Header("Players")]
    public int playerIndex;
    public GameObject p1;
    public GameObject p2;

    [Header("UI")]
    public GameObject main;
    public Slider p1Slider;
    public Slider p2Slider;
    public TextMeshProUGUI roundsWonText;
    public TextMeshProUGUI card1;
    public TextMeshProUGUI card2;
    public TextMeshProUGUI card3;

    [Header("Ground check")]
    public LayerMask groundLayer;
    public Transform groundCheck;

    [Header("Projectile")]
    public GameObject circle;

    [Header("Audio")]
    public AudioSource punchSound;
    public AudioSource kickSound;
    public AudioSource parrySound;

    [Header("Not used by this script (kept so other references don't break)")]
    public GameObject HblockPlayerTrigger;
    public GameObject LblockPlayerTrigger;

    // ---- Tuning ----
    private const float JumpForce = 14f;
    private const float GroundCheckRadius = 0.2f;
    private const float DashCooldown = 2f;
    private const float DashBurstTime = 0.1f;
    private const float DashSpeed = 30f;
    private const float ProjectileCooldown = 1f;
    private const float RegenInterval = 1f;
    private static readonly Vector3 P1Spawn = new Vector3(-2f, 0f, 0f);
    private static readonly Vector3 P2Spawn = new Vector3(2f, 0f, 0f);

    // Child order the prefab relies on.
    private const int HighAttackChild = 0;
    private const int LowAttackChild = 1;
    private const int HighBlockChild = 2;
    private const int LowBlockChild = 3;
    private const int SpriteChild = 4;

    // ---- Cached components (set once in Awake) ----
    private Rigidbody2D rb;
    private Animator animator;
    private SpriteRenderer sr;
    private UI ui;
    private Circle projectileLauncher;
    private AttackHit attackHit;
    private HBlockHit highBlockZone;
    private LBlockHit lowBlockZone;
    private CapsuleCollider2D highAttackCollider;
    private CapsuleCollider2D lowAttackCollider;
    private GameObject selfObject;
    private GameObject opponentObject;
    private PlayerMove opponent;

    // ---- Match state (survives between rounds) ----
    [SerializeField] private PlayerStats stats = new PlayerStats();
    private int roundsWon;
    private bool matchInitialized;

    // ---- Round state (reset every round) ----
    private float hp;
    private float speed;
    private float damage;            // current damage, includes block buffs and buildup
    private float roundBaseDamage;   // what damage resets to after a buildup swing
    private bool gameStarted;
    private bool connected;
    private bool busy;
    private float actionCooldown;
    private float animCooldown;
    private Vector2 moveInput;
    private bool slowed;
    private float slowTimer;
    private bool invisible;
    private float invisibleTimer;
    private bool floating;
    private float dashTimer;
    private float dashDirection;
    private float projectileCooldown;
    private float regenTimer;

    private Slider HealthBar => playerIndex == 0 ? p1Slider : p2Slider;

    // ===================================================================================
    // Lifecycle
    // ===================================================================================

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        Transform spriteChild = transform.GetChild(SpriteChild);
        sr = spriteChild.GetComponent<SpriteRenderer>();
        animator = spriteChild.GetComponent<Animator>();

        attackHit = transform.GetChild(HighAttackChild).GetComponent<AttackHit>();
        highAttackCollider = transform.GetChild(HighAttackChild).GetComponent<CapsuleCollider2D>();
        lowAttackCollider = transform.GetChild(LowAttackChild).GetComponent<CapsuleCollider2D>();
        highBlockZone = transform.GetChild(HighBlockChild).GetComponent<HBlockHit>();
        lowBlockZone = transform.GetChild(LowBlockChild).GetComponent<LBlockHit>();

        ui = main.GetComponent<UI>();
        projectileLauncher = circle.GetComponent<Circle>();

        selfObject = playerIndex == 0 ? p1 : p2;
        opponentObject = playerIndex == 0 ? p2 : p1;
        opponent = opponentObject.GetComponent<PlayerMove>();
    }

    /// <summary>
    /// Unity calls this once, and the round flow calls it again to reset for the next round.
    /// Kept public with the same name because other scripts call it.
    /// </summary>
    public void Start()
    {
        if (!matchInitialized)
            InitializeMatch();
        ResetRound();
    }

    /// <summary>Makes the next Start() wipe all upgrades and wins (new match).</summary>
    public void ChangeDictionary() => matchInitialized = false;

    private void InitializeMatch()
    {
        stats = new PlayerStats();
        roundsWon = 0;
        matchInitialized = true;
    }

    private void ResetRound()
    {
        hp = stats.maxHp;
        speed = stats.speed;
        damage = stats.damage;
        roundBaseDamage = damage;

        roundOver = false;
        gameStarted = false;
        connected = false;
        busy = false;
        actionCooldown = 0f;
        animCooldown = 0f;
        slowed = false;
        slowTimer = 0f;
        invisible = false;
        invisibleTimer = 0f;
        sr.enabled = true;
        floating = false;
        dashTimer = 0f;
        projectileCooldown = 0f;
        regenTimer = RegenInterval;
        ResetCombatState();

        highAttackCollider.size = new Vector2(HighAttackBaseLength + stats.range, HighAttackThickness);
        lowAttackCollider.size = new Vector2(LowAttackBaseLength + stats.range, LowAttackThickness);

        roundsWonText.text = roundsWon.ToString();
        transform.position = playerIndex == 0 ? P1Spawn : P2Spawn;
        HealthBar.maxValue = hp;
        UpdateHealthBar();
        animator.Play("Idle");
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        UpdateGrounded();
        UpdateSlow(dt);
        CheckForKnockout();
        UpdateInvisibility(dt);
        FaceOpponent();
        UpdateBlocks(dt);
        UpdateMovement(dt);
        UpdateAnimBusy(dt);
        UpdateAttacks(dt);
        UpdateRegen(dt);
        UpdateFloatAndDash(dt);
        projectileCooldown -= dt;
    }

    // ===================================================================================
    // Simple queries used by other scripts
    // ===================================================================================

    public void RoundStart() => gameStarted = true;
    public int GetPlayerIndex() => playerIndex;
    public bool IsBusy() => busy;
    public float CheckHP() => hp;
    public bool P1Player() => playerIndex == 0 && connected;
    public bool P2Player() => playerIndex == 1 && connected;

    // ===================================================================================
    // Input handlers
    // ===================================================================================

    public void Moving(Vector2 moveDirection)
    {
        if (gameStarted)
            moveInput = moveDirection;
    }

    public void Jump()
    {
        if (CheckGrounded())
        {
            animator.SetBool("isGrounded", false);
            animator.SetTrigger("jump");
            rb.velocity = new Vector2(rb.velocity.x, JumpForce);
        }
        if (stats.canFloat)
            floating = true;
    }

    /// <summary>Called when the jump ends (animation event).</summary>
    public void JumpEnd() => floating = false;

    public void Dash()
    {
        if (!stats.canDash || dashTimer > 0f || rb.velocity.x == 0f)
            return;

        animator.SetTrigger("dash");
        dashDirection = Mathf.Sign(rb.velocity.x);
        dashTimer = DashCooldown;
    }

    public void Circle()
    {
        if (!gameStarted || !stats.hasProjectile || projectileCooldown > 0f)
            return;

        animator.SetTrigger("circle");
        projectileLauncher.Shoot(selfObject.transform, playerIndex == 0 ? 0 : 180, opponentObject);
        projectileCooldown = ProjectileCooldown;
    }

    public void Pauser()
    {
        if (!gameStarted)
            return;
        ui.Pause();
        busy = true;
    }

    // ===================================================================================
    // Per-frame helpers
    // ===================================================================================

    private bool CheckGrounded()
    {
        return Physics2D.OverlapCircle(groundCheck.position, GroundCheckRadius, groundLayer) != null;
    }

    private void UpdateGrounded()
    {
        animator.SetBool("isGrounded", CheckGrounded());
    }

    private void FaceOpponent()
    {
        float dx = opponentObject.transform.position.x - transform.position.x;
        if (dx == 0f)
            return;

        // P1's sprite faces right by default and P2's faces left, so they flip on opposite sides.
        bool opponentOnLeft = dx < 0f;
        bool flip = playerIndex == 0 ? opponentOnLeft : !opponentOnLeft;

        Vector3 rotation = transform.eulerAngles;
        rotation.y = flip ? 180f : 0f;
        transform.eulerAngles = rotation;
    }

    private void UpdateMovement(float dt)
    {
        actionCooldown -= dt;
        bool canMove = actionCooldown <= 0f && Time.timeScale == 1f;

        if (canMove)
        {
            rb.velocity = new Vector2(moveInput.x * speed, rb.velocity.y);
            busy = false;
            actionCooldown = 0f;
        }
        else
        {
            rb.velocity = new Vector2(0f, rb.velocity.y);
        }

        float walk = canMove ? moveInput.x : 0f;
        animator.SetBool("walking 0", walk > 0f);
        animator.SetBool("walk back 0", walk < 0f);
    }

    private void UpdateAnimBusy(float dt)
    {
        animCooldown -= dt;
        if (animCooldown <= 0f)
        {
            animCooldown = 0f;
            animator.SetBool("busy", false);
        }
    }

    /// <summary>Locks movement and actions for <paramref name="duration"/> seconds.</summary>
    private void EnterBusy(float duration)
    {
        busy = true;
        animator.SetBool("busy", true);
        actionCooldown = duration;
        animCooldown = duration;
    }

    public void SlowMovement(float seconds)
    {
        speed /= 2f;   // stacks if hit again while already slowed
        slowTimer = seconds;
        slowed = true;
    }

    private void UpdateSlow(float dt)
    {
        slowTimer -= dt;
        if (slowed && slowTimer <= 0f)
        {
            speed = stats.speed;
            slowed = false;
        }
    }

    private void GoInvisible()
    {
        sr.enabled = false;
        invisible = true;
        invisibleTimer = stats.invisDuration;
    }

    private void UpdateInvisibility(float dt)
    {
        if (!invisible)
            return;
        invisibleTimer -= dt;
        if (invisibleTimer <= 0f)
        {
            sr.enabled = true;
            invisible = false;
        }
    }

    private void UpdateRegen(float dt)
    {
        regenTimer -= dt;
        if (regenTimer > 0f)
            return;
        regenTimer = RegenInterval;

        float amount = stats.regen;
        if (rb.velocity.x == 0f)
            amount += stats.stillRegen;
        Heal(amount);

        if (stats.buildupRate > 0f)
            damage *= stats.buildupRate;
    }

    private void UpdateFloatAndDash(float dt)
    {
        if (floating && rb.velocity.y <= 0f)
            rb.velocity = new Vector2(rb.velocity.x, 0f);

        // The first DashBurstTime seconds of the cooldown are the actual dash.
        if (dashTimer > DashCooldown - DashBurstTime)
            rb.velocity = new Vector2(DashSpeed * dashDirection, 0f);
        dashTimer -= dt;
    }
}
