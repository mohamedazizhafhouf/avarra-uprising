using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 12f;
    [SerializeField] private float sprintMultiplier = 1.6f;

    private Rigidbody rb;
    private Camera mainCam;
    private Vector3 moveInput;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        mainCam = Camera.main;
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) { moveInput = Vector3.zero; return; }

        float h = 0f, v = 0f;
        if (kb.aKey.isPressed) h -= 1f;
        if (kb.dKey.isPressed) h += 1f;
        if (kb.sKey.isPressed) v -= 1f;
        if (kb.wKey.isPressed) v += 1f;

        Vector3 forward = mainCam.transform.forward; forward.y = 0; forward.Normalize();
        Vector3 right = mainCam.transform.right; right.y = 0; right.Normalize();

        moveInput = (forward * v + right * h).normalized;
    }

    void FixedUpdate()
    {
        float speed = moveSpeed;
        var kb = Keyboard.current;
        if (kb != null && kb.leftShiftKey.isPressed) speed *= sprintMultiplier;

        rb.MovePosition(rb.position + moveInput * speed * Time.fixedDeltaTime);

        if (moveInput.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(moveInput);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRot,
                Time.fixedDeltaTime * rotationSpeed));
        }
    }
}