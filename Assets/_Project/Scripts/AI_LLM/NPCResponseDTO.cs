using System;

[Serializable]
public class NPCResponseDTO
{
    public string npc_reply;
    public string dialogue;
    public string reply;
    public string response;
    public string message;
    public string action;
    public string mood;
    public string reason;

    public void Normalize()
    {
        if (string.IsNullOrWhiteSpace(npc_reply))
        {
            if (!string.IsNullOrWhiteSpace(dialogue))
                npc_reply = dialogue;
            else if (!string.IsNullOrWhiteSpace(reply))
                npc_reply = reply;
            else if (!string.IsNullOrWhiteSpace(response))
                npc_reply = response;
            else if (!string.IsNullOrWhiteSpace(message))
                npc_reply = message;
        }

        if (string.IsNullOrWhiteSpace(dialogue) && !string.IsNullOrWhiteSpace(npc_reply))
        {
            dialogue = npc_reply;
        }

        if (string.IsNullOrWhiteSpace(action))
        {
            action = "WAIT";
        }
        else
        {
            action = action.Trim().ToUpperInvariant();
        }

        if (string.IsNullOrWhiteSpace(mood))
        {
            mood = action == "WAIT" ? "THINKING" : "NORMAL";
        }
        else
        {
            mood = mood.Trim().ToUpperInvariant();
            if (mood != "THINKING" && mood != "NORMAL")
            {
                mood = action == "WAIT" ? "THINKING" : "NORMAL";
            }
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            reason = "Muhafız oyuncunun niyetini değerlendirdi.";
        }

        if (string.IsNullOrWhiteSpace(dialogue))
        {
            if (action == "KILL_PLAYER")
                dialogue = "Bin yıllık sadakatimi sınamak ha? Küstahlığının bedelini kanınla ödeyeceksin!";
            else if (action == "OPEN_DOOR")
                dialogue = "İraden ve saygın hakikidir. Geç yolcu, Kutsal Emanet seni bekliyor.";
            else
                dialogue = "Sözlerin bu kadim kapıda henüz yeterli değil, yolcu. Gerçek niyetini kanıtla.";

            npc_reply = dialogue;
        }
    }
}
