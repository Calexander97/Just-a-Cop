using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Assertions.Must;
using UnityEngine.UI;
public class PlayerWeaponManager : MonoBehaviour
{
    [Header("Setup")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform firePoint;   // Child of camera
    [SerializeField] private LayerMask hitMask = ~0;

    [Header("Starting Weapons")]
    [SerializeField] private List<WeaponData> startingWeapons;

    [Header("HUD (optional now)")]
    [SerializeField] private Text ammoText;

    private int currentWeaponIndex = 0;
    private List<WeaponState> weaponStates = new List<WeaponState>();

    private bool isReloading = false;
    private float nextFireTime = 0f;

    [System.Serializable]
    private class WeaponState
    {
        public WeaponData data;
        public int currentMag;
        public int currentReserve;
    }

    private void Awake()
    {
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        // Init starting weapons
        foreach (var w in startingWeapons)
        {
            if (w == null) continue;
            WeaponState state = new WeaponState()
            {
                data = w,
                currentMag = w.magazineSize,
                currentReserve = w.maxReserveAmmo
            };
            weaponStates.Add(state);
        }

        currentWeaponIndex = Mathf.Clamp(currentWeaponIndex, 0, weaponStates.Count - 1);
        UpdateHUD();
    }

    private void Update()
    {
        if (weaponStates.Count == 0) return;

        HandleWeaponSwitchInput();
        HandleFireInput();
        HandleReloadInput();
    }
    #region Input
    private void HandleWeaponSwitchInput()
    {
        // Number keys
        if (Input.GetKeyDown(KeyCode.Alpha1)) TrySwitchWeapons(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) TrySwitchWeapons(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) TrySwitchWeapons(2);
    }
    

    private void HandleWeaponSwitch()
    {
        // Simple number keys: 1,2,3
        if (Input.GetKeyDown(KeyCode.Alpha1)) TrySwitchWeapons(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) TrySwitchWeapons(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) TrySwitchWeapons(2);
    }

    private void HandleFireInput()
    {
        if (isReloading) return;

        WeaponState current = weaponStates[currentWeaponIndex];
        WeaponData data = current.data;

        bool wantsToFire = data.isAutomatic ? Input.GetButton("Fire1") : Input.GetButtonDown("Fire1");
        if (!wantsToFire) return;

        if (Time.time < nextFireTime) return;

        if (current.currentMag <= 0)
        {
            // Optional play "empty" click sound
            return;
        }

        // Fire
        FireCurrentWeapon();
    }

    private void HandleReloadInput()
    {
        if (isReloading) return;

        if (Input.GetKeyDown(KeyCode.R))
        {
            TryReload();
        }
    }

    #endregion

    #region Weapons

    private void TrySwitchWeapons(int index)
    {
        if (index < 0 || index >= weaponStates.Count) return;
        if (index == currentWeaponIndex) return;

        currentWeaponIndex = index;
        // option: play switch sound/animation here
        UpdateHUD();
    }

    public void PickupWeapon(WeaponData newWeapon)
    {
        if (newWeapon == null) return;

        //1. If we already have a weapon of this type, just give reserve ammo
        for (int i = 0; i < weaponStates.Count; i++)
        {
            var ws = weaponStates[i];
            if (ws.data.weaponType == newWeapon.weaponType)
            {
                // Top-up reserve ammo, clamped to maxReserveAmmo
                ws.currentReserve = Mathf.Min(ws.currentReserve + newWeapon.maxReserveAmmo,
                                              newWeapon.maxReserveAmmo);

                // Optionally auto-switch to it when picking up ammo
                currentWeaponIndex = i;

                UpdateHUD();
                return;
            }
        }

        // 2. Otherwis, add as a NEW weapon slot
        WeaponState newState = new WeaponState()
        {
            data = newWeapon,
            currentMag = newWeapon.magazineSize,
            currentReserve = newWeapon.maxReserveAmmo
        };

        weaponStates.Add(newState);

        // Auto-equip the newly picked-up weapon
        currentWeaponIndex = weaponStates.Count - 1;

        UpdateHUD();

        // Debug (optional)
        Debug.Log($"Picked up new weapon: {newWeapon.weaponName} (index {currentWeaponIndex})");
    }

    private void FireCurrentWeapon()
    {
        WeaponState current = weaponStates[currentWeaponIndex];
        WeaponData data = current.data;

        current.currentMag--;
        nextFireTime = Time.time + 1f / data.fireRate;

        // Decide spread
        float spread = data.hipfireSpread; // Later change if ADS

        if (data.weaponType == WeaponType.Shotgun && data.pellets > 1)
        {
            // Fire multiple pellets
            for (int i = 0; i < data.pellets; i++)
            {
                FireRay(data.damage, spread);
            }
        }
        else
        {
            FireRay(data.damage, spread);
        }

        // Option: Play muzzle flash, animation, sound here

        UpdateHUD();
    }

    private void FireRay(float damage, float spreadDegrees)
    {
        if (playerCamera == null) return;

        Vector3 direction = playerCamera.transform.forward;

        if (spreadDegrees > 0f)
        {
            direction = ApplySpread(direction, spreadDegrees);
        }

        Ray ray = new Ray(firePoint != null ? firePoint.position : playerCamera.transform.position, direction);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 1000f, hitMask, QueryTriggerInteraction.Ignore))
        {
            // Debug draw
            Debug.DrawLine(ray.origin, hit.point, Color.red, 0.2f);

            IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(damage, hit.point, hit.normal);
            }

            // Option: Spawn impact effect here
        }
    }

    private Vector3 ApplySpread(Vector3 direction, float spreadDegrees)
    {
        // Random rotation with cone
        float spreadRad = spreadDegrees * Mathf.Deg2Rad;
        float randYaw = Random.Range(-spreadRad, spreadRad);
        float randPitch = Random.Range(-spreadRad, spreadRad);

        Quaternion yaw = Quaternion.AngleAxis(randYaw * Mathf.Rad2Deg, Vector3.up);
        Quaternion pitch = Quaternion.AngleAxis(randPitch * Mathf.Rad2Deg, Vector3.right);
        Quaternion rot = yaw * pitch;

        return rot * direction;
    }

    private void TryReload()
    {
        WeaponState current = weaponStates[currentWeaponIndex];
        WeaponData data = current.data;

        if (current.currentMag >= data.magazineSize) return;
        if (current.currentReserve <= 0) return;

        // Start reload coroutine
        StartCoroutine(ReloadRoutine());
    }

    private System.Collections.IEnumerator ReloadRoutine()
    {
        isReloading = true;

        WeaponState current = weaponStates[currentWeaponIndex];
        WeaponData data = current.data;

        // Option: play reload animation/sound here

        yield return new WaitForSeconds(data.reloadTime);

        int needed = data.magazineSize - current.currentMag;
        int toLoad = Mathf.Min(needed, current.currentReserve);
        current.currentMag += toLoad;
        current.currentReserve -= toLoad;

        isReloading = false;
        UpdateHUD();
    }

    private void UpdateHUD()
    {
        if (ammoText == null || weaponStates.Count == 0) return;

        WeaponState current = weaponStates[currentWeaponIndex];
        ammoText.text = $"{current.currentMag} / {current.currentReserve}";
    }

    #endregion
}
