using UnityEngine;

public sealed class LevelContext : MonoBehaviour
{
    [Header("Level")]
    [SerializeField] private LevelData levelData;

    public LevelData LevelData => levelData;
    public string LevelId => levelData != null ? levelData.levelId : null;

    private void Awake()
    {
        ValidateReferences();
    }

    private void ValidateReferences()
    {
        if (levelData == null)
        {
            Debug.LogError("LevelContext: levelData is not assigned", this);
            return;
        }

        if (string.IsNullOrWhiteSpace(levelData.levelId))
            Debug.LogError("LevelContext: assigned LevelData has no levelId", levelData);

        if (string.IsNullOrWhiteSpace(levelData.sceneName))
            Debug.LogWarning($"LevelContext: level '{levelData.levelId}' has no sceneName", levelData);
    }
}