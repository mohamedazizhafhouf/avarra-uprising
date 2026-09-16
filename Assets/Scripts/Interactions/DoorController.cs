using UnityEngine;
using UnityEngine.InputSystem;

public class DoorController : MonoBehaviour
{
    [Header("Door")]
    [SerializeField] private Transform doorHinge;

    [Header("Settings")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private float interactionDistance = 2.5f;
    [SerializeField] private float openAngle = -90f;
    [SerializeField] private float rotationSpeed = 120f;

    private bool playerInRange;
    private bool isOpen;
    private Transform player;

    private Quaternion closedRotation;
    private Quaternion openRotation;

    private void Awake()
    {
        if (doorHinge == null)
        {
            doorHinge = transform;
        }

        // Remember whatever rotation the door has when the game starts.
        closedRotation = doorHinge.localRotation;

        // Calculate the opened position relative to the closed position.
        openRotation = closedRotation * Quaternion.Euler(0f, openAngle, 0f);
    }

    private void Start()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag(playerTag);

        if (playerObject != null)
        {
            player = playerObject.transform;
        }
    }

    private void Update()
    {
        // Press E while standing near the door.
        if (IsPlayerNearDoor() &&
            Keyboard.current != null &&
            Keyboard.current.eKey.wasPressedThisFrame)
        {
            isOpen = !isOpen;
        }

        Quaternion targetRotation = isOpen ? openRotation : closedRotation;

        // Smoothly rotate toward the desired position.
        doorHinge.localRotation =
            Quaternion.RotateTowards(
                doorHinge.localRotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
    }

    private bool IsPlayerNearDoor()
    {
        if (playerInRange)
        {
            return true;
        }

        if (player == null)
        {
            return false;
        }

        return Vector3.Distance(player.position, transform.position) <= interactionDistance;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            playerInRange = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            playerInRange = false;
        }
    }
}
