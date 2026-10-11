
using UnityEngine;

public class SwordTipCollision : MonoBehaviour
{
    [Header("References")]
    public Transform fpsCamera;
    public Transform bladeBase;
    public Transform bladeTip;

    [Header("Collision")]
    public LayerMask wallLayers = ~0;
    public float tipRadius = 0.08f;
    public float padding = 0.03f;
    public float maxPullback = 1.2f;

    [Header("Movement")]
    public float moveSpeed = 20f;

    private Vector3 startLocalPosition;

    void Start()
    {
        startLocalPosition = transform.localPosition;
    }

    void LateUpdate()
    {
        if (fpsCamera == null ||
            bladeBase == null ||
            bladeTip == null)
            return;

        Vector3 basePos = bladeBase.position;
        Vector3 tipPos = bladeTip.position;
        Vector3 bladeDirection = tipPos - basePos;
        float bladeLength = bladeDirection.magnitude;

        float pullback = 0f;

        if (bladeLength > 0.001f)
        {
            RaycastHit[] hits = Physics.SphereCastAll(
                basePos,
                tipRadius,
                bladeDirection.normalized,
                bladeLength,
                wallLayers,
                QueryTriggerInteraction.Ignore
            );

            foreach (RaycastHit hit in hits)
            {
                if (hit.transform == fpsCamera.root ||
                    hit.transform.IsChildOf(fpsCamera.root))
                    continue;

                float required = bladeLength -
                                 hit.distance + padding;

                pullback = Mathf.Max(pullback, required);
            }
        }

        // Also check if the tip is already inside a wall.
        Collider[] overlaps = Physics.OverlapSphere(
            tipPos, tipRadius, wallLayers,
            QueryTriggerInteraction.Ignore
        );

        foreach (Collider col in overlaps)
        {
            if (col.transform == fpsCamera.root ||
                col.transform.IsChildOf(fpsCamera.root))
                continue;

            Vector3 closest = col.ClosestPoint(tipPos);
            float penetration =
                tipRadius - Vector3.Distance(tipPos, closest);

            pullback = Mathf.Max(
                pullback, penetration + padding
            );
        }

        pullback = Mathf.Clamp(pullback, 0f, maxPullback);

        Vector3 target = startLocalPosition;
        target.z -= pullback;

        transform.localPosition = Vector3.Lerp(
            transform.localPosition,
            target,
            moveSpeed * Time.deltaTime
        );
    }
}
