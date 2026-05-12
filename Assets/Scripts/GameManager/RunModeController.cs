using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class RunModeController : MonoBehaviour
{
    [Header("Core")]
    [SerializeField] private TickDriver tickDriver;
    [SerializeField] private MonoBehaviour simulationRunnerBehaviour;
    [SerializeField] private LevelContext levelContext;
    [SerializeField] private LevelRegistry levelRegistry;
    [SerializeField] private TileGrid gridUI;
    [SerializeField] private LevelSceneNavigator levelSceneNavigator;

    private bool simulationLockedByError;

    [Header("Evaluation")]
    [SerializeField] private Button evaluateButton;
    [SerializeField] private TextMeshProUGUI evaluateButtonLabel;
    [SerializeField] private int evaluationTickCount = 60;

    public bool IsEvaluationActive { get; private set; }

    private int evaluationTicksElapsed;

    [Header("Build Mode Systems")]
    [SerializeField] private PlacementController placementController;
    [SerializeField] private ShopManualsUI shopUI;
    [SerializeField] private LogicOverlayController logicOverlayController;

    [Header("UI")]
    [SerializeField] private Button toggleRunModeButton;
    [SerializeField] private TextMeshProUGUI toggleRunModeLabel;

    [SerializeField] private Button playPauseButton;
    [SerializeField] private TextMeshProUGUI playPauseLabel;

    [SerializeField] private Button stepButton;

    [SerializeField] private TMP_Dropdown speedDropdown;
    [SerializeField] private MessagePopupUI messagePopup;

    public bool IsRunMode { get; private set; }

    private ISimulationRunner sim;

    private LevelData CurrentLevelData => levelContext != null ? levelContext.LevelData : null;
    private string CurrentLevelId => levelContext != null ? levelContext.LevelId : null;

    private void Awake()
    {
        ValidateReferences();

        sim = simulationRunnerBehaviour as ISimulationRunner;
        if (sim == null)
            Debug.LogError("RunModeController: simulationRunnerBehaviour must implement ISimulationRunner", this);

        if (toggleRunModeButton != null)
        {
            toggleRunModeButton.onClick.RemoveAllListeners();
            toggleRunModeButton.onClick.AddListener(ToggleRunMode);
        }

        if (playPauseButton != null)
        {
            playPauseButton.onClick.RemoveAllListeners();
            playPauseButton.onClick.AddListener(TogglePlayPause);
        }

        if (stepButton != null)
        {
            stepButton.onClick.RemoveAllListeners();
            stepButton.onClick.AddListener(StepOnce);
        }

        if (evaluateButton != null)
        {
            evaluateButton.onClick.RemoveAllListeners();
            evaluateButton.onClick.AddListener(BeginEvaluation);
        }

        if (speedDropdown != null)
        {
            speedDropdown.onValueChanged.RemoveAllListeners();
            speedDropdown.onValueChanged.AddListener(OnSpeedChanged);
        }

        if (tickDriver != null)
            tickDriver.OnTick += OnTick;
    }

    private void Start()
    {
        RefreshAllUI();

        if (logicOverlayController != null)
            logicOverlayController.RefreshControlledSystems();

        if (levelSceneNavigator != null)
            levelSceneNavigator.RefreshLevelNavigationButtons();
    }

    private void OnDestroy()
    {
        if (tickDriver != null)
            tickDriver.OnTick -= OnTick;
    }

    private void Update()
    {
        if (IsRunMode && !simulationLockedByError && !IsEvaluationActive && !MessagePopupUI.IsAnyPopupOpen && Input.GetMouseButtonDown(0))
            TryRunInteractionAtHoveredTile();
    }

    private void ValidateReferences()
    {
        if (tickDriver == null)
            Debug.LogError("RunModeController: tickDriver is not assigned", this);

        if (simulationRunnerBehaviour == null)
            Debug.LogError("RunModeController: simulationRunnerBehaviour is not assigned", this);

        if (levelContext == null)
            Debug.LogError("RunModeController: levelContext is not assigned", this);
        else if (levelContext.LevelData == null)
            Debug.LogError("RunModeController: levelContext has no LevelData", this);

        if (levelRegistry == null)
            Debug.LogWarning("RunModeController: levelRegistry is not assigned", this);

        if (gridUI == null)
            Debug.LogWarning("RunModeController: gridUI is not assigned", this);

        if (levelSceneNavigator == null)
            Debug.LogWarning("RunModeController: levelSceneNavigator is not assigned", this);

        if (messagePopup == null)
            Debug.LogWarning("RunModeController: messagePopup is not assigned", this);
    }

    private void TryRunInteractionAtHoveredTile()
    {
        if (MessagePopupUI.IsAnyPopupOpen)
            return;

        if (placementController == null || sim == null)
            return;

        GridTile hoveredTile = placementController.GetTileUnderMousePublic();
        if (hoveredTile == null)
            return;

        sim.TryInteractAt(hoveredTile.gridPosition);
    }

    private void ToggleRunMode()
    {
        if (!IsRunMode)
            EnterRunMode();
        else
            ExitRunModeToBuild();
    }

    private void EnterRunMode()
    {
        IsRunMode = true;
        IsEvaluationActive = false;
        evaluationTicksElapsed = 0;
        simulationLockedByError = false;

        if (placementController != null)
        {
            placementController.ClearActivePlacementState();
            placementController.SetGhostVisiblePublic(false);
        }

        if (shopUI != null)
            shopUI.gameObject.SetActive(false);

        if (tickDriver != null)
        {
            tickDriver.Pause();
            tickDriver.ResetGate();
        }

        if (messagePopup != null)
            messagePopup.Hide();

        sim?.BeginRun();

        if (logicOverlayController != null)
            logicOverlayController.RefreshControlledSystems();

        RefreshAllUI();
    }

    private void ExitRunModeToBuild()
    {
        if (messagePopup != null)
            messagePopup.Hide();

        if (tickDriver != null)
        {
            tickDriver.Pause();
            tickDriver.ResetGate();
        }

        sim?.EndRun();

        if (placementController != null)
            placementController.ClearActivePlacementState();

        if (shopUI != null)
            shopUI.gameObject.SetActive(true);

        IsRunMode = false;
        IsEvaluationActive = false;
        evaluationTicksElapsed = 0;
        simulationLockedByError = false;

        if (logicOverlayController != null)
            logicOverlayController.RefreshControlledSystems();

        RefreshAllUI();
    }

    private void BeginEvaluation()
    {
        if (simulationLockedByError || sim == null)
            return;

        if (!IsRunMode)
        {
            EnterRunMode();
        }
        else
        {
            if (tickDriver != null)
            {
                tickDriver.Pause();
                tickDriver.ResetGate();
            }

            sim.EndRun();
            sim.BeginRun();
        }

        if (messagePopup != null)
            messagePopup.Hide();

        SetSpeedDropdownToMax();

        IsEvaluationActive = true;
        evaluationTicksElapsed = 0;
        simulationLockedByError = false;

        if (tickDriver != null)
            tickDriver.Play(GetCurrentSpeedMultiplier());

        RefreshAllUI();
    }

    private void SetSpeedDropdownToMax()
    {
        if (speedDropdown == null || speedDropdown.options == null || speedDropdown.options.Count == 0)
            return;

        int maxIndex = speedDropdown.options.Count - 1;

        if (speedDropdown.value == maxIndex)
            return;

        speedDropdown.SetValueWithoutNotify(maxIndex);
        speedDropdown.RefreshShownValue();
    }

    private void FinishEvaluationSuccess()
    {
        IsEvaluationActive = false;

        if (tickDriver != null)
        {
            tickDriver.Pause();
            tickDriver.ResetGate();
        }

        string levelId = CurrentLevelId;
        LevelData levelData = CurrentLevelData;

        EvaluationResult result = EvaluationService.Calculate(sim, levelData, evaluationTickCount, tickDriver != null ? tickDriver.baseTickSeconds : 0f);

        EvaluationRewardResult rewardResult = ApplyEvaluationReward(levelData, levelId, result);

        int bestIncomePerMinute = PlayerData.Instance != null && !string.IsNullOrEmpty(levelId) ? PlayerData.Instance.GetBestIncomePerMinute(levelId) : 0;

        EvaluationDisplayMessage displayMessage = EvaluationService.BuildDisplayMessage(
            result,
            rewardResult.NewBest,
            bestIncomePerMinute,
            rewardResult.FirstCompletionRewardBits);

        RefreshAllUI();

        if (messagePopup != null)
        {
            messagePopup.Show(displayMessage.Title, displayMessage.Body, displayMessage.MessageType, ExitRunModeToBuild);
        }
        else
        {
            ExitRunModeToBuild();
        }
    }

    private EvaluationRewardResult ApplyEvaluationReward(LevelData levelData, string levelId, EvaluationResult result)
    {
        if (PlayerData.Instance == null)
            return EvaluationRewardResult.NotApplied();

        List<PlacedComponentSaveEntry> placedComponents = gridUI != null ? gridUI.ExportPlacedComponentEntries() : null;

        List<WireSaveEntry> wireEntries = gridUI != null ? gridUI.ExportWireEntries() : null;

        EvaluationRewardResult rewardResult = EvaluationService.ApplySuccessfulEvaluation(
            PlayerData.Instance,
            levelRegistry,
            levelData,
            levelId,
            result,
            placedComponents,
            wireEntries);

        if (rewardResult.WasApplied && levelSceneNavigator != null)
            levelSceneNavigator.RefreshLevelNavigationButtons();

        return rewardResult;
    }

    private void FinishEvaluationFailure(string message)
    {
        IsEvaluationActive = false;
        simulationLockedByError = true;

        if (tickDriver != null)
        {
            tickDriver.Pause();
            tickDriver.CancelQueuedStep();
        }

        RefreshAllUI();

        if (messagePopup != null)
            messagePopup.Show("Evaluation Failed", message, PopupMessageType.Error, ExitRunModeToBuild);
        else
            ExitRunModeToBuild();
    }

    private void TogglePlayPause()
    {
        if (!IsRunMode || simulationLockedByError || tickDriver == null || IsEvaluationActive)
            return;

        if (tickDriver.IsPlaying)
            tickDriver.Pause();
        else
            tickDriver.Play(GetCurrentSpeedMultiplier());

        ApplyPlayPauseUI();
    }

    private void StepOnce()
    {
        if (!IsRunMode || simulationLockedByError || tickDriver == null || IsEvaluationActive)
            return;

        tickDriver.QueueStep();
        ApplyPlayPauseUI();
    }

    private void OnSpeedChanged(int _)
    {
        if (!IsRunMode || tickDriver == null || !tickDriver.IsPlaying)
            return;

        tickDriver.Play(GetCurrentSpeedMultiplier());
    }

    private float GetCurrentSpeedMultiplier()
    {
        if (speedDropdown == null)
            return 1f;

        return speedDropdown.value switch
        {
            0 => 1f,
            1 => 2f,
            2 => 5f,
            3 => 10f,
            _ => 1f
        };
    }

    private void OnTick()
    {
        if (!IsRunMode || sim == null)
            return;

        sim.TickOnce();

        if (sim.HasError)
        {
            string msg = sim.ErrorMessage ?? "Simulation error.";

            if (IsEvaluationActive)
            {
                FinishEvaluationFailure($"Evaluation failed.\n\n{msg}");
                return;
            }

            simulationLockedByError = true;

            if (tickDriver != null)
            {
                tickDriver.Pause();
                tickDriver.CancelQueuedStep();
            }

            RefreshAllUI();

            if (messagePopup != null)
                messagePopup.Show("Simulation Error", msg, PopupMessageType.Error, ExitRunModeToBuild);
            else
                ExitRunModeToBuild();

            return;
        }

        if (IsEvaluationActive)
        {
            evaluationTicksElapsed++;

            if (evaluationTicksElapsed >= evaluationTickCount)
                FinishEvaluationSuccess();
        }

        ApplyPlayPauseUI();
    }

    private void RefreshAllUI()
    {
        ApplyBuildOrRunModeUI();
        ApplyPlayPauseUI();
        ApplyEvaluationUI();
        ApplySpeedDropdownOptionColours();
    }

    private void ApplyBuildOrRunModeUI()
    {
        if (toggleRunModeLabel != null)
        {
            toggleRunModeLabel.text = IsRunMode ? "Build" : "Test";
            toggleRunModeLabel.color = UIColourUtility.TextPrimary;
        }

        if (toggleRunModeButton != null)
        {
            UIColourUtility.ApplySelectableColours(toggleRunModeButton, IsRunMode ? UIColourUtility.BuildOrange : UIColourUtility.ActionTeal);
        }

        bool runControlsAvailable = IsRunMode && !simulationLockedByError && !IsEvaluationActive;

        if (playPauseButton != null)
            playPauseButton.interactable = runControlsAvailable;

        if (speedDropdown != null)
            speedDropdown.interactable = IsRunMode && !simulationLockedByError;

        ApplyRunControlColours();
    }

    private void ApplyPlayPauseUI()
    {
        if (stepButton != null)
        {
            stepButton.interactable = IsRunMode && !simulationLockedByError && !IsEvaluationActive && tickDriver != null && !tickDriver.IsStepQueued;
        }

        if (playPauseLabel != null && tickDriver != null)
        {
            if (!IsRunMode)
                playPauseLabel.text = "Paused";
            else if (IsEvaluationActive)
                playPauseLabel.text = "Evaluating";
            else
                playPauseLabel.text = tickDriver.IsPlaying ? "Pause" : "Run";

            playPauseLabel.color = UIColourUtility.TextPrimary;
        }

        ApplyRunControlColours();
    }

    private void ApplyEvaluationUI()
    {
        if (evaluateButton != null)
        {
            evaluateButton.interactable = !simulationLockedByError && !IsEvaluationActive;
            UIColourUtility.ApplySelectableColours(evaluateButton, UIColourUtility.SuccessGreen);
        }

        if (evaluateButtonLabel != null)
        {
            evaluateButtonLabel.text = IsEvaluationActive ? "Evaluating..." : "Evaluate";
            evaluateButtonLabel.color = UIColourUtility.TextPrimary;
        }
    }

    private void ApplyRunControlColours()
    {
        UIColourUtility.ApplySelectableColours(
            playPauseButton,
            playPauseButton != null && playPauseButton.interactable? UIColourUtility.ActionTeal: UIColourUtility.DisabledGrey);

        UIColourUtility.ApplySelectableColours(stepButton, stepButton != null && stepButton.interactable ? UIColourUtility.ActionTeal : UIColourUtility.DisabledGrey);

        UIColourUtility.ApplySelectableColours(speedDropdown, speedDropdown != null && speedDropdown.interactable ? UIColourUtility.ActionTeal : UIColourUtility.DisabledGrey);

        ApplySpeedDropdownOptionColours();
    }

    private void ApplySpeedDropdownOptionColours()
    {
        if (speedDropdown == null)
            return;

        UIColourUtility.ApplySelectableColours(speedDropdown,speedDropdown.interactable ? UIColourUtility.ActionTeal : UIColourUtility.DisabledGrey);

        if (speedDropdown.captionText != null)
            speedDropdown.captionText.color = UIColourUtility.TextPrimary;

        if (speedDropdown.itemText != null)
            speedDropdown.itemText.color = UIColourUtility.TextPrimary;

        if (speedDropdown.template == null)
            return;

        Toggle[] optionToggles = speedDropdown.template.GetComponentsInChildren<Toggle>(true);

        foreach (Toggle toggle in optionToggles)
        {
            if (toggle == null)
                continue;

            UIColourUtility.ApplySelectableColours(toggle, UIColourUtility.ActionTeal);

            TextMeshProUGUI[] texts = toggle.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (TextMeshProUGUI text in texts)
            {
                if (text != null)
                    text.color = UIColourUtility.TextPrimary;
            }
        }
    }
}