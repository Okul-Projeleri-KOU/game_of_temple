using UnityEngine;

public class PlayerInteractor : MonoBehaviour
{
    private const string IndicatorChildName = "InteractionIndicator";

    [SerializeField] private float interactionRadius = 1.5f;

    private Rigidbody2D rb;
    private IInteractable currentInteractable;
    private GameObject currentIndicator;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        DetectInteractable();

        if (IsMoving() && DialogueManager.Instance != null)
        {
            DialogueManager.Instance.HideDialogue();
        }

        if (Input.GetKeyDown(KeyCode.E) && currentInteractable != null)
        {
            currentInteractable.Interact();
        }
    }

    private void DetectInteractable()
    {
        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, interactionRadius);

        IInteractable foundInteractable = null;
        Transform foundTransform = null;

        foreach (Collider2D col in colliders)
        {
            if (col.TryGetComponent<IInteractable>(out IInteractable interactable))
            {
                foundInteractable = interactable;
                foundTransform = col.transform;
                break;
            }
        }

        if (foundInteractable == currentInteractable)
            return;

        if (currentIndicator != null)
        {
            currentIndicator.SetActive(false);
            currentIndicator = null;
        }

        currentInteractable = foundInteractable;

        if (foundTransform != null)
        {
            currentIndicator = FindIndicator(foundTransform);
            if (currentIndicator != null)
            {
                currentIndicator.SetActive(true);
            }
        }
    }

    private GameObject FindIndicator(Transform parent)
    {
        Transform directChild = parent.Find(IndicatorChildName);
        if (directChild != null)
        {
            return directChild.gameObject;
        }

        Transform[] allChildren = parent.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in allChildren)
        {
            if (child.name == IndicatorChildName)
            {
                return child.gameObject;
            }
        }

        return null;
    }

    private bool IsMoving()
    {
        bool hasInput = Input.GetAxisRaw("Horizontal") != 0f || Input.GetAxisRaw("Vertical") != 0f;
        bool hasVelocity = rb != null && rb.linearVelocity.sqrMagnitude > 0.01f;
        return hasInput || hasVelocity;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionRadius);
    }
}
