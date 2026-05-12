using UnityEngine;

[CreateAssetMenu(fileName = "NewItem", menuName = "Factory/Item Data")]
public class ItemData : ScriptableObject
{
    [Header("Identity")]
    public string id;
    public string displayName;

    [Header("Visuals")]
    public Sprite sprite;
}