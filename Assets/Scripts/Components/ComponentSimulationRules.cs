using UnityEngine;

// Seperates out the editable but component-specific rules
[CreateAssetMenu(fileName = "ComponentSimulationRules", menuName = "Factory/Simulation Rules")]
public class ComponentSimulationRules : ScriptableObject
{
    [Header("Furnace")]
    public FurnaceRecipe furnaceRecipeBook;

    [Header("Anvil")]
    public AnvilRecipe anvilRecipeBook;

    [Tooltip("Relative tile used as the anvil surface. The item sits here.")]
    public Vector2Int anvilItemTile = Vector2Int.zero;

    [Tooltip("If any of these tiles are active, the anvil ejects the item.")]
    public Vector2Int[] anvilEjectLogicInputTiles = new Vector2Int[0];

    [Tooltip("If any of these tiles are active, the hammer strikes the item.")]
    public Vector2Int[] anvilHammerLogicInputTiles = new Vector2Int[0];
}