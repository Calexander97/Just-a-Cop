using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(WeaponRuntime))]
public class EnemyShooterAI : MonoBehaviour
{
    [Header("Weapon")]
    [SerializeField] private WeaponData weaponData;
    [SerializeField] private Transform firePoint;       // muzzle / shoot origin
    [SerializeField] private float aiFireMultiplier = 1f; // 1 = normal, 0.5 = slower


    [Header("Targeting")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private float detectionRange = 25f;
    [SerializeField] private float stoppingDistance = 10f;
    [SerializeField] private float aimHeightOffset = 1.2f;
    [SerializeField] private LayerMask losMask = ~0;

    [Header("Movement")]
    [SerializeField] private bool useNavMesh = true;

    [Header("Hit Mask (what bullets can hit)")]
    [SerializeField] private LayerMask hitMask = ~0;

    private Transform player;
    private WeaponRuntime runtime;
    private NavMeshAgent agent;

    // Enemy ammo (owned by AI, not by runtime)
    private bool isReloading = false;
    private int mag;
    private int reserve;

    private void Awake()
    {
        runtime = GetComponent<WeaponRuntime>();
        agent = GetComponent<NavMeshAgent>();
    }

    private void Start()
    {
        var p = GameObject.FindGameObjectWithTag(playerTag);
        if (p != null) player = p.transform;

        if (!useNavMesh && agent != null) agent.enabled = false;

        // Init ammo from weapon data
        if (weaponData != null)
        {
            mag = weaponData.magazineSize;
            reserve = weaponData.maxReserveAmmo;
        }
    }

    private void Update()
    {
        if (player == null) return;
        if (weaponData == null) return;
        if (firePoint == null) return;

        float dist = Vector3.Distance(transform.position, player.position);

        if (dist > detectionRange)
        {
            StopMove();
            return;
        }

        // Require LOS to engage (prevents wallhacks)
        if (!HasLOS(dist))
        {
            StopMove();
            return;
        }

        // Move toward player until stopping distance
        if (dist > stoppingDistance)
            MoveTo(player.position);
        else
            StopMove();

        // Face player (simple)
        Vector3 lookPos = player.position;
        lookPos.y = transform.position.y;
        transform.LookAt(lookPos);


        // Shoot if LOS
        if (HasLOS(dist))
        {
            // If we are currently reloading, wait until it's done
            if (runtime.IsReloading || isReloading)
                return;

            // Sync runtime with current weapon + ammo state (WITHOUT resetting cooldown)
            runtime.Init(weaponData, mag, reserve);

            Vector3 aimDir = (player.position + Vector3.up * aimHeightOffset - firePoint.position).normalized;

            bool fired = runtime.TryFire(firePoint, aimDir, hitMask);

            // Pull ammo state back out after firing attempt
            mag = runtime.CurrentMag;
            reserve = runtime.CurrentReserve;

            // If empty, start reload ONCE
            if (mag <= 0 && reserve > 0)
            {
                isReloading = true;
                runtime.TryReload(this);
                StartCoroutine(FinishReload());
            }
        }
    }


    private bool HasLOS(float dist)
    {
        Vector3 origin = firePoint.position;
        Vector3 target = player.position + Vector3.up * aimHeightOffset;
        Vector3 dir = (target - origin).normalized;

        if (Physics.Raycast(origin, dir, out var hit, dist, losMask, QueryTriggerInteraction.Ignore))
        {
            return hit.collider.CompareTag(playerTag);
        }

        return false;
    }

    private System.Collections.IEnumerator SyncAmmoAfterReload()
    {
        // Wait until runtime finished reloading
        while (runtime.IsReloading)
            yield return null;

        // Pull updated ammo back
        mag = runtime.CurrentMag;
        reserve = runtime.CurrentReserve;
    }

    private void MoveTo(Vector3 pos)
    {
        if (!useNavMesh || agent == null) return;
        agent.isStopped = false;
        agent.stoppingDistance = stoppingDistance;
        agent.SetDestination(pos);
    }

    private void StopMove()
    {
        if (!useNavMesh || agent == null) return;
        agent.isStopped = true;
    }

    private System.Collections.IEnumerator FinishReload()
    {
        while (runtime.IsReloading)
            yield return null;

        // Pull updated ammo back AFTER reload finishes
        mag = runtime.CurrentMag;
        reserve = runtime.CurrentReserve;

        isReloading = false;
    }

}
