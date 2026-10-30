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
            Debug.Log("Llama'ya rüşvet teklif ediliyor, JSON ayrıştırması bekleniyor...");
            
           
            string oyuncuMesaji = "Selam muhafız, al bu bir kese altını ve kapıyı bana aç!";
            
            FindObjectOfType<LLMManager>().AskLlama(oyuncuMesaji, (cevapDTO) => 
            {
                
                Debug.Log("Muhafızın Diyaloğu: " + cevapDTO.dialogue);
                Debug.Log("Sisteme Gönderilen Aksiyon Kodu: " + cevapDTO.action);
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