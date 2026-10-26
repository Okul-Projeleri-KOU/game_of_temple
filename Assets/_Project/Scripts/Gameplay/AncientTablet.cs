using UnityEngine;

public class AncientTablet : MonoBehaviour, IInteractable
{
    [TextArea(2, 5)]
    [SerializeField] private string clueText = "Kadim Tablet İpucusu: Kapıyı açmak için önce Rün Sunağı'nı uyandırmalısın.";

    public void Interact()
    {
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.ShowDialogue(clueText);
        }
    }
}

