using UnityEngine;
using UnityEngine.AI;

public class GuardAgent : MonoBehaviour
{
    public static bool IsAnyGuardAttacking => false;

    private NavMeshAgent nav;
    private float moveSpeed = 5f;

    private void Start()
    {
        nav = GetComponent<NavMeshAgent>();

        if (nav != null)
        {
            nav.speed = moveSpeed;
            nav.updateRotation = false;
            nav.updateUpAxis = false;
            nav.baseOffset = 0f;
        }
    }

    private void Update()
    {
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
        Debug.LogError("SİSTEM: Muhafız öfkelendi (KILL_PLAYER)! Kaybetme ekranı tetikleniyor.");
    }

    public void HandleGuardDecision(NPCResponseDTO cevapDTO)
    {
        if (cevapDTO == null)
            return;

        cevapDTO.Normalize();
        Debug.Log("Muhafızın Diyaloğu: " + cevapDTO.dialogue);

        if (cevapDTO.action == "KILL_PLAYER")
        {
            Debug.LogError("SİSTEM: Muhafız öfkelendi (KILL_PLAYER)! Oyun kaybedildi.");

            if (DialogueManager.Instance != null)
            {
                if (!DialogueManager.Instance.IsVictoryScreenOpen)
                {
                    DialogueManager.Instance.ShowEndScreen(
                        false,
                        "ÖLDÜNÜZ",
                        "Tapınak Muhafızı seni yakaladı.",
                        null,
                        0f);
                }
            }
            else if (GameManager.Instance != null)
            {
                GameManager.Instance.RespawnPlayer();
            }
        }
        else if (cevapDTO.action == "OPEN_DOOR")
        {
            bool isMainScene = DialogueManager.Instance != null;
            bool hasKey = GameManager.Instance != null && GameManager.Instance.HasKey;

            if (!hasKey)
            {
                Debug.LogWarning("SİSTEM GÜVENLİK KONTROLÜ: Muhafız OPEN_DOOR döndürdü fakat oyuncuda Kutsal Anahtar (HasKey == true) yok! Kapı açılamaz.");
                return;
            }

            if (isMainScene)
            {
                Debug.LogWarning("SİSTEM: Muhafız ikna oldu ve Kutsal Anahtar doğrulandı! Kapı açılıyor.");
                DialogueManager.Instance.HandleOpenDoorAction();
            }
            else
            {
                Debug.LogWarning("SİSTEM: Muhafız ikna oldu ve Kutsal Anahtar doğrulandı! Kapı açılıyor.");
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
