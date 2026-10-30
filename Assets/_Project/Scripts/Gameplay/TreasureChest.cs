using UnityEngine;

public class TreasureChest : MonoBehaviour, IInteractable
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite openSprite;

    private bool isOpen;

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
            Debug.Log("Sandık açıldı");
        }
    }
}

