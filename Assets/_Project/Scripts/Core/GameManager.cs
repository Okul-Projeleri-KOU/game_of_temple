using System;
using UnityEngine;
using UnityEngine.Tilemaps;

public enum GameState
{
    Playing,
    Dialogue,
    Paused
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public GameState CurrentState { get; private set; }

    public event Action<GameState> OnGameStateChanged;

    [Header("Oyun Durumu")]
    [SerializeField] private bool hasKey;
    [SerializeField] private bool hasCompanion;

    public bool HasKey => hasKey;
    public bool HasCompanion => hasCompanion;
    public Transform PlayerTransform => playerTransform;

    public event Action<bool> OnKeyStateChanged;
    public event Action<bool> OnCompanionStateChanged;

    [Header("Başlangıç Ayarları")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private string spawnLayerName = "Layer 2";
    [SerializeField] private string spawnSortingLayerName = "Layer 2";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Instance.spawnPoint = spawnPoint;
            Instance.playerTransform = playerTransform;
            Instance.SetHasKey(false);
            Instance.SetHasCompanion(false);
            Instance.SetGameState(GameState.Playing);
            Instance.RespawnPlayer();
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        CurrentState = GameState.Playing;
        OnGameStateChanged?.Invoke(CurrentState);

        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                playerTransform = playerObj.transform;
            }
        }

        RespawnPlayer();
    }

    public void RespawnPlayer()
    {
        if (spawnPoint == null)
        {
            GameObject foundSpawn = GameObject.Find("SpawnPoint") ?? GameObject.Find("SpawnPoint ");
            if (foundSpawn != null)
            {
                spawnPoint = foundSpawn.transform;
            }
            else
            {
                Debug.LogWarning("SpawnPoint atanmamış.");
                return;
            }
        }

        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                playerTransform = playerObj.transform;
            }
            else
            {
                Debug.LogWarning("Player bulunamadı.");
                return;
            }
        }

        Vector3 targetPosition = new Vector3(spawnPoint.position.x, spawnPoint.position.y, playerTransform.position.z);
        Vector3 delta = targetPosition - playerTransform.position;

        playerTransform.position = targetPosition;

        Rigidbody2D rb = playerTransform.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.position = new Vector2(targetPosition.x, targetPosition.y);
        }

        if (Camera.main != null)
        {
            Camera.main.transform.position += new Vector3(delta.x, delta.y, 0f);
        }

        ApplySpawnLayer(playerTransform.gameObject, targetPosition);
    }

    private void ApplySpawnLayer(GameObject target, Vector3 worldPos)
    {
        string detectedLayer = DetectGroundLayerAtPosition(worldPos);
        string finalLayerName = !string.IsNullOrEmpty(detectedLayer) ? detectedLayer : spawnLayerName;
        string finalSortingLayerName = !string.IsNullOrEmpty(detectedLayer) ? detectedLayer : spawnSortingLayerName;

        if (!string.IsNullOrEmpty(finalLayerName))
        {
            int layerIndex = LayerMask.NameToLayer(finalLayerName);
            if (layerIndex != -1)
            {
                target.layer = layerIndex;
            }
        }

        if (!string.IsNullOrEmpty(finalSortingLayerName))
        {
            SpriteRenderer[] renderers = target.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (SpriteRenderer sr in renderers)
            {
                sr.sortingLayerName = finalSortingLayerName;
            }
        }
    }

    private string DetectGroundLayerAtPosition(Vector3 worldPos)
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

    public void SetGameState(GameState newState)
    {
        if (CurrentState == newState)
            return;

        CurrentState = newState;
        OnGameStateChanged?.Invoke(CurrentState);
    }

    public void SetHasKey(bool value)
    {
        if (hasKey == value)
            return;

        hasKey = value;
        OnKeyStateChanged?.Invoke(hasKey);
    }

    public void SetHasCompanion(bool value)
    {
        if (hasCompanion == value)
            return;

        hasCompanion = value;
        OnCompanionStateChanged?.Invoke(hasCompanion);
    }

    private void OnDrawGizmos()
    {
        if (spawnPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(spawnPoint.position, 0.4f);
        }
    }
}
