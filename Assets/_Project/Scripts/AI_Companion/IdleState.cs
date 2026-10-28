using UnityEngine;

public class IdleState : BaseAgentState
{
    public override void Enter()
    {
        Debug.Log("Ajan: Çevrede yeni bir tuzak veya mekanizma göremiyorum. İşaretini bekliyorum.");
    }

    public override void Update()
    {
       
    }

    public override void Exit()
    {
        Debug.Log("Ajan: Anlaşıldı, hareket halindeyim.");
    }
}