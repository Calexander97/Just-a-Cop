using System.Collections.Generic;
using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    readonly HashSet<string> keys = new();
    public bool HasKey(KeyItemSO key) => key && keys.Contains(key.keyId);

    public void AddKey(KeyItemSO key)
    {
        if (!key) return;
        if (keys.Add(key.keyId))
            CenterBanner.Message($"{key.displayName.ToUpper()} ACQUIRED");
    }
    public bool ConsumeKey(KeyItemSO key) => key && keys.Remove(key.keyId);
}
