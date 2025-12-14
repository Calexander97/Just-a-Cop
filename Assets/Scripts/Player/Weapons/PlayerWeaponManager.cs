using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Assertions.Must;
using UnityEngine.UI;
using TMPro;
public class PlayerWeaponManager : MonoBehaviour
{
    [Header("Setup")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform firePoint;   // Child of camera
    [SerializeField] private LayerMask hitMask = ~0;

    [Header("Starting Weapons")]
    [SerializeField] private List<WeaponData> startingWeapons;

    [Header("HUD (optional now)")]
    [SerializeField] private TMP_Text ammoText;
    [SerializeField] private TMP_Text weaponNameText; // optional

    [Header("Viewmodel")]
    [SerializeField] private ViewModelController viewModelController;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;

    [Header("Hitmarker")]
    [SerializeField] private HitmarkerUI hitmarkerUI; // optional

    private int currentWeaponIndex = 0;
    private List<WeaponState> weaponStates = new List<WeaponState>();

    private bool isReloading = false;
    private float nextFireTime = 0f;

    private WeaponRuntime runtime;

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

        runtime = GetComponent<WeaponRuntime>();
        if (runtime == null) runtime = gameObject.AddComponent<WeaponRuntime>();
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
            if (audioSource != null && data.emptySFX != null)
                audioSource.PlayOneShot(data.emptySFX);

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
        EquipCurrentWeaponView();
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
        EquipCurrentWeaponView();

        UpdateHUD();

        // Debug (optional)
        Debug.Log($"Picked up new weapon: {newWeapon.weaponName} (index {currentWeaponIndex})");
    }

    private void FireCurrentWeapon()
    {
        WeaponState current = weaponStates[currentWeaponIndex];
        WeaponData data = current.data;

        // Synch runtime with current weapon state
        runtime.Init(data, current.currentMag, current.currentReserve);

        // Fire using shared logic
        bool fired = runtime.TryFire(
            firePoint != null ? firePoint : playerCamera.transform,
            playerCamera.transform.forward,
            hitMask
        );

       // Pull updated ammo back out
       current.currentMag = runtime.CurrentMag;
       current.currentReserve = runtime.CurrentReserve;

        // Muzzle flash (spawn at muzzle)
        if (data.muzzleFlashPrefab != null && firePoint != null)
        {
            Instantiate(data.muzzleFlashPrefab, firePoint.position, firePoint.rotation, firePoint);
        }

        // Shoot SFX
        if (audioSource != null && data.shootSFX != null)
        {
            audioSource.PlayOneShot(data.shootSFX);
        }


        if (fired)
            UpdateHUD();

        if (fired && viewModelController != null)
        {
            viewModelController.PlayRecoil();
        }
    }

    //private void FireRay(float damage, float spreadDegrees)
    //{
    //    if (playerCamera == null) return;

    //    Vector3 direction = playerCamera.transform.forward;

    //    if (spreadDegrees > 0f)
    //    {
    //        direction = ApplySpread(direction, spreadDegrees);
    //    }

    //    Ray ray = new Ray(firePoint != null ? firePoint.position : playerCamera.transform.position, direction);
    //    RaycastHit hit;

    //    if (Physics.Raycast(ray, out hit, 1000f, hitMask, QueryTriggerInteraction.Ignore))
    //    {
    //        // Debug draw
    //        Debug.DrawLine(ray.origin, hit.point, Color.red, 0.2f);

    //        WeaponState current = weaponStates[currentWeaponIndex];
    //        WeaponData data = current.data;

    //        // Impact VFX
    //        if (data.impactPrefab != null)
    //        {
    //            Instantiate(data.impactPrefab, hit.point, Quaternion.LookRotation(hit.normal));
    //        }

    //        IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
    //        if (damageable != null)
    //        {
    //            damageable.TakeDamage(damage, hit.point, hit.normal);

    //            // Hitmarker feedback
    //            if (hitmarkerUI != null) hitmarkerUI.Show();
    //        }
    //    }
    //}

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

        runtime.Init(current.data, current.currentMag, current.currentReserve);
        runtime.TryReload(this);

        // Lower weapon immediately when reload starts
        if (viewModelController != null)
            viewModelController.Lower();

        // Reload SFX
        if (audioSource != null && data.reloadSFX != null)
            audioSource.PlayOneShot(data.reloadSFX);

        StartCoroutine(SyncAmmoAfterReload(current));
    }



    private System.Collections.IEnumerator SyncAmmoAfterReload(WeaponState ws)
    {
        while (runtime.IsReloading)
            yield return null;

        ws.currentMag = runtime.CurrentMag;
        ws.currentReserve = runtime.CurrentReserve;

        // Raise weapon back up after reload completes
        if (viewModelController != null)
            viewModelController.Raise();

        UpdateHUD();
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
        if (weaponStates.Count == 0) return;

        WeaponState current = weaponStates[currentWeaponIndex];

        if (ammoText != null)
            ammoText.text = $"{current.currentMag} / {current.currentReserve}";

        if (weaponNameText != null && current.data != null)
            weaponNameText.text = current.data.weaponName.ToUpper();
    }


    private void EquipCurrentWeaponView()
    {
        if (viewModelController == null)
        {
            Debug.LogWarning("EquipCurrentWeaponView: viewModelController is NULL (not assigned).");
            return;
        }

        if (weaponStates.Count == 0) return;

        WeaponState current = weaponStates[currentWeaponIndex];
        if (current.data == null)
        {
            Debug.LogWarning("EquipCurrentWeaponView: current WeaponData is NULL.");
            return;
        }

        Debug.Log($"Equipping viewmodel for: {current.data.weaponName}. Prefab: {current.data.viewModelPrefab}");

        viewModelController.Equip(current.data);

        var muzzle = viewModelController.CurrentMuzzle;
        if (muzzle != null)
            firePoint = muzzle;
    }



    #endregion
}
