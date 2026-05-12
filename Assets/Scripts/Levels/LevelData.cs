using UnityEngine;
using System;
using System.Collections.Generic;

[Serializable]
public class SupplyBoxPlacement
{
    public string componentId;
    public Vector2Int anchor;
    public int rotationIndex;
    public string supplyItemId;
}

[Serializable]
public class StartingStoredComponentEntry
{
    public FactoryComponentData componentData;
    public int amount = 1;
}

[CreateAssetMenu(fileName = "LevelData", menuName = "Factory/Level Data")]
public class LevelData : ScriptableObject
{
    [Header("Identity")]
    public string levelId;
    public string displayName;
    public string sceneName;
    public bool unlockedByDefault = false;

    [Header("Target")]
    public string targetItemId;
    public int targetItemValue;
    public int minimumTargetItemsForSuccess = 1;

    [Header("Completion Reward")]
    [Tooltip("Bits awarded immediately the first time this level is completed")]
    [Min(0)]
    public int firstCompletionRewardBits = 0;

    [Header("Pricing")]
    [Tooltip("Multiplier applied to the base price of every purchasable component in this level")]
    [Min(0f)]
    public float componentBasePriceMultiplier = 1f;

    [Header("Shop")]
    [Tooltip("Components available in the shop in this level")]
    public List<FactoryComponentData> shopEntries = new();

    [Header("Manuals")]
    [Tooltip("Manuals available in this level")]
    public List<ManualData> manualEntries = new();

    [Header("Starting Stored Components")]
    [Tooltip("These components are stored when a level is first created")]
    public List<StartingStoredComponentEntry> startingStoredComponents = new();

    [Header("Fixed Layout")]
    public List<SupplyBoxPlacement> supplyBoxes = new();
    public string outputBinComponentId;
    public Vector2Int outputBinAnchor;
    public int outputBinRotationIndex;

    [Header("Fixed Start Signal")]
    public string startSignalComponentId;
    public Vector2Int startSignalAnchor;
    public int startSignalRotationIndex;
}