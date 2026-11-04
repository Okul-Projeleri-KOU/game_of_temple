using System.Collections;
using Cainos.PixelArtTopDown_Basic;
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
            if (transform.position.x < 0f && AncientTablet.ReadPillarsCount < 3)
            {
                if (DialogueManager.Instance != null)
                {
                    DialogueManager.Instance.ShowEventPopup(
                        null,
                        string.Empty,
                        "MÜHÜR KİLİTLİ",
                        $"Önce tapınaktaki 3 sütun anıtını incelemelisin! ({AncientTablet.ReadPillarsCount}/3)",
                        new Color(1f, 0.74f, 0.32f, 1f),
                        2.8f,
                        0f);
                }
                return;
            }

            if (spriteRenderer != null && openSprite != null)
            {
                spriteRenderer.sprite = openSprite;
            }

            isOpen = true;
            HideInteractionIndicator();

            if (GameManager.Instance != null)
            {
                if (transform.position.x < 0f)
                {
                    GameManager.Instance.SetHasCompanion(true);
                }
                else
                {
                    if (Camera.main != null)
                    {
                        CameraFollow camFollow = Camera.main.GetComponent<CameraFollow>();
                        if (camFollow != null)
                        {
                            camFollow.FocusTemporarily(transform, 1.65f);
                        }
                    }

                    SpawnKeyPopEffect();
                    GameManager.Instance.SetHasKey(true);
                }
            }

            Debug.Log("Sandık açıldı");
        }
    }

    private void SpawnKeyPopEffect()
    {
        Sprite keySprite = null;
        if (DialogueManager.Instance != null)
        {
            keySprite = DialogueManager.Instance.KeyIconSprite;
        }

        if (keySprite == null)
        {
            Sprite[] allSprites = Resources.FindObjectsOfTypeAll<Sprite>();
            foreach (Sprite s in allSprites)
            {
                if (s != null && s.name.Contains("anahtar"))
                {
                    keySprite = s;
                    break;
                }
            }
        }

        if (keySprite == null)
            return;

        StartCoroutine(AnimateFloatingKeyRoutine(keySprite));
    }

    private IEnumerator AnimateFloatingKeyRoutine(Sprite keySprite)
    {
        GameObject keyVisualObj = new GameObject("FloatingKeyEffect");
        Vector3 startPos = new Vector3(transform.position.x, transform.position.y + 0.35f, 0f);
        Vector3 peakPos = new Vector3(transform.position.x, transform.position.y + 1.05f, 0f);
        keyVisualObj.transform.position = startPos;

        float targetWorldHeight = 0.65f;
        float spriteHeight = keySprite.bounds.size.y > 0.001f ? keySprite.bounds.size.y : 1f;
        float baseScale = targetWorldHeight / spriteHeight;
        keyVisualObj.transform.localScale = Vector3.one * (baseScale * 0.4f);

        SpriteRenderer sr = keyVisualObj.AddComponent<SpriteRenderer>();
        sr.sprite = keySprite;
        sr.sortingLayerName = "Layer 3";
        sr.sortingOrder = 120;

        float duration = 2.0f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            float moveT = 1f - Mathf.Pow(1f - Mathf.Clamp01(t * 1.8f), 3f);
            float bobOffset = Mathf.Sin(elapsed * 6f) * 0.06f;
            keyVisualObj.transform.position = Vector3.Lerp(startPos, peakPos, moveT) + new Vector3(0f, bobOffset, 0f);

            float scaleMultiplier = t < 0.2f
                ? Mathf.Lerp(0.4f, 1.15f, t / 0.2f)
                : Mathf.Lerp(1.15f, 1.0f, (t - 0.2f) / 0.8f);
            keyVisualObj.transform.localScale = Vector3.one * (baseScale * scaleMultiplier);

            if (t > 0.72f)
            {
                float alpha = Mathf.Clamp01(1f - ((t - 0.72f) / 0.28f));
                sr.color = new Color(1f, 1f, 1f, alpha);
            }

            yield return null;
        }

        Destroy(keyVisualObj);
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
