using System.Collections;
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
    [SerializeField, Min(0.02f)] private float hitWindowDuration = 0.18f;

    [Header("Targeting")]
    [SerializeField] private LayerMask enemyLayer;

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;

    private float lastAttackTime = -999f;
    private bool isAttacking;
    private Coroutine hitWindowCoroutine;
    private readonly HashSet<RhinoAI> damagedThisAttack = new HashSet<RhinoAI>();

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
        damagedThisAttack.Clear();

        // Fire the sword's own attack animation
        swordAnimator.SetTrigger(AttackHash);
    }

    /// <summary>
    /// Called by an Animation Event on the SWORD's attack clip
    /// at the frame where the blade should connect.
    /// </summary>
    public void OnSwingHit()
    {
        if (hitWindowCoroutine != null)
            StopCoroutine(hitWindowCoroutine);

        hitWindowCoroutine = StartCoroutine(DamageWindow());
    }

    private IEnumerator DamageWindow()
    {
        float endTime = Time.time + hitWindowDuration;

        do
        {
            DetectHits();
            yield return new WaitForFixedUpdate();
        }
        while (isAttacking && Time.time <= endTime);

        hitWindowCoroutine = null;
    }

    private void DetectHits()
    {
        Transform playerRoot = transform.root;
        Vector3 bladeBase = playerRoot.position + Vector3.up * 0.9f;
        Vector3 bladeTip = attackOrigin.position;

        Collider[] hits = Physics.OverlapCapsule(
            bladeBase,
            bladeTip,
            attackRadius,
            enemyLayer,
            QueryTriggerInteraction.Collide);

        foreach (var hit in hits)
        {
            if (hit.transform.IsChildOf(playerRoot)) continue;

            RhinoAI rhino = hit.GetComponentInParent<RhinoAI>();
            if (rhino == null && hit.attachedRigidbody != null)
                rhino = hit.attachedRigidbody.GetComponent<RhinoAI>();

            if (rhino == null || !damagedThisAttack.Add(rhino)) continue;

            rhino.TakeDamage(attackDamage);
            Debug.Log($"Hit {rhino.name} for {attackDamage}");
        }
    }

    /// <summary>
    /// Called by an Animation Event at the END of the sword's attack clip.
    /// </summary>
    public void OnAttackFinished()
    {
        isAttacking = false;

        if (hitWindowCoroutine != null)
        {
            StopCoroutine(hitWindowCoroutine);
            hitWindowCoroutine = null;
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;

        Transform o = attackOrigin != null ? attackOrigin : transform;
        Transform playerRoot = transform.root;
        Vector3 bladeBase = playerRoot.position + Vector3.up * 0.9f;

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.35f);
        Gizmos.DrawWireSphere(bladeBase, attackRadius);
        Gizmos.DrawWireSphere(o.position, attackRadius);
        Gizmos.DrawLine(bladeBase, o.position);

        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, transform.forward * attackRange);

        Quaternion leftRot = Quaternion.Euler(0, -attackAngle * 0.5f, 0);
        Quaternion rightRot = Quaternion.Euler(0, attackAngle * 0.5f, 0);
        Gizmos.DrawRay(transform.position, leftRot * Vector3.forward * attackRange);
        Gizmos.DrawRay(transform.position, rightRot * Vector3.forward * attackRange);
    }
}
