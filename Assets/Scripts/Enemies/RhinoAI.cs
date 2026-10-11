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
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            player = p.transform;
            playerHealth = p.GetComponent<PlayerHealth>();
        }
        agent.speed = walkSpeed;
    }

    void Update()
    {
        // Re-find player if destroyed/respawned
        if (player == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
            {
                player = p.transform;
                playerHealth = p.GetComponent<PlayerHealth>();
            }
        }

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
            agent.ResetPath();
            return;
        }

        agent.SetDestination(player.position);
    }

    void TickAttack(float dist)
    {
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
            return;
        }

        if (Time.time - lastAttackTime >= attackCooldown)
        {
            lastAttackTime = Time.time;
            anim.SetTrigger(AttackHash);
        }

        if (dist <= detectRange && player != null)
        {
            anim.SetTrigger(ShoutHash);
            state = State.Chase;

            // NEW: tell the UI to appear
            GetComponent<RhinoHealthUI>()?.ShowHealthBar();

            return;
        }
    }

    // ---------- PUBLIC METHODS ----------
    public void TakeDamage(int amount)
    {
        if (isDead) return;

        currentHealth -= amount;
        if (currentHealth <= 0)
        {
            Die();
            return;
        }

        anim.SetTrigger(GetHitHash);

        GetComponent<RhinoHealthUI>()?.SetHealth(currentHealth, maxHealth);

    }

    public void Die()
    {
        if (isDead) return;
        isDead = true;
        state = State.Dead;
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
        if (isDead || player == null) return;

        if (Vector3.Distance(transform.position, player.position) <= attackRange + 0.5f)
        {
            // Prefer direct call — SendMessage is fragile
            if (playerHealth != null)
                playerHealth.TakeDamage(attackDamage);
            else
                player.SendMessage("TakeDamage", attackDamage, SendMessageOptions.DontRequireReceiver);
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