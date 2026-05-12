using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LogicOverlayController : MonoBehaviour
{
    private const string ViewWiresLabel = "View Wires";
    private const string HideWiresLabel = "Hide Wires";

    [Header("References")]
    [SerializeField] private PlacementController placementController;
    [SerializeField] private WireEditController wireEditController;
    [SerializeField] private TileGrid gridUI;
    [SerializeField] private RunModeController runModeController;

    [Header("UI")]
    [SerializeField] private Button toggleLogicModeButton;
    [SerializeField] private TextMeshProUGUI toggleLogicModeLabel;

    public bool IsLogicMode { get; private set; }

    private void Awake()
    {
        ValidateReferences();

        if (toggleLogicModeButton != null)
        {
            toggleLogicModeButton.onClick.RemoveAllListeners();
            toggleLogicModeButton.onClick.AddListener(ToggleLogicMode);
        }

        ApplyCurrentState();
    }

    private void Start()
    {
        ApplyCurrentState();
    }

    private void OnDestroy()
    {
        if (toggleLogicModeButton != null)
            toggleLogicModeButton.onClick.RemoveListener(ToggleLogicMode);
    }

    private void ValidateReferences()
    {
        if (placementController == null)
            Debug.LogError("LogicOverlayController: placementController is not assigned", this);

        if (wireEditController == null)
            Debug.LogError("LogicOverlayController: wireEditController is not assigned", this);

        if (gridUI == null)
            Debug.LogError("LogicOverlayController: gridUI is not assigned", this);

        if (runModeController == null)
            Debug.LogWarning("LogicOverlayController: runModeController is not assigned", this);

        if (toggleLogicModeButton == null)
            Debug.LogWarning("LogicOverlayController: toggleLogicModeButton is not assigned", this);

        if (toggleLogicModeLabel == null)
            Debug.LogWarning("LogicOverlayController: toggleLogicModeLabel is not assigned", this);
    }

    public void ToggleLogicMode()
    {
        SetLogicMode(!IsLogicMode);
    }

    public void SetLogicMode(bool enabled)
    {
        IsLogicMode = enabled;
        ApplyCurrentState();
    }

    public void RefreshControlledSystems()
    {
        ApplyCurrentState();
    }

    private void ApplyCurrentState()
    {
        bool isRunMode = runModeController != null && runModeController.IsRunMode;

        bool placementAllowed = !isRunMode;
        bool wireEditingAllowed = !isRunMode && IsLogicMode;

        if (placementController != null)
        {
            placementController.SetLogicMode(IsLogicMode);
            placementController.enabled = placementAllowed;

            if (!placementAllowed)
                placementController.SetGhostVisiblePublic(false);
            else
                placementController.SetGhostVisiblePublic(!IsLogicMode);
        }

        if (wireEditController != null)
        {
            wireEditController.enabled = true;
            wireEditController.SetLogicMode(wireEditingAllowed);
        }

        if (gridUI != null)
            gridUI.SetWireOverlayVisible(IsLogicMode);

        if (toggleLogicModeLabel != null)
        {
            toggleLogicModeLabel.text = IsLogicMode ? HideWiresLabel : ViewWiresLabel;
            toggleLogicModeLabel.color = UIColourUtility.TextPrimary;
        }

        if (toggleLogicModeButton != null)
        {
            UIColourUtility.ApplySelectableColours(toggleLogicModeButton, IsLogicMode ? UIColourUtility.ActionTeal : UIColourUtility.BuildOrange);
        }
    }
}