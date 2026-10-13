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
    public float attackRange = 2.5f;
    public float loseRange = 18f;
    public LayerMask playerLayer;

    [Header("Combat")]
    public float attackCooldown = 2f;
    public int attackDamage = 20;
    public int maxHealth = 100;

    [Tooltip("Seconds from the start of the attack animation to its hit frame.")]
    [SerializeField, Min(0f)] private float attackHitDelay = 0.5f;

    [Tooltip("Recovery after the hit frame before another attack can begin.")]
    [SerializeField, Min(0f)] private float attackRecoveryDuration = 0.75f;

    [Tooltip("Extra reach allowed at the hit frame so small movement does not make a visible hit miss.")]
    [SerializeField, Min(0f)] private float attackRangeBuffer = 0.5f;

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
            return;
        }

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
            return;
        }

        if (!attackInProgress && Time.time - lastAttackTime >= attackCooldown)
        {
            BeginAttack();
        }
    }

    private void FindPlayer()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject == null) return;

        player = playerObject.transform;
        playerHealth = playerObject.GetComponent<PlayerHealth>();

        if (playerHealth == null)
            playerHealth = playerObject.GetComponentInParent<PlayerHealth>();

        if (playerHealth == null)
            playerHealth = playerObject.GetComponentInChildren<PlayerHealth>();
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
        if (attackHitDelay > 0f)
            yield return new WaitForSeconds(attackHitDelay);

        // The animation event normally resolves the hit. This is a fallback for
        // transitions or frame skips that prevent the event from being delivered.
        ResolveAttackDamage();

        if (attackRecoveryDuration > 0f)
            yield return new WaitForSeconds(attackRecoveryDuration);

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

        // Resolve only once per attack, whether this method was reached by the
        // animation event or by AttackSequence's fallback.
        attackDamageResolved = true;

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        float damageRange = attackRange + attackRangeBuffer;

        if (toPlayer.sqrMagnitude > damageRange * damageRange)
            return;

        if (playerHealth == null)
            FindPlayer();

        if (playerHealth != null)
        {
            playerHealth.TakeDamage(attackDamage);
        }
        else
        {
            Debug.LogWarning("[RhinoAI] The tagged Player has no PlayerHealth component.", player);
        }
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
    }
}
