using UnityEngine;
using System.Collections.Generic;

public sealed class ProjectDataValidator : MonoBehaviour
{
    [Header("Project Data")]
    [SerializeField] private LevelRegistry levelRegistry;
    [SerializeField] private FactoryComponentDatabase componentDatabase;
    [SerializeField] private ItemDatabase itemDatabase;
    [SerializeField] private ComponentSimulationRules simulationRules;

    private void Awake()
    {
        Validate();
    }

    public void Validate()
    {
        ValidateLevelRegistry();
        ValidateComponentDatabase();
        ValidateItemDatabase();
        ValidateSimulationRules();
    }

    private void ValidateLevelRegistry()
    {
        if (levelRegistry == null)
        {
            Debug.LogError("ProjectDataValidator: levelRegistry is not assigned", this);
            return;
        }

        if (levelRegistry.levels == null || levelRegistry.levels.Count == 0)
        {
            Debug.LogError("ProjectDataValidator: levelRegistry has no levels", levelRegistry);
            return;
        }

        HashSet<string> levelIds = new();
        bool hasDefaultUnlockedLevel = false;

        for (int i = 0; i < levelRegistry.levels.Count; i++)
        {
            LevelData level = levelRegistry.levels[i];

            if (level == null)
            {
                Debug.LogError($"ProjectDataValidator: levelRegistry.levels[{i}] is null", levelRegistry);
                continue;
            }

            if (string.IsNullOrWhiteSpace(level.levelId))
                Debug.LogError($"ProjectDataValidator: level asset '{level.name}' has no levelId", level);

            if (!string.IsNullOrWhiteSpace(level.levelId) && !levelIds.Add(level.levelId))
                Debug.LogError($"ProjectDataValidator: duplicate levelId '{level.levelId}", level);

            if (level.unlockedByDefault)
                hasDefaultUnlockedLevel = true;

            if (string.IsNullOrWhiteSpace(level.sceneName))
                Debug.LogError($"ProjectDataValidator: level '{level.levelId}' has no sceneName", level);

            if (string.IsNullOrWhiteSpace(level.targetItemId))
                Debug.LogError($"ProjectDataValidator: level '{level.levelId}' has no targetItemId", level);

            ValidateLevelComponentReferences(level);
            ValidateLevelManualReferences(level);
            ValidateLevelStartingStoredComponents(level);
        }

        if (!hasDefaultUnlockedLevel)
            Debug.LogError("ProjectDataValidator: no level is unlockedByDefault", levelRegistry);
    }

    private void ValidateLevelComponentReferences(LevelData level)
    {
        if (level == null)
            return;

        if (componentDatabase == null)
            return;

        componentDatabase.RebuildLookup();

        if (level.shopEntries != null)
        {
            for (int i = 0; i < level.shopEntries.Count; i++)
            {
                FactoryComponentData component = level.shopEntries[i];

                if (component == null)
                {
                    Debug.LogError($"ProjectDataValidator: level '{level.levelId}' shopEntries[{i}] is null", level);
                    continue;
                }

                ValidateComponentData(component);
            }
        }

        if (level.supplyBoxes != null)
        {
            for (int i = 0; i < level.supplyBoxes.Count; i++)
            {
                SupplyBoxPlacement supplyBox = level.supplyBoxes[i];

                if (supplyBox == null)
                {
                    Debug.LogError($"ProjectDataValidator: level '{level.levelId}' supplyBoxes[{i}] is null", level);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(supplyBox.componentId))
                {
                    Debug.LogError($"ProjectDataValidator: level '{level.levelId}' supplyBoxes[{i}] has no componentId", level);
                    continue;
                }

                if (!componentDatabase.TryGet(supplyBox.componentId, out FactoryComponentData component))
                {
                    Debug.LogError($"ProjectDataValidator: level '{level.levelId}' supply box references missing component '{supplyBox.componentId}", level);
                    continue;
                }

                ValidateComponentData(component);

                if (component.type != ComponentType.SupplyBox)
                    Debug.LogError($"ProjectDataValidator: level '{level.levelId}' supply box component '{supplyBox.componentId}' is not type SupplyBox", component);

                if (string.IsNullOrWhiteSpace(supplyBox.supplyItemId))
                    Debug.LogError($"ProjectDataValidator: level '{level.levelId}' supply box at {supplyBox.anchor} has no supplyItemId", level);
            }
        }

        ValidateFixedComponentReference(level, level.outputBinComponentId, ComponentType.OutputBin, "Output Bin");
        ValidateFixedComponentReference(level, level.startSignalComponentId, ComponentType.StartSignal, "Start Signal");
    }

    private void ValidateFixedComponentReference(
        LevelData level,
        string componentId,
        ComponentType expectedType,
        string label)
    {
        if (level == null || componentDatabase == null)
            return;

        if (string.IsNullOrWhiteSpace(componentId))
        {
            Debug.LogError($"ProjectDataValidator: level '{level.levelId}' has no {label} component id", level);
            return;
        }

        if (!componentDatabase.TryGet(componentId, out FactoryComponentData component))
        {
            Debug.LogError($"ProjectDataValidator: level '{level.levelId}' references missing {label} component '{componentId}", level);
            return;
        }

        ValidateComponentData(component);

        if (component.type != expectedType)
        {
            Debug.LogError(
                $"ProjectDataValidator: level '{level.levelId}' {label} component '{componentId}' is type {component.type}, expected {expectedType}", component);
        }
    }

    private void ValidateLevelManualReferences(LevelData level)
    {
        if (level == null || level.manualEntries == null)
            return;

        HashSet<string> manualIds = new();

        for (int i = 0; i < level.manualEntries.Count; i++)
        {
            ManualData manual = level.manualEntries[i];

            if (manual == null)
            {
                Debug.LogError($"ProjectDataValidator: level '{level.levelId}' manualEntries[{i}] is null", level);
                continue;
            }

            if (string.IsNullOrWhiteSpace(manual.manualId))
                Debug.LogError($"ProjectDataValidator: manual asset '{manual.name}' in level '{level.levelId}' has no manualId", manual);

            if (!string.IsNullOrWhiteSpace(manual.manualId) && !manualIds.Add(manual.manualId))
                Debug.LogError($"ProjectDataValidator: level '{level.levelId}' has duplicate manualId '{manual.manualId}", manual);
        }
    }

    private void ValidateLevelStartingStoredComponents(LevelData level)
    {
        if (level == null || level.startingStoredComponents == null)
            return;

        for (int i = 0; i < level.startingStoredComponents.Count; i++)
        {
            StartingStoredComponentEntry entry = level.startingStoredComponents[i];

            if (entry == null)
            {
                Debug.LogError($"ProjectDataValidator: level '{level.levelId}' startingStoredComponents[{i}] is null", level);
                continue;
            }

            if (entry.componentData == null)
            {
                Debug.LogError($"ProjectDataValidator: level '{level.levelId}' startingStoredComponents[{i}] has no componentData", level);
                continue;
            }

            ValidateComponentData(entry.componentData);

            if (entry.amount <= 0)
                Debug.LogError($"ProjectDataValidator: level '{level.levelId}' startingStoredComponents[{i}] amount must be greater than zero", level);
        }
    }

    private void ValidateComponentDatabase()
    {
        if (componentDatabase == null)
        {
            Debug.LogError("ProjectDataValidator: componentDatabase is not assigned", this);
            return;
        }

        componentDatabase.RebuildLookup();
    }

    private void ValidateComponentData(FactoryComponentData component)
    {
        if (component == null)
            return;

        if (string.IsNullOrWhiteSpace(component.id))
            Debug.LogError($"ProjectDataValidator: component asset '{component.name}' has no id", component);

        if (component.component == null)
        {
            Debug.LogError($"ProjectDataValidator: component '{component.id}' has no FactoryComponent assigned", component);
            return;
        }

        if (string.IsNullOrWhiteSpace(component.component.name))
            Debug.LogError($"ProjectDataValidator: component '{component.id}' has no display name", component);

        if (component.component.price < 0)
            Debug.LogError($"ProjectDataValidator: component '{component.id}' has a negative price", component);

        if (component.footprintCells == null || component.footprintCells.Length == 0)
            Debug.LogError($"ProjectDataValidator: component '{component.id}' has no footprintCells", component);
    }

    private void ValidateItemDatabase()
    {
        if (itemDatabase == null)
        {
            Debug.LogError("ProjectDataValidator: itemDatabase is not assigned", this);
            return;
        }

        itemDatabase.RebuildLookup();
    }

    private void ValidateSimulationRules()
    {
        if (simulationRules == null)
        {
            Debug.LogError("ProjectDataValidator: simulationRules is not assigned", this);
            return;
        }

        if (simulationRules.furnaceRecipeBook == null)
            Debug.LogError("ProjectDataValidator: simulationRules has no furnaceRecipeBook assigned", simulationRules);

        if (simulationRules.anvilRecipeBook == null)
            Debug.LogError("ProjectDataValidator: simulationRules has no anvilRecipeBook assigned", simulationRules);
    }
}