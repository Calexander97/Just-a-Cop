using UnityEngine;

[RequireComponent(typeof(Collider))]
public class KeyPickup : MonoBehaviour
{
    [SerializeField] KeyItemSO key;
    [SerializeField] bool destroyOnPickup = true;

    // NEW: sound
    [Header("SFX")]
    [SerializeField] AudioClip pickupSfx;     // short chime/bling
    [SerializeField] float volume = 1f;       // 0..1

    void OnTriggerEnter(Collider other)
    {
        var inv = other.GetComponentInParent<PlayerInventory>();
        if (!inv) return;

        inv.AddKey(key);

        // play 3D one-shot at the key's position
        if (pickupSfx) AudioSource.PlayClipAtPoint(pickupSfx, transform.position, volume);

        if (destroyOnPickup) Destroy(gameObject);
    }
}
