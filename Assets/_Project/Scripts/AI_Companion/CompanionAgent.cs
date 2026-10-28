using UnityEngine;

public class CompanionAgent : MonoBehaviour
{
    private BaseAgentState currentState;

    private void Start()
    {
        ChangeState(new IdleState());
    }

   private void Update()
{
    if (currentState != null)
    {
        currentState.Update();
    }

    
    if (Input.GetKeyDown(KeyCode.Space))
    {
        ChangeState(new ThinkingState());
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