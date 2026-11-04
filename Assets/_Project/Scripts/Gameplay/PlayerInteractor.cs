using UnityEngine;

public class PlayerInteractor : MonoBehaviour
{
    private const string IndicatorChildName = "InteractionIndicator";

    [SerializeField] private float interactionRadius = 0.85f;

    private Rigidbody2D rb;
    private IInteractable currentInteractable;
    private GameObject currentIndicator;
    private Vector3 currentIndicatorBaseLocalPos;
    private Vector3 currentIndicatorBaseLocalScale = Vector3.one;

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

        if (interactionRadius > 1.0f)
        {
            interactionRadius = 0.85f;
        }
    }

    private void Update()
    {
        if (DialogueManager.Instance != null &&
            (DialogueManager.Instance.IsGuardChatOpen ||
             DialogueManager.Instance.IsVictoryScreenOpen ||
             DialogueManager.Instance.IsStartScreenOpen ||
             DialogueManager.Instance.IsPauseMenuOpen))
        {
            ResetAndHideCurrentIndicator();
            return;
        }

        DetectInteractable();
        AnimateCurrentIndicator();

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

        ResetAndHideCurrentIndicator();

        currentInteractable = closestInteractable;

        if (closestTransform != null)
        {
            currentIndicator = FindIndicator(closestTransform);
            if (currentIndicator != null)
            {
                currentIndicatorBaseLocalPos = currentIndicator.transform.localPosition;
                currentIndicatorBaseLocalScale = currentIndicator.transform.localScale;
                if (currentIndicatorBaseLocalScale.sqrMagnitude < 0.0001f)
                {
                    currentIndicatorBaseLocalScale = new Vector3(0.13f, 0.13f, 1f);
                }
                currentIndicator.SetActive(true);
            }
        }
    }

    private void AnimateCurrentIndicator()
    {
        if (currentIndicator == null || !currentIndicator.activeInHierarchy)
            return;

        float wave = Mathf.Sin(Time.unscaledTime * 5.2f);
        float bobOffset = wave * 0.085f;
        float scalePulse = 1f + wave * 0.06f;

        currentIndicator.transform.localPosition = currentIndicatorBaseLocalPos + new Vector3(0f, bobOffset, 0f);
        currentIndicator.transform.localScale = currentIndicatorBaseLocalScale * scalePulse;
    }

    private void ResetAndHideCurrentIndicator()
    {
        if (currentIndicator != null)
        {
            currentIndicator.transform.localPosition = currentIndicatorBaseLocalPos;
            currentIndicator.transform.localScale = currentIndicatorBaseLocalScale;
            currentIndicator.SetActive(false);
            currentIndicator = null;
        }
        currentInteractable = null;
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
