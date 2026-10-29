using UnityEngine;

public class FollowState : BaseAgentState
{
    public override void EnterState(CompanionAgent agent)
    {
        Debug.Log("Yoldaş Durumu: Takip başlatıldı.");
    }

    public override void UpdateState(CompanionAgent agent)
    {
        
        if (agent.oyuncuHedefi != null && agent.ajanMotoru != null)
        {
            agent.ajanMotoru.SetDestination(agent.oyuncuHedefi.position);
        }
    }

    public override void ExitState(CompanionAgent agent)
    {
        Debug.Log("Yoldaş Durumu: Takip sonlandı.");
    }
}