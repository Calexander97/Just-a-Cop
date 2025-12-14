using UnityEngine;
using UnityEngine.UI;

public class HitmarkerUI : MonoBehaviour
{
    [SerializeField] private CanvasGroup group;
    [SerializeField] private float showTime = 0.08f;

    private float t;

    private void Awake()
    {
        if (group != null) group.alpha = 0f;
    }

    private void Update()
    {
        if (group == null) return;

        if (group.alpha > 0f)
        {
            t -= Time.deltaTime;
            if (t <= 0f) group.alpha = 0f;
        }
    }

    public void Show()
    {
        if (group == null) return;
        group.alpha = 1f;
        t = showTime;
    }
}
