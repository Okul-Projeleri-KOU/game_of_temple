using System.Threading.Tasks;

public interface IDialogueService
{
    Task<NPCResponseDTO> SendMessageAsync(string text);
}

