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