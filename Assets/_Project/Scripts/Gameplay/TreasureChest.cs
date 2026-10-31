using UnityEngine;

public class TreasureChest : MonoBehaviour, IInteractable
{
    private const string IndicatorChildName = "InteractionIndicator";

    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite openSprite;

    private bool isOpen;

    public bool IsOpen => isOpen;

    private void Awake()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }
    }

    public void Interact()
    {
        if (!isOpen)
        {
            if (spriteRenderer != null && openSprite != null)
            {
                spriteRenderer.sprite = openSprite;
            }

            isOpen = true;
            HideInteractionIndicator();
            Debug.Log("Sandık açıldı");
        }
    }

    private void HideInteractionIndicator()
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            if (child != null && child.name == IndicatorChildName)
            {
                child.gameObject.SetActive(false);
            }
        }
    }
}
