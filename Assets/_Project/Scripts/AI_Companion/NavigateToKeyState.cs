using UnityEngine;

public class NavigateToKeyState : BaseAgentState
{
    private bool engelUyarisiVerildi;

    public override void EnterState(CompanionAgent agent)
    {
        engelUyarisiVerildi = false;
        Debug.Log("Yoldaş Durumu: Anahtara gitme başlatıldı.");

        if (agent.sandikHedefi != null && agent.ajanMotoru != null && agent.ajanMotoru.isOnNavMesh)
        {
            agent.ajanMotoru.SetDestination(agent.sandikHedefi.position);
        }
    }

    public override void UpdateState(CompanionAgent agent)
    {
        if (agent.ajanMotoru == null || agent.sandikHedefi == null || !agent.ajanMotoru.isOnNavMesh)
            return;

        if (agent.sensorKontrolcu != null && agent.sensorKontrolcu.IsObstacleAhead())
        {
            if (!engelUyarisiVerildi)
            {
                Debug.Log("Raycast Sensörü: Ön/çapraz açıda duvar engeli algılandı, NavMesh üzerinde dinamik rota takip ediliyor.");
                engelUyarisiVerildi = true;
            }
        }
        else
        {
            engelUyarisiVerildi = false;
        }

        if (!agent.ajanMotoru.hasPath && !agent.ajanMotoru.pathPending)
        {
            agent.ajanMotoru.SetDestination(agent.sandikHedefi.position);
        }

        if (!agent.ajanMotoru.pathPending)
        {
            float distToTarget = Vector3.Distance(agent.transform.position, agent.sandikHedefi.position);
            float stopThreshold = Mathf.Max(agent.ajanMotoru.stoppingDistance, 1.35f);

            if (distToTarget <= stopThreshold && agent.ajanMotoru.remainingDistance <= stopThreshold)
            {
                Debug.Log("Sandığa başarıyla ulaşıldı! Anahtar alınıp oyuncuya dönülüyor.");
                agent.ChangeState(new CollectAndReturnState());
            }
        }
    }

    public override void ExitState(CompanionAgent agent)
    {
        Debug.Log("Yoldaş Durumu: Anahtara gitme sonlandı.");
    }
}
