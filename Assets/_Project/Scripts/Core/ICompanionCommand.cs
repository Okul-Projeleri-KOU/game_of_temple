using UnityEngine;

public interface ICompanionCommand
{
    void CommandMoveTo(Transform target);
}

public interface ICompanionCommandReceiver : ICompanionCommand
{
}
