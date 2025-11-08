using UnityEngine;
using TMPro;

public class InteractPromptUI : MonoBehaviour
{
    public static InteractPromptUI Instance;
    [SerializeField] CanvasGroup group;
    [SerializeField] TMP_Text text;

    void Awake() { Instance = this; Hide(); }
    public static void Show(string msg) { if (!Instance) return; Instance.text.text = msg; Instance.group.alpha = 1f; }
    public static void Hide() { if (!Instance) return; Instance.group.alpha = 0f; }
}

public static class CenterBanner
{
    static CenterBannerUI ui;
    public static void Bind(CenterBannerUI b) => ui = b;
    public static void Message(string msg) { if (ui) ui.Show(msg); }
}
