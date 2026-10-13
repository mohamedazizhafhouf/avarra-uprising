using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class RhinoAI : MonoBehaviour
{
    [Header("Detection")]
    public float detectRange = 12f;
    public float attackRange = 4.5f;
    public float loseRange = 18f;
    public LayerMask playerLayer;

    [Header("Combat")]
    public float attackCooldown = 2f;
    public int attackDamage = 20;
    public int maxHealth = 100;

    [Header("Attack Hitbox")]
    [Tooltip("Optional animated head or horn transform. Uses Attack Point Offset when unassigned.")]
    [SerializeField] private Transform attackPoint;

    [Tooltip("Local-space head/horn position used when Attack Point is unassigned.")]
    [SerializeField] private Vector3 attackPointOffset = new Vector3(0f, 0.8f, 3.1f);

    [Tooltip("Radius of the head/horn strike evaluated only at the animation impact event.")]
    [SerializeField, Min(0.1f)] private float attackHitRadius = 1.35f;

    [Tooltip("Forward-facing arc of the strike. Players beside or behind the Rhino are not hit.")]
    [SerializeField, Range(1f, 180f)] private float attackHitAngle = 100f;

    [Tooltip("Locks out another attack until the current attack animation and recovery finish.")]
    [SerializeField, Min(0.1f)] private float attackLockDuration = 1.5f;

    [Header("Movement Speeds")]
    public float walkSpeed = 1.5f;
    public float runSpeed = 4f;

    [Header("Patrol (optional)")]
    public Transform[] waypoints;

    [Header("Physics")]
    [Tooltip("Push force applied to Rigidbodies the rhino bumps into (e.g. player with CC won't be pushed, but debris will).")]
    public float pushPower = 2f;

    private NavMeshAgent agent;
    private Animator anim;
    private Rigidbody rb;
    private Transform player;
    private PlayerHealth playerHealth;   // cached, avoids SendMessage
    private Collider playerCollider;

    private float lastAttackTime;
    private int patrolIndex;
    private int currentHealth;
    private bool isDead;
    private bool attackInProgress;
    private bool attackDamageResolved;
    private Coroutine attackRoutine;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int ShoutHash = Animator.StringToHash("Shout");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int EatHash = Animator.StringToHash("Eat");
    private static readonly int GetHitHash = Animator.StringToHash("GetHit");
    private static readonly int DeadHash = Animator.StringToHash("Dead");

    private enum State { Patrol, Chase, Attack, Dead }
    private State state = State.Patrol;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();

        // --- CRITICAL: stop Rigidbody physics from fighting the NavMeshAgent ---
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotationX
                       | RigidbodyConstraints.FreezeRotationY
                       | RigidbodyConstraints.FreezeRotationZ;

        currentHealth = maxHealth;
    }

    void Start()
    {
        FindPlayer();
        agent.speed = walkSpeed;
    }

    void Update()
    {
        // Re-find player if destroyed/respawned
        if (player == null)
            FindPlayer();

        if (isDead) return;

        float distToPlayer = player
            ? Vector3.Distance(transform.position, player.position)
            : Mathf.Infinity;

        switch (state)
        {
            case State.Patrol: TickPatrol(distToPlayer); break;
            case State.Chase: TickChase(distToPlayer); break;
            case State.Attack: TickAttack(distToPlayer); break;
        }

        anim.SetFloat(SpeedHash, agent.desiredVelocity.magnitude);
    }

    // ---------- STATE LOGIC ----------
    void TickPatrol(float dist)
    {
        if (dist <= detectRange && player != null)
        {
            anim.SetTrigger(ShoutHash);
            state = State.Chase;
            GetComponent<RhinoHealthUI>()?.ShowHealthBar();
            return;
        }

        if (waypoints == null || waypoints.Length == 0) return;

        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            patrolIndex = (patrolIndex + 1) % waypoints.Length;
            agent.SetDestination(waypoints[patrolIndex].position);
        }
    }

    void TickChase(float dist)
    {
        agent.isStopped = false;
        agent.speed = runSpeed;

        if (dist > loseRange)
        {
            agent.ResetPath();
            agent.speed = walkSpeed;
            state = State.Patrol;
            return;
        }

        if (dist <= attackRange)
        {
            state = State.Attack;
            agent.isStopped = true;
            agent.ResetPath();
            agent.updateRotation = false;
            return;
        }

        agent.SetDestination(player.position);
    }

    void TickAttack(float dist)
    {
        if (player == null)
        {
            state = State.Patrol;
            agent.isStopped = false;
            agent.updateRotation = true;
            return;
        }

        // Commit to the swing once it starts. Chasing and turning during the
        // animation made the hit appear to follow the player unnaturally.
        if (attackInProgress) return;

        Vector3 look = player.position - transform.position;
        look.y = 0;
        if (look.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(look), Time.deltaTime * 6f);
        }

        if (dist > attackRange)
        {
            state = State.Chase;
            agent.isStopped = false;
            agent.updateRotation = true;
            return;
        }

        // Finish facing the target before committing. After BeginAttack the
        // orientation stays locked, giving the player a real dodge window.
        if (look.sqrMagnitude > 0.001f &&
            Vector3.Angle(transform.forward, look) > 12f)
            return;

        if (Time.time - lastAttackTime >= attackCooldown)
        {
            BeginAttack();
        }
    }

    private void FindPlayer()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject == null) return;

        playerHealth = playerObject.GetComponent<PlayerHealth>();

        if (playerHealth == null)
            playerHealth = playerObject.GetComponentInParent<PlayerHealth>();

        if (playerHealth == null)
            playerHealth = playerObject.GetComponentInChildren<PlayerHealth>();

        if (playerHealth == null)
        {
            player = null;
            playerCollider = null;
            Debug.LogWarning("[RhinoAI] Player tag found without PlayerHealth in its hierarchy.", playerObject);
            return;
        }

        // The scene tags both the Player root and its PlayerArmature child.
        // Always track the actual character, irrespective of which Unity finds.
        player = playerHealth.transform;
        playerCollider = playerHealth.GetComponent<Collider>();
    }

    private void BeginAttack()
    {
        lastAttackTime = Time.time;
        attackInProgress = true;
        attackDamageResolved = false;

        // A queued shout can otherwise interrupt the attack before its event.
        anim.ResetTrigger(ShoutHash);
        anim.SetTrigger(AttackHash);

        attackRoutine = StartCoroutine(AttackSequence());
    }

    private IEnumerator AttackSequence()
    {
        // Damage is deliberately NOT applied here. The Attack animation's
        // ApplyDamage event is the single authoritative impact moment.
        yield return new WaitForSeconds(attackLockDuration);

        attackInProgress = false;
        attackRoutine = null;
    }

    // ---------- PUBLIC METHODS ----------
    public void TakeDamage(int amount)
    {
        if (isDead) return;

        currentHealth = Mathf.Max(0, currentHealth - amount);

        // Keep the health bar in sync for every hit, including the lethal hit.
        GetComponent<RhinoHealthUI>()?.SetHealth(currentHealth, maxHealth);

        if (currentHealth <= 0)
        {
            Die();
            return;
        }

        anim.SetTrigger(GetHitHash);
    }

    public void Die()
    {
        if (isDead) return;
        isDead = true;
        state = State.Dead;

        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }

        attackInProgress = false;
        agent.isStopped = true;
        agent.ResetPath();

        // Let the corpse fall / be pushed now that it's dead
        rb.isKinematic = false;
        rb.useGravity = true;

        anim.SetBool(DeadHash, true);

        GetComponent<RhinoHealthUI>()?.OnRhinoDied();

    }

    // ---------- CALLED BY ANIMATION EVENTS ----------
    public void ApplyDamage()
    {
        ResolveAttackDamage();
    }

    private void ResolveAttackDamage()
    {
        if (!attackInProgress || attackDamageResolved || isDead || player == null)
            return;

        // The animation event is the only entry point, and each swing can hit once.
        attackDamageResolved = true;

        Vector3 hitOrigin = GetAttackHitOrigin();

        Vector3 targetPoint = playerCollider != null
            ? playerCollider.ClosestPoint(hitOrigin)
            : player.position;

        Vector3 toPlayer = targetPoint - hitOrigin;

        if (toPlayer.sqrMagnitude > attackHitRadius * attackHitRadius)
            return;

        Vector3 flatDirection = Vector3.ProjectOnPlane(toPlayer, Vector3.up);
        if (flatDirection.sqrMagnitude > 0.0001f)
        {
            float halfAngle = attackHitAngle * 0.5f;
            if (Vector3.Angle(transform.forward, flatDirection) > halfAngle)
                return;
        }

        if (playerHealth != null && !playerHealth.IsDead)
        {
            playerHealth.TakeDamage(attackDamage);
        }
        else
        {
            Debug.LogWarning("[RhinoAI] The tagged Player has no PlayerHealth component.", player);
        }
    }

    private Vector3 GetAttackHitOrigin()
    {
        // The Rhino scene objects have 2x scale. Use an offset in world units
        // so the fallback hitbox stays by the head instead of stretching out.
        return attackPoint != null
            ? attackPoint.position
            : transform.position + transform.rotation * attackPointOffset;
    }

    // ---------- PHYSICS: PUSH THINGS THE RHINO BUMPS ----------
    void OnCollisionEnter(Collision collision)
    {
        // Kinematic Rigidbodies don't receive OnCollisionEnter from other kinematic bodies.
        // But if the rhino is temporarily non-kinematic (dead), this still fires.
        Rigidbody other = collision.rigidbody;
        if (other == null || other.isKinematic) return;

        Vector3 pushDir = collision.contacts[0].point - transform.position;
        pushDir.y = 0;
        other.AddForce(pushDir.normalized * pushPower, ForceMode.Impulse);
    }

    // ---------- GIZMOS ----------
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = Color.gray;
        Gizmos.DrawWireSphere(transform.position, loseRange);

        Vector3 hitOrigin = GetAttackHitOrigin();

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(hitOrigin, attackHitRadius);

        Vector3 leftEdge = Quaternion.AngleAxis(-attackHitAngle * 0.5f, Vector3.up)
            * transform.forward;
        Vector3 rightEdge = Quaternion.AngleAxis(attackHitAngle * 0.5f, Vector3.up)
            * transform.forward;
        Gizmos.DrawRay(hitOrigin, leftEdge * attackHitRadius);
        Gizmos.DrawRay(hitOrigin, rightEdge * attackHitRadius);
    }
}
