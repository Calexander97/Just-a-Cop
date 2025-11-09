using UnityEngine;

public class KeyLock : MonoBehaviour
{
    [SerializeField] string prompt = "[E] Unlock";
    [SerializeField] KeyItemSO requiredKey;
    [SerializeField] DoorController door;
    [SerializeField] bool consumeKey = true;

    bool unlocked;

    public string GetPrompt() => unlocked ? "" : prompt;
    public bool CanInteract(GameObject interactor) => !unlocked && door != null;

    public void Interact(GameObject interactor)
    {
        if (unlocked) return;
        var inv = interactor.GetComponentInParent<PlayerInventory>();
        if (inv && inv.HasKey(requiredKey))
        {
            if (consumeKey) inv.ConsumeKey(requiredKey);
            unlocked = true;
            door.SetLocked(false);
            door.Open();
        }
        else CenterBanner.Message($"YOU NEED THE {requiredKey.displayName.ToUpper()}");
    }
}
