using UnityEngine;

public class CollectAndReturnState : BaseAgentState
{
    public override void EnterState(CompanionAgent agent)
    {
        Debug.Log("Yoldaş Durumu: Sandıktan anahtar alındı, oyuncuya geri dönülüyor.");

        if (agent.oyuncuHedefi != null && agent.ajanMotoru != null && agent.ajanMotoru.isOnNavMesh)
        {
            agent.ajanMotoru.SetDestination(agent.oyuncuHedefi.position);
        }
    }

    public override void UpdateState(CompanionAgent agent)
    {
        if (agent.ajanMotoru == null || agent.oyuncuHedefi == null || !agent.ajanMotoru.isOnNavMesh)
            return;

        if (agent.sensorKontrolcu != null)
        {
            agent.sensorKontrolcu.IsObstacleAhead();
        }

        agent.ajanMotoru.SetDestination(agent.oyuncuHedefi.position);

        if (!agent.ajanMotoru.pathPending)
        {
            float distToPlayer = Vector3.Distance(agent.transform.position, agent.oyuncuHedefi.position);
            float stopThreshold = Mathf.Max(agent.ajanMotoru.stoppingDistance, 1.35f);

            if (distToPlayer <= stopThreshold && agent.ajanMotoru.remainingDistance <= stopThreshold)
            {
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.SetHasKey(true);
                }

                Debug.Log("Oyuncuya geri dönüldü ve Kutsal Anahtar teslim edildi (HasKey = true), görev tamam!");
                agent.ChangeState(new FollowState());
            }
        }
    }

    public override void ExitState(CompanionAgent agent)
    {
        Debug.Log("Yoldaş Durumu: Görevi tamamlayıp dönüldü.");
    }
}
