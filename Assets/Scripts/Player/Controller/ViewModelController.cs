using System.Collections;
using UnityEngine;

public class ViewModelController : MonoBehaviour
{
    [SerializeField] private Transform parent; // usually the FPS camera transform
    [SerializeField] private Vector3 loweredPos = new Vector3(0.2f, -0.35f, 0.5f);
    [SerializeField] private Vector3 raisedPos = new Vector3(0.2f, -0.2f, 0.5f);
    [SerializeField] private float swapSpeed = 14f;

    private GameObject current;
    private WeaponView currentView;

    public Transform CurrentMuzzle => currentView != null ? currentView.muzzle : null;

    public void Equip(WeaponData data)
    {
        if (current != null) Destroy(current);

        currentView = null;

        if (data == null || data.viewModelPrefab == null) return;

        current = Instantiate(data.viewModelPrefab, parent);
        current.transform.localPosition = loweredPos;
        current.transform.localRotation = Quaternion.identity;

        currentView = current.GetComponentInChildren<WeaponView>();

        StopAllCoroutines();
        StartCoroutine(RaiseRoutine());
    }

    private IEnumerator RaiseRoutine()
    {
        if (current == null) yield break;

        float t = 0f;
        while (t < 1f && current != null)
        {
            t += Time.deltaTime * swapSpeed;
            current.transform.localPosition = Vector3.Lerp(loweredPos, raisedPos, t);
            yield return null;
        }
    }

    public void LowerInstant()
    {
        if (current != null) current.transform.localPosition = loweredPos;
    }
}
