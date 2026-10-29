public abstract class BaseAgentState
{
    public abstract void EnterState(CompanionAgent agent);
    public abstract void UpdateState(CompanionAgent agent);
    public abstract void ExitState(CompanionAgent agent);
}