using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.VisualScripting;
using UnityEngine;
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

    private void HandleWeaponSwitch()
    {
        // Simple number keys: 1,2,3
        if (Input.GetKeyDown(KeyCode.Alpha1)) TrySwitchWeapon(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) TrySwitchWeapon(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) TrySwitchWeapon(2);
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
}
