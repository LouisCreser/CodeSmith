using UnityEngine;

public enum ComponentType
{
    Conveyor,
    Furnace,
    Splitter,
    SupplyBox,
    OutputBin,
    Anvil,
    Button,
    StartSignal,
    Sensor
}

[CreateAssetMenu(fileName = "NewFactoryComponent", menuName = "Factory/Factory Component")]
public class FactoryComponentData : ScriptableObject
{
    [Header("Identity")]
    public string id;

    [Header("Details")]
    public FactoryComponent component;

    [Tooltip("Default rotation used when placing this component from the shop")]
    [Range(0, 3)]
    public int defaultRotationIndex = 3;

    [Header("Simulation")]
    public ComponentType type;

    [Tooltip("Offsets relative to (0,0)")]
    public Vector2Int[] footprintCells = new Vector2Int[] {Vector2Int.zero};

    [Header("Logic")]
    [Tooltip("Logic input tiles relative to (0,0)")]
    public Vector2Int[] logicInputTiles = new Vector2Int[0];
}