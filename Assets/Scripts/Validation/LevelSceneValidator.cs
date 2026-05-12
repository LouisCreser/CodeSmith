using UnityEngine;

public sealed class LevelSceneValidator : MonoBehaviour
{
    [Header("Required Scene References")]
    [SerializeField] private LevelContext levelContext;
    [SerializeField] private TileGrid gridUI;
    [SerializeField] private FactorySimulationRunner simulationRunner;
    [SerializeField] private RunModeController runModeController;
    [SerializeField] private PlacementController placementController;
    [SerializeField] private WireEditController wireEditController;
    [SerializeField] private LogicOverlayController logicOverlayController;
    [SerializeField] private LevelBoardStateController levelBoardStateController;
    [SerializeField] private LevelBoardStateSaver levelBoardStateSaver;
    [SerializeField] private ComponentSimulationRules simulationRules;

    private void Awake()
    {
        Validate();
    }

    public void Validate()
    {
        ValidateObject(levelContext, nameof(levelContext));
        ValidateObject(gridUI, nameof(gridUI));
        ValidateObject(simulationRunner, nameof(simulationRunner));
        ValidateObject(runModeController, nameof(runModeController));
        ValidateObject(placementController, nameof(placementController));
        ValidateObject(wireEditController, nameof(wireEditController));
        ValidateObject(logicOverlayController, nameof(logicOverlayController));
        ValidateObject(levelBoardStateController, nameof(levelBoardStateController));
        ValidateObject(levelBoardStateSaver, nameof(levelBoardStateSaver));
        ValidateObject(simulationRules, nameof(simulationRules));

        if (levelContext != null && levelContext.LevelData == null)
            Debug.LogError("LevelSceneValidator: LevelContext has no LevelData assigned", levelContext);

        ValidateSimulationRules();
    }

    private void ValidateSimulationRules()
    {
        if (simulationRules == null)
            return;

        if (simulationRules.furnaceRecipeBook == null)
            Debug.LogError("LevelSceneValidator: ComponentSimulationRules has no furnaceRecipeBook assigned", simulationRules);

        if (simulationRules.anvilRecipeBook == null)
            Debug.LogError("LevelSceneValidator: ComponentSimulationRules has no anvilRecipeBook assigned", simulationRules);
    }

    private void ValidateObject(Object target, string fieldName)
    {
        if (target == null)
            Debug.LogError($"LevelSceneValidator: {fieldName} is not assigned", this);
    }
}