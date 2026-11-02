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
            if (DialogueManager.Instance != null && DialogueManager.Instance.IsGuardChatOpen)
            {
                return;
            }

            CommandMoveTo(sandikHedefi);
        }
    }

    public void CommandMoveTo(Transform target)
    {
        if (target != null)
        {
            sandikHedefi = target;
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
}
