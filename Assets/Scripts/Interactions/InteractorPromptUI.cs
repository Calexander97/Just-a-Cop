using UnityEngine;
using TMPro;

public class InteractPromptUI : MonoBehaviour
{
    public static InteractPromptUI Instance;
    [SerializeField] private CanvasGroup group;
    [SerializeField] private TMP_Text text;

    void Awake() { Instance = this; Hide(); }

    public static void Show(string msg)
    {
        if (Instance == null) return;
        Instance.text.text = msg;
        Instance.group.alpha = 1f;
        Instance.group.blocksRaycasts = false;
    }
    public static void Hide()
    {
        if (Instance == null) return;
        Instance.group.alpha = 0f;
        Instance.group.blocksRaycasts = false;
    }
}

public static class CenterBanner
{
    static CenterBannerUI _ui;
    public static void Bind(CenterBannerUI ui) => _ui = ui;
    public static void Message(string msg) { if (_ui) _ui.Show(msg); }
}
