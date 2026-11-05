using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Tilemaps;

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
    private int currentDirection = 0;
    private bool requiresChestUnlock;
    private bool isWaitingInChest;
    private bool isSubscribedToGameManager;
    private bool hasCompletedKeyTask;
    private Transform companionChestTransform;

    private readonly HashSet<Vector2Int> playableMapCells = new HashSet<Vector2Int>();
    private bool isPlayableMapInitialized;

    private enum PassageTransitMode
    {
        None = 0,
        TunnelNorth = 1,
        TunnelSouth = 2,
        AltarStairsNorth = 3,
        AltarStairsSouth = 4
    }

    private static readonly Vector3 TunnelSouthPoint = new Vector3(7.0f, 5.55f, 0f);
    private static readonly Vector3 TunnelNorthPoint = new Vector3(7.0f, 11.85f, 0f);
    private static readonly Vector3 AltarStairsSouthPoint = new Vector3(4.0f, 14.85f, 0f);
    private static readonly Vector3 AltarStairsNorthPoint = new Vector3(4.0f, 18.35f, 0f);

    private PassageTransitMode activeTransit = PassageTransitMode.None;
    private Vector2 manualFlightVelocity = Vector2.zero;

    public bool IsNavigationReady => ajanMotoru != null && (ajanMotoru.isOnNavMesh || activeTransit != PassageTransitMode.None || !ajanMotoru.enabled);

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

        EnsurePlayableMapInitialized();
        EnsureOwlSpritesLoaded();
        FindSceneChestReferences();

        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.SetKeyChestDiscovered(false);
        }

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

    public void EnsureKeyChestTargetAssigned()
    {
        if (sandikHedefi == null)
        {
            FindSceneChestReferences();
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
            Vector3 chestPos = GetSafeChestApproachPosition(companionChestTransform);
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
        EnsurePlayableMapInitialized();
        FindSceneChestReferences();

        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.NotifyClueDiscoveredEastChest();
        }

        if (companionChestTransform != null)
        {
            Vector3 spawnPos = GetSafeChestApproachPosition(companionChestTransform);

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

        EnforcePlayableMapBounds();
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
        if (isWaitingInChest)
        {
            if (baykusRenderer != null && baykusRenderer.enabled)
            {
                baykusRenderer.enabled = false;
            }
            return;
        }

        if (ajanMotoru != null && ajanMotoru.stoppingDistance < 0.2f)
        {
            ajanMotoru.stoppingDistance = 0.35f;
        }

        EnforcePlayableMapBounds();
    }

    private void EnsurePlayableMapInitialized()
    {
        if (isPlayableMapInitialized && playableMapCells.Count > 0)
            return;

        if (Object.FindFirstObjectByType<Grid>() == null)
            return;

        Tilemap[] grassMaps =
        {
            FindSceneTilemap("Layer 1 - Grass"),
            FindSceneTilemap("Layer 2 - Grass"),
            FindSceneTilemap("Layer 3 - Grass")
        };

        Tilemap[] wallMaps =
        {
            FindSceneTilemap("Layer 1 - Wall"),
            FindSceneTilemap("Layer 2 - Wall"),
            FindSceneTilemap("Layer 3 - Wall")
        };

        if (grassMaps[0] == null && grassMaps[1] == null && grassMaps[2] == null)
            return;

        playableMapCells.Clear();

        const int minX = -20;
        const int maxX = 24;
        const int minY = -16;
        const int maxY = 23;

        HashSet<Vector2Int> exteriorCells = new HashSet<Vector2Int>();
        Queue<Vector2Int> queue = new Queue<Vector2Int>();

        Vector2Int startCorner = new Vector2Int(minX, minY);
        exteriorCells.Add(startCorner);
        queue.Enqueue(startCorner);

        int[] dx = { 1, -1, 0, 0 };
        int[] dy = { 0, 0, 1, -1 };

        while (queue.Count > 0)
        {
            Vector2Int cur = queue.Dequeue();
            for (int i = 0; i < 4; i++)
            {
                int nx = cur.x + dx[i];
                int ny = cur.y + dy[i];
                if (nx < minX || nx > maxX || ny < minY || ny > maxY)
                    continue;

                Vector2Int next = new Vector2Int(nx, ny);
                if (exteriorCells.Contains(next))
                    continue;

                bool hasAnyTile = HasTileInAny(grassMaps, nx, ny) || HasTileInAny(wallMaps, nx, ny);
                if (!hasAnyTile)
                {
                    exteriorCells.Add(next);
                    queue.Enqueue(next);
                }
            }
        }

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (!exteriorCells.Contains(cell))
                {
                    playableMapCells.Add(cell);
                }
            }
        }

        isPlayableMapInitialized = playableMapCells.Count > 0;
    }

    private static Tilemap FindSceneTilemap(string name)
    {
        GameObject go = GameObject.Find(name);
        return go != null ? go.GetComponent<Tilemap>() : null;
    }

    private static bool HasTileInAny(Tilemap[] maps, int x, int y)
    {
        Vector3Int cell = new Vector3Int(x, y, 0);
        for (int i = 0; i < maps.Length; i++)
        {
            if (maps[i] != null && maps[i].HasTile(cell))
                return true;
        }
        return false;
    }

    public Vector3 GetPlayableTargetPosition(Vector3 targetWorldPos)
    {
        EnsurePlayableMapInitialized();
        if (!isPlayableMapInitialized || playableMapCells.Count == 0)
            return targetWorldPos;

        Vector2Int cell = new Vector2Int(Mathf.FloorToInt(targetWorldPos.x), Mathf.FloorToInt(targetWorldPos.y));
        if (playableMapCells.Contains(cell))
            return new Vector3(targetWorldPos.x, targetWorldPos.y, 0f);

        return ClampPositionToPlayableMap(targetWorldPos, 0.35f);
    }

    public Vector3 GetSafeChestApproachPosition(Transform chestTransform)
    {
        if (chestTransform == null)
            return transform.position;

        Vector3 pos = chestTransform.position;
        Vector3 approachPos = new Vector3(pos.x - 0.35f, pos.y - 0.65f, 0f);
        if (NavMesh.SamplePosition(approachPos, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
        {
            return new Vector3(hit.position.x, hit.position.y, 0f);
        }

        return GetPlayableTargetPosition(approachPos);
    }

    public Vector3 ClampPositionToPlayableMap(Vector3 worldPos, float edgePadding = 0.25f)
    {
        EnsurePlayableMapInitialized();
        if (!isPlayableMapInitialized || playableMapCells.Count == 0)
            return worldPos;

        float px = worldPos.x;
        float py = worldPos.y;
        Vector2Int currentCell = new Vector2Int(Mathf.FloorToInt(px), Mathf.FloorToInt(py));

        if (playableMapCells.Contains(currentCell))
        {
            return worldPos;
        }

        Vector2Int bestCell = currentCell;
        float bestDistSq = float.MaxValue;
        Vector2 pos2D = new Vector2(px, py);

        foreach (Vector2Int cell in playableMapCells)
        {
            Vector2 cellCenter = new Vector2(cell.x + 0.5f, cell.y + 0.5f);
            float dSq = (cellCenter - pos2D).sqrMagnitude;
            if (dSq < bestDistSq)
            {
                bestDistSq = dSq;
                bestCell = cell;
            }
        }

        px = Mathf.Clamp(px, bestCell.x + edgePadding, bestCell.x + 1f - edgePadding);
        py = Mathf.Clamp(py, bestCell.y + edgePadding, bestCell.y + 1f - edgePadding);

        return new Vector3(px, py, worldPos.z);
    }

    private void EnforcePlayableMapBounds()
    {
        if (activeTransit != PassageTransitMode.None)
            return;

        EnsurePlayableMapInitialized();
        if (!isPlayableMapInitialized || playableMapCells.Count == 0)
            return;

        Vector3 currentPos = transform.position;
        Vector2Int currentCell = new Vector2Int(Mathf.FloorToInt(currentPos.x), Mathf.FloorToInt(currentPos.y));

        if (playableMapCells.Contains(currentCell))
            return;

        Vector3 clampedPos = ClampPositionToPlayableMap(currentPos, 0.35f);
        transform.position = clampedPos;
        if (ajanMotoru != null && ajanMotoru.enabled && ajanMotoru.isOnNavMesh)
        {
            ajanMotoru.Warp(clampedPos);
        }
    }

    public int GetNavigationRegion(Vector3 worldPos)
    {
        if (worldPos.y >= 16.5f && worldPos.x >= 0.5f && worldPos.x <= 7.5f)
        {
            return 2;
        }

        bool inNorthCourtyard = worldPos.y >= 10.5f && worldPos.y < 16.5f && worldPos.x >= -3.8f && worldPos.x <= 8.6f;
        bool inUpperTunnel = worldPos.y >= 8.5f && worldPos.y < 10.5f && worldPos.x >= 5.6f && worldPos.x <= 8.4f;

        if (inNorthCourtyard || inUpperTunnel)
        {
            return 1;
        }

        return 0;
    }

    public void MoveAgentTowardsTarget(Vector3 rawTargetWorldPos)
    {
        if (ajanMotoru == null)
            return;

        EnsurePlayableMapInitialized();
        if (!isPlayableMapInitialized || playableMapCells.Count == 0)
        {
            if (ajanMotoru.enabled && ajanMotoru.isOnNavMesh)
            {
                ajanMotoru.SetDestination(rawTargetWorldPos);
            }
            return;
        }

        Vector3 safeTarget = GetPlayableTargetPosition(rawTargetWorldPos);
        int targetRegion = GetNavigationRegion(safeTarget);

        if (activeTransit != PassageTransitMode.None)
        {
            if (activeTransit == PassageTransitMode.TunnelNorth && targetRegion == 0)
            {
                activeTransit = PassageTransitMode.TunnelSouth;
            }
            else if (activeTransit == PassageTransitMode.TunnelSouth && targetRegion >= 1)
            {
                activeTransit = PassageTransitMode.TunnelNorth;
            }
            else if (activeTransit == PassageTransitMode.AltarStairsNorth && targetRegion <= 1)
            {
                activeTransit = PassageTransitMode.AltarStairsSouth;
            }
            else if (activeTransit == PassageTransitMode.AltarStairsSouth && targetRegion == 2)
            {
                activeTransit = PassageTransitMode.AltarStairsNorth;
            }

            StepActivePassageTransit();
            return;
        }

        EnsureAgentOnNavMesh();

        int currentRegion = GetNavigationRegion(transform.position);

        if (currentRegion == 0 && targetRegion >= 1)
        {
            Vector3 pos = transform.position;
            bool atTunnelSouthMouth = Vector2.Distance(pos, TunnelSouthPoint) <= 0.95f
                || (pos.x >= 5.6f && pos.x <= 8.4f && pos.y >= 5.1f && pos.y <= 11.9f);

            if (atTunnelSouthMouth)
            {
                BeginPassageTransit(PassageTransitMode.TunnelNorth);
            }
            else
            {
                SetDestinationOnNavMesh(TunnelSouthPoint);
            }
            return;
        }

        if (currentRegion == 1 && targetRegion == 0)
        {
            Vector3 pos = transform.position;
            bool atTunnelNorthMouth = Vector2.Distance(pos, TunnelNorthPoint) <= 0.95f
                || (pos.x >= 5.6f && pos.x <= 8.4f && pos.y >= 5.5f && pos.y <= 12.4f);

            if (atTunnelNorthMouth)
            {
                BeginPassageTransit(PassageTransitMode.TunnelSouth);
            }
            else
            {
                SetDestinationOnNavMesh(TunnelNorthPoint);
            }
            return;
        }

        if (currentRegion == 1 && targetRegion == 2)
        {
            Vector3 pos = transform.position;
            bool atStairsSouthMouth = Vector2.Distance(pos, AltarStairsSouthPoint) <= 0.95f
                || (pos.x >= 2.8f && pos.x <= 5.2f && pos.y >= 14.3f && pos.y <= 18.5f);

            if (atStairsSouthMouth)
            {
                BeginPassageTransit(PassageTransitMode.AltarStairsNorth);
            }
            else
            {
                SetDestinationOnNavMesh(AltarStairsSouthPoint);
            }
            return;
        }

        if (currentRegion == 2 && targetRegion <= 1)
        {
            Vector3 pos = transform.position;
            bool atStairsNorthMouth = Vector2.Distance(pos, AltarStairsNorthPoint) <= 0.95f
                || (pos.x >= 2.8f && pos.x <= 5.2f && pos.y >= 14.7f && pos.y <= 19.0f);

            if (atStairsNorthMouth)
            {
                BeginPassageTransit(PassageTransitMode.AltarStairsSouth);
            }
            else
            {
                SetDestinationOnNavMesh(AltarStairsNorthPoint);
            }
            return;
        }

        manualFlightVelocity = Vector2.zero;
        SetDestinationOnNavMesh(safeTarget);
    }

    private void BeginPassageTransit(PassageTransitMode mode)
    {
        activeTransit = mode;
        if (ajanMotoru != null)
        {
            if (ajanMotoru.enabled && ajanMotoru.isOnNavMesh)
            {
                ajanMotoru.ResetPath();
                ajanMotoru.isStopped = true;
            }
            ajanMotoru.enabled = false;
        }

        StepActivePassageTransit();
    }

    private void StepActivePassageTransit()
    {
        Vector3 exitPoint;
        float corridorX;

        switch (activeTransit)
        {
            case PassageTransitMode.TunnelNorth:
                exitPoint = TunnelNorthPoint;
                corridorX = 7.0f;
                break;
            case PassageTransitMode.TunnelSouth:
                exitPoint = TunnelSouthPoint;
                corridorX = 7.0f;
                break;
            case PassageTransitMode.AltarStairsNorth:
                exitPoint = AltarStairsNorthPoint;
                corridorX = 4.0f;
                break;
            case PassageTransitMode.AltarStairsSouth:
                exitPoint = AltarStairsSouthPoint;
                corridorX = 4.0f;
                break;
            default:
                activeTransit = PassageTransitMode.None;
                manualFlightVelocity = Vector2.zero;
                EnsureAgentOnNavMesh();
                return;
        }

        Vector3 currentPos = transform.position;
        float baseSpeed = (ajanMotoru != null && ajanMotoru.speed > 0.1f) ? ajanMotoru.speed : 3.5f;
        float transitSpeed = Mathf.Max(baseSpeed, 4.2f);
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);

        Vector3 nextPos = Vector3.MoveTowards(currentPos, exitPoint, transitSpeed * dt);
        nextPos.x = Mathf.MoveTowards(currentPos.x, corridorX, transitSpeed * 1.6f * dt);
        nextPos.z = 0f;

        Vector2 delta = new Vector2(nextPos.x - currentPos.x, nextPos.y - currentPos.y);
        manualFlightVelocity = delta / dt;
        transform.position = nextPos;

        if (Vector2.Distance(nextPos, exitPoint) <= 0.08f)
        {
            CompletePassageTransit(exitPoint);
        }
    }

    private void CompletePassageTransit(Vector3 exitPoint)
    {
        activeTransit = PassageTransitMode.None;
        manualFlightVelocity = Vector2.zero;

        Vector3 landedPos = SampleNavMeshOrFallback(exitPoint, 1.5f);
        transform.position = landedPos;

        if (ajanMotoru != null)
        {
            if (!ajanMotoru.enabled)
            {
                ajanMotoru.enabled = true;
            }

            if (ajanMotoru.isOnNavMesh)
            {
                ajanMotoru.Warp(landedPos);
                ajanMotoru.isStopped = false;
            }
        }
    }

    private void EnsureAgentOnNavMesh()
    {
        if (ajanMotoru == null)
            return;

        if (!ajanMotoru.enabled)
        {
            Vector3 snapPos = SampleNavMeshOrFallback(transform.position, 2.0f);
            transform.position = snapPos;
            ajanMotoru.enabled = true;
        }

        if (ajanMotoru.isOnNavMesh && ajanMotoru.isStopped)
        {
            ajanMotoru.isStopped = false;
        }
    }

    private void SetDestinationOnNavMesh(Vector3 targetWorldPos)
    {
        if (ajanMotoru == null || !ajanMotoru.enabled || !ajanMotoru.isOnNavMesh)
            return;

        Vector3 navTarget = SampleNavMeshOrFallback(targetWorldPos, 1.5f);
        if ((ajanMotoru.hasPath || ajanMotoru.pathPending) && Vector2.Distance(ajanMotoru.destination, navTarget) <= 0.08f)
            return;

        ajanMotoru.SetDestination(navTarget);
    }

    private Vector3 SampleNavMeshOrFallback(Vector3 worldPos, float maxDistance)
    {
        Vector3 flatPos = new Vector3(worldPos.x, worldPos.y, 0f);
        if (NavMesh.SamplePosition(flatPos, out NavMeshHit hit, maxDistance, NavMesh.AllAreas))
        {
            return new Vector3(hit.position.x, hit.position.y, 0f);
        }
        return GetPlayableTargetPosition(flatPos);
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

        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.NotifyClueDiscoveredEastChest();
        }

        if (target != null)
        {
            sandikHedefi = target;
        }

        if (sandikHedefi == null)
        {
            FindSceneChestReferences();
        }

        if (sandikHedefi == null)
            return;

        if (ajanMotoru != null && ajanMotoru.enabled && ajanMotoru.isOnNavMesh)
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
        if (activeTransit != PassageTransitMode.None || manualFlightVelocity.sqrMagnitude > 0.005f)
        {
            moveVel = manualFlightVelocity;
        }
        else if (ajanMotoru != null && ajanMotoru.enabled)
        {
            bool is2D = !ajanMotoru.updateUpAxis || Object.FindFirstObjectByType<Grid>() != null;
            if (is2D)
            {
                moveVel = new Vector2(ajanMotoru.velocity.x, ajanMotoru.velocity.y);
                if (moveVel.sqrMagnitude < 0.005f && ajanMotoru.isOnNavMesh)
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
            case 1:
                if (HasValidSprites(northFlyFrames))
                    return northFlyFrames;
                break;
            case 2:
            case 3:
                if (HasValidSprites(sideFlyFrames))
                    return sideFlyFrames;
                break;
            default:
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
