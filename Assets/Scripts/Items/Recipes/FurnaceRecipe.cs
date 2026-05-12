using UnityEngine;
using System;

[Serializable]
public class FurnaceRecipeEntry
{
    public string inputItemId;
    public string outputItemId;
    public int cookTicks = 1;
}

[CreateAssetMenu(fileName = "FurnaceRecipeBook", menuName = "Factory/Furnace Recipe Book")]
public class FurnaceRecipe : ScriptableObject
{
    public FurnaceRecipeEntry[] recipes = Array.Empty<FurnaceRecipeEntry>();

    public bool TryGetRecipe(string inputItemId, out FurnaceRecipeEntry recipe)
    {
        if (recipes != null)
        {
            foreach (var entry in recipes)
            {
                if (entry != null && entry.inputItemId == inputItemId)
                {
                    recipe = entry;
                    return true;
                }
            }
        }

        recipe = null;
        return false;
    }
}