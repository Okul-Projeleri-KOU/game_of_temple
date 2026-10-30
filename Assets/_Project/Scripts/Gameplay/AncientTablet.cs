using TMPro;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

public class AncientTablet : MonoBehaviour, IInteractable
{
    private const string IndicatorChildName = "InteractionIndicator";
    private const string LegacyFloatingCanvasName = "GravestoneFloatingTextCanvas";

    [TextArea(2, 5)]
    [SerializeField] private string clueText = "Kadim Tablet İpucusu: Kapıyı açmak için önce Rün Sunağı'nı uyandırmalısın.";

    [Tooltip("Mezarlık ve dikilitaş yazılarının içinde gösterileceği kitabe_0 görseli.")]
    [SerializeField] private Sprite kitabeSprite;

    [Tooltip("Sadece heykel (Statue) için kullanılacak kitabe_forheykel_0 görseli.")]
    [SerializeField] private Sprite statueKitabeSprite;

    private static int lastSyncedFrame = -1;

    // Ekranın ortasında açılan Kitabe (kitabe_0 & kitabe_forheykel_0) UI bileşenleri
    private static Canvas kitabeCanvas;
    private static CanvasGroup kitabeCanvasGroup;
    private static RectTransform kitabePanelRect;
    private static Image kitabeImage;
    private static Image kitabeOuterGlowImage;
    private static RectTransform kitabeTextRect;
    private static TextMeshProUGUI kitabeBodyText;
    private static AncientTablet activeKitabeTablet;
    private static bool isKitabeOpen;
    private static float kitabeOpenedTime;

#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
    private static void InitEditorTabletsSync()
    {
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (!Application.isPlaying)
            {
                SyncAllTabletsAndIndicators();
            }
        };
    }
#endif

    private void Awake()
    {
        if (lastSyncedFrame != Time.frameCount)
        {
            lastSyncedFrame = Time.frameCount;
            SyncAllTabletsAndIndicators();
        }

        Transform oldFloating = transform.Find(LegacyFloatingCanvasName);
        if (oldFloating != null)
        {
            Destroy(oldFloating.gameObject);
        }

        GameObject oldStatueCanvas = GameObject.Find("StatueCurseVisionCanvas");
        if (oldStatueCanvas != null)
        {
            Destroy(oldStatueCanvas);
        }

        EnsureSolidColliderOnObject(gameObject);
    }

    private void Update()
    {
        if (activeKitabeTablet == this)
        {
            UpdateKitabePopup();
        }
    }

    /// <summary>
    /// Oyuncu hareket ettiğinde açık olan kitabe okuma ekranını kapatır.
    /// </summary>
    public static void HideAllReadingsOnMove()
    {
        if (isKitabeOpen && Time.unscaledTime - kitabeOpenedTime > 0.15f)
        {
            isKitabeOpen = false;
        }
    }

    public static void SyncAllTabletsAndIndicators()
    {
        bool modified = false;
        GameObject dirtyRef = null;

        Sprite eSprite = FindInteractionSprite();
        Sprite loadedKitabeSprite = FindKitabeSprite();
        Sprite loadedStatueKitabeSprite = FindStatueKitabeSprite();

        // Sahneye yanlışlıkla dünya nesnesi olarak sürüklenmiş kitabe görselleri varsa gizle
        string[] looseNames = { "kitabe_0", "kitabe_forheykel_0", "kitabe", "kitabe_forheykel", "kapi_kilitli_0", "kapi_kilitli" };
        foreach (string looseName in looseNames)
        {
            GameObject looseObj = GameObject.Find(looseName);
            if (looseObj != null && looseObj.GetComponent<AncientTablet>() == null && looseObj.GetComponent<StoneGate>() == null)
            {
                SpriteRenderer kSr = looseObj.GetComponent<SpriteRenderer>();
                if (kSr != null && looseObj.activeSelf)
                {
                    looseObj.SetActive(false);
                    dirtyRef = looseObj;
                    modified = true;
                }
            }
        }

        AncientTablet[] tablets = Object.FindObjectsByType<AncientTablet>(FindObjectsSortMode.None);
        foreach (AncientTablet tablet in tablets)
        {
            if (tablet == null || tablet.gameObject == null)
                continue;

            GameObject obj = tablet.gameObject;
            dirtyRef = obj;

            if (loadedKitabeSprite != null && tablet.kitabeSprite != loadedKitabeSprite)
            {
                tablet.kitabeSprite = loadedKitabeSprite;
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(tablet);
#endif
                modified = true;
            }

            if (loadedStatueKitabeSprite != null && tablet.statueKitabeSprite != loadedStatueKitabeSprite)
            {
                tablet.statueKitabeSprite = loadedStatueKitabeSprite;
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(tablet);
#endif
                modified = true;
            }

            // 1. Z derinliği kaymış olan nesneleri z=0 düzlemine çek
            Vector3 pos = obj.transform.position;
            if (Mathf.Abs(pos.z) > 0.001f)
            {
                obj.transform.position = new Vector3(pos.x, pos.y, 0f);
                pos = obj.transform.position;
                modified = true;
            }

            // 2. Physics Layer = Default (0) yap
            if (obj.layer != 0)
            {
                obj.layer = 0;
                modified = true;
            }

            // 3. Bulunduğu zemine göre Sorting Layer'ı doğrula
            string groundSortingLayer = DetectGroundLayerAtPosition(pos);
            SpriteRenderer sr = obj.GetComponent<SpriteRenderer>();
            if (sr != null && !string.IsNullOrEmpty(groundSortingLayer))
            {
                if (sr.sortingLayerName != groundSortingLayer)
                {
                    sr.sortingLayerName = groundSortingLayer;
                    modified = true;
                }
            }

            // 4. Boş kalmış metinleri tamamla
            string trimmed = tablet.clueText != null ? tablet.clueText.Trim() : string.Empty;
            if (string.IsNullOrEmpty(trimmed) || trimmed == "Q")
            {
                string assignedText = GetFallbackTextForPosition(obj.name, pos);
                if (!string.IsNullOrEmpty(assignedText))
                {
                    tablet.clueText = assignedText;
#if UNITY_EDITOR
                    UnityEditor.EditorUtility.SetDirty(tablet);
#endif
                    modified = true;
                }
            }

            // 5. Katı Collider doğrula
            if (EnsureSolidColliderOnObject(obj))
            {
                modified = true;
            }

            // 6. InteractionIndicator ("E" simgesi) yoksa otomatik ekle
            if (EnsureInteractionIndicator(obj.transform, eSprite))
            {
                modified = true;
            }
        }

        TreasureChest[] chests = Object.FindObjectsByType<TreasureChest>(FindObjectsSortMode.None);
        foreach (TreasureChest chest in chests)
        {
            if (chest != null && chest.gameObject != null)
            {
                if (chest.gameObject.layer != 0)
                {
                    chest.gameObject.layer = 0;
                    dirtyRef = chest.gameObject;
                    modified = true;
                }

                Collider2D chestCol = chest.GetComponent<Collider2D>();
                if (chestCol != null && (!chestCol.enabled || chestCol.isTrigger))
                {
                    chestCol.enabled = true;
                    chestCol.isTrigger = false;
                    dirtyRef = chest.gameObject;
                    modified = true;
                }

                if (EnsureInteractionIndicator(chest.transform, eSprite))
                {
                    dirtyRef = chest.gameObject;
                    modified = true;
                }
            }
        }

#if UNITY_EDITOR
        if (modified && !Application.isPlaying && dirtyRef != null)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(dirtyRef.scene);
        }
#endif
    }

    private static bool EnsureSolidColliderOnObject(GameObject obj)
    {
        if (obj == null)
            return false;

        bool changed = false;

        if (obj.layer != 0)
        {
            obj.layer = 0;
            changed = true;
        }

        Collider2D[] allCols = obj.GetComponentsInChildren<Collider2D>(true);
        if (allCols == null || allCols.Length == 0)
        {
            BoxCollider2D added = obj.AddComponent<BoxCollider2D>();
            added.isTrigger = false;
            allCols = new Collider2D[] { added };
            changed = true;
        }

        foreach (Collider2D col in allCols)
        {
            if (col == null)
                continue;

            if (col.gameObject.layer != 0)
            {
                col.gameObject.layer = 0;
                changed = true;
            }

            if (!col.enabled || col.isTrigger)
            {
                col.enabled = true;
                col.isTrigger = false;
                changed = true;
            }
        }

        BoxCollider2D box = obj.GetComponent<BoxCollider2D>();
        if (box != null)
        {
            string n = obj.name;
            if (n.Contains("Gravestone"))
            {
                Vector2 targetSize = new Vector2(0.76f, 0.72f);
                Vector2 targetOffset = new Vector2(0f, 0.38f);
                if (Vector2.Distance(box.size, targetSize) > 0.04f || Vector2.Distance(box.offset, targetOffset) > 0.04f)
                {
                    box.size = targetSize;
                    box.offset = targetOffset;
                    changed = true;
                }
            }
            else if (n.Contains("Statue"))
            {
                Vector2 targetSize = new Vector2(0.95f, 1.15f);
                Vector2 targetOffset = new Vector2(0f, 0.58f);
                if (Vector2.Distance(box.size, targetSize) > 0.05f || Vector2.Distance(box.offset, targetOffset) > 0.05f)
                {
                    box.size = targetSize;
                    box.offset = targetOffset;
                    changed = true;
                }
            }
        }

        return changed;
    }

    private bool IsStatue()
    {
        return gameObject.name.Contains("Statue");
    }

    public void Interact()
    {
        ShowKitabePopup();
    }

    // =========================================================================
    // EKRANIN ORTASINDA KİTABE (kitabe_0 / kitabe_forheykel_0) + ÇEVRESEL HAFİF SİYAH KONTÜR
    // =========================================================================

    private void ShowKitabePopup()
    {
        bool isStatueObj = IsStatue();
        Sprite chosenSprite = isStatueObj
            ? (statueKitabeSprite != null ? statueKitabeSprite : FindStatueKitabeSprite())
            : (kitabeSprite != null ? kitabeSprite : FindKitabeSprite());

        if (chosenSprite == null)
        {
            chosenSprite = kitabeSprite != null ? kitabeSprite : FindKitabeSprite();
        }

        EnsureKitabeUI(chosenSprite);

        if (isKitabeOpen && activeKitabeTablet == this && Time.unscaledTime - kitabeOpenedTime > 0.15f)
        {
            isKitabeOpen = false;
            return;
        }

        activeKitabeTablet = this;
        isKitabeOpen = true;
        kitabeOpenedTime = Time.unscaledTime;

        if (kitabeImage != null && chosenSprite != null)
        {
            kitabeImage.sprite = chosenSprite;
        }

        if (kitabeOuterGlowImage != null && chosenSprite != null)
        {
            kitabeOuterGlowImage.sprite = chosenSprite;
        }

        if (kitabePanelRect != null)
        {
            kitabePanelRect.sizeDelta = isStatueObj
                ? new Vector2(1060f, 592f)
                : new Vector2(1020f, 684f);
            kitabePanelRect.localScale = new Vector3(0.88f, 0.88f, 1f);
        }

        if (kitabeTextRect != null)
        {
            if (isStatueObj)
            {
                kitabeTextRect.anchorMin = new Vector2(0.265f, 0.295f);
                kitabeTextRect.anchorMax = new Vector2(0.735f, 0.705f);
            }
            else
            {
                kitabeTextRect.anchorMin = new Vector2(0.245f, 0.295f);
                kitabeTextRect.anchorMax = new Vector2(0.755f, 0.645f);
            }
            kitabeTextRect.offsetMin = Vector2.zero;
            kitabeTextRect.offsetMax = Vector2.zero;
        }

        if (kitabeBodyText != null)
        {
            kitabeBodyText.text = clueText;
        }

        if (kitabeCanvas != null && !kitabeCanvas.gameObject.activeSelf)
        {
            kitabeCanvas.gameObject.SetActive(true);
        }
    }

    private void UpdateKitabePopup()
    {
        if (kitabeCanvas == null || kitabeCanvasGroup == null)
            return;

        if (isKitabeOpen)
        {
            bool movedAfterOpen = Time.unscaledTime - kitabeOpenedTime > 0.15f && IsPlayerMovingInput();
            if (Input.GetKeyDown(KeyCode.Escape) || movedAfterOpen)
            {
                isKitabeOpen = false;
            }
        }

        float targetAlpha = isKitabeOpen ? 1f : 0f;
        kitabeCanvasGroup.alpha = Mathf.MoveTowards(kitabeCanvasGroup.alpha, targetAlpha, Time.deltaTime * 6.5f);

        if (kitabeCanvasGroup.alpha > 0.01f)
        {
            if (kitabePanelRect != null)
            {
                float targetScale = isKitabeOpen ? 1f : 0.92f;
                float s = Mathf.Lerp(kitabePanelRect.localScale.x, targetScale, Time.deltaTime * 10f);
                kitabePanelRect.localScale = new Vector3(s, s, 1f);
            }
        }
        else if (kitabeCanvas.gameObject.activeSelf)
        {
            kitabeCanvas.gameObject.SetActive(false);
            if (activeKitabeTablet == this)
            {
                activeKitabeTablet = null;
            }
        }
    }

    private static void EnsureKitabeUI(Sprite spriteToUse)
    {
        if (kitabeCanvas != null)
        {
            if (kitabeImage != null && kitabeImage.sprite == null && spriteToUse != null)
            {
                kitabeImage.sprite = spriteToUse;
            }
            if (kitabeOuterGlowImage != null && kitabeOuterGlowImage.sprite == null && spriteToUse != null)
            {
                kitabeOuterGlowImage.sprite = spriteToUse;
            }
            return;
        }

        GameObject canvasObj = new GameObject("KitabeScreenCanvas");
        DontDestroyOnLoad(canvasObj);

        kitabeCanvas = canvasObj.AddComponent<Canvas>();
        kitabeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        kitabeCanvas.sortingOrder = 155;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        canvasObj.AddComponent<GraphicRaycaster>();

        // Ana taşıyıcı panel
        GameObject panelObj = new GameObject("Kitabe_Panel");
        panelObj.transform.SetParent(canvasObj.transform, false);

        kitabePanelRect = panelObj.AddComponent<RectTransform>();
        kitabePanelRect.anchorMin = new Vector2(0.5f, 0.5f);
        kitabePanelRect.anchorMax = new Vector2(0.5f, 0.5f);
        kitabePanelRect.pivot = new Vector2(0.5f, 0.5f);
        kitabePanelRect.anchoredPosition = Vector2.zero;
        kitabePanelRect.sizeDelta = new Vector2(1020f, 684f);

        kitabeCanvasGroup = panelObj.AddComponent<CanvasGroup>();
        kitabeCanvasGroup.alpha = 0f;
        kitabeCanvasGroup.interactable = false;
        kitabeCanvasGroup.blocksRaycasts = false;

        // 1. Tüm etrafını eşit saran hafif siyah dış hale (ön plana çıkaran yumuşak siyah çerçeve)
        GameObject outerAuraObj = new GameObject("AllAroundSoftBlackEdge");
        outerAuraObj.transform.SetParent(panelObj.transform, false);
        RectTransform outerRect = outerAuraObj.AddComponent<RectTransform>();
        outerRect.anchorMin = Vector2.zero;
        outerRect.anchorMax = Vector2.one;
        outerRect.offsetMin = new Vector2(-7f, -7f);
        outerRect.offsetMax = new Vector2(7f, 7f);

        kitabeOuterGlowImage = outerAuraObj.AddComponent<Image>();
        if (spriteToUse != null)
        {
            kitabeOuterGlowImage.sprite = spriteToUse;
            kitabeOuterGlowImage.preserveAspect = true;
        }
        kitabeOuterGlowImage.color = new Color(0f, 0f, 0f, 0.28f);

        Outline softOuterOutline = outerAuraObj.AddComponent<Outline>();
        softOuterOutline.effectColor = new Color(0f, 0f, 0f, 0.22f);
        softOuterOutline.effectDistance = new Vector2(3f, 3f);

        // 2. Asıl Kitabe Görseli + dört kenarında eşit hafif siyah Outline
        GameObject mainImgObj = new GameObject("KitabeMainGraphic");
        mainImgObj.transform.SetParent(panelObj.transform, false);
        RectTransform mainImgRect = mainImgObj.AddComponent<RectTransform>();
        mainImgRect.anchorMin = Vector2.zero;
        mainImgRect.anchorMax = Vector2.one;
        mainImgRect.offsetMin = Vector2.zero;
        mainImgRect.offsetMax = Vector2.zero;

        kitabeImage = mainImgObj.AddComponent<Image>();
        if (spriteToUse != null)
        {
            kitabeImage.sprite = spriteToUse;
            kitabeImage.preserveAspect = true;
            kitabeImage.color = Color.white;
        }
        else
        {
            kitabeImage.color = new Color(0.18f, 0.16f, 0.14f, 0.96f);
        }

        Outline allAroundEdge = mainImgObj.AddComponent<Outline>();
        allAroundEdge.effectColor = new Color(0f, 0f, 0f, 0.40f);
        allAroundEdge.effectDistance = new Vector2(2.5f, 2.5f);

        // 3. Kitabenin ortasındaki koyu taş oyuk alan
        GameObject textContainerObj = new GameObject("KitabeInnerTextArea");
        textContainerObj.transform.SetParent(mainImgObj.transform, false);

        kitabeTextRect = textContainerObj.AddComponent<RectTransform>();
        kitabeTextRect.anchorMin = new Vector2(0.245f, 0.295f);
        kitabeTextRect.anchorMax = new Vector2(0.755f, 0.645f);
        kitabeTextRect.offsetMin = Vector2.zero;
        kitabeTextRect.offsetMax = Vector2.zero;

        kitabeBodyText = textContainerObj.AddComponent<TextMeshProUGUI>();
        TMP_FontAsset sharedFont = FindSharedFont();
        if (sharedFont != null)
        {
            kitabeBodyText.font = sharedFont;
        }

        kitabeBodyText.enableAutoSizing = true;
        kitabeBodyText.fontSizeMin = 18f;
        kitabeBodyText.fontSizeMax = 27f;
        kitabeBodyText.fontStyle = FontStyles.Bold;
        kitabeBodyText.alignment = TextAlignmentOptions.Center;
        kitabeBodyText.color = new Color(0.93f, 0.89f, 0.80f, 1f);
        kitabeBodyText.textWrappingMode = TextWrappingModes.Normal;

        canvasObj.SetActive(false);
    }

    private static bool IsPlayerMovingInput()
    {
        return Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.01f ||
               Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.01f;
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

    private static Sprite FindKitabeSprite()
    {
#if UNITY_EDITOR
        Object[] allAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(
            "Assets/Cainos/Pixel Art Top Down - Basic/Texture/kitabe.png");
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
            if (s != null && (s.name == "kitabe_0" || s.name == "kitabe"))
            {
                return s;
            }
        }
        return null;
    }

    private static Sprite FindStatueKitabeSprite()
    {
#if UNITY_EDITOR
        Object[] allAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(
            "Assets/Cainos/Pixel Art Top Down - Basic/Texture/kitabe_forheykel.png");
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
            if (s != null && (s.name == "kitabe_forheykel_0" || s.name == "kitabe_forheykel"))
            {
                return s;
            }
        }
        return null;
    }

    private static string GetFallbackTextForPosition(string objName, Vector3 pos)
    {
        if (objName.Contains("Rune Pillar") || (pos.x > -5f && pos.x < -1f && pos.y > -3f && pos.y < -0.5f))
        {
            return "Kapıyı aşamayanların taşlaşmış hatıraları tapınağın dört bir yanına dağılmıştır. Son Muhafız'ın karşısına çıkmadan önce, düşenlerin son sözlerine kulak ver.";
        }

        if (pos.x < -6f)
        {
            return "Gezgin Bardo'nun mezarı: Muhafızın karşısında şakalar yapıp havadan sudan konuştum... Ciddiyetsizliğim yüzünden yüzüme bile bakmadan beni tapınaktan kovdu.";
        }

        if (pos.x > 1f && pos.x < 6f && pos.y > -2f && pos.y < 3f)
        {
            return "Muhafızın kalbini yumuşatmayı başarmıştım ama Kutsal Oda'nın anahtarı yanımda olmadığı için beni içeri almadı. Doğudaki dikenli geçidi aşacak kanatlı bir dostum olmadan kapının önünde bekleyip son nefesimi verdim.";
        }

        return "Burada Büyücü Doran yatıyor. Muhafıza sadece kendi merakım ve şöhretim için Kutsal Oda'ya girmek istediğimi söyledim... Sözlerim biter bitmez asasını yere vurdu ve beni karanlığa gömdü.";
    }

    private static bool EnsureInteractionIndicator(Transform parent, Sprite eSprite)
    {
        if (parent == null || eSprite == null)
            return false;

        Transform existing = parent.Find(IndicatorChildName);
        if (existing == null)
        {
            Transform[] children = parent.GetComponentsInChildren<Transform>(true);
            foreach (Transform c in children)
            {
                if (c != null && c.name == IndicatorChildName)
                {
                    existing = c;
                    break;
                }
            }
        }

        float indicatorY = CalculateIndicatorLocalY(parent);

        if (existing != null)
        {
            SpriteRenderer existingSr = existing.GetComponent<SpriteRenderer>();
            if (existingSr != null && existingSr.sprite == null)
            {
                existingSr.sprite = eSprite;
                return true;
            }
            return false;
        }

        GameObject indicatorObj = new GameObject(IndicatorChildName);
        indicatorObj.layer = 0;
        indicatorObj.transform.SetParent(parent, false);
        indicatorObj.transform.localPosition = new Vector3(0f, indicatorY, -0.09f);
        indicatorObj.transform.localRotation = Quaternion.identity;
        indicatorObj.transform.localScale = new Vector3(0.13f, 0.13f, 1f);

        SpriteRenderer sr = indicatorObj.AddComponent<SpriteRenderer>();
        sr.sprite = eSprite;
        sr.sortingLayerName = "Layer 3";
        sr.sortingOrder = 100;

        indicatorObj.SetActive(false);
        return true;
    }

    private static float CalculateIndicatorLocalY(Transform parent)
    {
        string n = parent.name;
        if (n.Contains("Rune Pillar") || n.Contains("Statue"))
        {
            return 2.25f;
        }
        if (n.Contains("Coffin"))
        {
            return 1.25f;
        }
        return 1.4f;
    }

    private static Sprite FindInteractionSprite()
    {
#if UNITY_EDITOR
        Sprite assetSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/Cainos/Pixel Art Top Down - Basic/Material/E_harfi-.png");
        if (assetSprite != null)
        {
            return assetSprite;
        }
#endif
        Transform[] allTransforms = Resources.FindObjectsOfTypeAll<Transform>();
        foreach (Transform t in allTransforms)
        {
            if (t != null && t.name == IndicatorChildName)
            {
                SpriteRenderer sr = t.GetComponent<SpriteRenderer>();
                if (sr != null && sr.sprite != null)
                {
                    return sr.sprite;
                }
            }
        }
        return null;
    }

    private static string DetectGroundLayerAtPosition(Vector3 worldPos)
    {
        string[] layerCandidates = { "Layer 3", "Layer 2", "Layer 1" };
        foreach (string candidate in layerCandidates)
        {
            GameObject grassObj = GameObject.Find(candidate + " - Grass");
            if (grassObj != null)
            {
                Tilemap tm = grassObj.GetComponent<Tilemap>();
                if (tm != null)
                {
                    Vector3Int cell = tm.WorldToCell(worldPos);
                    if (tm.HasTile(cell))
                    {
                        return candidate;
                    }
                }
            }
        }
        return null;
    }
}
