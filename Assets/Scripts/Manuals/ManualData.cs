using UnityEngine;

[CreateAssetMenu(fileName = "ManualData", menuName = "Factory/Manual Data")]
public class ManualData : ScriptableObject
{
    public string manualId;
    public string displayName;
    [TextArea(4, 12)]
    public string message;
    public int unlockPrice = 0;
}