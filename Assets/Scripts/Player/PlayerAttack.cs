using System.Collections.Generic;
using UnityEngine;

public class PlayerAttack : MonoBehaviour
{
    [Header("Sword Reference")]
    [Tooltip("Drag the Sword GameObject here (the one with its own Animator).")]
    [SerializeField] private Animator swordAnimator;

    [Tooltip("Where the damage check originates from. Usually the sword tip or the player's chest.")]
    [SerializeField] private Transform attackOrigin;

    [Header("Attack Settings")]
    [SerializeField] private int attackDamage = 25;
    [SerializeField] private float attackRange = 2.2f;
    [SerializeField] private float attackRadius = 1.2f;
    [SerializeField] private float attackCooldown = 0.6f;
    [SerializeField] private float attackAngle = 120f;

    [Header("Targeting")]
    [SerializeField] private LayerMask enemyLayer;

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;

    private float lastAttackTime = -999f;
    private bool isAttacking;

    private static readonly int AttackHash = Animator.StringToHash("attack");

    public bool IsAttacking => isAttacking;

    void Awake()
    {
        if (swordAnimator == null)
            swordAnimator = GetComponentInChildren<Animator>(); // fallback: find first Animator under player

        if (attackOrigin == null)
            attackOrigin = transform;

        if (swordAnimator == null)
            Debug.LogError("[PlayerAttack] No sword Animator assigned!");
    }

    void Update()
    {
        if (Input.GetButtonDown("Fire1"))
            TryAttack();
    }

    public void TryAttack()
    {
        if (isAttacking) return;
        if (Time.time - lastAttackTime < attackCooldown) return;

        lastAttackTime = Time.time;
        isAttacking = true;

        // Fire the sword's own attack animation
        swordAnimator.SetTrigger(AttackHash);
    }

    /// <summary>
    /// Called by an Animation Event on the SWORD's attack clip
    /// at the frame where the blade should connect.
    /// </summary>
    public void OnSwingHit()
    {
        Vector3 origin = attackOrigin.position;
        Vector3 forward = transform.forward;

        Collider[] hits = Physics.OverlapSphere(origin, attackRange, enemyLayer);
        HashSet<GameObject> damaged = new HashSet<GameObject>();

        foreach (var hit in hits)
        {
            if (hit.transform.IsChildOf(transform)) continue;

            Vector3 toEnemy = hit.transform.position - transform.position;
            toEnemy.y = 0;

            if (Vector3.Angle(forward, toEnemy) > attackAngle * 0.5f) continue;
            if (toEnemy.magnitude > attackRange + attackRadius) continue;

            GameObject root = hit.attachedRigidbody
                ? hit.attachedRigidbody.gameObject
                : hit.transform.root.gameObject;

            if (!damaged.Add(root)) continue;

            var rhino = root.GetComponentInChildren<RhinoAI>();
            if (rhino != null)
            {
                rhino.TakeDamage(attackDamage);
                Debug.Log($"Hit {root.name} for {attackDamage}");
            }
        }
    }

    /// <summary>
    /// Called by an Animation Event at the END of the sword's attack clip.
    /// </summary>
    public void OnAttackFinished()
    {
        isAttacking = false;
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;

        Transform o = attackOrigin != null ? attackOrigin : transform;

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.35f);
        Gizmos.DrawWireSphere(o.position, attackRange);

        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, transform.forward * attackRange);

        Quaternion leftRot = Quaternion.Euler(0, -attackAngle * 0.5f, 0);
        Quaternion rightRot = Quaternion.Euler(0, attackAngle * 0.5f, 0);
        Gizmos.DrawRay(transform.position, leftRot * Vector3.forward * attackRange);
        Gizmos.DrawRay(transform.position, rightRot * Vector3.forward * attackRange);
    }
}