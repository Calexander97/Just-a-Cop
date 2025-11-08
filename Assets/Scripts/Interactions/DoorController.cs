using System.Collections;
using UnityEngine;

public class DoorController : MonoBehaviour
{
    [Header("Motion")]
    [SerializeField] Transform door;
    [SerializeField] Vector3 openOffset = new(0, 3, 0);
    [SerializeField] float speed = 4f;

    [Header("State")]
    [SerializeField] bool startsOpen = false;
    [SerializeField] bool locked = false;

    Vector3 closedPos, openPos;
    bool isOpen, moving;

    private void Awake()
    {
        closedPos = door.localPosition;
        openPos = closedPos + openOffset;
        if (startsOpen) { door.localPosition = openPos; isOpen = true; }
    }

    public bool IsLocked => locked;
    public void SetLocked(bool v) { locked = v; }
    public void Open() { if (moving||isOpen || locked) return; StartCoroutine(Move(openPos, true)); }
    public void Close() { if (moving||!isOpen) return; StartCoroutine(Move(closedPos, false)); }
    public void Toggle() { if (locked) return; if (isOpen) Close(); else Open(); }

    IEnumerator Move(Vector3 target, bool opening)
    {
        moving = true;
        while ((door.localPosition - target).sqrMagnitude > 0.0001f)
        {
            door.localPosition = Vector3.MoveTowards(door.localPosition, target, speed * Time.deltaTime);
            yield return null;
        }
        door.localPosition = target;
        isOpen = opening;
        moving = false;
    }

}
