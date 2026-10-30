using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class RuneAltar : MonoBehaviour, IInteractable
{
    public event Action OnAltarActivated;
    public event Action OnAltarDeactivated;

    [Header("Rün Görselleri (İsteğe Bağlı)")]
    [SerializeField] private List<SpriteRenderer> runes = new List<SpriteRenderer>();
    [SerializeField] private float glowSpeed = 3f;

    [Header("Etkileşim Bilgilendirmesi")]
    [SerializeField] private string emptyAltarHint = "Kadim mühür, üzerine bir yük konulmadan açılmaz. Yük kaldırılırsa mühür yeniden kapanır.";

    private readonly HashSet<Collider2D> objectsOnAltar = new HashSet<Collider2D>();
    private Color currentColor = new Color(1f, 1f, 1f, 0f);
    private float targetAlpha = 0f;

    public bool IsActivated { get; private set; }

    private void Awake()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.isTrigger = true;
        }

        if (runes.Count > 0 && runes[0] != null)
        {
            currentColor = runes[0].color;
            currentColor.a = 0f;
            ApplyRuneColor(currentColor);
        }
    }

    private void Update()
    {
        if (runes.Count == 0)
            return;

        currentColor.a = Mathf.Lerp(currentColor.a, targetAlpha, glowSpeed * Time.deltaTime);
        ApplyRuneColor(currentColor);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (other.isTrigger)
            return;

        objectsOnAltar.Add(other);

        if (!IsActivated)
        {
            ActivateAltar();
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (objectsOnAltar.Remove(other) && objectsOnAltar.Count == 0 && IsActivated)
        {
            DeactivateAltar();
        }
    }

    private void ActivateAltar()
    {
        IsActivated = true;
        targetAlpha = 1f;
        Debug.Log("Kadim Mühür aktifleşti! Kapı açılıyor.");
        OnAltarActivated?.Invoke();
    }

    private void DeactivateAltar()
    {
        IsActivated = false;
        targetAlpha = 0f;
        Debug.Log("Kadim Mühür üzerindeki yük kaldırıldı! Kapı yeniden kapanıyor.");
        OnAltarDeactivated?.Invoke();
    }

    private void ApplyRuneColor(Color color)
    {
        foreach (SpriteRenderer rune in runes)
        {
            if (rune != null)
            {
                rune.color = color;
            }
        }
    }

    public void Interact()
    {
        if (!IsActivated && DialogueManager.Instance != null)
        {
            DialogueManager.Instance.ShowDialogue(emptyAltarHint);
        }
    }
}
