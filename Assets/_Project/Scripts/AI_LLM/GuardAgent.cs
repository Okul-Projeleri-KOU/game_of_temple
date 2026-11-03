using UnityEngine;
using UnityEngine.AI;

public class GuardAgent : MonoBehaviour
{
    public static bool IsAnyGuardAttacking { get; private set; }

    private NavMeshAgent nav;
    private Transform playerTarget;
    private Transform visualGuardTransform;
    private Collider2D visualGuardCollider2D;
    private bool visualGuardColliderWasEnabled;
    private Vector3 initialGuardPosition;
    private Vector3 initialVisualGuardPosition;
    private bool isAttacking = false;
    private float attackSpeed = 4.2f;
    private float attackElapsedTime = 0f;

    public bool IsAttacking => isAttacking;

    private void Start()
    {
        IsAnyGuardAttacking = false;
        initialGuardPosition = transform.position;
        nav = GetComponent<NavMeshAgent>();

        if (nav != null)
        {
            nav.speed = attackSpeed;
            nav.updateRotation = false;
            nav.updateUpAxis = false;
            nav.baseOffset = 0f;
        }

        FindVisualGuardInScene();
    }

    private void OnDisable()
    {
        if (isAttacking)
        {
            isAttacking = false;
            IsAnyGuardAttacking = false;
        }
    }

    private void FindVisualGuardInScene()
    {
        if (visualGuardTransform == null)
        {
            GameObject visualObj = GameObject.Find("muhafız - normal_0");
            if (visualObj != null)
            {
                visualGuardTransform = visualObj.transform;
                initialVisualGuardPosition = visualGuardTransform.position;
                visualGuardCollider2D = visualObj.GetComponent<Collider2D>();
            }
        }
    }

    private void Update()
    {
        if (isAttacking && playerTarget != null)
        {
            attackElapsedTime += Time.deltaTime;
            bool is2DScene = Object.FindFirstObjectByType<Grid>() != null;

            if (is2DScene)
            {
                FindVisualGuardInScene();
                Transform activeMover = visualGuardTransform != null ? visualGuardTransform : transform;

                Vector3 targetPos2D = new Vector3(playerTarget.position.x, playerTarget.position.y, activeMover.position.z);
                activeMover.position = Vector3.MoveTowards(activeMover.position, targetPos2D, attackSpeed * Time.deltaTime);
                transform.position = activeMover.position;

                float dist2D = Vector2.Distance(activeMover.position, playerTarget.position);
                if ((attackElapsedTime >= 0.45f && dist2D <= 0.65f) || attackElapsedTime >= 2.8f)
                {
                    CompleteGuardAttackAndRespawn();
                }
            }
            else
            {
                if (nav != null && nav.enabled && nav.isOnNavMesh)
                {
                    nav.SetDestination(playerTarget.position);
                }
                else
                {
                    transform.position = Vector3.MoveTowards(transform.position, playerTarget.position, attackSpeed * Time.deltaTime);
                }

                if (Vector3.Distance(transform.position, playerTarget.position) <= 1.5f || attackElapsedTime >= 5f)
                {
                    CompleteGuardAttackAndRespawn();
                }
            }
        }

        // Sadece DialogueManager bulunmayan TestScene ortamında Enter tuşu ile doğrudan test edilir.
        if (DialogueManager.Instance == null && Input.GetKeyDown(KeyCode.Return))
        {
            Debug.Log("Llama'ya saygılı bir talep iletiliyor, JSON bekleniyor...");

            string oyuncuMesaji = "Saygıdeğer muhafız, işte sana benden istenen 'Geçiş Anahtarı' ve tapınağa giriş iznimi gösteren kimliğim. Lütfen kapıyı aç.";

            LLMManager llm = FindFirstObjectByType<LLMManager>();
            if (llm != null)
            {
                llm.AskLlama(oyuncuMesaji, HandleGuardDecision);
            }
        }
    }

    public void StartAttackPlayer()
    {
        FindVisualGuardInScene();

        GameObject hedefObj = GameObject.FindGameObjectWithTag("Player");
        if (hedefObj == null)
        {
            hedefObj = GameObject.Find("PF Player") ?? GameObject.Find("Hedef");
        }

        if (hedefObj != null)
        {
            playerTarget = hedefObj.transform;
            attackElapsedTime = 0f;
            isAttacking = true;
            IsAnyGuardAttacking = true;

            if (visualGuardCollider2D != null)
            {
                visualGuardColliderWasEnabled = visualGuardCollider2D.enabled;
                visualGuardCollider2D.enabled = false;
            }

            if (nav != null && nav.enabled && nav.isOnNavMesh)
            {
                nav.isStopped = false;
            }
        }
        else if (GameManager.Instance != null)
        {
            GameManager.Instance.RespawnPlayer();
        }
    }

    private void CompleteGuardAttackAndRespawn()
    {
        Debug.LogError("OYUN BİTTİ! Muhafız seni yakaladı ve küle çevirdi!");
        isAttacking = false;
        IsAnyGuardAttacking = false;
        attackElapsedTime = 0f;

        if (visualGuardCollider2D != null)
        {
            visualGuardCollider2D.enabled = visualGuardColliderWasEnabled;
        }

        if (nav != null && nav.enabled && nav.isOnNavMesh)
        {
            nav.isStopped = true;
            nav.ResetPath();
            nav.velocity = Vector3.zero;
        }

        if (visualGuardTransform != null)
        {
            visualGuardTransform.position = initialVisualGuardPosition != Vector3.zero
                ? initialVisualGuardPosition
                : new Vector3(5.74f, -6.758f, 0f);
        }

        transform.position = initialGuardPosition;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.RespawnPlayer();
        }
    }

    public void HandleGuardDecision(NPCResponseDTO cevapDTO)
    {
        if (cevapDTO == null)
            return;

        cevapDTO.Normalize();
        Debug.Log("Muhafızın Diyaloğu: " + cevapDTO.dialogue);

        if (cevapDTO.action == "KILL_PLAYER")
        {
            Debug.LogError("SİSTEM: Muhafız öfkelendi! Oyuncuya saldırıyor!");
            StartAttackPlayer();
        }
        else if (cevapDTO.action == "OPEN_DOOR")
        {
            bool isMainScene = DialogueManager.Instance != null;
            bool hasKey = GameManager.Instance != null && GameManager.Instance.HasKey;

            if (isMainScene)
            {
                if (hasKey)
                {
                    Debug.LogWarning("SİSTEM: Muhafız ikna oldu ve Kutsal Anahtar doğrulandı! Kapı açılıyor.");
                    DialogueManager.Instance.HandleOpenDoorAction();
                }
                else
                {
                    Debug.LogWarning("SİSTEM: Muhafız ikna oldu fakat oyuncuda Kutsal Anahtar (HasKey) yok! Kapı açılamaz.");
                }
            }
            else
            {
                Debug.LogWarning("SİSTEM: Muhafız ikna oldu! Kapı açılıyor.");
                GameObject kapiObj = GameObject.Find("Kapi");
                if (kapiObj != null)
                {
                    kapiObj.SetActive(false);
                    Debug.Log("KAPI MEKANİZMASI: Geçit açıldı!");
                }
            }
        }
        else
        {
            Debug.Log("SİSTEM: Muhafız eylemsiz kaldı (WAIT). Karar vermeden önce daha fazla kanıt bekliyor.");
        }
    }
}
