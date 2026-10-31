using UnityEngine;
using UnityEngine.AI;

public class GuardAgent : MonoBehaviour
{
    private NavMeshAgent nav;
    private Transform playerTarget;
    private bool isAttacking = false;

    void Start()
    {
        nav = GetComponent<NavMeshAgent>();
        if (nav != null) nav.speed = 5f;
    }

    void Update()
    {
        
        if (isAttacking && playerTarget != null)
        {
            nav.SetDestination(playerTarget.position);
            
            if (Vector3.Distance(transform.position, playerTarget.position) <= 1.5f)
            {
                Debug.LogError("OYUN BİTTİ! Muhafız seni yakaladı ve küle çevirdi!");
                isAttacking = false; 
                nav.isStopped = true;
                nav.ResetPath();
                nav.velocity = Vector3.zero;
            }
        }

        
       
        if (Input.GetKeyDown(KeyCode.Return))
        {
            Debug.Log("Llama'ya saygılı bir talep iletiliyor, JSON bekleniyor...");
            
            string oyuncuMesaji = "Saygıdeğer muhafız, işte sana benden istenen 'Geçiş Anahtarı' ve tapınağa giriş iznimi gösteren kimliğim. Lütfen kapıyı aç.";
            
            FindObjectOfType<LLMManager>().AskLlama(oyuncuMesaji, (cevapDTO) => 
            {
                Debug.Log("Muhafızın Diyaloğu: " + cevapDTO.dialogue);

                if (cevapDTO.action == "KILL_PLAYER")
                {
                    Debug.LogError("SİSTEM: Muhafız öfkelendi! Oyuncuya saldırıyor!");
                    GameObject hedefObj = GameObject.Find("Hedef");
                    if (hedefObj != null)
                    {
                        playerTarget = hedefObj.transform;
                        isAttacking = true;
                    }
                }
                else if (cevapDTO.action == "OPEN_DOOR")
                {
                    Debug.LogWarning("SİSTEM: Muhafız ikna oldu! Kapı açılıyor.");
                    
                    
                    GameObject kapiObj = GameObject.Find("Kapi");
                    if (kapiObj != null)
                    {
                        kapiObj.SetActive(false); 
                        Debug.Log("KAPI MEKANİZMASI: Geçit açıldı!");
                    }
                }
                else 
                {
                    Debug.Log("SİSTEM: Muhafız eylemsiz kaldı (WAIT). Karar vermeden önce daha fazla kanıt bekliyor.");
                }
            });
        }
    }
}