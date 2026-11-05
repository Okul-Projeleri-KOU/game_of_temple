using UnityEngine;

public class NavigateToKeyState : BaseAgentState
{
    private bool engelUyarisiVerildi;

    public override void EnterState(CompanionAgent agent)
    {
        engelUyarisiVerildi = false;
        Debug.Log("Yoldaş Durumu: Anahtara gitme başlatıldı.");

        agent.EnsureKeyChestTargetAssigned();

        if (agent.sandikHedefi != null && agent.ajanMotoru != null && agent.ajanMotoru.isOnNavMesh)
        {
            agent.ajanMotoru.isStopped = false;
            Vector3 safeChestTarget = agent.GetSafeChestApproachPosition(agent.sandikHedefi);
            agent.ajanMotoru.SetDestination(safeChestTarget);
        }
    }

    public override void UpdateState(CompanionAgent agent)
    {
        if (agent.sandikHedefi == null)
        {
            agent.EnsureKeyChestTargetAssigned();
        }

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
            Vector3 safeChestTarget = agent.GetSafeChestApproachPosition(agent.sandikHedefi);
            agent.ajanMotoru.SetDestination(safeChestTarget);
        }

        if (!agent.ajanMotoru.pathPending)
        {
            bool is2D = !agent.ajanMotoru.updateUpAxis || Object.FindFirstObjectByType<Grid>() != null;
            float distToTarget = is2D
                ? Vector2.Distance(agent.transform.position, agent.sandikHedefi.position)
                : Vector3.Distance(agent.transform.position, agent.sandikHedefi.position);

            float stopThreshold = Mathf.Max(agent.ajanMotoru.stoppingDistance, 1.45f);

            if (distToTarget <= stopThreshold)
            {
                OpenTargetKeyChest(agent.sandikHedefi);
                agent.MarkKeyTaskCompleted();

                Debug.Log("Sandığa başarıyla ulaşıldı ve sandık açıldı! Anahtar alınıp oyuncuya dönülüyor.");
                agent.ChangeState(new CollectAndReturnState());
            }
        }
    }

    private static void OpenTargetKeyChest(Transform chestTarget)
    {
        if (chestTarget == null)
            return;

        TreasureChest chest = chestTarget.GetComponent<TreasureChest>();
        if (chest == null)
        {
            chest = chestTarget.GetComponentInChildren<TreasureChest>();
        }
        if (chest == null)
        {
            chest = chestTarget.GetComponentInParent<TreasureChest>();
        }

        if (chest == null)
        {
            TreasureChest[] allChests = Object.FindObjectsByType<TreasureChest>(FindObjectsSortMode.None);
            foreach (TreasureChest c in allChests)
            {
                if (c != null && c.transform.position.x > 0f)
                {
                    chest = c;
                    break;
                }
            }
        }

        if (chest != null && !chest.IsOpen)
        {
            chest.Interact();
        }
    }

    public override void ExitState(CompanionAgent agent)
    {
        Debug.Log("Yoldaş Durumu: Anahtara gitme sonlandı.");
    }
}
