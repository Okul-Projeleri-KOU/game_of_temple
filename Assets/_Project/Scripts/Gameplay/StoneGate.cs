using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class StoneGate : MonoBehaviour, IInteractable
{
    [Header("Mühür Bağlantısı")]
    [SerializeField] private RuneAltar runeAltar;

    [Header("Görsel Ayarlar (Açık / Kapalı Tasarım)")]
    [SerializeField] private SpriteRenderer gateRenderer;
    [SerializeField] private Sprite closedSprite;
    [SerializeField] private Sprite openSprite;
    [SerializeField] private GameObject closedVisualObject;

    [Header("Kilitli Kapı Uyarısı")]
    [TextArea(2, 4)]
    [SerializeField] private string lockedMessage = "Kapı Kilitli: Kadim mühür aktif değil!";

    private BoxCollider2D boxCollider;

    public bool IsOpen { get; private set; }

    private void Awake()
    {
        boxCollider = GetComponent<BoxCollider2D>();

        if (gateRenderer == null)
        {
            gateRenderer = GetComponent<SpriteRenderer>();
        }

        if (gateRenderer != null && closedSprite == null)
        {
            closedSprite = gateRenderer.sprite;
        }

        if (runeAltar == null)
        {
            runeAltar = FindFirstObjectByType<RuneAltar>();
        }
    }

    private void OnEnable()
    {
        if (runeAltar != null)
        {
            runeAltar.OnAltarActivated += OpenGate;
            runeAltar.OnAltarDeactivated += CloseGate;
        }
    }

    private void OnDisable()
    {
        if (runeAltar != null)
        {
            runeAltar.OnAltarActivated -= OpenGate;
            runeAltar.OnAltarDeactivated -= CloseGate;
        }
    }

    public void OpenGate()
    {
        IsOpen = true;
        boxCollider.enabled = false;
        UpdateGateVisuals(true);
        Debug.Log("Kemerli taş kapı açıldı!");
    }

    public void CloseGate()
    {
        IsOpen = false;
        boxCollider.enabled = true;
        UpdateGateVisuals(false);
        Debug.Log("Kemerli taş kapı yeniden kapandı!");
    }

    private void UpdateGateVisuals(bool isOpen)
    {
        if (closedVisualObject != null)
        {
            closedVisualObject.SetActive(!isOpen);
        }

        if (gateRenderer != null)
        {
            if (openSprite != null && closedSprite != null)
            {
                gateRenderer.sprite = isOpen ? openSprite : closedSprite;
            }
            else if (closedVisualObject == null)
            {
                gateRenderer.enabled = !isOpen;
            }
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (IsOpen)
            return;

        if (collision.collider.GetComponent<PlayerInteractor>() != null || collision.collider.CompareTag("Player"))
        {
            ShowLockedWarning();
        }
    }

    public void Interact()
    {
        if (!IsOpen)
        {
            ShowLockedWarning();
        }
    }

    private void ShowLockedWarning()
    {
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.ShowDialogue(lockedMessage);
        }
        else
        {
            Debug.LogWarning(lockedMessage);
        }
    }
}
