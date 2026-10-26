using System;
using UnityEngine;

public class RuneAltar : MonoBehaviour, IInteractable
{
    public event Action OnAltarActivated;

    public bool IsActivated { get; private set; }

    public void Interact()
    {
        if (IsActivated)
            return;

        IsActivated = true;
        Debug.Log("Rün Sunağı aktif edildi!");
        OnAltarActivated?.Invoke();
    }
}

