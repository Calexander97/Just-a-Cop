using UnityEngine;

[CreateAssetMenu(menuName = "JustACop/Key Item")]
public class KeyItemSO : ScriptableObject
{
    public string keyId = "BLUE";
    public string displayName = "Blue Key";
    public Sprite icon;
    public Color color = Color.blue;
}