using System;
using UnityEngine;

public enum GameState
{
    Playing,
    Dialogue,
    Paused
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public GameState CurrentState { get; private set; }

    public event Action<GameState> OnGameStateChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        CurrentState = GameState.Playing;
        OnGameStateChanged?.Invoke(CurrentState);
    }

    public void SetGameState(GameState newState)
    {
        if (CurrentState == newState)
            return;

        CurrentState = newState;
        OnGameStateChanged?.Invoke(CurrentState);
    }
}

