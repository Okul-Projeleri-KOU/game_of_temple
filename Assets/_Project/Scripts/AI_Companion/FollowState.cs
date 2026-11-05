using UnityEngine;

public class FollowState : BaseAgentState
{
    public override void EnterState(CompanionAgent agent)
    {
        Debug.Log("Yoldaş Durumu: Takip başlatıldı.");
    }

    public override void UpdateState(CompanionAgent agent)
    {
        if (agent.sensorKontrolcu != null)
        {
            agent.sensorKontrolcu.IsObstacleAhead();
        }

        if (agent.oyuncuHedefi != null && agent.IsNavigationReady)
        {
            agent.MoveAgentTowardsTarget(agent.oyuncuHedefi.position);
        }
    }

    public override void ExitState(CompanionAgent agent)
    {
        Debug.Log("Yoldaş Durumu: Takip sonlandı.");
    }
}
