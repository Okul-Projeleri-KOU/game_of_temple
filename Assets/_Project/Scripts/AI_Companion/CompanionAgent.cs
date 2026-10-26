using UnityEngine;

public class CompanionAgent : MonoBehaviour
{
    private BaseAgentState currentState;

    private void Start()
    {
        
    }

    private void Update()
    {
        if (currentState != null)
        {
            currentState.Update();
        }
    }

    public void ChangeState(BaseAgentState newState)
    {
        if (currentState != null)
        {
            currentState.Exit();
        }

        currentState = newState;

        if (currentState != null)
        {
            currentState.Enter();
        }
    }
}