using UnityEngine;
using UnityEngine.UI;

public class ShopManualsPanelController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LevelContext levelContext;
    [SerializeField] private ShopManualsUI panelUI;
    [SerializeField] private MessagePopupUI manualPopup;

    [Header("Tab Buttons")]
    [SerializeField] private Button shopButton;
    [SerializeField] private Button manualsButton;

    private LevelData CurrentLevelData => levelContext != null ? levelContext.LevelData : null;
    private string CurrentLevelId => levelContext != null ? levelContext.LevelId : null;

    private void Awake()
    {
        ValidateReferences();

        if (panelUI != null)
        {
            panelUI.ManualClicked += HandleManualClicked;
            panelUI.IsManualUnlocked = IsManualUnlockedForCurrentLevel;
        }

        if (shopButton != null)
        {
            shopButton.onClick.RemoveAllListeners();
            shopButton.onClick.AddListener(ShowShop);
        }

        if (manualsButton != null)
        {
            manualsButton.onClick.RemoveAllListeners();
            manualsButton.onClick.AddListener(ShowManuals);
        }
    }

    private void Start()
    {
        ShowShop();
    }

    private void OnDestroy()
    {
        if (panelUI != null)
        {
            panelUI.ManualClicked -= HandleManualClicked;

            if (panelUI.IsManualUnlocked == IsManualUnlockedForCurrentLevel)
                panelUI.IsManualUnlocked = null;
        }
    }

    public void ShowShop()
    {
        if (panelUI == null)
            return;

        LevelData levelData = CurrentLevelData;
        if (levelData == null)
        {
            Debug.LogError("ShopManualsPanelController: cannot show shop because current LevelData is missing", this);
            return;
        }

        panelUI.ShowShop(levelData);
        RefreshTabButtons();
    }

    public void ShowManuals()
    {
        if (panelUI == null)
            return;

        LevelData levelData = CurrentLevelData;
        if (levelData == null)
        {
            Debug.LogError("ShopManualsPanelController: cannot show manuals because current LevelData is missing", this);
            return;
        }

        panelUI.ShowManuals(levelData);
        RefreshTabButtons();
    }

    private void ValidateReferences()
    {
        if (levelContext == null)
            Debug.LogError("ShopManualsPanelController: levelContext is not assigned", this);
        else if (levelContext.LevelData == null)
            Debug.LogError("ShopManualsPanelController: levelContext has no LevelData", this);

        if (panelUI == null)
            Debug.LogError("ShopManualsPanelController: panelUI is not assigned", this);

        if (manualPopup == null)
            Debug.LogWarning("ShopManualsPanelController: manualPopup is not assigned. Unlocked manuals cannot display messages", this);

        if (shopButton == null)
            Debug.LogWarning("ShopManualsPanelController: shopButton is not assigned", this);

        if (manualsButton == null)
            Debug.LogWarning("ShopManualsPanelController: manualsButton is not assigned", this);
    }

    private void RefreshTabButtons()
    {
        if (panelUI == null)
            return;

        bool shopIsActive = panelUI.CurrentMode == ShopManualsUI.PanelMode.Shop;
        bool manualsIsActive = panelUI.CurrentMode == ShopManualsUI.PanelMode.Manuals;

        ApplyTabButtonState(shopButton, isClickable: !shopIsActive, clickableColours: UIColourUtility.BuildOrange);

        ApplyTabButtonState(manualsButton, isClickable: !manualsIsActive, clickableColours: UIColourUtility.InformationBlue);
    }

    private void ApplyTabButtonState(Button button, bool isClickable, UIButtonColourSet clickableColours)
    {
        if (button == null)
            return;

        button.interactable = isClickable;

        UIColourUtility.ApplySelectableColours(button, isClickable ? clickableColours : UIColourUtility.DisabledGrey);
    }

    private bool IsManualUnlockedForCurrentLevel(ManualData manual)
    {
        string levelId = CurrentLevelId;

        if (manual == null || string.IsNullOrEmpty(manual.manualId))
            return false;

        if (PlayerData.Instance == null || string.IsNullOrEmpty(levelId))
            return false;

        return PlayerData.Instance.IsManualUnlocked(levelId, manual.manualId);
    }

    private void HandleManualClicked(ManualData manual)
    {
        if (manual == null)
            return;

        string levelId = CurrentLevelId;
        if (string.IsNullOrEmpty(levelId))
        {
            Debug.LogError("ShopManualsPanelController: cannot handle manual click because current level ID is missing", this);
            return;
        }

        if (PlayerData.Instance == null)
        {
            Debug.LogError("ShopManualsPanelController: PlayerData.Instance is missing", this);
            return;
        }

        bool unlocked = PlayerData.Instance.IsManualUnlocked(levelId, manual.manualId);

        if (!unlocked)
        {
            if (!PlayerData.Instance.TrySpend(manual.unlockPrice))
                return;

            PlayerData.Instance.UnlockManual(levelId, manual.manualId);
            PlayerData.Instance.SaveData();

            if (panelUI != null)
                panelUI.RefreshVisibleRows();

            RefreshTabButtons();
            return;
        }

        if (manualPopup != null)
            manualPopup.Show(manual.displayName, manual.message, PopupMessageType.Info);
    }
}