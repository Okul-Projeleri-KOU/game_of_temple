using TMPro;
using UnityEngine;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private TextMeshProUGUI dialogueText;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        HideDialogue();
    }

    private void Update()
    {
        if (dialoguePanel != null && dialoguePanel.activeSelf && Input.GetKeyDown(KeyCode.Escape))
        {
            HideDialogue();
        }
    }

    public void ShowDialogue(string text)
    {
        if (dialogueText != null)
        {
            dialogueText.text = text;
        }

        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(true);
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetGameState(GameState.Dialogue);
        }
    }

    public void HideDialogue()
    {
        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
        }

        if (dialogueText != null)
        {
            dialogueText.text = string.Empty;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetGameState(GameState.Playing);
        }
    }
}

