using UnityEngine;

public interface IInteractable
{
    string GetPrompt();
    bool CanInteract(GameObject interactor);
    void Interact(GameObject interactor);
}
