using System;

[Serializable]
public class NPCResponseDTO
{
    public string npc_reply;
    public string dialogue;
    public string action;
    public string reason;

    public void Normalize()
    {
        if (string.IsNullOrWhiteSpace(dialogue) && !string.IsNullOrWhiteSpace(npc_reply))
        {
            dialogue = npc_reply;
        }
        else if (string.IsNullOrWhiteSpace(npc_reply) && !string.IsNullOrWhiteSpace(dialogue))
        {
            npc_reply = dialogue;
        }

        if (string.IsNullOrWhiteSpace(action))
        {
            action = "WAIT";
        }
        else
        {
            action = action.Trim().ToUpperInvariant();
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            reason = "Muhafız oyuncunun niyetini değerlendirdi.";
        }
    }
}
