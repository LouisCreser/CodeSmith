using UnityEngine;

public static class ComponentPricingService
{
    // How much components increase in price everytime they are purchased in a level
    public const float RepeatedPurchasePriceMultiplier = 1.2f;

    public static int GetCurrentPrice(
        FactoryComponentData componentData,
        LevelData levelData,
        int purchaseCount)
    {
        if (componentData == null || componentData.component == null)
            return 0;

        return GetCurrentPrice(componentData.component.price, levelData, purchaseCount);
    }

    public static int GetCurrentPrice(
        int basePrice,
        LevelData levelData,
        int purchaseCount)
    {
        int safeBasePrice = Mathf.Max(0, basePrice);
        int safePurchaseCount = Mathf.Max(0, purchaseCount);

        float levelMultiplier = levelData != null ? Mathf.Max(0f, levelData.componentBasePriceMultiplier) : 1f;

        float rawPrice = safeBasePrice * levelMultiplier * Mathf.Pow(RepeatedPurchasePriceMultiplier, safePurchaseCount);

        return Mathf.Max(0, Mathf.CeilToInt(rawPrice));
    }

    public static int GetLevelAdjustedPrice(FactoryComponentData componentData, LevelData levelData)
    {
        return GetCurrentPrice(componentData, levelData, purchaseCount: 0);
    }

    public static int GetLevelAdjustedPrice(int basePrice, LevelData levelData)
    {
        return GetCurrentPrice(basePrice, levelData, purchaseCount: 0);
    }
}