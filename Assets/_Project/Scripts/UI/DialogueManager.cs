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
    [SerializeField] private Button victoryRestartButton;

    [Header("Olay Bildirim Paneli")]
    [SerializeField] private GameObject eventPopupPanel;
    [SerializeField] private CanvasGroup eventPopupCanvasGroup;
    [SerializeField] private Image eventPopupBorderImage;
    [SerializeField] private Image eventPopupIconBorderImage;
    [SerializeField] private Image eventPopupIconImage;
    [SerializeField] private TextMeshProUGUI eventPopupTitleText;
    [SerializeField] private TextMeshProUGUI eventPopupSubtitleText;

    [Header("Başlangıç Ekranı")]
    [SerializeField] private GameObject startScreenPanel;
    [SerializeField] private Button startGameButton;

    [Header("Duraklatma Menüsü ve Görev Hedefi")]
    [SerializeField] private GameObject pauseMenuPanel;
    [SerializeField] private TextMeshProUGUI pauseMuteButtonText;
    [SerializeField] private TextMeshProUGUI objectiveHudText;

    private static bool hasShownInitialStartScreen;
    private static bool isAudioMuted;

    private LLMManager llmManager;
    private CompanionAgent cachedCompanionAgent;
    private RuneAltar cachedRuneAltar;
    private System.Reflection.FieldInfo companionStateField;
    private Vector3 companionBaseLocalScale = Vector3.zero;
    private SpriteRenderer worldGuardRenderer;
    private Vector3 worldGuardBaseLocalScale = Vector3.zero;
    private AudioSource ambientAudioSource;
    private AudioSource sfxAudioSource;
    private AudioClip companionUnlockClip;
    private AudioClip companionSendClip;
    private AudioClip keyUnlockClip;
    private AudioClip runeActivateClip;
    private AudioClip victoryClip;
    private AudioClip defeatClip;
    private bool isWaitingForLLM;
    private float thinkingAnimTimer;
    private Coroutine typewriterCoroutine;
    private Coroutine victorySequenceCoroutine;
    private Coroutine eventPopupCoroutine;
    private bool stateTrackingInitialized;
    private bool lastTrackedHasCompanion;
    private bool lastTrackedHasKey;
    private string lastTrackedCompanionState = string.Empty;
    private bool hasDiscoveredKeyChest;
    private Transform storedKeyChestTarget;
    private TreasureChest cachedEastChest;

    public void NotifyClueDiscoveredEastChest()
    {
        SetKeyChestDiscovered(true);
    }

    public void SetKeyChestDiscovered(bool fromClue = false)
    {
        if (hasDiscoveredKeyChest)
            return;

        hasDiscoveredKeyChest = true;

        if (cachedCompanionAgent != null)
        {
            if (storedKeyChestTarget != null)
                cachedCompanionAgent.sandikHedefi = storedKeyChestTarget;
            else if (cachedEastChest != null)
                cachedCompanionAgent.sandikHedefi = cachedEastChest.transform;
        }

        bool hasComp = GameManager.Instance != null && GameManager.Instance.HasCompanion;
        bool hasK = GameManager.Instance != null && GameManager.Instance.HasKey;

        if (hasComp && !hasK)
        {
            PlayKeyUnlockSfx();
            string sub = fromClue
                ? "Yazıttan sandığın yerini öğrendin! [SPACE] ile Yoldaş'ı gönder"
                : "Doğu sandığı keşfedildi! [SPACE] ile Yoldaş'ı anahtara gönder";

            ShowEventPopup(
                keyIconSprite,
                string.Empty,
                "ANAHTAR SANDIĞI KEŞFEDİLDİ",
                sub,
                new Color(1f, 0.85f, 0.38f, 1f),
                3.2f,
                0f);
        }
    }

    public Sprite KeyIconSprite
    {
        get
        {
            EnsureSpritesLoaded();
            return keyIconSprite;
        }
    }

    public Sprite CompanionIconSprite
    {
        get
        {
            EnsureSpritesLoaded();
            return companionIconSprite;
        }
    }

    public bool IsGuardChatOpen => guardChatPanel != null && guardChatPanel.activeSelf;

    public bool IsVictoryScreenOpen => victoryScreenPanel != null && victoryScreenPanel.activeSelf;

    public bool IsStartScreenOpen => startScreenPanel != null && startScreenPanel.activeSelf;

    public bool IsPauseMenuOpen => pauseMenuPanel != null && pauseMenuPanel.activeSelf;

#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
    private static void InitEditorGuardChatUI()
    {
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (!Application.isPlaying)
            {
                if (UnityEditor.EditorBuildSettings.scenes == null || UnityEditor.EditorBuildSettings.scenes.Length == 0)
                {
                    const string mainScenePath = "Assets/Scenes/MainScene.unity";
                    if (System.IO.File.Exists(mainScenePath))
                    {
                        UnityEditor.EditorBuildSettings.scenes = new[]
                        {
                            new UnityEditor.EditorBuildSettingsScene(mainScenePath, true)
                        };
                    }
                }

                DialogueManager dm = Object.FindFirstObjectByType<DialogueManager>();
                if (dm != null)
                {
                    dm.EnsureSpritesLoaded();
                    dm.EnsureGuardChatUI();
                    dm.EnsureStatusHudUI();
                    dm.EnsureEventPopupUI();
                    dm.EnsureVictoryScreenUI();
                    dm.EnsureStartScreenUI();
                    dm.EnsurePauseMenuUI();
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
        Time.timeScale = 1f;

        EnsureSpritesLoaded();
        EnsureLLMManager();
        EnsureGuardChatUI();
        EnsureStatusHudUI();
        EnsureEventPopupUI();
        EnsureVictoryScreenUI();
        EnsureStartScreenUI();
        EnsurePauseMenuUI();
        EnsureGuardInteractableInScene();

        HideDialogue();
        CloseGuardChat();
        HideVictoryScreen();
        ClosePauseMenu();
        HideEventPopupImmediate();
        ShowInitialStartScreenIfNeeded();
        RefreshStatusHud();

        if (Application.isPlaying)
        {
            EnsureAudioSources();
        }
    }

    private void Update()
    {
        RefreshStatusHud();

        if (IsStartScreenOpen)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
                Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape))
            {
                CloseStartScreen();
            }
            return;
        }

        if (IsVictoryScreenOpen)
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Return) ||
                Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
            {
                ;
            }
            return;
        }

        if (IsPauseMenuOpen)
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
            {
                ClosePauseMenu();
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

        if (Input.GetKeyDown(KeyCode.P))
        {
            OpenPauseMenu();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (dialoguePanel != null && dialoguePanel.activeSelf)
            {
                HideDialogue();
            }
            else if (!AncientTablet.IsAnyKitabeOpen)
            {
                OpenPauseMenu();
            }
        }

        bool hasComp = GameManager.Instance != null && GameManager.Instance.HasCompanion;
        bool hasK = GameManager.Instance != null && GameManager.Instance.HasKey;

        if (Input.GetKeyDown(KeyCode.Space) && hasComp && !hasK && !hasDiscoveredKeyChest)
        {
            if (cachedCompanionAgent != null)
            {
                cachedCompanionAgent.ChangeState(new FollowState());
                if (cachedCompanionAgent.ajanMotoru != null && cachedCompanionAgent.ajanMotoru.isOnNavMesh)
                {
                    cachedCompanionAgent.ajanMotoru.ResetPath();
                }
            }

            PlayDefeatSfx();
            ShowEventPopup(
                companionIconSprite,
                string.Empty,
                "HEDEF BİLİNMİYOR",
                "Önce anahtarın bulunduğu Doğu sandığını keşfetmelisin!",
                new Color(1f, 0.74f, 0.32f, 1f),
                2.6f,
                0f);
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

        bool isThinkingMood = !string.IsNullOrWhiteSpace(response.mood) &&
                              response.mood.Trim().Equals("THINKING", System.StringComparison.OrdinalIgnoreCase);
        SetWorldGuardThinkingSprite(isThinkingMood);
        if (guardPortraitImage != null)
        {
            Sprite moodSprite = isThinkingMood
                ? (guardThinkingSprite != null ? guardThinkingSprite : guardNormalSprite)
                : (guardNormalSprite != null ? guardNormalSprite : guardThinkingSprite);
            if (moodSprite != null)
                guardPortraitImage.sprite = moodSprite;
        }

        if (action == "KILL_PLAYER")
        {
            UpdateStatusBadge("SALDIRIYOR!", new Color(1f, 0.32f, 0.28f, 1f));
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
                UpdateStatusBadge("KAPI AÇILDI", new Color(0.40f, 0.92f, 0.52f, 1f));
                if (guardReplyText != null)
                {
                    guardReplyText.color = new Color(0.65f, 0.96f, 0.72f, 1f);
                }
                ShowGuardReplyTypewriter(reply);
                HandleOpenDoorAction();
            }
            else
            {
                UpdateStatusBadge("ANAHTAR GEREKLİ", new Color(1f, 0.72f, 0.25f, 1f));
                if (guardReplyText != null)
                {
                    guardReplyText.color = new Color(1f, 0.85f, 0.52f, 1f);
                }
                ShowGuardReplyTypewriter(
                    reply +
                    "\n\n<color=#FFBE44>[Muhafız ikna oldu ancak kapıyı açmak için Kutsal Anahtar gerekiyor. Önce Yoldaş ile doğu terasındaki sandıktan anahtarı al.]</color>");

                if (playerInputField != null && IsGuardChatOpen)
                {
                    playerInputField.Select();
                    playerInputField.ActivateInputField();
                }
            }
        }
        else
        {
            UpdateStatusBadge("DİNLİYOR", new Color(0.92f, 0.78f, 0.42f, 1f));
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

        yield return new WaitForSecondsRealtime(1.8f);

        SetWorldGuardThinkingSprite(false);
        CloseGuardChat();

        GuardAgent guardAgent = FindFirstObjectByType<GuardAgent>();
        if (guardAgent == null)
        {
            GameObject guardVisualObj = GameObject.Find("muhafız - normal_0");
            if (guardVisualObj != null)
            {
                guardAgent = guardVisualObj.GetComponent<GuardAgent>();
                if (guardAgent == null)
                {
                    guardAgent = guardVisualObj.AddComponent<GuardAgent>();
                }
            }
        }

        if (guardAgent != null)
        {
            guardAgent.StartAttackPlayer();
            float waitTimer = 0f;
            while (GuardAgent.IsAnyGuardAttacking && waitTimer < 3.2f)
            {
                waitTimer += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        ShowEndScreen(
            false,
            "ÖLDÜNÜZ",
            "Tapınak Muhafızı seni yakaladı.",
            guardNormalSprite != null ? guardNormalSprite : keyIconSprite,
            0f);
    }

    public void HandleOpenDoorAction()
    {
        EnsureSpritesLoaded();

        if (GameManager.Instance == null || !GameManager.Instance.HasKey)
        {
            Debug.LogWarning("GÜVENLİK KONTROLÜ: HasKey == false olduğu için OPEN_DOOR işlemi engellendi.");
            return;
        }

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

        yield return new WaitForSecondsRealtime(1.8f);

        CloseGuardChat();

        ShowEventPopup(
            woodenGateOpenedSprite != null ? woodenGateOpenedSprite : keyIconSprite,
            string.Empty,
            "KAPI AÇILDI",
            "Tapınağın kapısı açıldı!",
            new Color(0.42f, 0.94f, 0.56f, 1f),
            1.6f,
            0f);

        yield return new WaitForSecondsRealtime(1.3f);

        if (!IsVictoryScreenOpen)
        {
            ShowVictoryScreen();
        }
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
            targetCanvas = dialoguePanel.GetComponentInParent<Canvas>(true);
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

        EnsureQuickPromptButtons();
    }

    private void EnsureQuickPromptButtons()
    {
        if (guardChatPanel == null)
            return;

        Transform mainFrame = guardChatPanel.transform.Find("MainFrame");
        if (mainFrame == null)
            return;

        Transform existingQuickRow = mainFrame.Find("QuickPromptsRow");
        Button persuadeBtn = null;
        Button provokeBtn = null;

        if (existingQuickRow != null)
        {
            Transform p1 = existingQuickRow.Find("PersuadeQuickBtn");
            if (p1 != null)
                persuadeBtn = p1.GetComponent<Button>();
            Transform p2 = existingQuickRow.Find("ProvokeQuickBtn");
            if (p2 != null)
                provokeBtn = p2.GetComponent<Button>();
        }
        else
        {
            TMP_FontAsset sharedFont = FindSharedFont();

            GameObject rowObj = new GameObject("QuickPromptsRow");
            rowObj.transform.SetParent(mainFrame, false);
            RectTransform rowRt = rowObj.AddComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0f, 1f);
            rowRt.anchorMax = new Vector2(1f, 1f);
            rowRt.pivot = new Vector2(0.5f, 1f);
            rowRt.anchoredPosition = new Vector2(0f, -14f);
            rowRt.sizeDelta = new Vector2(0f, 34f);

            persuadeBtn = CreateQuickPromptButton(
                rowObj.transform,
                "PersuadeQuickBtn",
                new Vector2(360f, 0f),
                new Vector2(175f, 30f),
                "✦ İkna Cümlesi",
                new Color(0.16f, 0.36f, 0.24f, 0.95f),
                new Color(0.45f, 0.90f, 0.58f, 0.85f),
                sharedFont);

            provokeBtn = CreateQuickPromptButton(
                rowObj.transform,
                "ProvokeQuickBtn",
                new Vector2(545f, 0f),
                new Vector2(175f, 30f),
                "⚔ Kışkırtma Cümlesi",
                new Color(0.40f, 0.16f, 0.16f, 0.95f),
                new Color(0.95f, 0.45f, 0.42f, 0.85f),
                sharedFont);
        }

        if (persuadeBtn != null)
        {
            persuadeBtn.onClick.RemoveAllListeners();
            persuadeBtn.onClick.AddListener(() =>
            {
                FillQuickPrompt("Saygıdeğer Muhafız, rün sınavını geçtim ve Kutsal Anahtar'ı getirdim. Tapınağın emanetini korumaya geldim, lütfen kapıyı aç.");
            });
        }

        if (provokeBtn != null)
        {
            provokeBtn.onClick.RemoveAllListeners();
            provokeBtn.onClick.AddListener(() =>
            {
                FillQuickPrompt("Çekil yolumdan muhafız! Bu tapınaktaki tüm hazineleri zorla alacağım!");
            });
        }
    }

    private static Button CreateQuickPromptButton(
        Transform parent,
        string name,
        Vector2 anchoredPos,
        Vector2 size,
        string label,
        Color bgColor,
        Color borderColor,
        TMP_FontAsset sharedFont)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);
        RectTransform rt = btnObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        Image bg = btnObj.AddComponent<Image>();
        bg.color = bgColor;

        Outline outline = btnObj.AddComponent<Outline>();
        outline.effectColor = borderColor;
        outline.effectDistance = new Vector2(1f, -1f);

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = bg;

        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(btnObj.transform, false);
        RectTransform txtRt = txtObj.AddComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = new Vector2(6f, 2f);
        txtRt.offsetMax = new Vector2(-6f, -2f);

        TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
        if (sharedFont != null)
            tmp.font = sharedFont;
        tmp.text = label;
        tmp.fontSize = 13.5f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.95f, 0.94f, 0.90f, 1f);

        return btn;
    }

    private void FillQuickPrompt(string promptText)
    {
        if (isWaitingForLLM || playerInputField == null)
            return;

        playerInputField.text = promptText;
        playerInputField.Select();
        playerInputField.ActivateInputField();
    }


    public void EnsureStatusHudUI()
    {
        Canvas targetCanvas = null;
        if (dialoguePanel != null)
            targetCanvas = dialoguePanel.GetComponentInParent<Canvas>(true);
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
                EnsureObjectiveBannerUI(statusHudPanel.transform, FindSharedFont());
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

        EnsureObjectiveBannerUI(statusHudPanel.transform, sharedFont);

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
        bool hasCompanion = GameManager.Instance != null && GameManager.Instance.HasCompanion;

        if (cachedCompanionAgent == null)
        {
            cachedCompanionAgent = FindFirstObjectByType<CompanionAgent>(FindObjectsInactive.Include);
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

            if (Application.isPlaying)
            {
                if (companionBaseLocalScale == Vector3.zero)
                {
                    companionBaseLocalScale = cachedCompanionAgent.transform.localScale;
                    if (companionBaseLocalScale.sqrMagnitude < 0.0001f)
                        companionBaseLocalScale = Vector3.one;
                }

                float flapSpeed = (cachedCompanionAgent.ajanMotoru != null && cachedCompanionAgent.ajanMotoru.velocity.sqrMagnitude > 0.05f)
                    ? 13.5f
                    : 7.5f;
                float flap = Mathf.Sin(Time.time * flapSpeed);
                float sx = companionBaseLocalScale.x * (1f + flap * 0.065f);
                float sy = companionBaseLocalScale.y * (1f - flap * 0.045f);
                cachedCompanionAgent.transform.localScale = new Vector3(sx, sy, companionBaseLocalScale.z);
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
                        TreasureChest chest = cachedCompanionAgent.sandikHedefi.GetComponent<TreasureChest>();
                        if (chest == null)
                            chest = cachedCompanionAgent.sandikHedefi.GetComponentInParent<TreasureChest>();
                        if (chest == null)
                            chest = cachedCompanionAgent.sandikHedefi.GetComponentInChildren<TreasureChest>();

                        if (chest != null && !chest.IsOpen)
                        {
                            chest.Interact();
                        }
                        else if (!hasKey && GameManager.Instance != null)
                        {
                            GameManager.Instance.SetHasKey(true);
                        }
                        hasKey = GameManager.Instance != null && GameManager.Instance.HasKey;
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

        if (Application.isPlaying)
        {
            if (!stateTrackingInitialized)
            {
                lastTrackedHasCompanion = hasCompanion;
                lastTrackedHasKey = hasKey;
                lastTrackedCompanionState = companionStateName;
                stateTrackingInitialized = true;
            }
            else
            {
                if (!lastTrackedHasCompanion && hasCompanion)
                {
                    lastTrackedHasCompanion = true;
                    EnsureSpritesLoaded();
                    PlayCompanionUnlockSfx();
                    ShowEventPopup(
                        companionIconSprite,
                        string.Empty,
                        "YOLDAŞ AÇILDI",
                        "Doğu terasındaki Kutsal Anahtar sandığını ara",
                        new Color(0.42f, 0.86f, 1f, 1f),
                        3.0f,
                        0.15f);
                }

                if (lastTrackedCompanionState != "NavigateToKeyState" && companionStateName == "NavigateToKeyState")
                {
                    EnsureSpritesLoaded();
                    PlayCompanionSendSfx();
                    ShowEventPopup(
                        companionIconSprite,
                        string.Empty,
                        "YOLDAŞ ANAHTARI ALMAYA GÖNDERİLDİ",
                        "Doğu terasındaki sandığa gidiyor...",
                        new Color(1f, 0.82f, 0.34f, 1f),
                        2.6f,
                        0f);
                }

                if (!lastTrackedHasKey && hasKey)
                {
                    lastTrackedHasKey = true;
                    EnsureSpritesLoaded();
                    PlayKeyUnlockSfx();
                    ShowEventPopup(
                        keyIconSprite,
                        string.Empty,
                        "ANAHTAR AÇILDI",
                        "Kutsal Anahtar envantere eklendi",
                        new Color(0.45f, 0.95f, 0.58f, 1f),
                        3.2f,
                        0f);
                }

                lastTrackedHasCompanion = hasCompanion;
                lastTrackedHasKey = hasKey;
                lastTrackedCompanionState = companionStateName;
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
                keyHudStateText.text = "Alındı";
                keyHudStateText.color = new Color(0.45f, 0.94f, 0.56f, 1f);
            }
            else
            {
                keyHudStateText.text = "Yok";
                keyHudStateText.color = new Color(0.68f, 0.66f, 0.62f, 0.9f);
            }
        }

        bool companionActive = hasCompanion ||
                               (cachedCompanionAgent != null && cachedCompanionAgent.gameObject.activeInHierarchy);

        if (companionHudIcon != null)
        {
            companionHudIcon.color = companionActive
                ? Color.white
                : new Color(0.38f, 0.38f, 0.42f, 0.55f);
        }

        if (!hasDiscoveredKeyChest)
        {
            if (cachedEastChest == null)
            {
                TreasureChest[] allChests = FindObjectsByType<TreasureChest>(FindObjectsSortMode.None);
                foreach (var c in allChests)
                {
                    if (c != null && c.transform.position.x > 0f)
                    {
                        cachedEastChest = c;
                        break;
                    }
                }
            }

            Transform playerTr = null;
            if (GameManager.Instance != null)
                playerTr = GameManager.Instance.PlayerTransform;
            if (playerTr == null && Camera.main != null)
            {
                var camFollow = Camera.main.GetComponent<Cainos.PixelArtTopDown_Basic.CameraFollow>();
                if (camFollow != null && camFollow.target != null)
                    playerTr = camFollow.target;
            }
            if (playerTr == null)
            {
                var pc = Object.FindAnyObjectByType<Cainos.PixelArtTopDown_Basic.TopDownCharacterController>();
                if (pc != null)
                    playerTr = pc.transform;
            }

            if (playerTr != null)
            {
                float distToChest = cachedEastChest != null
                    ? Vector2.Distance(playerTr.position, cachedEastChest.transform.position)
                    : 999f;

                if (distToChest < 4.2f || (playerTr.position.x > 3.0f && playerTr.position.y > -2.2f))
                {
                    SetKeyChestDiscovered(false);
                }
            }

            if (cachedCompanionAgent != null)
            {
                if (cachedCompanionAgent.sandikHedefi != null)
                {
                    storedKeyChestTarget = cachedCompanionAgent.sandikHedefi;
                    cachedCompanionAgent.sandikHedefi = null;
                }
                if (companionStateName == "NavigateToKeyState")
                {
                    cachedCompanionAgent.ChangeState(new FollowState());
                }
            }
        }
        else if (cachedCompanionAgent != null && cachedCompanionAgent.sandikHedefi == null)
        {
            if (storedKeyChestTarget != null)
                cachedCompanionAgent.sandikHedefi = storedKeyChestTarget;
            else if (cachedEastChest != null)
                cachedCompanionAgent.sandikHedefi = cachedEastChest.transform;
        }

        if (companionHudStateText != null)
        {
            if (!companionActive)
            {
                companionHudStateText.text = "Sandıkta";
                companionHudStateText.color = new Color(0.68f, 0.66f, 0.62f, 0.9f);
            }
            else if (!hasDiscoveredKeyChest && !hasKey)
            {
                companionHudStateText.text = "Sandığı Bul";
                companionHudStateText.color = new Color(1f, 0.78f, 0.32f, 1f);
            }
            else if (companionStateName == "NavigateToKeyState")
            {
                companionHudStateText.text = "Görevde...";
                companionHudStateText.color = new Color(1f, 0.78f, 0.32f, 1f);
            }
            else if (companionStateName == "CollectAndReturnState")
            {
                companionHudStateText.text = hasKey ? "Dönüyor..." : "Görevde...";
                companionHudStateText.color = new Color(0.45f, 0.94f, 0.56f, 1f);
            }
            else
            {
                companionHudStateText.text = hasKey ? "Yanında" : "Hazır [SPACE]";
                companionHudStateText.color = hasKey
                    ? new Color(0.45f, 0.94f, 0.56f, 1f)
                    : new Color(0.85f, 0.92f, 1f, 1f);
            }
        }

        if (objectiveHudText != null)
        {
            if (cachedRuneAltar == null)
            {
                cachedRuneAltar = FindFirstObjectByType<RuneAltar>();
            }

            bool altarActive = cachedRuneAltar != null && cachedRuneAltar.IsActivated;
            int pillarCount = AncientTablet.ReadPillarsCount;
            bool hasRead3Pillars = pillarCount >= 3;

            if (!altarActive && !hasCompanion && !hasKey)
            {
                objectiveHudText.text =
                    "<color=#F2CA58>HEDEF 1/5:</color> Kapıyı açmanın yolunu bul\n" +
                    "<size=11><color=#D8CEAA>Kutuyu güneydeki Kadim Rün Sunağı'nın üzerine it</color></size>";
            }
            else if (!hasRead3Pillars && !hasCompanion && !hasKey)
            {
                objectiveHudText.text =
                    $"<color=#62DAFF>HEDEF 2/5:</color> Tapınaktaki 3 sütun anıtını oku ({pillarCount}/3)\n" +
                    "<size=11><color=#BFE8F2>Avluya gir ve [E] ile dikili sütunlardaki (Pillar) yazıtları incele</color></size>";
            }
            else if (!hasCompanion && !hasKey)
            {
                objectiveHudText.text =
                    "<color=#62DAFF>HEDEF 3/5:</color> Haritadaki otonom yoldaşı bul\n" +
                    "<size=11><color=#BFE8F2>Batı terasındaki sandığa git ve [E] ile kanatlı yoldaşı uyandır</color></size>";
            }
            else if (!hasKey)
            {
                if (!hasDiscoveredKeyChest)
                {
                    objectiveHudText.text =
                        "<color=#FFD15C>HEDEF 4/5:</color> Anahtarın bulunduğu sandığın yerini bul\n" +
                        "<size=11><color=#EEDAA4>Doğu terasındaki dikenli geçide ve sandığa yaklaş</color></size>";
                }
                else if (companionStateName == "NavigateToKeyState" || companionStateName == "CollectAndReturnState")
                {
                    objectiveHudText.text =
                        "<color=#FFD15C>HEDEF 4/5:</color> Yoldaş Doğu sandığından anahtarı getiriyor...\n" +
                        "<size=11><color=#EEDAA4>Dikenlerin üzerinden uçup anahtarla dönmesini bekle</color></size>";
                }
                else
                {
                    objectiveHudText.text =
                        "<color=#FFD15C>HEDEF 4/5:</color> [SPACE] ile Yoldaş'ı anahtara gönder\n" +
                        "<size=11><color=#EEDAA4>Doğu sandığı keşfedildi, [SPACE] tuşuna basarak yoldaşı yolla</color></size>";
                }
            }
            else
            {
                int readCount = AncientTablet.ReadTabletsCount;
                int gravestoneCount = Mathf.Max(0, readCount - AncientTablet.ReadPillarsCount);
                string hintSuffix = gravestoneCount > 0
                    ? $"({gravestoneCount} mezar taşı incelendi)"
                    : "(Muhafız ciddiyet ve saygı bekler)";

                objectiveHudText.text =
                    "<color=#70F294>HEDEF 5/5:</color> Muhafız'la konuş ve tapınak emanetine giden kapıyı açtır\n" +
                    $"<size=11><color=#CAD6E2>✦ İsteğe bağlı: Mezar taşlarını oku ve tecrübelen {hintSuffix}</color></size>";
            }
        }
    }

    private void EnsureObjectiveBannerUI(Transform hudParent, TMP_FontAsset sharedFont)
    {
        if (hudParent == null)
            return;

        Transform existingBanner = hudParent.Find("ObjectiveBanner");
        if (existingBanner != null)
        {
            RectTransform existingRt = existingBanner.GetComponent<RectTransform>();
            if (existingRt != null && existingRt.sizeDelta.y < 45f)
            {
                existingRt.sizeDelta = new Vector2(0f, 48f);
            }
            Transform txtTr = existingBanner.Find("Text");
            if (txtTr != null)
            {
                objectiveHudText = txtTr.GetComponent<TextMeshProUGUI>();
                if (objectiveHudText != null)
                {
                    objectiveHudText.lineSpacing = -12f;
                    return;
                }
            }
            DestroyImmediate(existingBanner.gameObject);
        }

        GameObject bannerObj = new GameObject("ObjectiveBanner");
        bannerObj.transform.SetParent(hudParent, false);
        RectTransform bannerRt = bannerObj.AddComponent<RectTransform>();
        bannerRt.anchorMin = new Vector2(0f, 0f);
        bannerRt.anchorMax = new Vector2(1f, 0f);
        bannerRt.pivot = new Vector2(0f, 1f);
        bannerRt.anchoredPosition = new Vector2(0f, -8f);
        bannerRt.sizeDelta = new Vector2(0f, 48f);

        Image bannerBg = bannerObj.AddComponent<Image>();
        bannerBg.color = new Color(0.09f, 0.10f, 0.13f, 0.92f);

        Outline bannerOutline = bannerObj.AddComponent<Outline>();
        bannerOutline.effectColor = new Color(0.58f, 0.48f, 0.31f, 0.90f);
        bannerOutline.effectDistance = new Vector2(1.5f, -1.5f);

        Shadow bannerShadow = bannerObj.AddComponent<Shadow>();
        bannerShadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
        bannerShadow.effectDistance = new Vector2(0f, -4f);

        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(bannerObj.transform, false);
        RectTransform txtRt = txtObj.AddComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = new Vector2(14f, 4f);
        txtRt.offsetMax = new Vector2(-14f, -4f);

        objectiveHudText = txtObj.AddComponent<TextMeshProUGUI>();
        if (sharedFont != null)
            objectiveHudText.font = sharedFont;
        objectiveHudText.enableAutoSizing = true;
        objectiveHudText.fontSizeMin = 10.5f;
        objectiveHudText.fontSizeMax = 13.5f;
        objectiveHudText.lineSpacing = -12f;
        objectiveHudText.fontStyle = FontStyles.Bold;
        objectiveHudText.alignment = TextAlignmentOptions.MidlineLeft;
        objectiveHudText.color = new Color(0.92f, 0.91f, 0.88f, 1f);
        objectiveHudText.text = "<color=#F2CA58>HEDEF 1/5:</color> Kapıyı açmanın yolunu bul\n<size=11><color=#D8CEAA>Kutuyu güneydeki Kadim Rün Sunağı'nın üzerine it</color></size>";
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

    public void EnsureEventPopupUI()
    {
        EnsureSpritesLoaded();

        Canvas targetCanvas = null;
        if (dialoguePanel != null)
            targetCanvas = dialoguePanel.GetComponentInParent<Canvas>(true);
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

        Transform existingPopup = targetCanvas.transform.Find("EventPopupPanel");
        if (existingPopup != null)
        {
            Transform v2Marker = existingPopup.Find("Inner/V2Marker");
            if (v2Marker != null)
            {
                eventPopupPanel = existingPopup.gameObject;
                eventPopupCanvasGroup = eventPopupPanel.GetComponent<CanvasGroup>();
                eventPopupBorderImage = eventPopupPanel.GetComponent<Image>();

                Transform iconBorderTr = eventPopupPanel.transform.Find("Inner/IconFrame");
                if (iconBorderTr != null)
                    eventPopupIconBorderImage = iconBorderTr.GetComponent<Image>();

                Transform iconTr = eventPopupPanel.transform.Find("Inner/IconFrame/Icon");
                if (iconTr != null)
                    eventPopupIconImage = iconTr.GetComponent<Image>();

                Transform titleTr = eventPopupPanel.transform.Find("Inner/TitleText");
                if (titleTr != null)
                    eventPopupTitleText = titleTr.GetComponent<TextMeshProUGUI>();

                Transform subTr = eventPopupPanel.transform.Find("Inner/SubtitleText");
                if (subTr != null)
                    eventPopupSubtitleText = subTr.GetComponent<TextMeshProUGUI>();

                if (eventPopupCanvasGroup != null && eventPopupIconImage != null &&
                    eventPopupTitleText != null && eventPopupSubtitleText != null)
                {
                    return;
                }
            }

            if (Application.isPlaying)
                Destroy(existingPopup.gameObject);
            else
                DestroyImmediate(existingPopup.gameObject);
        }

        TMP_FontAsset sharedFont = FindSharedFont();

        eventPopupPanel = new GameObject("EventPopupPanel");
        eventPopupPanel.layer = 5;
        eventPopupPanel.transform.SetParent(targetCanvas.transform, false);

        RectTransform popupRect = eventPopupPanel.AddComponent<RectTransform>();
        popupRect.anchorMin = new Vector2(0.5f, 1f);
        popupRect.anchorMax = new Vector2(0.5f, 1f);
        popupRect.pivot = new Vector2(0.5f, 1f);
        popupRect.anchoredPosition = new Vector2(0f, -26f);
        popupRect.sizeDelta = new Vector2(510f, 88f);

        eventPopupCanvasGroup = eventPopupPanel.AddComponent<CanvasGroup>();
        eventPopupCanvasGroup.alpha = 0f;
        eventPopupCanvasGroup.interactable = false;
        eventPopupCanvasGroup.blocksRaycasts = false;

        GameObject shadowObj = new GameObject("SoftShadow");
        shadowObj.transform.SetParent(eventPopupPanel.transform, false);
        RectTransform shadowRect = shadowObj.AddComponent<RectTransform>();
        shadowRect.anchorMin = Vector2.zero;
        shadowRect.anchorMax = Vector2.one;
        shadowRect.offsetMin = new Vector2(-5f, -6f);
        shadowRect.offsetMax = new Vector2(5f, 5f);
        Image shadowImg = shadowObj.AddComponent<Image>();
        shadowImg.color = new Color(0f, 0f, 0f, 0.48f);
        shadowImg.raycastTarget = false;

        eventPopupBorderImage = eventPopupPanel.AddComponent<Image>();
        eventPopupBorderImage.color = new Color(0.86f, 0.72f, 0.36f, 0.92f);
        eventPopupBorderImage.raycastTarget = false;

        GameObject innerObj = new GameObject("Inner");
        innerObj.transform.SetParent(eventPopupPanel.transform, false);
        RectTransform innerRect = innerObj.AddComponent<RectTransform>();
        innerRect.anchorMin = Vector2.zero;
        innerRect.anchorMax = Vector2.one;
        innerRect.offsetMin = new Vector2(2f, 2f);
        innerRect.offsetMax = new Vector2(-2f, -2f);
        Image innerBg = innerObj.AddComponent<Image>();
        innerBg.color = new Color(0.09f, 0.11f, 0.15f, 0.95f);
        innerBg.raycastTarget = false;

        GameObject v2Obj = new GameObject("V2Marker");
        v2Obj.transform.SetParent(innerObj.transform, false);

        GameObject iconFrameObj = new GameObject("IconFrame");
        iconFrameObj.transform.SetParent(innerObj.transform, false);
        RectTransform iconFrameRect = iconFrameObj.AddComponent<RectTransform>();
        iconFrameRect.anchorMin = new Vector2(0f, 0.5f);
        iconFrameRect.anchorMax = new Vector2(0f, 0.5f);
        iconFrameRect.pivot = new Vector2(0f, 0.5f);
        iconFrameRect.anchoredPosition = new Vector2(12f, 0f);
        iconFrameRect.sizeDelta = new Vector2(64f, 64f);

        eventPopupIconBorderImage = iconFrameObj.AddComponent<Image>();
        eventPopupIconBorderImage.color = new Color(0.14f, 0.16f, 0.21f, 1f);
        eventPopupIconBorderImage.raycastTarget = false;

        Outline iconOutline = iconFrameObj.AddComponent<Outline>();
        iconOutline.effectColor = new Color(0.86f, 0.72f, 0.36f, 0.65f);
        iconOutline.effectDistance = new Vector2(1.5f, -1.5f);

        GameObject iconObj = new GameObject("Icon");
        iconObj.transform.SetParent(iconFrameObj.transform, false);
        RectTransform iconRect = iconObj.AddComponent<RectTransform>();
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.offsetMin = new Vector2(7f, 7f);
        iconRect.offsetMax = new Vector2(-7f, -7f);

        eventPopupIconImage = iconObj.AddComponent<Image>();
        eventPopupIconImage.preserveAspect = true;
        eventPopupIconImage.raycastTarget = false;
        if (companionIconSprite != null)
            eventPopupIconImage.sprite = companionIconSprite;

        GameObject titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(innerObj.transform, false);
        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 0.5f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.offsetMin = new Vector2(90f, -2f);
        titleRect.offsetMax = new Vector2(-16f, -10f);

        eventPopupTitleText = titleObj.AddComponent<TextMeshProUGUI>();
        if (sharedFont != null)
            eventPopupTitleText.font = sharedFont;
        eventPopupTitleText.enableAutoSizing = true;
        eventPopupTitleText.fontSizeMin = 15f;
        eventPopupTitleText.fontSizeMax = 21f;
        eventPopupTitleText.fontStyle = FontStyles.Bold;
        eventPopupTitleText.alignment = TextAlignmentOptions.BottomLeft;
        eventPopupTitleText.color = Color.white;
        eventPopupTitleText.raycastTarget = false;
        eventPopupTitleText.text = "YOLDAŞ AÇILDI";

        GameObject subObj = new GameObject("SubtitleText");
        subObj.transform.SetParent(innerObj.transform, false);
        RectTransform subRect = subObj.AddComponent<RectTransform>();
        subRect.anchorMin = new Vector2(0f, 0f);
        subRect.anchorMax = new Vector2(1f, 0.5f);
        subRect.offsetMin = new Vector2(90f, 10f);
        subRect.offsetMax = new Vector2(-16f, 0f);

        eventPopupSubtitleText = subObj.AddComponent<TextMeshProUGUI>();
        if (sharedFont != null)
            eventPopupSubtitleText.font = sharedFont;
        eventPopupSubtitleText.enableAutoSizing = true;
        eventPopupSubtitleText.fontSizeMin = 12f;
        eventPopupSubtitleText.fontSizeMax = 15f;
        eventPopupSubtitleText.alignment = TextAlignmentOptions.TopLeft;
        eventPopupSubtitleText.color = new Color(0.82f, 0.82f, 0.78f, 0.95f);
        eventPopupSubtitleText.raycastTarget = false;
        eventPopupSubtitleText.text = string.Empty;

        eventPopupPanel.SetActive(false);
    }

    public void ShowEventPopup(
        Sprite icon,
        string badgeText,
        string titleText,
        string subtitleText,
        Color accentColor,
        float duration = 3.0f,
        float delay = 0f)
    {
        EnsureEventPopupUI();
        if (eventPopupPanel == null)
            return;

        if (eventPopupCoroutine != null)
        {
            StopCoroutine(eventPopupCoroutine);
        }

        eventPopupCoroutine = StartCoroutine(
            AnimateEventPopupRoutine(icon, titleText, subtitleText, accentColor, duration, delay));
    }

    private IEnumerator AnimateEventPopupRoutine(
        Sprite icon,
        string titleText,
        string subtitleText,
        Color accentColor,
        float duration,
        float delay)
    {
        if (delay > 0.001f)
        {
            yield return new WaitForSecondsRealtime(delay);
        }

        if (eventPopupIconImage != null)
        {
            eventPopupIconImage.sprite = icon;
            eventPopupIconImage.enabled = icon != null;
        }

        if (eventPopupTitleText != null)
        {
            eventPopupTitleText.text = titleText;
            eventPopupTitleText.color = Color.Lerp(accentColor, Color.white, 0.35f);
        }

        if (eventPopupSubtitleText != null)
        {
            eventPopupSubtitleText.text = subtitleText;
        }

        if (eventPopupBorderImage != null)
        {
            eventPopupBorderImage.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.92f);
        }

        eventPopupPanel.transform.SetAsLastSibling();
        eventPopupPanel.SetActive(true);

        RectTransform rt = eventPopupPanel.GetComponent<RectTransform>();
        float enterDuration = 0.20f;
        float elapsed = 0f;

        while (elapsed < enterDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / enterDuration);
            float easeOut = 1f - Mathf.Pow(1f - t, 3f);

            if (eventPopupCanvasGroup != null)
                eventPopupCanvasGroup.alpha = easeOut;

            if (rt != null)
            {
                float scale = Mathf.Lerp(0.90f, 1.0f, easeOut);
                rt.localScale = new Vector3(scale, scale, 1f);
            }

            yield return null;
        }

        if (eventPopupCanvasGroup != null)
            eventPopupCanvasGroup.alpha = 1f;
        if (rt != null)
            rt.localScale = Vector3.one;

        yield return new WaitForSecondsRealtime(duration);

        float exitDuration = 0.28f;
        elapsed = 0f;
        while (elapsed < exitDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / exitDuration);

            if (eventPopupCanvasGroup != null)
                eventPopupCanvasGroup.alpha = 1f - t;

            if (rt != null)
            {
                float scale = Mathf.Lerp(1.0f, 0.95f, t);
                rt.localScale = new Vector3(scale, scale, 1f);
            }

            yield return null;
        }

        HideEventPopupImmediate();
        eventPopupCoroutine = null;
    }

    private void HideEventPopupImmediate()
    {
        if (eventPopupCanvasGroup != null)
            eventPopupCanvasGroup.alpha = 0f;
        if (eventPopupPanel != null)
            eventPopupPanel.SetActive(false);
    }

    public void EnsureVictoryScreenUI()
    {
        EnsureSpritesLoaded();

        Canvas targetCanvas = null;
        if (dialoguePanel != null)
        {
            targetCanvas = dialoguePanel.GetComponentInParent<Canvas>(true);
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
            Transform v2Card = existingVictory.Find("EndScreenCard_V2");
            if (v2Card != null)
            {
                victoryScreenPanel = existingVictory.gameObject;
                Transform restartBtnTr = v2Card.Find("RestartButton");
                if (restartBtnTr != null)
                    victoryRestartButton = restartBtnTr.GetComponent<Button>();

                Transform iconTr = v2Card.Find("IconBadge/MainIcon");
                if (iconTr != null)
                    victoryGateIcon = iconTr.GetComponent<Image>();

                BindVictoryButtons();
                return;
            }

            if (Application.isPlaying)
                Destroy(existingVictory.gameObject);
            else
                DestroyImmediate(existingVictory.gameObject);
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
        overlayImg.color = new Color(0.03f, 0.04f, 0.06f, 0.82f);
        overlayImg.raycastTarget = true;

        GameObject cardObj = new GameObject("EndScreenCard_V2");
        cardObj.transform.SetParent(overlayObj.transform, false);
        RectTransform cardRt = cardObj.AddComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0.5f, 0.5f);
        cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.sizeDelta = new Vector2(460f, 300f);

        Image cardBg = cardObj.AddComponent<Image>();
        cardBg.color = new Color(0.09f, 0.11f, 0.15f, 0.98f);

        Outline cardBorder = cardObj.AddComponent<Outline>();
        cardBorder.effectColor = new Color(0.88f, 0.74f, 0.36f, 0.90f);
        cardBorder.effectDistance = new Vector2(2f, -2f);

        Shadow cardShadow = cardObj.AddComponent<Shadow>();
        cardShadow.effectColor = new Color(0f, 0f, 0f, 0.80f);
        cardShadow.effectDistance = new Vector2(0f, -10f);

        GameObject iconBadge = new GameObject("IconBadge");
        iconBadge.transform.SetParent(cardObj.transform, false);
        RectTransform iconBadgeRt = iconBadge.AddComponent<RectTransform>();
        iconBadgeRt.anchorMin = new Vector2(0.5f, 1f);
        iconBadgeRt.anchorMax = new Vector2(0.5f, 1f);
        iconBadgeRt.pivot = new Vector2(0.5f, 1f);
        iconBadgeRt.anchoredPosition = new Vector2(0f, -28f);
        iconBadgeRt.sizeDelta = new Vector2(84f, 84f);

        Image iconBadgeBg = iconBadge.AddComponent<Image>();
        iconBadgeBg.color = new Color(0.13f, 0.16f, 0.20f, 1f);
        Outline iconBadgeOutline = iconBadge.AddComponent<Outline>();
        iconBadgeOutline.effectColor = new Color(0.88f, 0.74f, 0.36f, 0.75f);
        iconBadgeOutline.effectDistance = new Vector2(1.5f, -1.5f);

        GameObject mainIconObj = new GameObject("MainIcon");
        mainIconObj.transform.SetParent(iconBadge.transform, false);
        RectTransform mainIconRt = mainIconObj.AddComponent<RectTransform>();
        mainIconRt.anchorMin = Vector2.zero;
        mainIconRt.anchorMax = Vector2.one;
        mainIconRt.offsetMin = new Vector2(10f, 10f);
        mainIconRt.offsetMax = new Vector2(-10f, -10f);

        victoryGateIcon = mainIconObj.AddComponent<Image>();
        victoryGateIcon.preserveAspect = true;
        if (woodenGateOpenedSprite != null)
            victoryGateIcon.sprite = woodenGateOpenedSprite;

        GameObject titleObj = new GameObject("EndTitle");
        titleObj.transform.SetParent(cardObj.transform, false);
        RectTransform titleRt = titleObj.AddComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0f, -128f);
        titleRt.sizeDelta = new Vector2(-40f, 42f);

        TextMeshProUGUI titleTmp = titleObj.AddComponent<TextMeshProUGUI>();
        if (sharedFont != null)
            titleTmp.font = sharedFont;
        titleTmp.text = "OYUN TAMAMLANDI";
        titleTmp.fontSize = 28f;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.color = new Color(0.48f, 0.96f, 0.62f, 1f);

        GameObject descObj = new GameObject("EndSubtitle");
        descObj.transform.SetParent(cardObj.transform, false);
        RectTransform descRt = descObj.AddComponent<RectTransform>();
        descRt.anchorMin = new Vector2(0f, 1f);
        descRt.anchorMax = new Vector2(1f, 1f);
        descRt.pivot = new Vector2(0.5f, 1f);
        descRt.anchoredPosition = new Vector2(0f, -174f);
        descRt.sizeDelta = new Vector2(-50f, 36f);

        TextMeshProUGUI descTmp = descObj.AddComponent<TextMeshProUGUI>();
        if (sharedFont != null)
            descTmp.font = sharedFont;
        descTmp.text = "Tapınağın kapısını açtın.";
        descTmp.fontSize = 17f;
        descTmp.alignment = TextAlignmentOptions.Center;
        descTmp.color = new Color(0.86f, 0.85f, 0.82f, 1f);

        GameObject restartBtnObj = new GameObject("RestartButton");
        restartBtnObj.transform.SetParent(cardObj.transform, false);
        RectTransform restartRt = restartBtnObj.AddComponent<RectTransform>();
        restartRt.anchorMin = new Vector2(0.5f, 0f);
        restartRt.anchorMax = new Vector2(0.5f, 0f);
        restartRt.pivot = new Vector2(0.5f, 0f);
        restartRt.anchoredPosition = new Vector2(0f, 26f);
        restartRt.sizeDelta = new Vector2(220f, 48f);

        Image restartBg = restartBtnObj.AddComponent<Image>();
        restartBg.color = new Color(0.20f, 0.56f, 0.32f, 1f);
        Outline restartOutline = restartBtnObj.AddComponent<Outline>();
        restartOutline.effectColor = new Color(0.55f, 0.95f, 0.65f, 0.85f);
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
        restartTmp.text = "BAŞA DÖN";
        restartTmp.fontSize = 18.5f;
        restartTmp.fontStyle = FontStyles.Bold;
        restartTmp.alignment = TextAlignmentOptions.Center;
        restartTmp.color = Color.white;

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
    }

    public void ShowVictoryScreen()
    {
        ShowEndScreen(
            true,
            "OYUN TAMAMLANDI",
            "Tapınağın kapısını açtın.",
            woodenGateOpenedSprite != null ? woodenGateOpenedSprite : keyIconSprite,
            0f);
    }

    public void ShowEndScreen(bool isVictory, string title, string subtitle, Sprite icon, float autoRestartSeconds = 0f)
    {
        EnsureSpritesLoaded();
        EnsureVictoryScreenUI();
        HideEventPopupImmediate();

        if (victoryScreenPanel != null)
        {
            Transform cardTr = victoryScreenPanel.transform.Find("EndScreenCard_V2");
            if (cardTr != null)
            {
                Color accent = isVictory
                    ? new Color(0.45f, 0.94f, 0.58f, 1f)
                    : new Color(0.96f, 0.36f, 0.32f, 1f);

                Outline border = cardTr.GetComponent<Outline>();
                if (border != null)
                    border.effectColor = accent;

                Transform titleTr = cardTr.Find("EndTitle");
                if (titleTr != null)
                {
                    TextMeshProUGUI titleTmp = titleTr.GetComponent<TextMeshProUGUI>();
                    if (titleTmp != null)
                    {
                        titleTmp.text = title;
                        titleTmp.color = accent;
                    }
                }

                Transform subTr = cardTr.Find("EndSubtitle");
                if (subTr != null)
                {
                    TextMeshProUGUI subTmp = subTr.GetComponent<TextMeshProUGUI>();
                    if (subTmp != null)
                        subTmp.text = subtitle;
                }

                Transform btnTr = cardTr.Find("RestartButton");
                if (btnTr != null)
                {
                    Image btnImg = btnTr.GetComponent<Image>();
                    if (btnImg != null)
                    {
                        btnImg.color = isVictory
                            ? new Color(0.18f, 0.54f, 0.30f, 1f)
                            : new Color(0.62f, 0.20f, 0.20f, 1f);
                    }
                    Outline btnOutline = btnTr.GetComponent<Outline>();
                    if (btnOutline != null)
                        btnOutline.effectColor = accent;
                }
            }

            if (victoryGateIcon != null && icon != null)
            {
                victoryGateIcon.sprite = icon;
            }

            victoryScreenPanel.transform.SetAsLastSibling();
            victoryScreenPanel.SetActive(true);
        }

        if (isVictory)
            PlayVictorySfx();
        else
            PlayDefeatSfx();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetGameState(GameState.Dialogue);
        }

        if (autoRestartSeconds > 0f && gameObject.activeInHierarchy)
        {
            StartCoroutine(AutoRestartRoutine(autoRestartSeconds));
        }
    }

    private IEnumerator AutoRestartRoutine(float delaySeconds)
    {
        yield return new WaitForSecondsRealtime(delaySeconds);
        if (IsVictoryScreenOpen)
        {
            RestartGameFromVictory();
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
        HideEventPopupImmediate();
        stateTrackingInitialized = false;
        hasDiscoveredKeyChest = false;
        storedKeyChestTarget = null;
        AncientTablet.ResetReadCount();
        if (llmManager != null)
            llmManager.ResetConversationState();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetHasKey(false);
            GameManager.Instance.SetHasCompanion(false);
            GameManager.Instance.SetGameState(GameState.Playing);
        }

        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }

    public void EnsureStartScreenUI()
    {
        EnsureSpritesLoaded();

        Canvas targetCanvas = null;
        if (dialoguePanel != null)
            targetCanvas = dialoguePanel.GetComponentInParent<Canvas>(true);
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

        Transform existingStart = targetCanvas.transform.Find("StartScreenPanel");
        if (existingStart != null)
        {
            startScreenPanel = existingStart.gameObject;
            Transform btnTr = existingStart.Find("StartCard/StartButton");
            if (btnTr != null)
            {
                startGameButton = btnTr.GetComponent<Button>();
                if (startGameButton != null)
                {
                    startGameButton.onClick.RemoveListener(CloseStartScreen);
                    startGameButton.onClick.AddListener(CloseStartScreen);
                }
            }
            return;
        }

        TMP_FontAsset sharedFont = FindSharedFont();

        GameObject overlayObj = new GameObject("StartScreenPanel");
        overlayObj.transform.SetParent(targetCanvas.transform, false);
        RectTransform overlayRt = overlayObj.AddComponent<RectTransform>();
        overlayRt.anchorMin = Vector2.zero;
        overlayRt.anchorMax = Vector2.one;
        overlayRt.offsetMin = Vector2.zero;
        overlayRt.offsetMax = Vector2.zero;

        Image overlayImg = overlayObj.AddComponent<Image>();
        overlayImg.color = new Color(0.03f, 0.04f, 0.06f, 0.80f);
        overlayImg.raycastTarget = true;

        GameObject cardObj = new GameObject("StartCard");
        cardObj.transform.SetParent(overlayObj.transform, false);
        RectTransform cardRt = cardObj.AddComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0.5f, 0.5f);
        cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.sizeDelta = new Vector2(480f, 330f);

        Image cardBg = cardObj.AddComponent<Image>();
        cardBg.color = new Color(0.09f, 0.11f, 0.15f, 0.98f);

        Outline cardBorder = cardObj.AddComponent<Outline>();
        cardBorder.effectColor = new Color(0.88f, 0.74f, 0.36f, 0.92f);
        cardBorder.effectDistance = new Vector2(2f, -2f);

        Shadow cardShadow = cardObj.AddComponent<Shadow>();
        cardShadow.effectColor = new Color(0f, 0f, 0f, 0.80f);
        cardShadow.effectDistance = new Vector2(0f, -10f);

        GameObject iconBadge = new GameObject("IconBadge");
        iconBadge.transform.SetParent(cardObj.transform, false);
        RectTransform iconBadgeRt = iconBadge.AddComponent<RectTransform>();
        iconBadgeRt.anchorMin = new Vector2(0.5f, 1f);
        iconBadgeRt.anchorMax = new Vector2(0.5f, 1f);
        iconBadgeRt.pivot = new Vector2(0.5f, 1f);
        iconBadgeRt.anchoredPosition = new Vector2(0f, -24f);
        iconBadgeRt.sizeDelta = new Vector2(82f, 82f);

        Image iconBadgeBg = iconBadge.AddComponent<Image>();
        iconBadgeBg.color = new Color(0.13f, 0.16f, 0.20f, 1f);
        Outline iconBadgeOutline = iconBadge.AddComponent<Outline>();
        iconBadgeOutline.effectColor = new Color(0.88f, 0.74f, 0.36f, 0.75f);
        iconBadgeOutline.effectDistance = new Vector2(1.5f, -1.5f);

        GameObject iconObj = new GameObject("Icon");
        iconObj.transform.SetParent(iconBadge.transform, false);
        RectTransform iconRt = iconObj.AddComponent<RectTransform>();
        iconRt.anchorMin = Vector2.zero;
        iconRt.anchorMax = Vector2.one;
        iconRt.offsetMin = new Vector2(10f, 10f);
        iconRt.offsetMax = new Vector2(-10f, -10f);

        Image iconImg = iconObj.AddComponent<Image>();
        iconImg.preserveAspect = true;
        if (companionIconSprite != null)
            iconImg.sprite = companionIconSprite;
        else if (keyIconSprite != null)
            iconImg.sprite = keyIconSprite;

        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(cardObj.transform, false);
        RectTransform titleRt = titleObj.AddComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0f, -118f);
        titleRt.sizeDelta = new Vector2(-40f, 40f);

        TextMeshProUGUI titleTmp = titleObj.AddComponent<TextMeshProUGUI>();
        if (sharedFont != null)
            titleTmp.font = sharedFont;
        titleTmp.text = "GAME OF TEMPLE";
        titleTmp.fontSize = 29f;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.color = new Color(0.96f, 0.84f, 0.44f, 1f);

        GameObject subObj = new GameObject("Subtitle");
        subObj.transform.SetParent(cardObj.transform, false);
        RectTransform subRt = subObj.AddComponent<RectTransform>();
        subRt.anchorMin = new Vector2(0f, 1f);
        subRt.anchorMax = new Vector2(1f, 1f);
        subRt.pivot = new Vector2(0.5f, 1f);
        subRt.anchoredPosition = new Vector2(0f, -162f);
        subRt.sizeDelta = new Vector2(-50f, 34f);

        TextMeshProUGUI subTmp = subObj.AddComponent<TextMeshProUGUI>();
        if (sharedFont != null)
            subTmp.font = sharedFont;
        subTmp.text = "Tapınağın sırrını çöz, anahtarı al ve muhafızı ikna et.";
        subTmp.fontSize = 15.5f;
        subTmp.alignment = TextAlignmentOptions.Center;
        subTmp.color = new Color(0.86f, 0.85f, 0.82f, 1f);

        GameObject controlsObj = new GameObject("ControlsText");
        controlsObj.transform.SetParent(cardObj.transform, false);
        RectTransform controlsRt = controlsObj.AddComponent<RectTransform>();
        controlsRt.anchorMin = new Vector2(0f, 1f);
        controlsRt.anchorMax = new Vector2(1f, 1f);
        controlsRt.pivot = new Vector2(0.5f, 1f);
        controlsRt.anchoredPosition = new Vector2(0f, -204f);
        controlsRt.sizeDelta = new Vector2(-40f, 28f);

        TextMeshProUGUI controlsTmp = controlsObj.AddComponent<TextMeshProUGUI>();
        if (sharedFont != null)
            controlsTmp.font = sharedFont;
        controlsTmp.text = "[WASD] Hareket   •   [E] Etkileşim   •   [SPACE] Yoldaş";
        controlsTmp.fontSize = 13.5f;
        controlsTmp.fontStyle = FontStyles.Bold;
        controlsTmp.alignment = TextAlignmentOptions.Center;
        controlsTmp.color = new Color(0.65f, 0.78f, 0.90f, 0.95f);

        GameObject startBtnObj = new GameObject("StartButton");
        startBtnObj.transform.SetParent(cardObj.transform, false);
        RectTransform startBtnRt = startBtnObj.AddComponent<RectTransform>();
        startBtnRt.anchorMin = new Vector2(0.5f, 0f);
        startBtnRt.anchorMax = new Vector2(0.5f, 0f);
        startBtnRt.pivot = new Vector2(0.5f, 0f);
        startBtnRt.anchoredPosition = new Vector2(0f, 26f);
        startBtnRt.sizeDelta = new Vector2(220f, 48f);

        Image startBg = startBtnObj.AddComponent<Image>();
        startBg.color = new Color(0.76f, 0.58f, 0.24f, 1f);
        Outline startOutline = startBtnObj.AddComponent<Outline>();
        startOutline.effectColor = new Color(0.95f, 0.82f, 0.48f, 0.9f);
        startOutline.effectDistance = new Vector2(1.5f, -1.5f);

        startGameButton = startBtnObj.AddComponent<Button>();
        startGameButton.targetGraphic = startBg;
        startGameButton.onClick.RemoveListener(CloseStartScreen);
        startGameButton.onClick.AddListener(CloseStartScreen);

        GameObject startTxtObj = new GameObject("Text");
        startTxtObj.transform.SetParent(startBtnObj.transform, false);
        RectTransform startTxtRt = startTxtObj.AddComponent<RectTransform>();
        startTxtRt.anchorMin = Vector2.zero;
        startTxtRt.anchorMax = Vector2.one;
        startTxtRt.offsetMin = Vector2.zero;
        startTxtRt.offsetMax = Vector2.zero;

        TextMeshProUGUI startTmp = startTxtObj.AddComponent<TextMeshProUGUI>();
        if (sharedFont != null)
            startTmp.font = sharedFont;
        startTmp.text = "OYUNA BAŞLA";
        startTmp.fontSize = 18.5f;
        startTmp.fontStyle = FontStyles.Bold;
        startTmp.alignment = TextAlignmentOptions.Center;
        startTmp.color = new Color(0.10f, 0.08f, 0.06f, 1f);

        startScreenPanel = overlayObj;
        startScreenPanel.SetActive(false);
    }

    private void ShowInitialStartScreenIfNeeded()
    {
        if (!Application.isPlaying)
        {
            if (startScreenPanel != null)
                startScreenPanel.SetActive(false);
            return;
        }

        if (!hasShownInitialStartScreen)
        {
            hasShownInitialStartScreen = true;
            EnsureStartScreenUI();
            if (startScreenPanel != null)
            {
                startScreenPanel.transform.SetAsLastSibling();
                startScreenPanel.SetActive(true);
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.SetGameState(GameState.Dialogue);
                }
            }
        }
        else if (startScreenPanel != null)
        {
            startScreenPanel.SetActive(false);
        }
    }

    public void CloseStartScreen()
    {
        if (startScreenPanel != null)
        {
            startScreenPanel.SetActive(false);
        }

        PlayCompanionSendSfx();

        if (!IsGuardChatOpen && !IsVictoryScreenOpen && GameManager.Instance != null)
        {
            GameManager.Instance.SetGameState(GameState.Playing);
        }
    }

    public void EnsurePauseMenuUI()
    {
        EnsureSpritesLoaded();

        Canvas targetCanvas = null;
        if (dialoguePanel != null)
            targetCanvas = dialoguePanel.GetComponentInParent<Canvas>(true);
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

        Transform existingPause = targetCanvas.transform.Find("PauseMenuPanel");
        if (existingPause != null)
        {
            pauseMenuPanel = existingPause.gameObject;
            Transform muteTxtTr = existingPause.Find("PauseCard/MuteButton/Text");
            if (muteTxtTr != null)
            {
                pauseMuteButtonText = muteTxtTr.GetComponent<TextMeshProUGUI>();
            }
            BindPauseMenuButtons(existingPause);
            UpdatePauseMuteLabel();
            return;
        }

        TMP_FontAsset sharedFont = FindSharedFont();

        GameObject overlayObj = new GameObject("PauseMenuPanel");
        overlayObj.transform.SetParent(targetCanvas.transform, false);
        RectTransform overlayRt = overlayObj.AddComponent<RectTransform>();
        overlayRt.anchorMin = Vector2.zero;
        overlayRt.anchorMax = Vector2.one;
        overlayRt.offsetMin = Vector2.zero;
        overlayRt.offsetMax = Vector2.zero;

        Image overlayImg = overlayObj.AddComponent<Image>();
        overlayImg.color = new Color(0.03f, 0.04f, 0.06f, 0.78f);
        overlayImg.raycastTarget = true;

        GameObject cardObj = new GameObject("PauseCard");
        cardObj.transform.SetParent(overlayObj.transform, false);
        RectTransform cardRt = cardObj.AddComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0.5f, 0.5f);
        cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.sizeDelta = new Vector2(410f, 340f);

        Image cardBg = cardObj.AddComponent<Image>();
        cardBg.color = new Color(0.09f, 0.11f, 0.15f, 0.98f);

        Outline cardBorder = cardObj.AddComponent<Outline>();
        cardBorder.effectColor = new Color(0.86f, 0.72f, 0.36f, 0.90f);
        cardBorder.effectDistance = new Vector2(2f, -2f);

        Shadow cardShadow = cardObj.AddComponent<Shadow>();
        cardShadow.effectColor = new Color(0f, 0f, 0f, 0.80f);
        cardShadow.effectDistance = new Vector2(0f, -10f);

        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(cardObj.transform, false);
        RectTransform titleRt = titleObj.AddComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0f, -22f);
        titleRt.sizeDelta = new Vector2(-40f, 34f);

        TextMeshProUGUI titleTmp = titleObj.AddComponent<TextMeshProUGUI>();
        if (sharedFont != null)
            titleTmp.font = sharedFont;
        titleTmp.text = "OYUN DURAKLATILDI";
        titleTmp.fontSize = 23f;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.color = new Color(0.96f, 0.84f, 0.44f, 1f);

        GameObject subObj = new GameObject("Subtitle");
        subObj.transform.SetParent(cardObj.transform, false);
        RectTransform subRt = subObj.AddComponent<RectTransform>();
        subRt.anchorMin = new Vector2(0f, 1f);
        subRt.anchorMax = new Vector2(1f, 1f);
        subRt.pivot = new Vector2(0.5f, 1f);
        subRt.anchoredPosition = new Vector2(0f, -56f);
        subRt.sizeDelta = new Vector2(-40f, 22f);

        TextMeshProUGUI subTmp = subObj.AddComponent<TextMeshProUGUI>();
        if (sharedFont != null)
            subTmp.font = sharedFont;
        subTmp.text = "[ESC] veya [P] ile devam edebilirsin";
        subTmp.fontSize = 13f;
        subTmp.alignment = TextAlignmentOptions.Center;
        subTmp.color = new Color(0.74f, 0.76f, 0.80f, 0.90f);

        CreatePauseMenuButton(
            cardObj.transform,
            "ResumeButton",
            new Vector2(0f, -104f),
            "DEVAM ET",
            new Color(0.76f, 0.58f, 0.24f, 1f),
            new Color(0.95f, 0.82f, 0.48f, 0.9f),
            new Color(0.10f, 0.08f, 0.06f, 1f),
            sharedFont,
            out _);

        CreatePauseMenuButton(
            cardObj.transform,
            "MuteButton",
            new Vector2(0f, -158f),
            "SES: AÇIK",
            new Color(0.16f, 0.20f, 0.27f, 1f),
            new Color(0.46f, 0.62f, 0.82f, 0.85f),
            new Color(0.92f, 0.94f, 0.98f, 1f),
            sharedFont,
            out pauseMuteButtonText);

        CreatePauseMenuButton(
            cardObj.transform,
            "RestartButton",
            new Vector2(0f, -212f),
            "BAŞTAN BAŞLA",
            new Color(0.18f, 0.22f, 0.26f, 1f),
            new Color(0.58f, 0.64f, 0.72f, 0.85f),
            new Color(0.92f, 0.92f, 0.92f, 1f),
            sharedFont,
            out _);

        CreatePauseMenuButton(
            cardObj.transform,
            "QuitButton",
            new Vector2(0f, -266f),
            "OYUNDAN ÇIK",
            new Color(0.46f, 0.16f, 0.16f, 1f),
            new Color(0.88f, 0.42f, 0.42f, 0.85f),
            new Color(0.98f, 0.92f, 0.92f, 1f),
            sharedFont,
            out _);

        BindPauseMenuButtons(overlayObj.transform);
        UpdatePauseMuteLabel();

        pauseMenuPanel = overlayObj;
        pauseMenuPanel.SetActive(false);
    }

    private static void CreatePauseMenuButton(
        Transform parent,
        string btnName,
        Vector2 anchoredPos,
        string labelText,
        Color bgColor,
        Color outlineColor,
        Color textColor,
        TMP_FontAsset sharedFont,
        out TextMeshProUGUI labelTmp)
    {
        GameObject btnObj = new GameObject(btnName);
        btnObj.transform.SetParent(parent, false);
        RectTransform rt = btnObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(250f, 42f);

        Image bg = btnObj.AddComponent<Image>();
        bg.color = bgColor;

        Outline outline = btnObj.AddComponent<Outline>();
        outline.effectColor = outlineColor;
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = bg;

        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(btnObj.transform, false);
        RectTransform txtRt = txtObj.AddComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = Vector2.zero;
        txtRt.offsetMax = Vector2.zero;

        labelTmp = txtObj.AddComponent<TextMeshProUGUI>();
        if (sharedFont != null)
            labelTmp.font = sharedFont;
        labelTmp.text = labelText;
        labelTmp.fontSize = 16.5f;
        labelTmp.fontStyle = FontStyles.Bold;
        labelTmp.alignment = TextAlignmentOptions.Center;
        labelTmp.color = textColor;
    }

    private void BindPauseMenuButtons(Transform pauseRoot)
    {
        if (pauseRoot == null)
            return;

        Transform resumeTr = pauseRoot.Find("PauseCard/ResumeButton");
        if (resumeTr != null && resumeTr.TryGetComponent(out Button resumeBtn))
        {
            resumeBtn.onClick.RemoveAllListeners();
            resumeBtn.onClick.AddListener(ClosePauseMenu);
        }

        Transform muteTr = pauseRoot.Find("PauseCard/MuteButton");
        if (muteTr != null && muteTr.TryGetComponent(out Button muteBtn))
        {
            muteBtn.onClick.RemoveAllListeners();
            muteBtn.onClick.AddListener(ToggleAudioMute);
        }

        Transform restartTr = pauseRoot.Find("PauseCard/RestartButton");
        if (restartTr != null && restartTr.TryGetComponent(out Button restartBtn))
        {
            restartBtn.onClick.RemoveAllListeners();
            restartBtn.onClick.AddListener(RestartFromPauseMenu);
        }

        Transform quitTr = pauseRoot.Find("PauseCard/QuitButton");
        if (quitTr != null && quitTr.TryGetComponent(out Button quitBtn))
        {
            quitBtn.onClick.RemoveAllListeners();
            quitBtn.onClick.AddListener(QuitGameFromPauseMenu);
        }
    }

    public void OpenPauseMenu()
    {
        EnsurePauseMenuUI();
        if (pauseMenuPanel == null)
            return;

        UpdatePauseMuteLabel();
        pauseMenuPanel.transform.SetAsLastSibling();
        pauseMenuPanel.SetActive(true);
        Time.timeScale = 0f;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetGameState(GameState.Dialogue);
        }
    }

    public void ClosePauseMenu()
    {
        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(false);
        }

        Time.timeScale = 1f;

        if (!IsGuardChatOpen && !IsVictoryScreenOpen && !IsStartScreenOpen && GameManager.Instance != null)
        {
            GameManager.Instance.SetGameState(GameState.Playing);
        }
    }

    public void ToggleAudioMute()
    {
        isAudioMuted = !isAudioMuted;
        AudioListener.volume = isAudioMuted ? 0f : 1f;
        UpdatePauseMuteLabel();
    }

    private void UpdatePauseMuteLabel()
    {
        AudioListener.volume = isAudioMuted ? 0f : 1f;
        if (pauseMuteButtonText != null)
        {
            pauseMuteButtonText.text = isAudioMuted ? "SES: KAPALI" : "SES: AÇIK";
        }
    }

    private void RestartFromPauseMenu()
    {
        ClosePauseMenu();
        RestartGameFromVictory();
    }

    private void QuitGameFromPauseMenu()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void EnsureAudioSources()
    {
        if (ambientAudioSource == null)
        {
            Transform ambTr = transform.Find("TempleAmbientAudio");
            GameObject ambObj = ambTr != null ? ambTr.gameObject : new GameObject("TempleAmbientAudio");
            ambObj.transform.SetParent(transform, false);
            ambientAudioSource = ambObj.GetComponent<AudioSource>();
            if (ambientAudioSource == null)
            {
                ambientAudioSource = ambObj.AddComponent<AudioSource>();
            }
            ambientAudioSource.loop = true;
            ambientAudioSource.playOnAwake = false;
            ambientAudioSource.spatialBlend = 0f;
            ambientAudioSource.volume = 0.065f;
            if (ambientAudioSource.clip == null)
            {
                ambientAudioSource.clip = CreateTempleAmbientClip();
            }
            if (!ambientAudioSource.isPlaying)
            {
                ambientAudioSource.Play();
            }
        }

        if (sfxAudioSource == null)
        {
            Transform sfxTr = transform.Find("TempleSfxAudio");
            GameObject sfxObj = sfxTr != null ? sfxTr.gameObject : new GameObject("TempleSfxAudio");
            sfxObj.transform.SetParent(transform, false);
            sfxAudioSource = sfxObj.GetComponent<AudioSource>();
            if (sfxAudioSource == null)
            {
                sfxAudioSource = sfxObj.AddComponent<AudioSource>();
            }
            sfxAudioSource.loop = false;
            sfxAudioSource.playOnAwake = false;
            sfxAudioSource.spatialBlend = 0f;
            sfxAudioSource.volume = 0.32f;
        }

        if (companionUnlockClip == null)
            companionUnlockClip = CreateArpeggioClip("CompanionUnlock", new[] { 523.25f, 659.25f, 783.99f }, 0.11f);
        if (companionSendClip == null)
            companionSendClip = CreateArpeggioClip("CompanionSend", new[] { 587.33f, 880.0f }, 0.09f);
        if (keyUnlockClip == null)
            keyUnlockClip = CreateArpeggioClip("KeyUnlock", new[] { 587.33f, 739.99f, 880.0f, 1174.66f }, 0.12f);
        if (runeActivateClip == null)
            runeActivateClip = CreateArpeggioClip("RuneActivate", new[] { 293.66f, 440.0f, 587.33f }, 0.16f);
        if (victoryClip == null)
            victoryClip = CreateArpeggioClip("Victory", new[] { 523.25f, 659.25f, 783.99f, 1046.50f }, 0.16f);
        if (defeatClip == null)
            defeatClip = CreateArpeggioClip("Defeat", new[] { 311.13f, 261.63f, 196.00f }, 0.18f);
    }

    public void PlayCompanionUnlockSfx()
    {
        EnsureAudioSources();
        if (sfxAudioSource != null && companionUnlockClip != null)
            sfxAudioSource.PlayOneShot(companionUnlockClip, 0.85f);
    }

    public void PlayCompanionSendSfx()
    {
        EnsureAudioSources();
        if (sfxAudioSource != null && companionSendClip != null)
            sfxAudioSource.PlayOneShot(companionSendClip, 0.70f);
    }

    public void PlayKeyUnlockSfx()
    {
        EnsureAudioSources();
        if (sfxAudioSource != null && keyUnlockClip != null)
            sfxAudioSource.PlayOneShot(keyUnlockClip, 0.95f);
    }

    public void PlayRuneActivateSfx()
    {
        EnsureAudioSources();
        if (sfxAudioSource != null && runeActivateClip != null)
            sfxAudioSource.PlayOneShot(runeActivateClip, 0.85f);
    }

    public void PlayVictorySfx()
    {
        EnsureAudioSources();
        if (sfxAudioSource != null && victoryClip != null)
            sfxAudioSource.PlayOneShot(victoryClip, 0.95f);
    }

    public void PlayDefeatSfx()
    {
        EnsureAudioSources();
        if (sfxAudioSource != null && defeatClip != null)
            sfxAudioSource.PlayOneShot(defeatClip, 0.90f);
    }

    private static AudioClip CreateArpeggioClip(string clipName, float[] frequencies, float noteDuration)
    {
        int sampleRate = 44100;
        int samplesPerNote = Mathf.Max(1, Mathf.RoundToInt(sampleRate * noteDuration));
        int tailSamples = Mathf.RoundToInt(sampleRate * 0.18f);
        int totalSamples = (frequencies.Length * samplesPerNote) + tailSamples;
        float[] data = new float[totalSamples];

        for (int n = 0; n < frequencies.Length; n++)
        {
            float freq = frequencies[n];
            int startSample = n * samplesPerNote;
            int length = samplesPerNote + tailSamples;

            for (int i = 0; i < length && (startSample + i) < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 9.5f) * Mathf.Clamp01(i / (sampleRate * 0.008f));
                float wave = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.65f +
                             Mathf.Sin(2f * Mathf.PI * (freq * 2f) * t) * 0.20f;
                data[startSample + i] += wave * env * 0.45f;
            }
        }

        AudioClip clip = AudioClip.Create(clipName, totalSamples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static AudioClip CreateTempleAmbientClip()
    {
        int sampleRate = 22050;
        float duration = 6.0f;
        int totalSamples = Mathf.RoundToInt(sampleRate * duration);
        float[] data = new float[totalSamples];

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / sampleRate;
            float loopEnv = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * t / duration);
            float pad =
                Mathf.Sin(2f * Mathf.PI * 110.0f * t) * 0.35f +
                Mathf.Sin(2f * Mathf.PI * 164.81f * t) * 0.25f +
                Mathf.Sin(2f * Mathf.PI * 220.0f * t) * 0.15f;
            data[i] = pad * loopEnv * 0.35f;
        }

        AudioClip clip = AudioClip.Create("TempleAmbientLoop", totalSamples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
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

        if (!GuardAgent.IsAnyGuardAttacking && gameObject.name.Contains("muhafız"))
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

        if (gameObject.name.Contains("muhafız"))
        {
            BoxCollider2D guardCol = GetComponent<BoxCollider2D>();
            if (guardCol == null)
                guardCol = gameObject.AddComponent<BoxCollider2D>();

            guardCol.enabled = true;
            guardCol.isTrigger = false;
            guardCol.size = new Vector2(1.95f, 3.54f);
            guardCol.offset = new Vector2(-0.027f, -0.60f);
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
