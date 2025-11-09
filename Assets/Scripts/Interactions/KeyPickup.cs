using UnityEngine;

[RequireComponent(typeof(Collider))]
public class KeyPickup : MonoBehaviour
{
    [SerializeField] KeyItemSO key;
    [SerializeField] bool destroyOnPickup = true;

    void OnTriggerEnter(Collider other)
    {
        var inv = other.GetComponentInParent<PlayerInventory>();
        if (!inv) return;
        inv.AddKey(key);
        if (destroyOnPickup) Destroy(gameObject);
    }
}
