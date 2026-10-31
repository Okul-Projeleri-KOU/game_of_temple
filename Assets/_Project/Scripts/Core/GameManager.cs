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

    [Header("Spawn Settings (Aşama 1 - Bölge 1)")]
    [Tooltip("Oyuncunun doğacağı başlangıç noktası (SpawnPoint).")]
    [SerializeField] private Transform spawnPoint;

    [Tooltip("Oyuncu Transform referansı (Boş bırakılırsa 'Player' tag'i ile otomatik bulunur).")]
    [SerializeField] private Transform playerTransform;

    [Tooltip("Başlangıç bölgesinin Cainos fizik katmanı (Sol üst teras için 'Layer 2').")]
    [SerializeField] private string spawnLayerName = "Layer 2";

    [Tooltip("Başlangıç bölgesinin Cainos Sorting Layer adı (Sol üst teras için 'Layer 2').")]
    [SerializeField] private string spawnSortingLayerName = "Layer 2";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
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

        // CameraFollow.Start() çalışmadan önce oyuncuyu ve kamerayı başlangıç noktasına taşı
        RespawnPlayer();
    }

    /// <summary>
    /// Oyuncuyu (ve kamerayı) başlangıç noktasına (SpawnPoint) ışınlar ve katmanlarını günceller.
    /// Hem oyun başında hem de Faz 3'te Muhafız 'KILL_PLAYER' kararı verdiğinde çağrılır.
    /// </summary>
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
                Debug.LogWarning("[GameManager] SpawnPoint atanmamış! Lütfen Inspector'dan SpawnPoint Transform'unu atayın.");
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
                Debug.LogWarning("[GameManager] Sahne içinde 'Player' tag'ine sahip karakter bulunamadı!");
                return;
            }
        }

        Vector3 targetPosition = new Vector3(spawnPoint.position.x, spawnPoint.position.y, playerTransform.position.z);
        Vector3 delta = targetPosition - playerTransform.position;

        // Oyuncuyu ışınla
        playerTransform.position = targetPosition;

        Rigidbody2D rb = playerTransform.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.position = new Vector2(targetPosition.x, targetPosition.y);
        }

        // Kamerayı aynı fark kadar kaydır ki CameraFollow.Start() yanlış offset hesaplamasın
        if (Camera.main != null)
        {
            Camera.main.transform.position += new Vector3(delta.x, delta.y, 0f);
        }

        // Cainos çok katmanlı harita mimarisine uygun olarak fizik ve Sorting Layer değerlerini güncelle
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

    /// <summary>
    /// SpawnPoint hangi katmanın zemin karosu (Layer 3 / Layer 2 / Layer 1) üzerindeyse onu otomatik tespit eder.
    /// Böylece Inspector'da Layer 1 kalsa bile karakter üst katmanda doğduğunda zeminin altında görünmez olmaz.
    /// </summary>
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

    private void OnDrawGizmos()
    {
        if (spawnPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(spawnPoint.position, 0.4f);
        }
    }
}
