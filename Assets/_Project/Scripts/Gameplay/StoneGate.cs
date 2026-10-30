using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

    [Header("Kilitli Kapı Uyarısı (kapi_kilitli_0)")]
    [SerializeField] private Sprite lockedBannerSprite;
    [TextArea(2, 4)]
    [SerializeField] private string lockedMessage = "KAPI KİLİTLİ\nKadim mühür aktif değil!";
    [SerializeField] private float autoWarnDistance = 1.45f;

    private BoxCollider2D boxCollider;
    private Transform playerTransform;
    private Vector3 initialLocalPos;
    private Color initialGateColor = Color.white;

    // Otomatik ekran uyarı efekti bileşenleri (kapi_kilitli_0)
    private static Canvas warningCanvas;
    private static CanvasGroup warningCanvasGroup;
    private static RectTransform warningBannerRect;
    private static Image warningBannerImage;
    private static Image warningOuterEdgeImage;
    private static TextMeshProUGUI warningBodyText;

    private bool isPlayerNearLockedGate;
    private float shakeTimer;
    private float bannerAnimTime;

    public bool IsOpen { get; private set; }

#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
    private static void InitEditorGateSync()
    {
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (!Application.isPlaying)
            {
                Sprite loadedBanner = FindLockedGateBannerSprite();
                if (loadedBanner != null)
                {
                    StoneGate[] gates = Object.FindObjectsByType<StoneGate>(FindObjectsSortMode.None);
                    foreach (StoneGate g in gates)
                    {
                        if (g != null && g.lockedBannerSprite != loadedBanner)
                        {
                            g.lockedBannerSprite = loadedBanner;
                            UnityEditor.EditorUtility.SetDirty(g);
                            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(g.gameObject.scene);
                        }
                    }
                }
            }
        };
    }
#endif

    private void Awake()
    {
        boxCollider = GetComponent<BoxCollider2D>();

        // Kapının her katmandaki (Layer 1/2/3) oyuncuyla çarpışması için Default (0) katmanına al
        gameObject.layer = 0;
        if (boxCollider != null)
        {
            boxCollider.isTrigger = false;
        }

        if (gateRenderer == null)
        {
            gateRenderer = GetComponent<SpriteRenderer>();
        }

        if (gateRenderer != null)
        {
            initialGateColor = gateRenderer.color;
            if (closedSprite == null)
            {
                closedSprite = gateRenderer.sprite;
            }
        }

        if (lockedBannerSprite == null)
        {
            lockedBannerSprite = FindLockedGateBannerSprite();
        }

        initialLocalPos = transform.localPosition;

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

    private void Update()
    {
        if (playerTransform == null)
        {
            PlayerInteractor pi = FindFirstObjectByType<PlayerInteractor>();
            if (pi != null)
            {
                playerTransform = pi.transform;
            }
        }

        CheckProximityWarning();
        AnimateGateAndBanner();
    }

    private void CheckProximityWarning()
    {
        if (IsOpen || playerTransform == null || boxCollider == null)
        {
            if (isPlayerNearLockedGate)
            {
                isPlayerNearLockedGate = false;
                ResetGateVisualEffect();
            }
            return;
        }

        Vector2 playerPos = (Vector2)playerTransform.position + new Vector2(0f, 0.25f);
        Vector2 closest = boxCollider.ClosestPoint(playerPos);
        float dist = Vector2.Distance(playerPos, closest);

        bool shouldWarn = dist <= autoWarnDistance;

        if (shouldWarn && !isPlayerNearLockedGate)
        {
            isPlayerNearLockedGate = true;
            shakeTimer = 0.35f;
            bannerAnimTime = 0f;
            EnsureWarningBannerUI(lockedBannerSprite != null ? lockedBannerSprite : FindLockedGateBannerSprite());
            UpdateBannerText();
        }
        else if (!shouldWarn && isPlayerNearLockedGate && dist > autoWarnDistance + 0.2f)
        {
            isPlayerNearLockedGate = false;
            ResetGateVisualEffect();
        }
    }

    private void AnimateGateAndBanner()
    {
        if (isPlayerNearLockedGate && !IsOpen)
        {
            bannerAnimTime += Time.deltaTime;

            if (shakeTimer > 0f)
            {
                shakeTimer -= Time.deltaTime;
                float offsetX = Mathf.Sin(Time.time * 55f) * 0.045f * (shakeTimer / 0.35f);
                transform.localPosition = initialLocalPos + new Vector3(offsetX, 0f, 0f);
            }
            else
            {
                transform.localPosition = initialLocalPos;
            }
        }

        if (warningCanvasGroup != null)
        {
            float targetAlpha = (isPlayerNearLockedGate && !IsOpen) ? 1f : 0f;
            warningCanvasGroup.alpha = Mathf.MoveTowards(warningCanvasGroup.alpha, targetAlpha, Time.deltaTime * 5.5f);

            if (warningCanvasGroup.alpha > 0.01f)
            {
                if (!warningCanvas.gameObject.activeSelf)
                {
                    warningCanvas.gameObject.SetActive(true);
                }

                float scalePop = (isPlayerNearLockedGate && bannerAnimTime < 0.25f)
                    ? Mathf.Lerp(0.86f, 1.03f, bannerAnimTime / 0.25f)
                    : Mathf.Lerp(warningBannerRect.localScale.x, 1f, Time.deltaTime * 8f);

                warningBannerRect.localScale = new Vector3(scalePop, scalePop, 1f);
            }
            else if (warningCanvas != null && warningCanvas.gameObject.activeSelf)
            {
                warningCanvas.gameObject.SetActive(false);
            }
        }
    }

    private void ResetGateVisualEffect()
    {
        transform.localPosition = initialLocalPos;
        if (gateRenderer != null)
        {
            gateRenderer.color = initialGateColor;
        }
    }

    public void OpenGate()
    {
        IsOpen = true;
        isPlayerNearLockedGate = false;
        ResetGateVisualEffect();
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
            shakeTimer = 0.35f;
            isPlayerNearLockedGate = true;
            EnsureWarningBannerUI(lockedBannerSprite != null ? lockedBannerSprite : FindLockedGateBannerSprite());
            UpdateBannerText();
        }
    }

    public void Interact()
    {
        if (!IsOpen)
        {
            shakeTimer = 0.35f;
            isPlayerNearLockedGate = true;
            EnsureWarningBannerUI(lockedBannerSprite != null ? lockedBannerSprite : FindLockedGateBannerSprite());
            UpdateBannerText();
        }
    }

    private void UpdateBannerText()
    {
        if (warningBodyText == null)
            return;

        string sub = string.IsNullOrWhiteSpace(lockedMessage)
            ? "Kadim mühür aktif değil!"
            : lockedMessage.Replace("Kapı Kilitli:", "").Replace("KAPI KİLİTLİ", "").Trim();
        if (string.IsNullOrEmpty(sub))
        {
            sub = "Kadim mühür aktif değil!";
        }

        warningBodyText.text = $"<color=#F2C166><b>KAPI KİLİTLİ</b></color>\n{sub}";
    }

    private static void EnsureWarningBannerUI(Sprite spriteToUse)
    {
        if (warningCanvas != null)
        {
            if (warningBannerImage != null && warningBannerImage.sprite == null && spriteToUse != null)
            {
                warningBannerImage.sprite = spriteToUse;
            }
            if (warningOuterEdgeImage != null && warningOuterEdgeImage.sprite == null && spriteToUse != null)
            {
                warningOuterEdgeImage.sprite = spriteToUse;
            }
            return;
        }

        GameObject canvasObj = new GameObject("LockedGateWarningCanvas");
        DontDestroyOnLoad(canvasObj);

        warningCanvas = canvasObj.AddComponent<Canvas>();
        warningCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        warningCanvas.sortingOrder = 150;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        canvasObj.AddComponent<GraphicRaycaster>();

        // kapi_kilitli_0 (2400 x 793) oranına uygun boyut: 780 x 258
        GameObject bannerObj = new GameObject("LockedWarningBanner_kapi_kilitli_0");
        bannerObj.transform.SetParent(canvasObj.transform, false);

        warningBannerRect = bannerObj.AddComponent<RectTransform>();
        warningBannerRect.anchorMin = new Vector2(0.5f, 0.78f);
        warningBannerRect.anchorMax = new Vector2(0.5f, 0.78f);
        warningBannerRect.pivot = new Vector2(0.5f, 0.5f);
        warningBannerRect.sizeDelta = new Vector2(780f, 258f);

        warningCanvasGroup = bannerObj.AddComponent<CanvasGroup>();
        warningCanvasGroup.alpha = 0f;
        warningCanvasGroup.blocksRaycasts = false;
        warningCanvasGroup.interactable = false;

        // 1. Tüm etrafını eşit saran hafif siyah dış hale (ön plana çıkaran yumuşak siyah çerçeve)
        GameObject outerAuraObj = new GameObject("AllAroundSoftBlackEdge");
        outerAuraObj.transform.SetParent(bannerObj.transform, false);
        RectTransform outerRect = outerAuraObj.AddComponent<RectTransform>();
        outerRect.anchorMin = Vector2.zero;
        outerRect.anchorMax = Vector2.one;
        outerRect.offsetMin = new Vector2(-6f, -6f);
        outerRect.offsetMax = new Vector2(6f, 6f);

        warningOuterEdgeImage = outerAuraObj.AddComponent<Image>();
        if (spriteToUse != null)
        {
            warningOuterEdgeImage.sprite = spriteToUse;
            warningOuterEdgeImage.preserveAspect = true;
        }
        warningOuterEdgeImage.color = new Color(0f, 0f, 0f, 0.28f);

        Outline softOuterOutline = outerAuraObj.AddComponent<Outline>();
        softOuterOutline.effectColor = new Color(0f, 0f, 0f, 0.22f);
        softOuterOutline.effectDistance = new Vector2(3f, 3f);

        // 2. Asıl kapi_kilitli_0 görseli + dört kenarında eşit hafif siyah Outline
        GameObject mainImgObj = new GameObject("LockedGateMainGraphic");
        mainImgObj.transform.SetParent(bannerObj.transform, false);
        RectTransform mainImgRect = mainImgObj.AddComponent<RectTransform>();
        mainImgRect.anchorMin = Vector2.zero;
        mainImgRect.anchorMax = Vector2.one;
        mainImgRect.offsetMin = Vector2.zero;
        mainImgRect.offsetMax = Vector2.zero;

        warningBannerImage = mainImgObj.AddComponent<Image>();
        if (spriteToUse != null)
        {
            warningBannerImage.sprite = spriteToUse;
            warningBannerImage.preserveAspect = true;
            warningBannerImage.color = Color.white;
        }
        else
        {
            warningBannerImage.color = new Color(0.18f, 0.14f, 0.12f, 0.96f);
        }

        Outline allAroundEdge = mainImgObj.AddComponent<Outline>();
        allAroundEdge.effectColor = new Color(0f, 0f, 0f, 0.40f);
        allAroundEdge.effectDistance = new Vector2(2.5f, 2.5f);

        // 3. kapi_kilitli_0 görselinin sağ tarafındaki koyu taş oyuk metin alanı (sol tarafta kilit simgesi var)
        // X: 0.295 .. 0.885, Y: 0.28 .. 0.72
        GameObject textObj = new GameObject("WarningTextContainer");
        textObj.transform.SetParent(mainImgObj.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.295f, 0.28f);
        textRect.anchorMax = new Vector2(0.885f, 0.72f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        warningBodyText = textObj.AddComponent<TextMeshProUGUI>();
        TMP_FontAsset sharedFont = FindSharedFont();
        if (sharedFont != null)
        {
            warningBodyText.font = sharedFont;
        }

        warningBodyText.enableAutoSizing = true;
        warningBodyText.fontSizeMin = 16f;
        warningBodyText.fontSizeMax = 25f;
        warningBodyText.alignment = TextAlignmentOptions.Center;
        warningBodyText.color = new Color(0.95f, 0.91f, 0.84f, 1f);
        warningBodyText.textWrappingMode = TextWrappingModes.Normal;

        canvasObj.SetActive(false);
    }

    private static Sprite FindLockedGateBannerSprite()
    {
#if UNITY_EDITOR
        Object[] allAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(
            "Assets/Cainos/Pixel Art Top Down - Basic/Texture/kapi_kilitli.png");
        if (allAssets != null)
        {
            foreach (Object asset in allAssets)
            {
                if (asset is Sprite s)
                {
                    return s;
                }
            }
        }
#endif
        Sprite[] loadedSprites = Resources.FindObjectsOfTypeAll<Sprite>();
        foreach (Sprite s in loadedSprites)
        {
            if (s != null && (s.name == "kapi_kilitli_0" || s.name == "kapi_kilitli"))
            {
                return s;
            }
        }
        return null;
    }

    private static TMP_FontAsset FindSharedFont()
    {
        TextMeshProUGUI existingTmp = FindFirstObjectByType<TextMeshProUGUI>();
        if (existingTmp != null && existingTmp.font != null)
        {
            return existingTmp.font;
        }
        return TMP_Settings.defaultFontAsset;
    }
}
