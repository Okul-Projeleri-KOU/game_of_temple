using UnityEngine;

public class CollectAndReturnState : BaseAgentState
{
    public override void EnterState(CompanionAgent agent)
    {
        Debug.Log("Yoldaş Durumu: Sandıktan anahtar alındı, oyuncuya geri dönülüyor.");

        if (agent.oyuncuHedefi != null && agent.ajanMotoru != null && agent.ajanMotoru.isOnNavMesh)
        {
            agent.ajanMotoru.isStopped = false;
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
            bool is2D = !agent.ajanMotoru.updateUpAxis || Object.FindFirstObjectByType<Grid>() != null;
            float distToPlayer = is2D
                ? Vector2.Distance(agent.transform.position, agent.oyuncuHedefi.position)
                : Vector3.Distance(agent.transform.position, agent.oyuncuHedefi.position);

            float stopThreshold = Mathf.Max(agent.ajanMotoru.stoppingDistance, 1.45f);

            if (distToPlayer <= stopThreshold)
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
