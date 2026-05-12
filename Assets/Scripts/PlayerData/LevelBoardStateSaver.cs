using UnityEngine;

public sealed class LevelBoardStateSaver : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LevelContext levelContext;
    [SerializeField] private TileGrid gridUI;

    private string CurrentLevelId => levelContext != null ? levelContext.LevelId : null;

    private void Awake()
    {
        ValidateReferences();
    }

    private void ValidateReferences()
    {
        if (levelContext == null)
            Debug.LogError("LevelBoardStateSaver: levelContext is not assigned", this);
        else if (levelContext.LevelData == null)
            Debug.LogError("LevelBoardStateSaver: levelContext has no LevelData", this);

        if (gridUI == null)
            Debug.LogError("LevelBoardStateSaver: gridUI is not assigned", this);
    }

    public bool SaveCurrentBoardState()
    {
        string levelId = CurrentLevelId;

        if (PlayerData.Instance == null)
        {
            Debug.LogError("LevelBoardStateSaver: cannot save board because PlayerData.Instance is missing", this);
            return false;
        }

        if (string.IsNullOrEmpty(levelId))
        {
            Debug.LogError("LevelBoardStateSaver: cannot save board because current level ID is missing", this);
            return false;
        }

        if (gridUI == null)
        {
            Debug.LogError("LevelBoardStateSaver: cannot save board because gridUI is missing", this);
            return false;
        }

        PlayerData.Instance.SetPlacedComponentsForLevel(levelId, gridUI.ExportPlacedComponentEntries());
        PlayerData.Instance.SetWireEntriesForLevel(levelId, gridUI.ExportWireEntries());

        return true;
    }

    public bool SaveCurrentBoardStateAndPlayerData()
    {
        bool savedBoard = SaveCurrentBoardState();

        if (PlayerData.Instance == null)
            return false;

        PlayerData.Instance.SaveData();
        return savedBoard;
    }
}