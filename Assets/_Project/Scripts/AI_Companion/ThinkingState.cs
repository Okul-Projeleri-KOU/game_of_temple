using UnityEngine;

public class ThinkingState : BaseAgentState
{
    public override void Enter()
    {
        Debug.Log("Ajan: Düşünme durumuna geçtim. Karar veriyorum...");
    }

    public override void Update()
    {
        
    }

    public override void Exit()
    {
        Debug.Log("Ajan: Düşünme bitti, harekete geçiyorum!");
    }
}