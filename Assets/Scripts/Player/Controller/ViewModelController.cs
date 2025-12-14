using System.Collections;
using UnityEngine;

public class ViewModelController : MonoBehaviour
{
    [Header("Parent")]
    [SerializeField] private Transform parent; // ViewModelSocket under camera

    [Header("Positions")]
    [SerializeField] private Vector3 loweredPos = new Vector3(0.25f, -0.35f, 0.55f);
    [SerializeField] private Vector3 raisedPos = new Vector3(0.25f, -0.20f, 0.55f);

    [Header("Swap/Reload Motion")]
    [SerializeField] private float moveSpeed = 14f;

    [Header("Recoil")]
    [SerializeField] private Vector3 recoilEuler = new Vector3(-6f, 0f, 0f); // pitch up (negative X)
    [SerializeField] private float recoilKickSpeed = 35f;
    [SerializeField] private float recoilReturnSpeed = 22f;

    private GameObject current;
    private WeaponView currentView;

    private Vector3 targetPos;
    private Quaternion baseRot;      // weapon's baseline rotation (after equip)
    private Quaternion recoilRot;    // baseRot * recoil
    private Quaternion currentRot;

    private Coroutine recoilCo;

    public Transform CurrentMuzzle => currentView != null ? currentView.muzzle : null;

    private void Awake()
    {
        if (parent == null) parent = transform;
        targetPos = raisedPos;
    }

    public void Equip(WeaponData data)
    {
        if (current != null) Destroy(current);
        currentView = null;

        if (data == null || data.viewModelPrefab == null) return;

        current = Instantiate(data.viewModelPrefab, parent);
        current.transform.localPosition = loweredPos;

        // Keep whatever rotation you baked into prefab pivot
        current.transform.localRotation = Quaternion.identity;

        currentView = current.GetComponentInChildren<WeaponView>();

        // Baseline rotation = current local rotation (usually identity)
        baseRot = current.transform.localRotation;
        recoilRot = baseRot * Quaternion.Euler(recoilEuler);
        currentRot = baseRot;

        // Raise on equip
        LowerInstant();
        Raise();
    }

    public void Raise()
    {
        targetPos = raisedPos;
        StopAllCoroutines();
        StartCoroutine(MoveToTarget());
    }

    public void Lower()
    {
        targetPos = loweredPos;
        StopAllCoroutines();
        StartCoroutine(MoveToTarget());
    }

    public void LowerInstant()
    {
        if (current == null) return;
        current.transform.localPosition = loweredPos;
    }

    private IEnumerator MoveToTarget()
    {
        if (current == null) yield break;

        while (current != null && Vector3.Distance(current.transform.localPosition, targetPos) > 0.001f)
        {
            current.transform.localPosition = Vector3.Lerp(current.transform.localPosition, targetPos, Time.deltaTime * moveSpeed);
            yield return null;
        }

        if (current != null)
            current.transform.localPosition = targetPos;
    }

    public void PlayRecoil()
    {
        if (current == null) return;

        if (recoilCo != null) StopCoroutine(recoilCo);
        recoilCo = StartCoroutine(RecoilRoutine());
    }

    private IEnumerator RecoilRoutine()
    {
        // Kick
        float t = 0f;
        while (t < 1f && current != null)
        {
            t += Time.deltaTime * recoilKickSpeed;
            currentRot = Quaternion.Slerp(currentRot, recoilRot, t);
            current.transform.localRotation = currentRot;
            yield return null;
        }

        // Return
        t = 0f;
        while (t < 1f && current != null)
        {
            t += Time.deltaTime * recoilReturnSpeed;
            currentRot = Quaternion.Slerp(currentRot, baseRot, t);
            current.transform.localRotation = currentRot;
            yield return null;
        }

        recoilCo = null;
    }
}
