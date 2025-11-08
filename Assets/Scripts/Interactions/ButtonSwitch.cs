using UnityEngine;
using UnityEngine.Events;

public class ButtonSwitch : MonoBehaviour, IInteractable
{
    [SerializeField] string prompt = "[E] Use";
    [SerializeField] bool oneShot = false;
    [SerializeField] UnityEvent onPressed;

    bool used;

    public string GetPrompt() => used && oneShot ? "" : prompt;
    public bool CanInteract(GameObject interactor) => !used || !oneShot;

    public void Interact(GameObject interactor)
    {
        if (used && oneShot) return;
        onPressed?.Invoke();
        if (oneShot) used = true;
    }
}


