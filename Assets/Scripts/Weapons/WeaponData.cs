using UnityEngine;

public enum WeaponType
{
    Handgun,
    Shotgun,
    AssaultRifle,
}

[CreateAssetMenu(menuName = "JustACop/Weapon Data", fileName = "NewWeaponData")]
public class WeaponData : ScriptableObject
{
    [Header("General")]
    public string weaponName = "Handgun";
    public WeaponType weaponType = WeaponType.Handgun;

    [Header("Damage & Firing")]
    public float damage = 20f;
    public bool isAutomatic = false;
    public float fireRate = 4f;         // Shots per second
    public float range = 100f;

    [Header("Ammo")]
    public int magazineSize = 9;
    public int maxRserveAmmo = 90;
    public float reloadTime = 1.2f;

    [Header("Accuracy & Spread")]
    public float hipfireSpread = 1.0f;  // Degrees
    public float adsSpread = 0.3f;
    public int pellets = 1;             // Shotgun uses >1
}
