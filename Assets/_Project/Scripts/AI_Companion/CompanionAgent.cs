using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class CompanionAgent : MonoBehaviour, ICompanionCommand
{
    private BaseAgentState currentState;

    public BaseAgentState CurrentState => currentState;

    [Header("NavMesh, Sensör ve Hedefler")]
    public NavMeshAgent ajanMotoru;
    public RaycastSensorController sensorKontrolcu;
    public Transform oyuncuHedefi;
    public Transform sandikHedefi;

    [Header("Baykuş Yoldaş Görsel & Animasyon")]
    public SpriteRenderer baykusRenderer;
    public float flyAnimFps = 10f;
    public float hoverAnimFps = 6f;
    public Sprite[] northFlyFrames;
    public Sprite[] southFlyFrames;
    public Sprite[] sideFlyFrames;

    private float animTimer;
    private int currentDirection = 0; // 0: Güney (Ön), 1: Kuzey (Arka), 2: Doğu (Sağ), 3: Batı (Sol)
    private bool requiresChestUnlock;
    private bool isWaitingInChest;
    private bool isSubscribedToGameManager;
    private bool hasCompletedKeyTask;
    private Transform companionChestTransform;

#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
    private static void InitEditorOwlSpritesSync()
    {
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (!Application.isPlaying)
            {
                CompanionAgent[] agents = Object.FindObjectsByType<CompanionAgent>(FindObjectsSortMode.None);
                foreach (CompanionAgent agent in agents)
                {
                    if (agent != null)
                    {
                        agent.EnsureOwlSpritesLoaded();
                        agent.ConfigureOwlRenderer(true);
                    }
                }
            }
        };
    }
#endif

    private void Awake()
    {
        if (ajanMotoru == null)
        {
            ajanMotoru = GetComponent<NavMeshAgent>();
        }

        if (sensorKontrolcu == null)
        {
            sensorKontrolcu = GetComponent<RaycastSensorController>();
        }

        if (baykusRenderer == null)
        {
            baykusRenderer = GetComponent<SpriteRenderer>();
            if (baykusRenderer == null)
            {
                baykusRenderer = GetComponentInChildren<SpriteRenderer>();
            }
        }

        EnsureOwlSpritesLoaded();
    }

    private void Start()
    {
        if (ajanMotoru != null && Object.FindFirstObjectByType<Grid>() != null)
        {
            ajanMotoru.updateRotation = false;
            ajanMotoru.updateUpAxis = false;
            ajanMotoru.baseOffset = 0f;
        }

        EnsureOwlSpritesLoaded();
        FindSceneChestReferences();

        requiresChestUnlock = companionChestTransform != null && GameManager.Instance != null;

        if (requiresChestUnlock)
        {
            GameManager.Instance.OnCompanionStateChanged += HandleCompanionUnlocked;
            isSubscribedToGameManager = true;

            if (!GameManager.Instance.HasCompanion)
            {
                EnterWaitingInChestMode();
                return;
            }
        }

        isWaitingInChest = false;
        ConfigureOwlRenderer(true);
        ChangeState(new FollowState());
    }

    private void OnDestroy()
    {
        if (isSubscribedToGameManager && GameManager.Instance != null)
        {
            GameManager.Instance.OnCompanionStateChanged -= HandleCompanionUnlocked;
            isSubscribedToGameManager = false;
        }
    }

    private void FindSceneChestReferences()
    {
        TreasureChest[] chests = Object.FindObjectsByType<TreasureChest>(FindObjectsSortMode.None);
        foreach (TreasureChest chest in chests)
        {
            if (chest == null)
                continue;

            if (chest.transform.position.x < 0f)
            {
                companionChestTransform = chest.transform;
            }
            else if (sandikHedefi == null && chest.transform.position.x > 0f)
            {
                sandikHedefi = chest.transform;
            }
        }
    }

    private void EnterWaitingInChestMode()
    {
        isWaitingInChest = true;

        if (companionChestTransform != null)
        {
            Vector3 chestPos = new Vector3(companionChestTransform.position.x, companionChestTransform.position.y, 0f);
            transform.position = chestPos;
            if (ajanMotoru != null && ajanMotoru.isOnNavMesh)
            {
                ajanMotoru.Warp(chestPos);
                ajanMotoru.ResetPath();
                ajanMotoru.isStopped = true;
            }
        }

        ConfigureOwlRenderer(false);
        ChangeState(new IdleState());
        gameObject.SetActive(false);
    }

    private void HandleCompanionUnlocked(bool hasCompanion)
    {
        if (hasCompanion && isWaitingInChest)
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
            SpawnFromCompanionChest();
        }
    }

    private void SpawnFromCompanionChest()
    {
        isWaitingInChest = false;

        if (companionChestTransform != null)
        {
            Vector3 spawnPos = new Vector3(
                companionChestTransform.position.x,
                companionChestTransform.position.y + 0.45f,
                0f);

            transform.position = spawnPos;
            if (ajanMotoru != null && ajanMotoru.isOnNavMesh)
            {
                ajanMotoru.Warp(spawnPos);
                ajanMotoru.isStopped = false;
            }
        }
        else if (ajanMotoru != null && ajanMotoru.isOnNavMesh)
        {
            ajanMotoru.isStopped = false;
        }

        ConfigureOwlRenderer(true);
        Debug.Log("Baykuş Yoldaş sandıktan çıktı! Oyuncuyu takip etmeye başlıyor.");
        ChangeState(new FollowState());
    }

    private void Update()
    {
        if (isWaitingInChest)
        {
            if (GameManager.Instance != null && GameManager.Instance.HasCompanion)
            {
                SpawnFromCompanionChest();
            }
            else
            {
                if (baykusRenderer != null && baykusRenderer.enabled)
                {
                    baykusRenderer.enabled = false;
                }
                if (ajanMotoru != null && ajanMotoru.isOnNavMesh && !ajanMotoru.isStopped)
                {
                    ajanMotoru.ResetPath();
                    ajanMotoru.isStopped = true;
                }
                return;
            }
        }

        if (currentState != null)
        {
            currentState.UpdateState(this);
        }

        UpdateOwlFlightAnimation();

        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (DialogueManager.Instance != null && DialogueManager.Instance.IsGuardChatOpen)
            {
                return;
            }

            if (requiresChestUnlock && (GameManager.Instance == null || !GameManager.Instance.HasCompanion))
            {
                return;
            }

            if (IsKeyTaskAlreadyCompletedOrActive())
            {
                Debug.Log("Kutsal Anahtar zaten alındı veya yoldaş şu an görevde!");
                return;
            }

            CommandMoveTo(sandikHedefi);
        }
    }

    private void LateUpdate()
    {
        if (isWaitingInChest && baykusRenderer != null && baykusRenderer.enabled)
        {
            baykusRenderer.enabled = false;
        }
    }

    public void MarkKeyTaskCompleted()
    {
        hasCompletedKeyTask = true;
    }

    private bool IsKeyTaskAlreadyCompletedOrActive()
    {
        if (hasCompletedKeyTask)
        {
            return true;
        }

        if (currentState is NavigateToKeyState || currentState is CollectAndReturnState)
        {
            return true;
        }

        if (GameManager.Instance != null && GameManager.Instance.HasKey)
        {
            hasCompletedKeyTask = true;
            return true;
        }

        if (sandikHedefi != null)
        {
            TreasureChest chest = sandikHedefi.GetComponent<TreasureChest>()
                                  ?? sandikHedefi.GetComponentInChildren<TreasureChest>()
                                  ?? sandikHedefi.GetComponentInParent<TreasureChest>();
            if (chest != null && chest.IsOpen)
            {
                hasCompletedKeyTask = true;
                return true;
            }
        }

        return false;
    }

    public void CommandMoveTo(Transform target)
    {
        if (isWaitingInChest || IsKeyTaskAlreadyCompletedOrActive())
            return;

        if (target != null)
        {
            sandikHedefi = target;
        }

        if (ajanMotoru != null && ajanMotoru.isOnNavMesh)
        {
            ajanMotoru.isStopped = false;
        }

        ChangeState(new NavigateToKeyState());
    }

    public void ChangeState(BaseAgentState newState)
    {
        if (currentState != null)
        {
            currentState.ExitState(this);
        }

        currentState = newState;

        if (currentState != null)
        {
            currentState.EnterState(this);
        }
    }

    private void ConfigureOwlRenderer(bool visible)
    {
        if (baykusRenderer == null)
        {
            baykusRenderer = GetComponent<SpriteRenderer>();
            if (baykusRenderer == null)
            {
                baykusRenderer = gameObject.AddComponent<SpriteRenderer>();
            }
        }

        baykusRenderer.enabled = visible;
        baykusRenderer.color = Color.white;

        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (mr != null && mr.enabled)
        {
            mr.enabled = false;
        }

        if (Object.FindFirstObjectByType<Grid>() != null)
        {
            if (Mathf.Abs(transform.localScale.x - 0.5f) < 0.05f && Mathf.Abs(transform.localScale.y - 0.75f) < 0.05f)
            {
                transform.localScale = new Vector3(0.42f, 0.42f, 1f);
            }

            baykusRenderer.sortingLayerName = "Layer 3";
            if (baykusRenderer.sortingOrder < 50)
            {
                baykusRenderer.sortingOrder = 50;
            }
        }

        if (baykusRenderer.sprite == null)
        {
            if (southFlyFrames != null && southFlyFrames.Length > 0 && southFlyFrames[0] != null)
            {
                baykusRenderer.sprite = southFlyFrames[0];
            }
            else if (northFlyFrames != null && northFlyFrames.Length > 0 && northFlyFrames[0] != null)
            {
                baykusRenderer.sprite = northFlyFrames[0];
            }
        }
    }

    private void UpdateOwlFlightAnimation()
    {
        if (baykusRenderer == null || !baykusRenderer.enabled)
            return;

        Vector2 moveVel = Vector2.zero;
        if (ajanMotoru != null)
        {
            bool is2D = !ajanMotoru.updateUpAxis || Object.FindFirstObjectByType<Grid>() != null;
            if (is2D)
            {
                moveVel = new Vector2(ajanMotoru.velocity.x, ajanMotoru.velocity.y);
                if (moveVel.sqrMagnitude < 0.005f)
                {
                    moveVel = new Vector2(ajanMotoru.desiredVelocity.x, ajanMotoru.desiredVelocity.y);
                }
            }
            else
            {
                moveVel = new Vector2(ajanMotoru.velocity.x, ajanMotoru.velocity.z);
            }
        }

        bool isMoving = moveVel.sqrMagnitude > 0.02f;
        if (isMoving)
        {
            if (Mathf.Abs(moveVel.x) > Mathf.Abs(moveVel.y) * 1.1f)
            {
                currentDirection = moveVel.x >= 0f ? 2 : 3;
            }
            else
            {
                currentDirection = moveVel.y >= 0f ? 1 : 0;
            }
        }

        Sprite[] activeFrames = GetFramesForDirection(currentDirection);
        if (activeFrames == null || activeFrames.Length == 0)
            return;

        baykusRenderer.flipX = (currentDirection == 3);

        float fps = isMoving ? flyAnimFps : hoverAnimFps;
        animTimer += Time.deltaTime * Mathf.Max(1f, fps);
        int frameIndex = Mathf.FloorToInt(animTimer) % activeFrames.Length;

        Sprite nextSprite = activeFrames[frameIndex];
        if (nextSprite != null && baykusRenderer.sprite != nextSprite)
        {
            baykusRenderer.sprite = nextSprite;
        }
    }

    private Sprite[] GetFramesForDirection(int dir)
    {
        switch (dir)
        {
            case 1: // Kuzey (Yukarı)
                if (HasValidSprites(northFlyFrames))
                    return northFlyFrames;
                break;
            case 2: // Doğu (Sağ)
            case 3: // Batı (Sol - flipX)
                if (HasValidSprites(sideFlyFrames))
                    return sideFlyFrames;
                break;
            default: // Güney (Aşağı / Ön)
                if (HasValidSprites(southFlyFrames))
                    return southFlyFrames;
                break;
        }

        if (HasValidSprites(southFlyFrames))
            return southFlyFrames;
        if (HasValidSprites(northFlyFrames))
            return northFlyFrames;
        return sideFlyFrames;
    }

    private static bool HasValidSprites(Sprite[] arr)
    {
        return arr != null && arr.Length > 0 && arr[0] != null;
    }

    public void EnsureOwlSpritesLoaded()
    {
        if (HasValidSprites(northFlyFrames) && HasValidSprites(southFlyFrames) && HasValidSprites(sideFlyFrames))
            return;

        Dictionary<string, Sprite> spriteMap = new Dictionary<string, Sprite>();

#if UNITY_EDITOR
        string[] sheetPaths =
        {
            "Assets/_Project/Sprites/Baykuş Yoldaş Temizlenmiş.png",
            "Assets/_Project/Sprites/görsel_2026-10-31_130739059-removebg-preview.png"
        };

        foreach (string path in sheetPaths)
        {
            Object[] subAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
            if (subAssets != null)
            {
                foreach (Object obj in subAssets)
                {
                    if (obj is Sprite sp && !spriteMap.ContainsKey(sp.name))
                    {
                        spriteMap[sp.name] = sp;
                    }
                }
            }
        }
#endif

        if (spriteMap.Count == 0)
        {
            Sprite[] allSprites = Resources.FindObjectsOfTypeAll<Sprite>();
            foreach (Sprite sp in allSprites)
            {
                if (sp != null && (sp.name.Contains("Baykuş") || sp.name.Contains("removebg")) && !spriteMap.ContainsKey(sp.name))
                {
                    spriteMap[sp.name] = sp;
                }
            }
        }

        if (spriteMap.Count == 0)
            return;

        if (!HasValidSprites(northFlyFrames))
        {
            northFlyFrames = PickOwlSprites(
                spriteMap,
                "Baykuş Yoldaş Temizlenmiş_0",
                "Baykuş Yoldaş Temizlenmiş_1",
                "Baykuş Yoldaş Temizlenmiş_2",
                "Baykuş Yoldaş Temizlenmiş_3",
                "Baykuş Yoldaş Temizlenmiş_4",
                "Baykuş Yoldaş Temizlenmiş_5",
                "Baykuş Yoldaş Temizlenmiş_6",
                "Baykuş Yoldaş Temizlenmiş_7");
        }

        if (!HasValidSprites(southFlyFrames))
        {
            southFlyFrames = PickOwlSprites(
                spriteMap,
                "Baykuş Yoldaş Temizlenmiş_8",
                "Baykuş Yoldaş Temizlenmiş_9",
                "Baykuş Yoldaş Temizlenmiş_10",
                "Baykuş Yoldaş Temizlenmiş_11",
                "Baykuş Yoldaş Temizlenmiş_12");
        }

        if (!HasValidSprites(sideFlyFrames))
        {
            sideFlyFrames = PickOwlSprites(
                spriteMap,
                "Baykuş Yoldaş Temizlenmiş_13",
                "Baykuş Yoldaş Temizlenmiş_14",
                "Baykuş Yoldaş Temizlenmiş_15",
                "Baykuş Yoldaş Temizlenmiş_16",
                "Baykuş Yoldaş Temizlenmiş_17",
                "Baykuş Yoldaş Temizlenmiş_18",
                "Baykuş Yoldaş Temizlenmiş_19",
                "Baykuş Yoldaş Temizlenmiş_20");
        }
    }

    private static Sprite[] PickOwlSprites(Dictionary<string, Sprite> map, params string[] names)
    {
        List<Sprite> list = new List<Sprite>(names.Length);
        foreach (string n in names)
        {
            if (map.TryGetValue(n, out Sprite s) && s != null)
            {
                list.Add(s);
            }
        }
        return list.ToArray();
    }
}
