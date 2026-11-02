using UnityEngine;

public class IdleState : BaseAgentState
{
    public override void EnterState(CompanionAgent agent)
    {
        Debug.Log("Yoldaş Durumu: Bekleme (Idle) moduna geçildi. Çevre gözlemleniyor ve komut bekleniyor.");

        if (agent.ajanMotoru != null && agent.ajanMotoru.isOnNavMesh)
        {
            agent.ajanMotoru.ResetPath();
        }
    }

    public override void UpdateState(CompanionAgent agent)
    {
        if (agent.sensorKontrolcu != null)
        {
            agent.sensorKontrolcu.IsObstacleAhead();
        }

        if (agent.oyuncuHedefi != null && agent.ajanMotoru != null)
        {
            float stopDist = Mathf.Max(agent.ajanMotoru.stoppingDistance, 1.2f);
            float distToPlayer = Vector2.Distance(agent.transform.position, agent.oyuncuHedefi.position);

            if (distToPlayer > stopDist + 0.5f)
            {
                agent.ChangeState(new FollowState());
            }
        }
    }

    public override void ExitState(CompanionAgent agent)
    {
        Debug.Log("Yoldaş Durumu: Bekleme (Idle) modundan çıkıldı.");
    }
}

