using UnityEngine;

public class PlayerInteractor : MonoBehaviour
{
    private const string IndicatorChildName = "InteractionIndicator";

    [Tooltip("Nesneye ne kadar yaklaşıldığında 'E' göstergesinin (InteractionIndicator) aktif olacağı mesafe.")]
    [SerializeField] private float interactionRadius = 0.85f;

    private Rigidbody2D rb;
    private IInteractable currentInteractable;
    private GameObject currentIndicator;

#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
    private static void InitEditorRadiusSync()
    {
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (!Application.isPlaying)
            {
                PlayerInteractor pi = Object.FindFirstObjectByType<PlayerInteractor>();
                if (pi != null && pi.interactionRadius > 1.0f)
                {
                    pi.interactionRadius = 0.85f;
                    UnityEditor.EditorUtility.SetDirty(pi);
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(pi.gameObject.scene);
                }
            }
        };
    }
#endif

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        // Sahnedeki eski 1.5f değeri kalmışsa bir tık daha yaklaşınca aktif olması için 0.85f'e çek
        if (interactionRadius > 1.0f)
        {
            interactionRadius = 0.85f;
        }
    }

    private void Update()
    {
        DetectInteractable();

        if (IsMoving())
        {
            if (DialogueManager.Instance != null)
            {
                DialogueManager.Instance.HideDialogue();
            }
            AncientTablet.HideAllReadingsOnMove();
        }

        if (Input.GetKeyDown(KeyCode.E) && currentInteractable != null)
        {
            currentInteractable.Interact();
        }
    }

    private void DetectInteractable()
    {
        // Karakterin gövde merkezini baz alarak çevredeki etkileşimli nesneleri tara
        Vector2 playerCenter = (Vector2)transform.position + new Vector2(0f, 0.3f);
        Collider2D[] colliders = Physics2D.OverlapCircleAll(playerCenter, interactionRadius);

        IInteractable closestInteractable = null;
        Transform closestTransform = null;
        float closestDistanceSqr = float.MaxValue;

        foreach (Collider2D col in colliders)
        {
            if (col == null || col.gameObject == gameObject)
                continue;

            IInteractable interactable = col.GetComponent<IInteractable>();
            if (interactable == null)
            {
                interactable = col.GetComponentInParent<IInteractable>();
            }

            if (interactable != null && interactable is MonoBehaviour mb)
            {
                // Sandık zaten açılmışsa tekrar 'E' göstergesi çıkmasın
                if (interactable is TreasureChest chest && chest.IsOpen)
                    continue;

                Vector2 closestPoint = col.ClosestPoint(playerCenter);
                float distSqr = (closestPoint - playerCenter).sqrMagnitude;

                if (distSqr <= interactionRadius * interactionRadius && distSqr < closestDistanceSqr)
                {
                    closestDistanceSqr = distSqr;
                    closestInteractable = interactable;
                    closestTransform = mb.transform;
                }
            }
        }

        if (closestInteractable == currentInteractable)
            return;

        if (currentIndicator != null)
        {
            currentIndicator.SetActive(false);
            currentIndicator = null;
        }

        currentInteractable = closestInteractable;

        if (closestTransform != null)
        {
            currentIndicator = FindIndicator(closestTransform);
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
        Vector3 center = transform.position + new Vector3(0f, 0.3f, 0f);
        Gizmos.DrawWireSphere(center, interactionRadius);
    }
}
