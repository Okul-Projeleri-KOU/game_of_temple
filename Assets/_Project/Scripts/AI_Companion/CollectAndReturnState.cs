using UnityEngine;

public class CollectAndReturnState : BaseAgentState
{
    public override void EnterState(CompanionAgent agent)
    {
        Debug.Log("Yoldaş Durumu: Görevi tamamlayıp dönülüyor.");
        
        
        if (agent.oyuncuHedefi != null && agent.ajanMotoru != null)
        {
            agent.ajanMotoru.SetDestination(agent.oyuncuHedefi.position);
        }
    }

    public override void UpdateState(CompanionAgent agent)
    {
        
        if (agent.ajanMotoru != null && !agent.ajanMotoru.pathPending && agent.ajanMotoru.remainingDistance <= agent.ajanMotoru.stoppingDistance)
        {
            Debug.Log("Oyuncuya geri dönüldü, görev tamam!");
            
        }
    }

    public override void ExitState(CompanionAgent agent)
    {
        Debug.Log("Yoldaş Durumu: Görevi tamamlayıp dönüldü.");
    }
}