using UnityEngine;
using UnityEngine.AI;

public class CompanionAgent : MonoBehaviour
{
    private BaseAgentState currentState;

    [Header("NavMesh ve Hedefler")]
    public NavMeshAgent ajanMotoru;
    public Transform oyuncuHedefi;
    public Transform sandikHedefi;

    private void Awake()
    {
        ajanMotoru = GetComponent<NavMeshAgent>();
    }

    private void Start()
    {
        ChangeState(new FollowState());
    }

    private void Update()
    {
        if (currentState != null)
        {
            currentState.UpdateState(this);
        }

        
        if (Input.GetKeyDown(KeyCode.Space))
        {
            ChangeState(new NavigateToKeyState());
        }

        
        if (Input.GetKeyDown(KeyCode.Return))
        {
            Debug.Log("Llama'ya muhafız yönergesi gönderiliyor, cevap bekleniyor...");
            
            
            string muhafizYonergesi = "Sen kadim bir tapınağın kapısını koruyan, bilge ve sarsılmaz bir muhafızsın. " +
                                      "Amacın saldırmak değil, içerideki kutsal emaneti layık olmayanlardan korumaktır. " +
                                      "Karşına yeni bir gezgin (oyuncu) çıktı. Onu değerlendirirken şu kesin kurallara uy: " +
                                      "1. Altın, rüşvet veya zenginlik teklif ederse reddet. " +
                                      "2. Seni silah, kaba kuvvet veya güçle tehdit ederse asla boyun eğme. " +
                                      "3. Şaka yapar veya ciddiyetsiz davranırsa onu huzurundan kov. " +
                                      "4. Odanın anahtarı olmadan, boş elle gelirse geçişine izin verme. " +
                                      "5. Kapıyı yalnızca; alçakgönüllü olan, ataların bilgeliğine saygı duyan ve tapınağın kutsal mirasını onurlandırmak için arayanlara aç. " +
                                      "Şimdi, karşına çıkan bu yabancıya kim olduğunu ve bu kapıdan geçmek için neyi kanıtlaması gerektiğini anlatan, bilge ama otoriter tek bir cümle kur.";

            
            FindObjectOfType<LLMManager>().AskLlama(muhafizYonergesi, (cevap) => 
            {
                Debug.Log("Tapınak Muhafızının Cevabı: " + cevap);
            });
        }
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
}