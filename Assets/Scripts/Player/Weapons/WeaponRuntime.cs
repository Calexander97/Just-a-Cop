using UnityEngine;

public class WeaponRuntime : MonoBehaviour
{
    public WeaponData Data { get; private set; }

    public int CurrentMag { get; private set; }
    public int CurrentReserve { get; private set; }
    public bool IsReloading { get; private set; }

    private float nextFireTime;

    public void Init(WeaponData data, int mag, int reserve)
    {
        Data = data;
        CurrentMag = mag;
        CurrentReserve = reserve;
        IsReloading = false;
        nextFireTime = 0f;
    }

    public bool CanFire(float timeNow) =>
        Data != null && !IsReloading && CurrentMag > 0 && timeNow >= nextFireTime;

    public bool TryFire(Transform origin, Vector3 forward, LayerMask hitMask)
    {
        if (Data == null) return false;
        if (!CanFire(Time.time)) return false;

        CurrentMag--;
        nextFireTime = Time.time + 1f / Data.fireRate;

        float spread = Data.hipfireSpread;

        if (Data.weaponType == WeaponType.Shotgun && Data.pellets > 1)
        {
            for (int i = 0; i < Data.pellets; i++)
                FireRay(origin, forward, hitMask, Data.damage, spread);
        }
        else
        {
            FireRay(origin, forward, hitMask, Data.damage, spread);
        }

        return true;
    }

    public void TryReload(MonoBehaviour runner)
    {
        if (Data == null) return;
        if (IsReloading) return;
        if (CurrentMag >= Data.magazineSize) return;
        if (CurrentReserve <= 0) return;

        runner.StartCoroutine(ReloadRoutine());
    }

    private System.Collections.IEnumerator ReloadRoutine()
    {
        IsReloading = true;

        yield return new WaitForSeconds(Data.reloadTime);

        int needed = Data.magazineSize - CurrentMag;
        int toLoad = Mathf.Min(needed, CurrentReserve);

        CurrentMag += toLoad;
        CurrentReserve -= toLoad;

        IsReloading = false;
    }

    private void FireRay(Transform origin, Vector3 forward, LayerMask hitMask, float damage, float spreadDegrees)
    {
        Vector3 dir = forward;

        if (spreadDegrees > 0f)
            dir = ApplySpread(dir, spreadDegrees);

        Ray ray = new Ray(origin.position, dir);

        if (Physics.Raycast(ray, out RaycastHit hit, Data.range, hitMask, QueryTriggerInteraction.Ignore))
        {
            Debug.DrawLine(ray.origin, hit.point, Color.red, 0.15f);

            IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(damage, hit.point, hit.normal);
            }
        }
    }

    private Vector3 ApplySpread(Vector3 direction, float spreadDegrees)
    {
        float spreadRad = spreadDegrees * Mathf.Deg2Rad;
        float randYaw = Random.Range(-spreadRad, spreadRad);
        float randPitch = Random.Range(-spreadRad, spreadRad);

        Quaternion yaw = Quaternion.AngleAxis(randYaw * Mathf.Rad2Deg, Vector3.up);
        Quaternion pitch = Quaternion.AngleAxis(randPitch * Mathf.Rad2Deg, Vector3.right);

        return (yaw * pitch) * direction;
    }
}
