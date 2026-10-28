using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class StoneGate : MonoBehaviour
{
    [SerializeField] private RuneAltar runeAltar;

    private BoxCollider2D boxCollider;

    private void Awake()
    {
        boxCollider = GetComponent<BoxCollider2D>();

        if (runeAltar == null)
        {
            runeAltar = FindFirstObjectByType<RuneAltar>();
        }
    }

    private void OnEnable()
    {
        if (runeAltar != null)
        {
            runeAltar.OnAltarActivated += OpenGate;
        }
    }

    private void OnDisable()
    {
        if (runeAltar != null)
        {
            runeAltar.OnAltarActivated -= OpenGate;
        }
    }

    private void OpenGate()
    {
        boxCollider.enabled = false;
        Debug.Log("Taş kapı açıldı!");
    }
}

