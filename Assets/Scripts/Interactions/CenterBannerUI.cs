using UnityEngine;
using TMPro;

public class CenterBannerUI : MonoBehaviour
{
    [SerializeField] CanvasGroup group;
    [SerializeField] TMP_Text text;
    [SerializeField] float showSeconds = 1.5f;
    float t;

    void Start() { CenterBanner.Bind(this); Hide(); }
    void Update() { if (group.alpha > 0f) { t -= Time.unscaledDeltaTime; if (t <= 0f) Hide(); } }
    public void Show(string msg) { text.text = msg; group.alpha = 1f; t = showSeconds; }
    void Hide() { group.alpha = 0f; }
}
