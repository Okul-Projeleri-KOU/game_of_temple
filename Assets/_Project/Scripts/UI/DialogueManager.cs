using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    [Header("Diyalog Paneli")]
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private TextMeshProUGUI dialogueText;

    [Header("Muhafız Sohbet Penceresi")]
    [SerializeField] private GameObject guardChatPanel;
    [SerializeField] private TextMeshProUGUI guardTitleText;
    [SerializeField] private TextMeshProUGUI guardStatusBadgeText;
    [SerializeField] private TextMeshProUGUI guardReplyText;
    [SerializeField] private GameObject thinkingIndicatorObj;
    [SerializeField] private TextMeshProUGUI thinkingIndicatorText;
    [SerializeField] private TMP_InputField playerInputField;
    [SerializeField] private Button sendButton;
    [SerializeField] private TextMeshProUGUI sendButtonText;
    [SerializeField] private Button closeButton;
    [SerializeField] private Image guardPortraitImage;

    [Header("Muhafız Görselleri")]
    [SerializeField] private Sprite guardNormalSprite;
    [SerializeField] private Sprite guardThinkingSprite;

    [Header("Durum HUD")]
    [SerializeField] private GameObject statusHudPanel;
    [SerializeField] private Image keyHudIcon;
    [SerializeField] private TextMeshProUGUI keyHudStateText;
    [SerializeField] private Image companionHudIcon;
    [SerializeField] private TextMeshProUGUI companionHudStateText;
    [SerializeField] private Sprite keyIconSprite;
    [SerializeField] private Sprite companionIconSprite;

    [Header("Hedef Kapı ve Kazanma Ekranı")]
    [SerializeField] private Sprite woodenGateClosedSprite;
    [SerializeField] private Sprite woodenGateOpenedSprite;
    [SerializeField] private Sprite woodenGateOpenedShadowSprite;
    [SerializeField] private GameObject victoryScreenPanel;
    [SerializeField] private Image victoryGateIcon;
    [SerializeField] private Image victoryKeyIcon;
    [SerializeField] private Button victoryRestartButton;
    [SerializeField] private Button victoryContinueButton;

    private LLMManager llmManager;
    private CompanionAgent cachedCompanionAgent;
    private System.Reflection.FieldInfo companionStateField;
    private SpriteRenderer worldGuardRenderer;
    private Vector3 worldGuardBaseLocalScale = Vector3.zero;
    private bool isWaitingForLLM;
    private bool isTargetDoorOpened;
    private float thinkingAnimTimer;
    private Coroutine typewriterCoroutine;
    private Coroutine victorySequenceCoroutine;

    public bool IsGuardChatOpen => guardChatPanel != null && guardChatPanel.activeSelf;

    public bool IsVictoryScreenOpen => victoryScreenPanel != null && victoryScreenPanel.activeSelf;

#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
    private static void InitEditorGuardChatUI()
    {
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (!Application.isPlaying)
            {
                DialogueManager dm = Object.FindFirstObjectByType<DialogueManager>();
                if (dm != null)
                {
                    dm.EnsureSpritesLoaded();
                    dm.EnsureGuardChatUI();
                    dm.EnsureStatusHudUI();
                    dm.EnsureVictoryScreenUI();
                    dm.EnsureGuardInteractableInScene();
                }
            }
        };
    }
#endif

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        EnsureSpritesLoaded();
        EnsureLLMManager();
        EnsureGuardChatUI();
        EnsureStatusHudUI();
        EnsureVictoryScreenUI();
        EnsureGuardInteractableInScene();

        HideDialogue();
        CloseGuardChat();
        HideVictoryScreen();
        RefreshStatusHud();
    }

    private void Update()
    {
        RefreshStatusHud();

        if (IsVictoryScreenOpen)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                HideVictoryScreen();
            }
            return;
        }

        if (IsGuardChatOpen)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CloseGuardChat();
                return;
            }

            if (!isWaitingForLLM && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
            {
                if (playerInputField != null && !string.IsNullOrWhiteSpace(playerInputField.text))
                {
                    OnSendButtonClicked();
                }
            }

            if (isWaitingForLLM && thinkingIndicatorText != null)
            {
                thinkingAnimTimer += Time.unscaledDeltaTime;
                int dots = (Mathf.FloorToInt(thinkingAnimTimer * 3f) % 3) + 1;
                thinkingIndicatorText.text = "Muhafız düşünüyor" + new string('.', dots);
            }

            return;
        }

        if (dialoguePanel != null && dialoguePanel.activeSelf && Input.GetKeyDown(KeyCode.Escape))
        {
            HideDialogue();
        }
    }


    public void OpenGuardChat()
    {
        EnsureSpritesLoaded();
        EnsureLLMManager();
        EnsureGuardChatUI();

        AncientTablet.HideAllReadingsOnMove();

        if (dialoguePanel != null && dialoguePanel != guardChatPanel)
        {
            dialoguePanel.SetActive(false);
        }

        if (guardChatPanel != null)
        {
            guardChatPanel.SetActive(true);
        }

        SetThinkingState(false);
        UpdateStatusBadge("DİNLİYOR", new Color(0.92f, 0.78f, 0.42f, 1f));

        if (guardReplyText != null && string.IsNullOrEmpty(guardReplyText.text))
        {
            ShowGuardReplyTypewriter("Dur yabancı! Kutsal Oda'nın eşiğindesin. Neden burada olduğunu ve tapınağın emanetine niyetini söyle...");
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetGameState(GameState.Dialogue);
        }

        if (playerInputField != null)
        {
            playerInputField.text = string.Empty;
            playerInputField.interactable = true;
            playerInputField.Select();
            playerInputField.ActivateInputField();
        }
    }

    public void CloseGuardChat()
    {
        isWaitingForLLM = false;
        SetWorldGuardThinkingSprite(false);

        if (guardChatPanel != null)
        {
            guardChatPanel.SetActive(false);
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetGameState(GameState.Playing);
        }
    }

    public void OnSendButtonClicked()
    {
        if (isWaitingForLLM || playerInputField == null)
            return;

        string userMessage = playerInputField.text != null ? playerInputField.text.Trim() : string.Empty;
        if (string.IsNullOrEmpty(userMessage))
            return;

        playerInputField.text = string.Empty;
        SetThinkingState(true);
        UpdateStatusBadge("DÜŞÜNÜYOR...", new Color(0.95f, 0.65f, 0.25f, 1f));

        if (guardReplyText != null)
        {
            if (typewriterCoroutine != null)
            {
                StopCoroutine(typewriterCoroutine);
            }
            guardReplyText.color = new Color(0.78f, 0.75f, 0.68f, 0.85f);
            guardReplyText.text = $"<i>Sen: \"{userMessage}\"</i>";
        }

        EnsureLLMManager();
        llmManager.AskLlama(userMessage, OnLLMResponseReceived);
    }

    private void OnLLMResponseReceived(NPCResponseDTO response)
    {
        SetThinkingState(false);

        if (response == null)
        {
            response = new NPCResponseDTO
            {
                dialogue = "Muhafız derin bir transa geçmiş gibi görünüyor ve şu an sesini duymuyor... [Yerel LLM Bağlantı Hatası - Lütfen Ollama servisini kontrol edin]",
                action = "WAIT"
            };
        }

        string reply = !string.IsNullOrWhiteSpace(response.dialogue)
            ? response.dialogue
            : "Muhafız sessizce gözlerinin içine bakıyor...";

        string action = !string.IsNullOrWhiteSpace(response.action)
            ? response.action.Trim().ToUpperInvariant()
            : "WAIT";

        if (action == "KILL_PLAYER")
        {
            UpdateStatusBadge("ÖFKELENDİ (KILL_PLAYER)", new Color(1f, 0.32f, 0.28f, 1f));
            if (guardReplyText != null)
            {
                guardReplyText.color = new Color(1f, 0.45f, 0.40f, 1f);
            }
            SetWorldGuardThinkingSprite(true);
            ShowGuardReplyTypewriter(reply);
            StartCoroutine(HandleKillPlayerSequence());
        }
        else if (action == "OPEN_DOOR")
        {
            bool hasKey = GameManager.Instance != null && GameManager.Instance.HasKey;
            if (hasKey)
            {
                UpdateStatusBadge("İKNA OLDU & KAPI AÇILDI (OPEN_DOOR)", new Color(0.40f, 0.92f, 0.52f, 1f));
                if (guardReplyText != null)
                {
                    guardReplyText.color = new Color(0.65f, 0.96f, 0.72f, 1f);
                }
                ShowGuardReplyTypewriter(reply + "\n\n<color=#72F594>[Kutsal Anahtar onaylandı — Tapınağın Hedef Kapısı açıldı!]</color>");
                HandleOpenDoorAction();
            }
            else
            {
                UpdateStatusBadge("ANAHTAR EKSİK! (ÖNCE ANAHTARI BUL)", new Color(1f, 0.72f, 0.25f, 1f));
                if (guardReplyText != null)
                {
                    guardReplyText.color = new Color(1f, 0.85f, 0.52f, 1f);
                }
                ShowGuardReplyTypewriter(
                    reply +
                    "\n\n<color=#FFBE44>[Muhafız sözlerine ikna oldu (OPEN_DOOR), ancak envanterinde 'Kutsal Anahtar' (HasKey = false) olmadığı için kapının kilidini açamıyor! Önce Otonom Yoldaş'ı (SPACE) doğu terasındaki sandığa gönderip anahtarı almalısın.]</color>");

                if (playerInputField != null && IsGuardChatOpen)
                {
                    playerInputField.Select();
                    playerInputField.ActivateInputField();
                }
            }
        }
        else
        {
            UpdateStatusBadge("BEKLİYOR (WAIT)", new Color(0.92f, 0.78f, 0.42f, 1f));
            if (guardReplyText != null)
            {
                guardReplyText.color = new Color(0.95f, 0.91f, 0.83f, 1f);
            }
            ShowGuardReplyTypewriter(reply);

            if (playerInputField != null && IsGuardChatOpen)
            {
                playerInputField.Select();
                playerInputField.ActivateInputField();
            }
        }
    }

    private IEnumerator HandleKillPlayerSequence()
    {
        if (playerInputField != null)
            playerInputField.interactable = false;
        if (sendButton != null)
            sendButton.interactable = false;

        yield return new WaitForSecondsRealtime(2.4f);

        SetWorldGuardThinkingSprite(false);
        CloseGuardChat();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.RespawnPlayer();
        }
    }

    public void HandleOpenDoorAction()
    {
        EnsureSpritesLoaded();
        isTargetDoorOpened = true;

        GameObject targetWoodenGate = FindTargetWoodenGate();
        if (targetWoodenGate != null)
        {
            SpriteRenderer gateSr = targetWoodenGate.GetComponent<SpriteRenderer>();
            if (gateSr != null && woodenGateOpenedSprite != null)
            {
                if (woodenGateClosedSprite == null)
                {
                    woodenGateClosedSprite = gateSr.sprite;
                }
                gateSr.sprite = woodenGateOpenedSprite;
            }

            Transform shadowTr = targetWoodenGate.transform.Find("Shadow");
            if (shadowTr != null && woodenGateOpenedShadowSprite != null)
            {
                SpriteRenderer shadowSr = shadowTr.GetComponent<SpriteRenderer>();
                if (shadowSr != null)
                {
                    shadowSr.sprite = woodenGateOpenedShadowSprite;
                }
            }

            Collider2D[] cols = targetWoodenGate.GetComponents<Collider2D>();
            foreach (Collider2D col in cols)
            {
                if (col != null)
                {
                    col.isTrigger = true;
                }
            }

            if (targetWoodenGate.GetComponent<VictoryGateTrigger>() == null)
            {
                targetWoodenGate.AddComponent<VictoryGateTrigger>();
            }
        }

        StoneGate[] gates = FindObjectsByType<StoneGate>(FindObjectsSortMode.None);
        foreach (StoneGate gate in gates)
        {
            if (gate != null && !gate.IsOpen)
            {
                gate.OpenGate();
            }
        }

        if (victorySequenceCoroutine != null)
        {
            StopCoroutine(victorySequenceCoroutine);
        }
        victorySequenceCoroutine = StartCoroutine(HandleVictorySequence());
    }

    private IEnumerator HandleVictorySequence()
    {
        if (playerInputField != null)
            playerInputField.interactable = false;
        if (sendButton != null)
            sendButton.interactable = false;

        yield return new WaitForSecondsRealtime(2.5f);

        CloseGuardChat();
        ShowVictoryScreen();
        victorySequenceCoroutine = null;
    }

    private GameObject FindTargetWoodenGate()
    {
        GameObject byName = GameObject.Find("PF Props - Wooden Gate 01");
        if (byName != null)
            return byName;

        SpriteRenderer[] allSrs = FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
        foreach (SpriteRenderer sr in allSrs)
        {
            if (sr != null && sr.gameObject != null &&
                (sr.gameObject.name.Contains("Wooden Gate") ||
                 (sr.sprite != null && sr.sprite.name.Contains("Wooden Gate"))))
            {
                return sr.gameObject;
            }
        }

        return null;
    }

    private void SetThinkingState(bool thinking)
    {
        isWaitingForLLM = thinking;
        thinkingAnimTimer = 0f;

        if (thinkingIndicatorObj != null)
        {
            thinkingIndicatorObj.SetActive(thinking);
        }

        if (playerInputField != null)
        {
            playerInputField.interactable = !thinking;
        }

        if (sendButton != null)
        {
            sendButton.interactable = !thinking;
        }

        if (sendButtonText != null)
        {
            sendButtonText.text = thinking ? "BEKLENİYOR" : "GÖNDER";
        }

        if (guardPortraitImage != null)
        {
            Sprite targetSprite = thinking
                ? (guardThinkingSprite != null ? guardThinkingSprite : guardNormalSprite)
                : (guardNormalSprite != null ? guardNormalSprite : guardThinkingSprite);

            if (targetSprite != null)
            {
                guardPortraitImage.sprite = targetSprite;
            }
        }

        SetWorldGuardThinkingSprite(thinking);
    }

    private void SetWorldGuardThinkingSprite(bool thinking)
    {
        if (worldGuardRenderer == null)
        {
            GameObject guardObj = GameObject.Find("muhafız - normal_0");
            if (guardObj != null)
            {
                worldGuardRenderer = guardObj.GetComponent<SpriteRenderer>();
            }
        }

        if (worldGuardRenderer != null)
        {
            if (worldGuardBaseLocalScale == Vector3.zero)
            {
                worldGuardBaseLocalScale = worldGuardRenderer.transform.localScale;
            }

            Sprite s = thinking ? guardThinkingSprite : guardNormalSprite;
            if (s != null)
            {
                worldGuardRenderer.sprite = s;

                if (thinking && guardNormalSprite != null && s.bounds.size.y > 0.001f)
                {
                    float heightRatio = guardNormalSprite.bounds.size.y / s.bounds.size.y;
                    worldGuardRenderer.transform.localScale = new Vector3(
                        worldGuardBaseLocalScale.x * heightRatio,
                        worldGuardBaseLocalScale.y * heightRatio,
                        worldGuardBaseLocalScale.z);
                }
                else
                {
                    worldGuardRenderer.transform.localScale = worldGuardBaseLocalScale;
                }
            }
        }
    }

    private void UpdateStatusBadge(string status, Color color)
    {
        if (guardStatusBadgeText != null)
        {
            guardStatusBadgeText.text = status;
            guardStatusBadgeText.color = color;
        }
    }

    private void ShowGuardReplyTypewriter(string fullText)
    {
        if (guardReplyText == null)
            return;

        if (typewriterCoroutine != null)
        {
            StopCoroutine(typewriterCoroutine);
        }

        if (!gameObject.activeInHierarchy)
        {
            guardReplyText.text = fullText;
            return;
        }

        typewriterCoroutine = StartCoroutine(TypewriterRoutine(fullText));
    }

    private IEnumerator TypewriterRoutine(string fullText)
    {
        guardReplyText.text = string.Empty;
        for (int i = 0; i < fullText.Length; i++)
        {
            guardReplyText.text = fullText.Substring(0, i + 1);
            yield return new WaitForSecondsRealtime(0.016f);
        }
        typewriterCoroutine = null;
    }


    public void ShowDialogue(string text)
    {
        if (dialogueText != null)
        {
            dialogueText.text = text;
        }

        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(true);
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetGameState(GameState.Dialogue);
        }
    }

    public void HideDialogue()
    {
        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
        }

        if (dialogueText != null)
        {
            dialogueText.text = string.Empty;
        }

        if (!IsGuardChatOpen && GameManager.Instance != null)
        {
            GameManager.Instance.SetGameState(GameState.Playing);
        }
    }


    private void EnsureLLMManager()
    {
        if (llmManager == null)
        {
            llmManager = FindFirstObjectByType<LLMManager>();
            if (llmManager == null)
            {
                llmManager = gameObject.GetComponent<LLMManager>();
                if (llmManager == null)
                {
                    llmManager = gameObject.AddComponent<LLMManager>();
                }
            }
        }
    }

    private void EnsureGuardInteractableInScene()
    {
        GameObject guardSpriteObj = GameObject.Find("muhafız - normal_0");
        if (guardSpriteObj != null)
        {
            if (worldGuardRenderer == null)
            {
                worldGuardRenderer = guardSpriteObj.GetComponent<SpriteRenderer>();
            }

            GuardDialogueTrigger trigger = guardSpriteObj.GetComponent<GuardDialogueTrigger>();
            if (trigger == null)
            {
                trigger = guardSpriteObj.AddComponent<GuardDialogueTrigger>();
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    UnityEditor.EditorUtility.SetDirty(guardSpriteObj);
                }
#endif
            }
            trigger.SyncGuardIndicatorTransform();
        }

        GuardAgent guardAgent = FindFirstObjectByType<GuardAgent>();
        if (guardAgent != null)
        {
            Transform duplicateIndicator = guardAgent.transform.Find("InteractionIndicator");
            if (duplicateIndicator != null)
            {
                if (Application.isPlaying)
                    Destroy(duplicateIndicator.gameObject);
                else
                    DestroyImmediate(duplicateIndicator.gameObject);
            }

            if (guardAgent.GetComponent<GuardDialogueTrigger>() == null)
            {
                guardAgent.gameObject.AddComponent<GuardDialogueTrigger>();
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    UnityEditor.EditorUtility.SetDirty(guardAgent.gameObject);
                }
#endif
            }
        }

        Cainos.PixelArtTopDown_Basic.StairsLayerTrigger.Configure2DNavMeshAgents();
    }

    private void EnsureSpritesLoaded()
    {
#if UNITY_EDITOR
        if (guardNormalSprite == null)
        {
            Object[] normalAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(
                "Assets/Cainos/Pixel Art Top Down - Basic/Texture/muhafız - normal.png");
            if (normalAssets != null)
            {
                foreach (Object a in normalAssets)
                {
                    if (a is Sprite s)
                    {
                        guardNormalSprite = s;
                        break;
                    }
                }
            }
        }

        if (guardThinkingSprite == null)
        {
            Object[] thinkingAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(
                "Assets/Cainos/Pixel Art Top Down - Basic/Texture/muhafız - dusunuyor.png");
            if (thinkingAssets != null)
            {
                foreach (Object a in thinkingAssets)
                {
                    if (a is Sprite s)
                    {
                        guardThinkingSprite = s;
                        break;
                    }
                }
            }
        }

        if (keyIconSprite == null)
        {
            Object[] keyAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(
                "Assets/Cainos/Pixel Art Top Down - Basic/Texture/anahtar.png");
            if (keyAssets != null)
            {
                foreach (Object a in keyAssets)
                {
                    if (a is Sprite s)
                    {
                        keyIconSprite = s;
                        break;
                    }
                }
            }
        }

        if (companionIconSprite == null)
        {
            string[] owlPaths =
            {
                "Assets/_Project/Sprites/görsel_2026-10-31_130739059-removebg-preview.png",
                "Assets/_Project/Sprites/Baykuş Yoldaş Temizlenmiş.png"
            };
            foreach (string owlPath in owlPaths)
            {
                Object[] owlAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(owlPath);
                if (owlAssets != null)
                {
                    foreach (Object a in owlAssets)
                    {
                        if (a is Sprite s)
                        {
                            companionIconSprite = s;
                            break;
                        }
                    }
                }
                if (companionIconSprite != null)
                    break;
            }
        }

        if (woodenGateClosedSprite == null || woodenGateOpenedSprite == null)
        {
            Object[] propsAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(
                "Assets/Cainos/Pixel Art Top Down - Basic/Texture/TX Props.png");
            if (propsAssets != null)
            {
                foreach (Object a in propsAssets)
                {
                    if (a is Sprite s)
                    {
                        if (s.name == "TX Props Wooden Gate")
                            woodenGateClosedSprite = s;
                        else if (s.name == "TX Props Wooden Gate Opened")
                            woodenGateOpenedSprite = s;
                    }
                }
            }
        }

        if (woodenGateOpenedShadowSprite == null)
        {
            Object[] shadowAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(
                "Assets/Cainos/Pixel Art Top Down - Basic/Texture/TX Shadow.png");
            if (shadowAssets != null)
            {
                foreach (Object a in shadowAssets)
                {
                    if (a is Sprite s && s.name == "TX Shadow Wooden Gate Opened")
                    {
                        woodenGateOpenedShadowSprite = s;
                        break;
                    }
                }
            }
        }
#endif
        if (guardNormalSprite == null || guardThinkingSprite == null ||
            keyIconSprite == null || companionIconSprite == null ||
            woodenGateClosedSprite == null || woodenGateOpenedSprite == null || woodenGateOpenedShadowSprite == null)
        {
            Sprite[] allSprites = Resources.FindObjectsOfTypeAll<Sprite>();
            foreach (Sprite s in allSprites)
            {
                if (s == null)
                    continue;
                if (guardNormalSprite == null && s.name.Contains("muhafız - normal"))
                    guardNormalSprite = s;
                if (guardThinkingSprite == null && s.name.Contains("muhafız - dusunuyor"))
                    guardThinkingSprite = s;
                if (keyIconSprite == null && s.name.Contains("anahtar"))
                    keyIconSprite = s;
                if (companionIconSprite == null && (s.name.Contains("removebg") || s.name.Contains("Baykuş")))
                    companionIconSprite = s;
                if (woodenGateClosedSprite == null && s.name == "TX Props Wooden Gate")
                    woodenGateClosedSprite = s;
                if (woodenGateOpenedSprite == null && s.name == "TX Props Wooden Gate Opened")
                    woodenGateOpenedSprite = s;
                if (woodenGateOpenedShadowSprite == null && s.name == "TX Shadow Wooden Gate Opened")
                    woodenGateOpenedShadowSprite = s;
            }
        }
    }

    public void EnsureGuardChatUI()
    {
        if (FindFirstObjectByType<EventSystem>() == null && Application.isPlaying)
        {
            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<EventSystem>();
            esObj.AddComponent<StandaloneInputModule>();
        }

        if (guardChatPanel != null && playerInputField != null && sendButton != null && thinkingIndicatorObj != null)
        {
            BindUIEvents();
            return;
        }

        Canvas targetCanvas = null;
        if (dialoguePanel != null)
        {
            targetCanvas = dialoguePanel.GetComponentInParent<Canvas>();
        }
        if (targetCanvas == null)
        {
            GameObject canvasObj = GameObject.Find("Canvas");
            if (canvasObj != null)
            {
                targetCanvas = canvasObj.GetComponent<Canvas>();
            }
        }
        if (targetCanvas == null)
        {
            targetCanvas = FindFirstObjectByType<Canvas>();
        }
        if (targetCanvas == null)
            return;

        CanvasScaler scaler = targetCanvas.GetComponent<CanvasScaler>();
        if (scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ConstantPixelSize)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
        }

        Transform existingChat = targetCanvas.transform.Find("GuardChatWindow");
        if (existingChat != null)
        {
            guardChatPanel = existingChat.gameObject;
            playerInputField = guardChatPanel.GetComponentInChildren<TMP_InputField>(true);
            Transform sendTr = guardChatPanel.transform.Find("MainFrame/InputRow/SendButton");
            if (sendTr != null)
            {
                sendButton = sendTr.GetComponent<Button>();
                sendButtonText = sendTr.GetComponentInChildren<TextMeshProUGUI>(true);
            }
            Transform closeTr = guardChatPanel.transform.Find("MainFrame/HeaderBar/CloseButton");
            if (closeTr != null)
            {
                closeButton = closeTr.GetComponent<Button>();
            }
            Transform replyTr = guardChatPanel.transform.Find("MainFrame/ContentArea/ReplyBox/GuardReplyText");
            if (replyTr != null)
            {
                guardReplyText = replyTr.GetComponent<TextMeshProUGUI>();
            }
            Transform thinkTr = guardChatPanel.transform.Find("MainFrame/ContentArea/ReplyBox/ThinkingIndicator");
            if (thinkTr != null)
            {
                thinkingIndicatorObj = thinkTr.gameObject;
                thinkingIndicatorText = thinkTr.GetComponentInChildren<TextMeshProUGUI>(true);
            }
            Transform badgeTr = guardChatPanel.transform.Find("MainFrame/HeaderBar/StatusBadgeText");
            if (badgeTr != null)
            {
                guardStatusBadgeText = badgeTr.GetComponent<TextMeshProUGUI>();
            }
            Transform portraitTr = guardChatPanel.transform.Find("MainFrame/ContentArea/PortraitFrame/PortraitImage");
            if (portraitTr != null)
            {
                guardPortraitImage = portraitTr.GetComponent<Image>();
            }

            if (playerInputField != null && sendButton != null && thinkingIndicatorObj != null)
            {
                BindUIEvents();
                return;
            }

            DestroyImmediate(existingChat.gameObject);
        }

        TMP_FontAsset sharedFont = FindSharedFont();

        guardChatPanel = new GameObject("GuardChatWindow");
        guardChatPanel.layer = 5;
        guardChatPanel.transform.SetParent(targetCanvas.transform, false);

        RectTransform rootRect = guardChatPanel.AddComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 0.04f);
        rootRect.anchorMax = new Vector2(0.5f, 0.04f);
        rootRect.pivot = new Vector2(0.5f, 0f);
        rootRect.anchoredPosition = Vector2.zero;
        rootRect.sizeDelta = new Vector2(960f, 345f);

        GameObject outerEdgeObj = new GameObject("AllAroundSoftBlackEdge");
        outerEdgeObj.transform.SetParent(guardChatPanel.transform, false);
        RectTransform outerEdgeRect = outerEdgeObj.AddComponent<RectTransform>();
        outerEdgeRect.anchorMin = Vector2.zero;
        outerEdgeRect.anchorMax = Vector2.one;
        outerEdgeRect.offsetMin = new Vector2(-7f, -7f);
        outerEdgeRect.offsetMax = new Vector2(7f, 7f);

        Image outerEdgeImg = outerEdgeObj.AddComponent<Image>();
        outerEdgeImg.color = new Color(0f, 0f, 0f, 0.42f);

        GameObject borderObj = new GameObject("StoneGoldBorder");
        borderObj.transform.SetParent(guardChatPanel.transform, false);
        RectTransform borderRect = borderObj.AddComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.offsetMin = Vector2.zero;
        borderRect.offsetMax = Vector2.zero;

        Image borderImg = borderObj.AddComponent<Image>();
        borderImg.color = new Color(0.62f, 0.52f, 0.34f, 0.96f);

        GameObject mainFrameObj = new GameObject("MainFrame");
        mainFrameObj.transform.SetParent(guardChatPanel.transform, false);
        RectTransform mainFrameRect = mainFrameObj.AddComponent<RectTransform>();
        mainFrameRect.anchorMin = Vector2.zero;
        mainFrameRect.anchorMax = Vector2.one;
        mainFrameRect.offsetMin = new Vector2(3f, 3f);
        mainFrameRect.offsetMax = new Vector2(-3f, -3f);

        Image mainFrameImg = mainFrameObj.AddComponent<Image>();
        mainFrameImg.color = new Color(0.09f, 0.09f, 0.11f, 0.96f);

        GameObject headerObj = new GameObject("HeaderBar");
        headerObj.transform.SetParent(mainFrameObj.transform, false);
        RectTransform headerRect = headerObj.AddComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 0.83f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.offsetMin = Vector2.zero;
        headerRect.offsetMax = Vector2.zero;

        Image headerBg = headerObj.AddComponent<Image>();
        headerBg.color = new Color(0.14f, 0.13f, 0.16f, 0.98f);

        GameObject titleObj = new GameObject("GuardTitleText");
        titleObj.transform.SetParent(headerObj.transform, false);
        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.025f, 0f);
        titleRect.anchorMax = new Vector2(0.55f, 1f);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;

        guardTitleText = titleObj.AddComponent<TextMeshProUGUI>();
        if (sharedFont != null)
            guardTitleText.font = sharedFont;
        guardTitleText.fontSize = 22f;
        guardTitleText.fontStyle = FontStyles.Bold;
        guardTitleText.alignment = TextAlignmentOptions.MidlineLeft;
        guardTitleText.color = new Color(0.95f, 0.80f, 0.44f, 1f);
        guardTitleText.text = "KADİM TAPINAK MUHAFIZI";

        GameObject badgeObj = new GameObject("StatusBadgeText");
        badgeObj.transform.SetParent(headerObj.transform, false);
        RectTransform badgeRect = badgeObj.AddComponent<RectTransform>();
        badgeRect.anchorMin = new Vector2(0.55f, 0f);
        badgeRect.anchorMax = new Vector2(0.84f, 1f);
        badgeRect.offsetMin = Vector2.zero;
        badgeRect.offsetMax = Vector2.zero;

        guardStatusBadgeText = badgeObj.AddComponent<TextMeshProUGUI>();
        if (sharedFont != null)
            guardStatusBadgeText.font = sharedFont;
        guardStatusBadgeText.fontSize = 17f;
        guardStatusBadgeText.fontStyle = FontStyles.Bold;
        guardStatusBadgeText.alignment = TextAlignmentOptions.MidlineRight;
        guardStatusBadgeText.color = new Color(0.92f, 0.78f, 0.42f, 1f);
        guardStatusBadgeText.text = "DİNLİYOR";

        GameObject closeBtnObj = new GameObject("CloseButton");
        closeBtnObj.transform.SetParent(headerObj.transform, false);
        RectTransform closeBtnRect = closeBtnObj.AddComponent<RectTransform>();
        closeBtnRect.anchorMin = new Vector2(0.86f, 0.15f);
        closeBtnRect.anchorMax = new Vector2(0.985f, 0.85f);
        closeBtnRect.offsetMin = Vector2.zero;
        closeBtnRect.offsetMax = Vector2.zero;

        Image closeBtnImg = closeBtnObj.AddComponent<Image>();
        closeBtnImg.color = new Color(0.32f, 0.16f, 0.16f, 0.95f);

        closeButton = closeBtnObj.AddComponent<Button>();
        closeButton.targetGraphic = closeBtnImg;

        GameObject closeTxtObj = new GameObject("CloseText");
        closeTxtObj.transform.SetParent(closeBtnObj.transform, false);
        RectTransform closeTxtRect = closeTxtObj.AddComponent<RectTransform>();
        closeTxtRect.anchorMin = Vector2.zero;
        closeTxtRect.anchorMax = Vector2.one;
        closeTxtRect.offsetMin = Vector2.zero;
        closeTxtRect.offsetMax = Vector2.zero;

        TextMeshProUGUI closeTmp = closeTxtObj.AddComponent<TextMeshProUGUI>();
        if (sharedFont != null)
            closeTmp.font = sharedFont;
        closeTmp.fontSize = 16f;
        closeTmp.fontStyle = FontStyles.Bold;
        closeTmp.alignment = TextAlignmentOptions.Center;
        closeTmp.color = new Color(0.96f, 0.85f, 0.85f, 1f);
        closeTmp.text = "KAPAT [ESC]";

        GameObject contentObj = new GameObject("ContentArea");
        contentObj.transform.SetParent(mainFrameObj.transform, false);
        RectTransform contentRect = contentObj.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0.02f, 0.25f);
        contentRect.anchorMax = new Vector2(0.98f, 0.80f);
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;

        GameObject portraitFrameObj = new GameObject("PortraitFrame");
        portraitFrameObj.transform.SetParent(contentObj.transform, false);
        RectTransform portraitFrameRect = portraitFrameObj.AddComponent<RectTransform>();
        portraitFrameRect.anchorMin = new Vector2(0f, 0.04f);
        portraitFrameRect.anchorMax = new Vector2(0.18f, 0.96f);
        portraitFrameRect.offsetMin = Vector2.zero;
        portraitFrameRect.offsetMax = Vector2.zero;

        Image portraitBg = portraitFrameObj.AddComponent<Image>();
        portraitBg.color = new Color(0.15f, 0.14f, 0.18f, 0.98f);

        Outline portraitOutline = portraitFrameObj.AddComponent<Outline>();
        portraitOutline.effectColor = new Color(0.55f, 0.45f, 0.28f, 0.85f);
        portraitOutline.effectDistance = new Vector2(2f, 2f);

        GameObject portraitImgObj = new GameObject("PortraitImage");
        portraitImgObj.transform.SetParent(portraitFrameObj.transform, false);
        RectTransform portraitImgRect = portraitImgObj.AddComponent<RectTransform>();
        portraitImgRect.anchorMin = new Vector2(0.08f, 0.08f);
        portraitImgRect.anchorMax = new Vector2(0.92f, 0.92f);
        portraitImgRect.offsetMin = Vector2.zero;
        portraitImgRect.offsetMax = Vector2.zero;

        guardPortraitImage = portraitImgObj.AddComponent<Image>();
        guardPortraitImage.preserveAspect = true;
        if (guardNormalSprite != null)
        {
            guardPortraitImage.sprite = guardNormalSprite;
        }

        GameObject replyBoxObj = new GameObject("ReplyBox");
        replyBoxObj.transform.SetParent(contentObj.transform, false);
        RectTransform replyBoxRect = replyBoxObj.AddComponent<RectTransform>();
        replyBoxRect.anchorMin = new Vector2(0.20f, 0.04f);
        replyBoxRect.anchorMax = new Vector2(1f, 0.96f);
        replyBoxRect.offsetMin = Vector2.zero;
        replyBoxRect.offsetMax = Vector2.zero;

        Image replyBoxBg = replyBoxObj.AddComponent<Image>();
        replyBoxBg.color = new Color(0.12f, 0.12f, 0.15f, 0.95f);

        Outline replyBoxOutline = replyBoxObj.AddComponent<Outline>();
        replyBoxOutline.effectColor = new Color(0.32f, 0.28f, 0.22f, 0.8f);
        replyBoxOutline.effectDistance = new Vector2(1.5f, 1.5f);

        GameObject replyTxtObj = new GameObject("GuardReplyText");
        replyTxtObj.transform.SetParent(replyBoxObj.transform, false);
        RectTransform replyTxtRect = replyTxtObj.AddComponent<RectTransform>();
        replyTxtRect.anchorMin = new Vector2(0.03f, 0.22f);
        replyTxtRect.anchorMax = new Vector2(0.97f, 0.92f);
        replyTxtRect.offsetMin = Vector2.zero;
        replyTxtRect.offsetMax = Vector2.zero;

        guardReplyText = replyTxtObj.AddComponent<TextMeshProUGUI>();
        if (sharedFont != null)
            guardReplyText.font = sharedFont;
        guardReplyText.enableAutoSizing = true;
        guardReplyText.fontSizeMin = 17f;
        guardReplyText.fontSizeMax = 23f;
        guardReplyText.alignment = TextAlignmentOptions.TopLeft;
        guardReplyText.color = new Color(0.95f, 0.91f, 0.83f, 1f);
        guardReplyText.textWrappingMode = TextWrappingModes.Normal;
        guardReplyText.text = "Dur yabancı! Kutsal Oda'nın eşiğindesin. Neden burada olduğunu ve tapınağın emanetine niyetini söyle...";

        thinkingIndicatorObj = new GameObject("ThinkingIndicator");
        thinkingIndicatorObj.transform.SetParent(replyBoxObj.transform, false);
        RectTransform thinkRect = thinkingIndicatorObj.AddComponent<RectTransform>();
        thinkRect.anchorMin = new Vector2(0.03f, 0.03f);
        thinkRect.anchorMax = new Vector2(0.97f, 0.24f);
        thinkRect.offsetMin = Vector2.zero;
        thinkRect.offsetMax = Vector2.zero;

        GameObject thinkTxtObj = new GameObject("ThinkingText");
        thinkTxtObj.transform.SetParent(thinkingIndicatorObj.transform, false);
        RectTransform thinkTxtRect = thinkTxtObj.AddComponent<RectTransform>();
        thinkTxtRect.anchorMin = Vector2.zero;
        thinkTxtRect.anchorMax = Vector2.one;
        thinkTxtRect.offsetMin = Vector2.zero;
        thinkTxtRect.offsetMax = Vector2.zero;

        thinkingIndicatorText = thinkTxtObj.AddComponent<TextMeshProUGUI>();
        if (sharedFont != null)
            thinkingIndicatorText.font = sharedFont;
        thinkingIndicatorText.fontSize = 19f;
        thinkingIndicatorText.fontStyle = FontStyles.Italic | FontStyles.Bold;
        thinkingIndicatorText.alignment = TextAlignmentOptions.MidlineLeft;
        thinkingIndicatorText.color = new Color(1f, 0.76f, 0.32f, 1f);
        thinkingIndicatorText.text = "Muhafız düşünüyor...";

        thinkingIndicatorObj.SetActive(false);

        GameObject inputRowObj = new GameObject("InputRow");
        inputRowObj.transform.SetParent(mainFrameObj.transform, false);
        RectTransform inputRowRect = inputRowObj.AddComponent<RectTransform>();
        inputRowRect.anchorMin = new Vector2(0.02f, 0.04f);
        inputRowRect.anchorMax = new Vector2(0.98f, 0.21f);
        inputRowRect.offsetMin = Vector2.zero;
        inputRowRect.offsetMax = Vector2.zero;

        GameObject inputFieldObj = new GameObject("PlayerInputField");
        inputFieldObj.transform.SetParent(inputRowObj.transform, false);
        RectTransform inputFieldRect = inputFieldObj.AddComponent<RectTransform>();
        inputFieldRect.anchorMin = new Vector2(0f, 0f);
        inputFieldRect.anchorMax = new Vector2(0.81f, 1f);
        inputFieldRect.offsetMin = Vector2.zero;
        inputFieldRect.offsetMax = Vector2.zero;

        Image inputBg = inputFieldObj.AddComponent<Image>();
        inputBg.color = new Color(0.16f, 0.16f, 0.20f, 1f);

        Outline inputOutline = inputFieldObj.AddComponent<Outline>();
        inputOutline.effectColor = new Color(0.58f, 0.48f, 0.30f, 0.9f);
        inputOutline.effectDistance = new Vector2(1.5f, 1.5f);

        GameObject textAreaObj = new GameObject("Text Area");
        textAreaObj.transform.SetParent(inputFieldObj.transform, false);
        RectTransform textAreaRect = textAreaObj.AddComponent<RectTransform>();
        textAreaRect.anchorMin = Vector2.zero;
        textAreaRect.anchorMax = Vector2.one;
        textAreaRect.offsetMin = new Vector2(16f, 6f);
        textAreaRect.offsetMax = new Vector2(-16f, -6f);
        textAreaObj.AddComponent<RectMask2D>();

        GameObject placeholderObj = new GameObject("Placeholder");
        placeholderObj.transform.SetParent(textAreaObj.transform, false);
        RectTransform placeholderRect = placeholderObj.AddComponent<RectTransform>();
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.offsetMin = Vector2.zero;
        placeholderRect.offsetMax = Vector2.zero;

        TextMeshProUGUI placeholderTmp = placeholderObj.AddComponent<TextMeshProUGUI>();
        if (sharedFont != null)
            placeholderTmp.font = sharedFont;
        placeholderTmp.fontSize = 20f;
        placeholderTmp.fontStyle = FontStyles.Italic;
        placeholderTmp.alignment = TextAlignmentOptions.MidlineLeft;
        placeholderTmp.color = new Color(0.62f, 0.60f, 0.56f, 0.75f);
        placeholderTmp.text = "Muhafıza söyleyeceklerini buraya yaz... (Enter veya Gönder)";

        GameObject inputTextObj = new GameObject("Text");
        inputTextObj.transform.SetParent(textAreaObj.transform, false);
        RectTransform inputTextRect = inputTextObj.AddComponent<RectTransform>();
        inputTextRect.anchorMin = Vector2.zero;
        inputTextRect.anchorMax = Vector2.one;
        inputTextRect.offsetMin = Vector2.zero;
        inputTextRect.offsetMax = Vector2.zero;

        TextMeshProUGUI inputTmp = inputTextObj.AddComponent<TextMeshProUGUI>();
        if (sharedFont != null)
            inputTmp.font = sharedFont;
        inputTmp.fontSize = 20f;
        inputTmp.alignment = TextAlignmentOptions.MidlineLeft;
        inputTmp.color = new Color(0.98f, 0.96f, 0.90f, 1f);

        playerInputField = inputFieldObj.AddComponent<TMP_InputField>();
        playerInputField.textViewport = textAreaRect;
        playerInputField.textComponent = inputTmp;
        playerInputField.placeholder = placeholderTmp;
        playerInputField.fontAsset = sharedFont;
        playerInputField.pointSize = 20f;
        playerInputField.lineType = TMP_InputField.LineType.SingleLine;
        playerInputField.customCaretColor = true;
        playerInputField.caretColor = new Color(0.95f, 0.80f, 0.44f, 1f);

        GameObject sendBtnObj = new GameObject("SendButton");
        sendBtnObj.transform.SetParent(inputRowObj.transform, false);
        RectTransform sendBtnRect = sendBtnObj.AddComponent<RectTransform>();
        sendBtnRect.anchorMin = new Vector2(0.825f, 0f);
        sendBtnRect.anchorMax = new Vector2(1f, 1f);
        sendBtnRect.offsetMin = Vector2.zero;
        sendBtnRect.offsetMax = Vector2.zero;

        Image sendBtnImg = sendBtnObj.AddComponent<Image>();
        sendBtnImg.color = new Color(0.76f, 0.58f, 0.24f, 1f);

        Outline sendBtnOutline = sendBtnObj.AddComponent<Outline>();
        sendBtnOutline.effectColor = new Color(0.95f, 0.82f, 0.48f, 0.9f);
        sendBtnOutline.effectDistance = new Vector2(1.5f, 1.5f);

        sendButton = sendBtnObj.AddComponent<Button>();
        sendButton.targetGraphic = sendBtnImg;

        GameObject sendTxtObj = new GameObject("SendButtonText");
        sendTxtObj.transform.SetParent(sendBtnObj.transform, false);
        RectTransform sendTxtRect = sendTxtObj.AddComponent<RectTransform>();
        sendTxtRect.anchorMin = Vector2.zero;
        sendTxtRect.anchorMax = Vector2.one;
        sendTxtRect.offsetMin = Vector2.zero;
        sendTxtRect.offsetMax = Vector2.zero;

        sendButtonText = sendTxtObj.AddComponent<TextMeshProUGUI>();
        if (sharedFont != null)
            sendButtonText.font = sharedFont;
        sendButtonText.fontSize = 21f;
        sendButtonText.fontStyle = FontStyles.Bold;
        sendButtonText.alignment = TextAlignmentOptions.Center;
        sendButtonText.color = new Color(0.10f, 0.08f, 0.06f, 1f);
        sendButtonText.text = "GÖNDER";

        guardChatPanel.SetActive(false);
        BindUIEvents();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
    }

    private void BindUIEvents()
    {
        if (sendButton != null)
        {
            sendButton.onClick.RemoveListener(OnSendButtonClicked);
            sendButton.onClick.AddListener(OnSendButtonClicked);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(CloseGuardChat);
            closeButton.onClick.AddListener(CloseGuardChat);
        }
    }


    public void EnsureStatusHudUI()
    {
        Canvas targetCanvas = null;
        if (dialoguePanel != null)
            targetCanvas = dialoguePanel.GetComponentInParent<Canvas>();
        if (targetCanvas == null)
        {
            GameObject canvasObj = GameObject.Find("Canvas");
            if (canvasObj != null)
                targetCanvas = canvasObj.GetComponent<Canvas>();
        }
        if (targetCanvas == null)
            targetCanvas = FindFirstObjectByType<Canvas>();
        if (targetCanvas == null)
            return;

        Transform existingHud = targetCanvas.transform.Find("StatusHUDPanel");
        if (existingHud != null)
        {
            statusHudPanel = existingHud.gameObject;
            Transform keyIconTr = statusHudPanel.transform.Find("KeyCard/Inner/Icon");
            if (keyIconTr != null)
                keyHudIcon = keyIconTr.GetComponent<Image>();
            Transform keyStateTr = statusHudPanel.transform.Find("KeyCard/Inner/StateText");
            if (keyStateTr != null)
                keyHudStateText = keyStateTr.GetComponent<TextMeshProUGUI>();

            Transform compIconTr = statusHudPanel.transform.Find("CompanionCard/Inner/Icon");
            if (compIconTr != null)
                companionHudIcon = compIconTr.GetComponent<Image>();
            Transform compStateTr = statusHudPanel.transform.Find("CompanionCard/Inner/StateText");
            if (compStateTr != null)
                companionHudStateText = compStateTr.GetComponent<TextMeshProUGUI>();

            if (keyHudIcon != null && keyHudStateText != null && companionHudIcon != null && companionHudStateText != null)
            {
                if (keyIconSprite != null)
                    keyHudIcon.sprite = keyIconSprite;
                if (companionIconSprite != null)
                    companionHudIcon.sprite = companionIconSprite;
                return;
            }

            DestroyImmediate(existingHud.gameObject);
        }

        TMP_FontAsset sharedFont = FindSharedFont();

        statusHudPanel = new GameObject("StatusHUDPanel");
        statusHudPanel.layer = 5;
        statusHudPanel.transform.SetParent(targetCanvas.transform, false);

        RectTransform hudRect = statusHudPanel.AddComponent<RectTransform>();
        hudRect.anchorMin = new Vector2(0f, 1f);
        hudRect.anchorMax = new Vector2(0f, 1f);
        hudRect.pivot = new Vector2(0f, 1f);
        hudRect.anchoredPosition = new Vector2(24f, -22f);
        hudRect.sizeDelta = new Vector2(540f, 86f);

        CreateHudCard(
            statusHudPanel.transform,
            "KeyCard",
            new Vector2(0f, 0f),
            new Vector2(0.485f, 1f),
            keyIconSprite,
            "KUTSAL ANAHTAR",
            "Bulunmadı",
            sharedFont,
            out keyHudIcon,
            out keyHudStateText);

        CreateHudCard(
            statusHudPanel.transform,
            "CompanionCard",
            new Vector2(0.515f, 0f),
            new Vector2(1f, 1f),
            companionIconSprite,
            "OTONOM YOLDAŞ",
            "Hazır",
            sharedFont,
            out companionHudIcon,
            out companionHudStateText);

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
    }

    private static void CreateHudCard(
        Transform parent,
        string cardName,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Sprite iconSprite,
        string headerTitle,
        string initialState,
        TMP_FontAsset sharedFont,
        out Image iconImg,
        out TextMeshProUGUI stateTmp)
    {
        GameObject cardObj = new GameObject(cardName);
        cardObj.transform.SetParent(parent, false);
        RectTransform cardRect = cardObj.AddComponent<RectTransform>();
        cardRect.anchorMin = anchorMin;
        cardRect.anchorMax = anchorMax;
        cardRect.offsetMin = Vector2.zero;
        cardRect.offsetMax = Vector2.zero;

        GameObject edgeObj = new GameObject("SoftBlackEdge");
        edgeObj.transform.SetParent(cardObj.transform, false);
        RectTransform edgeRect = edgeObj.AddComponent<RectTransform>();
        edgeRect.anchorMin = Vector2.zero;
        edgeRect.anchorMax = Vector2.one;
        edgeRect.offsetMin = new Vector2(-4f, -4f);
        edgeRect.offsetMax = new Vector2(4f, 4f);
        Image edgeImg = edgeObj.AddComponent<Image>();
        edgeImg.color = new Color(0f, 0f, 0f, 0.36f);

        Image borderImg = cardObj.AddComponent<Image>();
        borderImg.color = new Color(0.58f, 0.48f, 0.31f, 0.94f);

        GameObject innerObj = new GameObject("Inner");
        innerObj.transform.SetParent(cardObj.transform, false);
        RectTransform innerRect = innerObj.AddComponent<RectTransform>();
        innerRect.anchorMin = Vector2.zero;
        innerRect.anchorMax = Vector2.one;
        innerRect.offsetMin = new Vector2(2.5f, 2.5f);
        innerRect.offsetMax = new Vector2(-2.5f, -2.5f);
        Image innerBg = innerObj.AddComponent<Image>();
        innerBg.color = new Color(0.10f, 0.10f, 0.13f, 0.94f);

        GameObject iconFrameObj = new GameObject("IconBg");
        iconFrameObj.transform.SetParent(innerObj.transform, false);
        RectTransform iconFrameRect = iconFrameObj.AddComponent<RectTransform>();
        iconFrameRect.anchorMin = new Vector2(0.04f, 0.12f);
        iconFrameRect.anchorMax = new Vector2(0.29f, 0.88f);
        iconFrameRect.offsetMin = Vector2.zero;
        iconFrameRect.offsetMax = Vector2.zero;
        Image iconFrameBg = iconFrameObj.AddComponent<Image>();
        iconFrameBg.color = new Color(0.15f, 0.15f, 0.19f, 0.95f);

        GameObject iconObj = new GameObject("Icon");
        iconObj.transform.SetParent(innerObj.transform, false);
        RectTransform iconRect = iconObj.AddComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.06f, 0.16f);
        iconRect.anchorMax = new Vector2(0.27f, 0.84f);
        iconRect.offsetMin = Vector2.zero;
        iconRect.offsetMax = Vector2.zero;

        iconImg = iconObj.AddComponent<Image>();
        iconImg.preserveAspect = true;
        if (iconSprite != null)
        {
            iconImg.sprite = iconSprite;
        }

        GameObject titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(innerObj.transform, false);
        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.33f, 0.52f);
        titleRect.anchorMax = new Vector2(0.96f, 0.90f);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;

        TextMeshProUGUI titleTmp = titleObj.AddComponent<TextMeshProUGUI>();
        if (sharedFont != null)
            titleTmp.font = sharedFont;
        titleTmp.fontSize = 14.5f;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.alignment = TextAlignmentOptions.BottomLeft;
        titleTmp.color = new Color(0.88f, 0.76f, 0.46f, 1f);
        titleTmp.text = headerTitle;

        GameObject stateObj = new GameObject("StateText");
        stateObj.transform.SetParent(innerObj.transform, false);
        RectTransform stateRect = stateObj.AddComponent<RectTransform>();
        stateRect.anchorMin = new Vector2(0.33f, 0.10f);
        stateRect.anchorMax = new Vector2(0.96f, 0.52f);
        stateRect.offsetMin = Vector2.zero;
        stateRect.offsetMax = Vector2.zero;

        stateTmp = stateObj.AddComponent<TextMeshProUGUI>();
        if (sharedFont != null)
            stateTmp.font = sharedFont;
        stateTmp.enableAutoSizing = true;
        stateTmp.fontSizeMin = 13f;
        stateTmp.fontSizeMax = 17.5f;
        stateTmp.fontStyle = FontStyles.Bold;
        stateTmp.alignment = TextAlignmentOptions.TopLeft;
        stateTmp.color = new Color(0.75f, 0.75f, 0.78f, 1f);
        stateTmp.text = initialState;
    }

    private void RefreshStatusHud()
    {
        bool hasKey = GameManager.Instance != null && GameManager.Instance.HasKey;

        if (cachedCompanionAgent == null)
        {
            cachedCompanionAgent = FindFirstObjectByType<CompanionAgent>();
        }

        string companionStateName = string.Empty;
        if (cachedCompanionAgent != null && cachedCompanionAgent.gameObject.activeInHierarchy)
        {
            if (cachedCompanionAgent.ajanMotoru != null)
            {
                if (cachedCompanionAgent.ajanMotoru.updateRotation)
                    cachedCompanionAgent.ajanMotoru.updateRotation = false;
                if (cachedCompanionAgent.ajanMotoru.updateUpAxis)
                    cachedCompanionAgent.ajanMotoru.updateUpAxis = false;
                if (Mathf.Abs(cachedCompanionAgent.ajanMotoru.baseOffset) > 0.001f)
                    cachedCompanionAgent.ajanMotoru.baseOffset = 0f;
            }

            if (Quaternion.Angle(cachedCompanionAgent.transform.rotation, Quaternion.identity) > 0.05f)
            {
                cachedCompanionAgent.transform.rotation = Quaternion.identity;
            }

            Vector3 cPos = cachedCompanionAgent.transform.position;
            if (Mathf.Abs(cPos.z) > 0.01f)
            {
                cachedCompanionAgent.transform.position = new Vector3(cPos.x, cPos.y, 0f);
            }

            if (companionStateField == null)
            {
                companionStateField = typeof(CompanionAgent).GetField(
                    "currentState",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            }

            if (companionStateField != null)
            {
                object stateObj = companionStateField.GetValue(cachedCompanionAgent);
                if (stateObj != null)
                {
                    companionStateName = stateObj.GetType().Name;
                }
            }

            if (cachedCompanionAgent.ajanMotoru != null && cachedCompanionAgent.sandikHedefi != null)
            {
                if (companionStateName == "NavigateToKeyState")
                {
                    float distToChest = Vector2.Distance(
                        cachedCompanionAgent.transform.position,
                        cachedCompanionAgent.sandikHedefi.position);

                    if (distToChest > 1.35f)
                    {
                        cachedCompanionAgent.ajanMotoru.stoppingDistance = -0.1f;
                    }
                    else
                    {
                        cachedCompanionAgent.ajanMotoru.stoppingDistance = 1.4f;
                        if (!hasKey && GameManager.Instance != null)
                        {
                            GameManager.Instance.SetHasKey(true);
                            hasKey = true;
                        }
                    }
                }
                else if (cachedCompanionAgent.ajanMotoru.stoppingDistance < 0.5f)
                {
                    cachedCompanionAgent.ajanMotoru.stoppingDistance = 1.2f;
                }
            }

            if (!hasKey && companionStateName == "CollectAndReturnState" &&
                cachedCompanionAgent.ajanMotoru != null &&
                !cachedCompanionAgent.ajanMotoru.pathPending &&
                cachedCompanionAgent.ajanMotoru.remainingDistance <= cachedCompanionAgent.ajanMotoru.stoppingDistance + 0.25f)
            {
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.SetHasKey(true);
                    hasKey = true;
                }
            }
        }

        if (keyHudIcon != null)
        {
            keyHudIcon.color = hasKey
                ? Color.white
                : new Color(0.38f, 0.38f, 0.42f, 0.55f);
        }

        if (keyHudStateText != null)
        {
            if (hasKey)
            {
                keyHudStateText.text = "Envanterde (Alındı)";
                keyHudStateText.color = new Color(0.45f, 0.94f, 0.56f, 1f);
            }
            else
            {
                keyHudStateText.text = "Bulunmadı";
                keyHudStateText.color = new Color(0.68f, 0.66f, 0.62f, 0.9f);
            }
        }

        bool companionActive = (GameManager.Instance != null && GameManager.Instance.HasCompanion) ||
                               (cachedCompanionAgent != null && cachedCompanionAgent.gameObject.activeInHierarchy);

        if (companionHudIcon != null)
        {
            companionHudIcon.color = companionActive
                ? Color.white
                : new Color(0.38f, 0.38f, 0.42f, 0.55f);
        }

        if (companionHudStateText != null)
        {
            if (!companionActive)
            {
                companionHudStateText.text = "Sandıkta Bekliyor";
                companionHudStateText.color = new Color(0.68f, 0.66f, 0.62f, 0.9f);
            }
            else if (companionStateName == "NavigateToKeyState")
            {
                companionHudStateText.text = "Anahtara Gidiyor...";
                companionHudStateText.color = new Color(1f, 0.78f, 0.32f, 1f);
            }
            else if (companionStateName == "CollectAndReturnState")
            {
                companionHudStateText.text = hasKey ? "Görev Tamamlandı" : "Anahtarla Dönüyor...";
                companionHudStateText.color = new Color(0.45f, 0.94f, 0.56f, 1f);
            }
            else
            {
                companionHudStateText.text = "Takip Ediyor [SPACE]";
                companionHudStateText.color = new Color(0.85f, 0.92f, 1f, 1f);
            }
        }
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


    public void EnsureVictoryScreenUI()
    {
        EnsureSpritesLoaded();

        Canvas targetCanvas = null;
        if (dialoguePanel != null)
        {
            targetCanvas = dialoguePanel.GetComponentInParent<Canvas>();
        }
        if (targetCanvas == null)
        {
            GameObject canvasObj = GameObject.Find("Canvas");
            if (canvasObj != null)
            {
                targetCanvas = canvasObj.GetComponent<Canvas>();
            }
        }
        if (targetCanvas == null)
        {
            targetCanvas = FindFirstObjectByType<Canvas>();
        }
        if (targetCanvas == null)
            return;

        Transform existingVictory = targetCanvas.transform.Find("VictoryScreenPanel");
        if (existingVictory != null)
        {
            victoryScreenPanel = existingVictory.gameObject;
            Transform cardTr = existingVictory.Find("VictoryCard");
            if (cardTr != null)
            {
                Transform restartBtnTr = cardTr.Find("ButtonsRow/RestartButton");
                if (restartBtnTr != null)
                    victoryRestartButton = restartBtnTr.GetComponent<Button>();

                Transform continueBtnTr = cardTr.Find("ButtonsRow/ContinueButton");
                if (continueBtnTr != null)
                    victoryContinueButton = continueBtnTr.GetComponent<Button>();

                Transform gateIconTr = cardTr.Find("IconsRow/GateIconBadge/GateIcon");
                if (gateIconTr != null)
                    victoryGateIcon = gateIconTr.GetComponent<Image>();

                Transform keyIconTr = cardTr.Find("IconsRow/KeyIconBadge/KeyIcon");
                if (keyIconTr != null)
                    victoryKeyIcon = keyIconTr.GetComponent<Image>();
            }

            BindVictoryButtons();
            if (victoryGateIcon != null && woodenGateOpenedSprite != null)
                victoryGateIcon.sprite = woodenGateOpenedSprite;
            if (victoryKeyIcon != null && keyIconSprite != null)
                victoryKeyIcon.sprite = keyIconSprite;

            return;
        }

        TMP_FontAsset sharedFont = FindSharedFont();

        GameObject overlayObj = new GameObject("VictoryScreenPanel");
        overlayObj.transform.SetParent(targetCanvas.transform, false);
        RectTransform overlayRt = overlayObj.AddComponent<RectTransform>();
        overlayRt.anchorMin = Vector2.zero;
        overlayRt.anchorMax = Vector2.one;
        overlayRt.offsetMin = Vector2.zero;
        overlayRt.offsetMax = Vector2.zero;

        Image overlayImg = overlayObj.AddComponent<Image>();
        overlayImg.color = new Color(0.03f, 0.04f, 0.06f, 0.86f);
        overlayImg.raycastTarget = true;

        GameObject cardObj = new GameObject("VictoryCard");
        cardObj.transform.SetParent(overlayObj.transform, false);
        RectTransform cardRt = cardObj.AddComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0.5f, 0.5f);
        cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.sizeDelta = new Vector2(820f, 520f);

        Image cardBg = cardObj.AddComponent<Image>();
        cardBg.color = new Color(0.08f, 0.10f, 0.13f, 0.98f);

        Outline cardGoldBorder = cardObj.AddComponent<Outline>();
        cardGoldBorder.effectColor = new Color(0.92f, 0.76f, 0.35f, 0.95f);
        cardGoldBorder.effectDistance = new Vector2(3f, -3f);

        Shadow cardShadow = cardObj.AddComponent<Shadow>();
        cardShadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
        cardShadow.effectDistance = new Vector2(0f, -12f);

        GameObject headerBar = new GameObject("HeaderBar");
        headerBar.transform.SetParent(cardObj.transform, false);
        RectTransform headerRt = headerBar.AddComponent<RectTransform>();
        headerRt.anchorMin = new Vector2(0f, 1f);
        headerRt.anchorMax = new Vector2(1f, 1f);
        headerRt.pivot = new Vector2(0.5f, 1f);
        headerRt.anchoredPosition = Vector2.zero;
        headerRt.sizeDelta = new Vector2(0f, 76f);

        Image headerImg = headerBar.AddComponent<Image>();
        headerImg.color = new Color(0.14f, 0.19f, 0.15f, 1f);

        Outline headerLine = headerBar.AddComponent<Outline>();
        headerLine.effectColor = new Color(0.85f, 0.70f, 0.32f, 0.65f);
        headerLine.effectDistance = new Vector2(0f, -2f);

        GameObject badgeObj = new GameObject("VictoryBadgeText");
        badgeObj.transform.SetParent(headerBar.transform, false);
        RectTransform badgeRt = badgeObj.AddComponent<RectTransform>();
        badgeRt.anchorMin = Vector2.zero;
        badgeRt.anchorMax = Vector2.one;
        badgeRt.offsetMin = new Vector2(20f, 8f);
        badgeRt.offsetMax = new Vector2(-20f, -8f);

        TextMeshProUGUI badgeTmp = badgeObj.AddComponent<TextMeshProUGUI>();
        if (sharedFont != null)
            badgeTmp.font = sharedFont;
        badgeTmp.text = "★  GÖREV TAMAMLANDI — TAPINAĞIN KUTSAL KAPISI AÇILDI  ★";
        badgeTmp.fontSize = 24f;
        badgeTmp.fontStyle = FontStyles.Bold;
        badgeTmp.alignment = TextAlignmentOptions.Center;
        badgeTmp.color = new Color(0.98f, 0.86f, 0.44f, 1f);

        GameObject iconsRow = new GameObject("IconsRow");
        iconsRow.transform.SetParent(cardObj.transform, false);
        RectTransform iconsRt = iconsRow.AddComponent<RectTransform>();
        iconsRt.anchorMin = new Vector2(0.5f, 1f);
        iconsRt.anchorMax = new Vector2(0.5f, 1f);
        iconsRt.pivot = new Vector2(0.5f, 1f);
        iconsRt.anchoredPosition = new Vector2(0f, -96f);
        iconsRt.sizeDelta = new Vector2(340f, 120f);

        GameObject gateBadge = new GameObject("GateIconBadge");
        gateBadge.transform.SetParent(iconsRow.transform, false);
        RectTransform gateBadgeRt = gateBadge.AddComponent<RectTransform>();
        gateBadgeRt.anchorMin = new Vector2(0.5f, 0.5f);
        gateBadgeRt.anchorMax = new Vector2(0.5f, 0.5f);
        gateBadgeRt.pivot = new Vector2(0.5f, 0.5f);
        gateBadgeRt.anchoredPosition = new Vector2(-72f, 0f);
        gateBadgeRt.sizeDelta = new Vector2(112f, 112f);

        Image gateBadgeBg = gateBadge.AddComponent<Image>();
        gateBadgeBg.color = new Color(0.12f, 0.16f, 0.14f, 1f);
        Outline gateBadgeOutline = gateBadge.AddComponent<Outline>();
        gateBadgeOutline.effectColor = new Color(0.45f, 0.92f, 0.58f, 0.85f);
        gateBadgeOutline.effectDistance = new Vector2(2f, -2f);

        GameObject gateIconObj = new GameObject("GateIcon");
        gateIconObj.transform.SetParent(gateBadge.transform, false);
        RectTransform gateIconRt = gateIconObj.AddComponent<RectTransform>();
        gateIconRt.anchorMin = Vector2.zero;
        gateIconRt.anchorMax = Vector2.one;
        gateIconRt.offsetMin = new Vector2(12f, 12f);
        gateIconRt.offsetMax = new Vector2(-12f, -12f);

        victoryGateIcon = gateIconObj.AddComponent<Image>();
        victoryGateIcon.preserveAspect = true;
        if (woodenGateOpenedSprite != null)
            victoryGateIcon.sprite = woodenGateOpenedSprite;

        GameObject keyBadge = new GameObject("KeyIconBadge");
        keyBadge.transform.SetParent(iconsRow.transform, false);
        RectTransform keyBadgeRt = keyBadge.AddComponent<RectTransform>();
        keyBadgeRt.anchorMin = new Vector2(0.5f, 0.5f);
        keyBadgeRt.anchorMax = new Vector2(0.5f, 0.5f);
        keyBadgeRt.pivot = new Vector2(0.5f, 0.5f);
        keyBadgeRt.anchoredPosition = new Vector2(72f, 0f);
        keyBadgeRt.sizeDelta = new Vector2(112f, 112f);

        Image keyBadgeBg = keyBadge.AddComponent<Image>();
        keyBadgeBg.color = new Color(0.14f, 0.14f, 0.10f, 1f);
        Outline keyBadgeOutline = keyBadge.AddComponent<Outline>();
        keyBadgeOutline.effectColor = new Color(0.95f, 0.80f, 0.35f, 0.85f);
        keyBadgeOutline.effectDistance = new Vector2(2f, -2f);

        GameObject keyIconObj = new GameObject("KeyIcon");
        keyIconObj.transform.SetParent(keyBadge.transform, false);
        RectTransform keyIconRt = keyIconObj.AddComponent<RectTransform>();
        keyIconRt.anchorMin = Vector2.zero;
        keyIconRt.anchorMax = Vector2.one;
        keyIconRt.offsetMin = new Vector2(16f, 16f);
        keyIconRt.offsetMax = new Vector2(-16f, -16f);

        victoryKeyIcon = keyIconObj.AddComponent<Image>();
        victoryKeyIcon.preserveAspect = true;
        if (keyIconSprite != null)
            victoryKeyIcon.sprite = keyIconSprite;

        GameObject titleObj = new GameObject("VictoryTitle");
        titleObj.transform.SetParent(cardObj.transform, false);
        RectTransform titleRt = titleObj.AddComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0f, -230f);
        titleRt.sizeDelta = new Vector2(-60f, 48f);

        TextMeshProUGUI titleTmp = titleObj.AddComponent<TextMeshProUGUI>();
        if (sharedFont != null)
            titleTmp.font = sharedFont;
        titleTmp.text = "TEBRİKLER, OYUNU KAZANDINIZ!";
        titleTmp.fontSize = 32f;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.color = new Color(0.48f, 0.96f, 0.62f, 1f);

        GameObject descObj = new GameObject("VictoryDescription");
        descObj.transform.SetParent(cardObj.transform, false);
        RectTransform descRt = descObj.AddComponent<RectTransform>();
        descRt.anchorMin = new Vector2(0f, 1f);
        descRt.anchorMax = new Vector2(1f, 1f);
        descRt.pivot = new Vector2(0.5f, 1f);
        descRt.anchoredPosition = new Vector2(0f, -286f);
        descRt.sizeDelta = new Vector2(-90f, 118f);

        TextMeshProUGUI descTmp = descObj.AddComponent<TextMeshProUGUI>();
        if (sharedFont != null)
            descTmp.font = sharedFont;
        descTmp.text =
            "Kadim rün sunağının mührünü çözdün, Otonom Yoldaş'ı dikenli engellerin ötesine göndererek " +
            "<color=#FFE07D>Kutsal Anahtar'ı (HasKey = true)</color> ele geçirdin ve Tapınak Muhafızı'nı " +
            "bilgeliğinle ikna ettin (<color=#72F594>OPEN_DOOR</color>).\n" +
            "Tapınağın hedef kapısı ardına kadar açıldı!";
        descTmp.fontSize = 19.5f;
        descTmp.alignment = TextAlignmentOptions.Center;
        descTmp.color = new Color(0.90f, 0.89f, 0.85f, 1f);

        GameObject buttonsRow = new GameObject("ButtonsRow");
        buttonsRow.transform.SetParent(cardObj.transform, false);
        RectTransform buttonsRt = buttonsRow.AddComponent<RectTransform>();
        buttonsRt.anchorMin = new Vector2(0.5f, 0f);
        buttonsRt.anchorMax = new Vector2(0.5f, 0f);
        buttonsRt.pivot = new Vector2(0.5f, 0f);
        buttonsRt.anchoredPosition = new Vector2(0f, 28f);
        buttonsRt.sizeDelta = new Vector2(680f, 58f);

        GameObject restartBtnObj = new GameObject("RestartButton");
        restartBtnObj.transform.SetParent(buttonsRow.transform, false);
        RectTransform restartRt = restartBtnObj.AddComponent<RectTransform>();
        restartRt.anchorMin = new Vector2(0f, 0f);
        restartRt.anchorMax = new Vector2(0.48f, 1f);
        restartRt.offsetMin = Vector2.zero;
        restartRt.offsetMax = Vector2.zero;

        Image restartBg = restartBtnObj.AddComponent<Image>();
        restartBg.color = new Color(0.18f, 0.56f, 0.30f, 1f);
        Outline restartOutline = restartBtnObj.AddComponent<Outline>();
        restartOutline.effectColor = new Color(0.55f, 0.95f, 0.65f, 0.9f);
        restartOutline.effectDistance = new Vector2(1.5f, -1.5f);

        victoryRestartButton = restartBtnObj.AddComponent<Button>();
        victoryRestartButton.targetGraphic = restartBg;

        GameObject restartTxtObj = new GameObject("Text");
        restartTxtObj.transform.SetParent(restartBtnObj.transform, false);
        RectTransform restartTxtRt = restartTxtObj.AddComponent<RectTransform>();
        restartTxtRt.anchorMin = Vector2.zero;
        restartTxtRt.anchorMax = Vector2.one;
        restartTxtRt.offsetMin = Vector2.zero;
        restartTxtRt.offsetMax = Vector2.zero;

        TextMeshProUGUI restartTmp = restartTxtObj.AddComponent<TextMeshProUGUI>();
        if (sharedFont != null)
            restartTmp.font = sharedFont;
        restartTmp.text = "TEKRAR OYNA";
        restartTmp.fontSize = 20f;
        restartTmp.fontStyle = FontStyles.Bold;
        restartTmp.alignment = TextAlignmentOptions.Center;
        restartTmp.color = Color.white;

        GameObject continueBtnObj = new GameObject("ContinueButton");
        continueBtnObj.transform.SetParent(buttonsRow.transform, false);
        RectTransform continueRt = continueBtnObj.AddComponent<RectTransform>();
        continueRt.anchorMin = new Vector2(0.52f, 0f);
        continueRt.anchorMax = new Vector2(1f, 1f);
        continueRt.offsetMin = Vector2.zero;
        continueRt.offsetMax = Vector2.zero;

        Image continueBg = continueBtnObj.AddComponent<Image>();
        continueBg.color = new Color(0.22f, 0.24f, 0.28f, 1f);
        Outline continueOutline = continueBtnObj.AddComponent<Outline>();
        continueOutline.effectColor = new Color(0.85f, 0.72f, 0.38f, 0.8f);
        continueOutline.effectDistance = new Vector2(1.5f, -1.5f);

        victoryContinueButton = continueBtnObj.AddComponent<Button>();
        victoryContinueButton.targetGraphic = continueBg;

        GameObject continueTxtObj = new GameObject("Text");
        continueTxtObj.transform.SetParent(continueBtnObj.transform, false);
        RectTransform continueTxtRt = continueTxtObj.AddComponent<RectTransform>();
        continueTxtRt.anchorMin = Vector2.zero;
        continueTxtRt.anchorMax = Vector2.one;
        continueTxtRt.offsetMin = Vector2.zero;
        continueTxtRt.offsetMax = Vector2.zero;

        TextMeshProUGUI continueTmp = continueTxtObj.AddComponent<TextMeshProUGUI>();
        if (sharedFont != null)
            continueTmp.font = sharedFont;
        continueTmp.text = "AÇIK KAPIDAN GEÇ (DEVAM ET)";
        continueTmp.fontSize = 18.5f;
        continueTmp.fontStyle = FontStyles.Bold;
        continueTmp.alignment = TextAlignmentOptions.Center;
        continueTmp.color = new Color(0.96f, 0.88f, 0.64f, 1f);

        victoryScreenPanel = overlayObj;
        victoryScreenPanel.SetActive(false);

        BindVictoryButtons();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.EditorUtility.SetDirty(victoryScreenPanel);
        }
#endif
    }

    private void BindVictoryButtons()
    {
        if (victoryRestartButton != null)
        {
            victoryRestartButton.onClick.RemoveListener(RestartGameFromVictory);
            victoryRestartButton.onClick.AddListener(RestartGameFromVictory);
        }

        if (victoryContinueButton != null)
        {
            victoryContinueButton.onClick.RemoveListener(HideVictoryScreen);
            victoryContinueButton.onClick.AddListener(HideVictoryScreen);
        }
    }

    public void ShowVictoryScreen()
    {
        EnsureSpritesLoaded();
        EnsureVictoryScreenUI();

        if (victoryGateIcon != null && woodenGateOpenedSprite != null)
        {
            victoryGateIcon.sprite = woodenGateOpenedSprite;
        }
        if (victoryKeyIcon != null && keyIconSprite != null)
        {
            victoryKeyIcon.sprite = keyIconSprite;
        }

        if (victoryScreenPanel != null)
        {
            victoryScreenPanel.transform.SetAsLastSibling();
            victoryScreenPanel.SetActive(true);
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetGameState(GameState.Dialogue);
        }
    }

    public void HideVictoryScreen()
    {
        if (victoryScreenPanel != null)
        {
            victoryScreenPanel.SetActive(false);
        }

        if (!IsGuardChatOpen && GameManager.Instance != null)
        {
            GameManager.Instance.SetGameState(GameState.Playing);
        }
    }

    private void RestartGameFromVictory()
    {
        HideVictoryScreen();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetHasKey(false);
            GameManager.Instance.SetHasCompanion(false);
            GameManager.Instance.SetGameState(GameState.Playing);
        }

        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }

    [ContextMenu("Test Open Door & Victory (HasKey=true)")]
    private void TestOpenDoorAndVictory()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetHasKey(true);
        }
        HandleOpenDoorAction();
    }
}

public class VictoryGateTrigger : MonoBehaviour
{
    private bool hasTriggeredOnWalkThrough;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasTriggeredOnWalkThrough)
            return;

        if (other != null && (other.CompareTag("Player") || other.GetComponent<PlayerInteractor>() != null))
        {
            hasTriggeredOnWalkThrough = true;
            if (DialogueManager.Instance != null && !DialogueManager.Instance.IsVictoryScreenOpen)
            {
                DialogueManager.Instance.ShowVictoryScreen();
            }
        }
    }
}

public class GuardDialogueTrigger : MonoBehaviour, IInteractable
{
    private const string IndicatorChildName = "InteractionIndicator";

    private const float TargetWorldScale = 0.125f;
    private static readonly Vector3 HeadLocalOffset = new Vector3(-0.14f, 1.42f, 0f);

    private void Awake()
    {
        Cainos.PixelArtTopDown_Basic.StairsLayerTrigger.Configure2DNavMeshAgents();
        SyncGuardIndicatorTransform();
    }

    private void LateUpdate()
    {
        if (Quaternion.Angle(transform.rotation, Quaternion.identity) > 0.05f)
        {
            transform.rotation = Quaternion.identity;
        }

        if (gameObject.name.Contains("muhafız"))
        {
            Vector3 pos = transform.position;
            if (Mathf.Abs(pos.z) > 0.01f || Vector2.Distance(new Vector2(pos.x, pos.y), new Vector2(5.74f, -6.758f)) > 0.5f)
            {
                transform.position = new Vector3(5.74f, -6.758f, 0f);
            }
        }
    }

    public void Interact()
    {
        if (DialogueManager.Instance != null)
        {
            if (DialogueManager.Instance.IsGuardChatOpen)
            {
                DialogueManager.Instance.CloseGuardChat();
            }
            else
            {
                DialogueManager.Instance.OpenGuardChat();
            }
        }
    }

    public void SyncGuardIndicatorTransform()
    {
        if (GetComponent<SpriteRenderer>() == null)
        {
            Transform stray = transform.Find(IndicatorChildName);
            if (stray != null)
            {
                if (Application.isPlaying)
                    Destroy(stray.gameObject);
                else
                    DestroyImmediate(stray.gameObject);
            }
            return;
        }

        Sprite eSprite = null;
#if UNITY_EDITOR
        eSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/Cainos/Pixel Art Top Down - Basic/Material/E_harfi-.png");
#endif
        if (eSprite == null)
        {
            Sprite[] allSprites = Resources.FindObjectsOfTypeAll<Sprite>();
            foreach (Sprite s in allSprites)
            {
                if (s != null && s.name.Contains("E_harfi"))
                {
                    eSprite = s;
                    break;
                }
            }
        }

        Transform indicatorTr = transform.Find(IndicatorChildName);
        GameObject indicatorObj;

        if (indicatorTr == null)
        {
            if (eSprite == null)
                return;

            indicatorObj = new GameObject(IndicatorChildName);
            indicatorObj.transform.SetParent(transform, false);
            SpriteRenderer sr = indicatorObj.AddComponent<SpriteRenderer>();
            sr.sprite = eSprite;
            sr.sortingLayerName = "Layer 3";
            sr.sortingOrder = 100;
            indicatorObj.SetActive(false);
        }
        else
        {
            indicatorObj = indicatorTr.gameObject;
            SpriteRenderer sr = indicatorObj.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                if (sr.sprite == null && eSprite != null)
                    sr.sprite = eSprite;
                sr.sortingLayerName = "Layer 3";
                sr.sortingOrder = 100;
            }
        }

        indicatorObj.layer = gameObject.layer;

        Vector3 lossy = transform.lossyScale;
        float sx = Mathf.Abs(lossy.x) > 0.001f ? TargetWorldScale / Mathf.Abs(lossy.x) : 0.18f;
        float sy = Mathf.Abs(lossy.y) > 0.001f ? TargetWorldScale / Mathf.Abs(lossy.y) : 0.18f;

        indicatorObj.transform.localPosition = HeadLocalOffset;
        indicatorObj.transform.localRotation = Quaternion.identity;
        indicatorObj.transform.localScale = new Vector3(sx, sy, 1f);
    }
}
