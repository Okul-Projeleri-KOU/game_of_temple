using UnityEngine;

public class NavigateToKeyState : BaseAgentState
{
    public override void EnterState(CompanionAgent agent)
    {
        Debug.Log("Yoldaş Durumu: Anahtara gitme başlatıldı.");
        
        
        if (agent.sandikHedefi != null && agent.ajanMotoru != null)
        {
            agent.ajanMotoru.SetDestination(agent.sandikHedefi.position);
        }
    }

    public override void UpdateState(CompanionAgent agent)
    {
        
        if (agent.ajanMotoru.remainingDistance <= agent.ajanMotoru.stoppingDistance)
        {
           Debug.Log("Sandığa başarıyla ulaşıldı!");
           agent.ChangeState(new FollowState()); 
        }
    }

    public override void ExitState(CompanionAgent agent)
    {
        Debug.Log("Yoldaş Durumu: Anahtara gitme sonlandı.");
    }
}