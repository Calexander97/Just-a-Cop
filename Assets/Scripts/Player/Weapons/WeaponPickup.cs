using UnityEngine;

public class WeaponPickup : MonoBehaviour, IInteractable
{
    [SerializeField] private WeaponData weaponData;
    [SerializeField] private string prompt = "[E] Pick Up";

    // This is what InteractPromptUI will show
    public string GetPrompt()
    {
        // If no weapon data assigned, don't show anything
        if (weaponData == null) return "";

        // You can either use a fixed prompt or include the weapon name
        return string.IsNullOrWhiteSpace(prompt)
            ? $"[E] PICK UP {weaponData.weaponName.ToUpper()}"
            : prompt;
    }

    // Keep this simple like ButtonSwitch: if we have data, we can interact
    public bool CanInteract(GameObject interactor)
    {
        return weaponData != null;
    }

    public void Interact(GameObject interactor)
    {
        if (weaponData == null) return;

        // Get the weapon manager from the player or its parents
        var weaponManager = interactor.GetComponentInParent<PlayerWeaponManager>();
        if (weaponManager == null)
        {
            Debug.LogWarning("WeaponPickup: No PlayerWeaponManager found on interactor/parents.");
            return;
        }

        weaponManager.PickupWeapon(weaponData);

        // Feedback just like keys
        CenterBanner.Message($"{weaponData.weaponName.ToUpper()} ACQUIRED");

        Destroy(gameObject);
    }
}
