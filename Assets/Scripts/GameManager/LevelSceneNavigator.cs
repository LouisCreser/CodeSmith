using UnityEngine;
using UnityEngine.UI;

public class LevelSceneNavigator : MonoBehaviour
{
    [Header("Scene Loading")]
    [SerializeField] private SceneLoader sceneLoader;

    [Header("Level Navigation")]
    [SerializeField] private LevelRegistry levelRegistry;
    [SerializeField] private LevelContext levelContext;
    [SerializeField] private LevelBoardStateSaver boardStateSaver;
    [SerializeField] private string levelSelectionSceneName = "LevelMenu";

    [Header("Level Buttons")]
    [SerializeField] private Button previousLevelButton;
    [SerializeField] private Button nextLevelButton;
    [SerializeField] private Button viewLevelsButton;

    private LevelData CurrentLevelData => levelContext != null ? levelContext.LevelData : null;

    private void Start()
    {
        ValidateReferences();

        if (previousLevelButton != null)
        {
            previousLevelButton.onClick.RemoveAllListeners();
            previousLevelButton.onClick.AddListener(LoadPreviousLevel);
        }

        if (nextLevelButton != null)
        {
            nextLevelButton.onClick.RemoveAllListeners();
            nextLevelButton.onClick.AddListener(LoadNextLevel);
        }

        if (viewLevelsButton != null)
        {
            viewLevelsButton.onClick.RemoveAllListeners();
            viewLevelsButton.onClick.AddListener(LoadLevelSelectionScene);
            ApplyViewLevelsButtonState();
        }

        RefreshLevelNavigationButtons();
    }

    private void OnApplicationQuit()
    {
        SaveBeforeSceneChange();
    }

    private void ValidateReferences()
    {
        if (sceneLoader == null)
            Debug.LogError("LevelSceneNavigator: sceneLoader is not assigned", this);

        if (levelRegistry == null)
            Debug.LogWarning("LevelSceneNavigator: levelRegistry is not assigned", this);

        if (levelContext == null)
            Debug.LogWarning("LevelSceneNavigator: levelContext is not assigned", this);
        else if (levelContext.LevelData == null)
            Debug.LogWarning("LevelSceneNavigator: levelContext has no LevelData", this);

        if (boardStateSaver == null)
            Debug.LogWarning("LevelSceneNavigator: boardStateSaver is not assigned", this);

        if (string.IsNullOrWhiteSpace(levelSelectionSceneName))
            Debug.LogWarning("LevelSceneNavigator: levelSelectionSceneName is empty", this);
    }

    public void LoadScene(string sceneName)
    {
        SaveBeforeSceneChange();
        LoadSceneWithoutSavingAgain(sceneName);
    }

    public void LoadLevelSelectionScene()
    {
        if (string.IsNullOrWhiteSpace(levelSelectionSceneName))
        {
            Debug.LogError("LevelSceneNavigator: levelSelectionSceneName is null or empty", this);
            return;
        }

        SaveBeforeSceneChange();
        LoadSceneWithoutSavingAgain(levelSelectionSceneName);
    }

    public void LoadPreviousLevel()
    {
        LevelData previousLevel = GetPreviousUnlockedLevel();
        if (previousLevel == null)
            return;

        if (string.IsNullOrWhiteSpace(previousLevel.sceneName))
        {
            Debug.LogError($"LevelSceneNavigator: previous level '{previousLevel.levelId}' has no sceneName", this);
            return;
        }

        SaveBeforeSceneChange();
        LoadSceneWithoutSavingAgain(previousLevel.sceneName);
    }

    public void LoadNextLevel()
    {
        LevelData nextLevel = GetNextUnlockedLevel();
        if (nextLevel == null)
            return;

        if (string.IsNullOrWhiteSpace(nextLevel.sceneName))
        {
            Debug.LogError($"LevelSceneNavigator: next level '{nextLevel.levelId}' has no sceneName", this);
            return;
        }

        SaveBeforeSceneChange();
        LoadSceneWithoutSavingAgain(nextLevel.sceneName);
    }

    public void RefreshLevelNavigationButtons()
    {
        bool hasPreviousLevel = GetPreviousUnlockedLevel() != null;
        bool hasNextLevel = GetNextUnlockedLevel() != null;

        ApplyNavigationButtonState(previousLevelButton, hasPreviousLevel);
        ApplyNavigationButtonState(nextLevelButton, hasNextLevel);
        ApplyViewLevelsButtonState();
    }

    public void Quit()
    {
        SaveBeforeSceneChange();

        if (sceneLoader != null)
            sceneLoader.Quit();
        else
            Application.Quit();
    }

    private void ApplyNavigationButtonState(Button button, bool isAccessible)
    {
        if (button == null)
            return;

        button.interactable = isAccessible;

        UIColourUtility.ApplySelectableColours(button, isAccessible ? UIColourUtility.ActionTeal : UIColourUtility.DisabledGrey);
    }

    private void ApplyViewLevelsButtonState()
    {
        if (viewLevelsButton != null)
            UIColourUtility.ApplySelectableColours(viewLevelsButton, UIColourUtility.ActionTeal);
    }

    private void LoadSceneWithoutSavingAgain(string sceneName)
    {
        if (sceneLoader == null)
        {
            Debug.LogError("LevelSceneNavigator: cannot load scene because sceneLoader is not assigned", this);
            return;
        }

        sceneLoader.LoadScene(sceneName);
    }

    private void SaveBeforeSceneChange()
    {
        if (boardStateSaver != null)
            boardStateSaver.SaveCurrentBoardStateAndPlayerData();
        else if (PlayerData.Instance != null)
            PlayerData.Instance.SaveData();
    }

    private LevelData GetPreviousUnlockedLevel()
    {
        LevelData currentLevelData = CurrentLevelData;

        if (levelRegistry == null || currentLevelData == null || levelRegistry.levels == null)
            return null;

        for (int i = 0; i < levelRegistry.levels.Count; i++)
        {
            LevelData level = levelRegistry.levels[i];
            if (level == null)
                continue;

            if (level.levelId != currentLevelData.levelId)
                continue;

            for (int j = i - 1; j >= 0; j--)
            {
                LevelData previous = levelRegistry.levels[j];
                if (previous == null || string.IsNullOrWhiteSpace(previous.sceneName))
                    continue;

                if (PlayerData.Instance != null && !PlayerData.Instance.IsLevelUnlocked(previous.levelId))
                    continue;

                return previous;
            }

            return null;
        }

        return null;
    }

    private LevelData GetNextUnlockedLevel()
    {
        LevelData currentLevelData = CurrentLevelData;

        if (levelRegistry == null || currentLevelData == null || levelRegistry.levels == null)
            return null;

        for (int i = 0; i < levelRegistry.levels.Count; i++)
        {
            LevelData level = levelRegistry.levels[i];
            if (level == null)
                continue;

            if (level.levelId != currentLevelData.levelId)
                continue;

            for (int j = i + 1; j < levelRegistry.levels.Count; j++)
            {
                LevelData next = levelRegistry.levels[j];
                if (next == null || string.IsNullOrWhiteSpace(next.sceneName))
                    continue;

                if (PlayerData.Instance != null && !PlayerData.Instance.IsLevelUnlocked(next.levelId))
                    continue;

                return next;
            }

            return null;
        }

        return null;
    }
}