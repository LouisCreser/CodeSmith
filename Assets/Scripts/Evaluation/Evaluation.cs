using UnityEngine;
using System.Collections.Generic;

public enum EvaluationOutcome
{
    Passed,
    UnderTarget,
    NoProduction
}

public readonly struct EvaluationResult
{
    public readonly EvaluationOutcome Outcome;

    public readonly int ProducedCount;
    public readonly int ProducedValue;
    public readonly int RequiredTargetCount;

    public readonly float ProducedPerMinute;
    public readonly float ValuePerMinute;

    public readonly int ProducedPerMinuteRounded;
    public readonly int ValuePerMinuteRounded;

    public bool Passed => Outcome == EvaluationOutcome.Passed;

    public EvaluationResult(
        EvaluationOutcome outcome,
        int producedCount,
        int producedValue,
        int requiredTargetCount,
        float producedPerMinute,
        float valuePerMinute,
        int producedPerMinuteRounded,
        int valuePerMinuteRounded)
    {
        Outcome = outcome;
        ProducedCount = producedCount;
        ProducedValue = producedValue;
        RequiredTargetCount = requiredTargetCount;
        ProducedPerMinute = producedPerMinute;
        ValuePerMinute = valuePerMinute;
        ProducedPerMinuteRounded = producedPerMinuteRounded;
        ValuePerMinuteRounded = valuePerMinuteRounded;
    }
}

public readonly struct EvaluationDisplayMessage
{
    public readonly string Title;
    public readonly string Body;
    public readonly PopupMessageType MessageType;

    public EvaluationDisplayMessage(string title, string body, PopupMessageType messageType)
    {
        Title = title;
        Body = body;
        MessageType = messageType;
    }
}

public readonly struct EvaluationRewardResult
{
    public readonly bool WasApplied;
    public readonly bool NewBest;
    public readonly string UnlockedLevelId;
    public readonly int FirstCompletionRewardBits;

    public bool UnlockedNextLevel => !string.IsNullOrEmpty(UnlockedLevelId);
    public bool AwardedFirstCompletionReward => FirstCompletionRewardBits > 0;

    public EvaluationRewardResult(
        bool wasApplied,
        bool newBest,
        string unlockedLevelId,
        int firstCompletionRewardBits)
    {
        WasApplied = wasApplied;
        NewBest = newBest;
        UnlockedLevelId = unlockedLevelId;
        FirstCompletionRewardBits = firstCompletionRewardBits;
    }

    public static EvaluationRewardResult NotApplied()
    {
        return new EvaluationRewardResult(false, false, null, 0);
    }
}

public static class EvaluationService
{
    public static EvaluationResult Calculate(
        ISimulationRunner simulationRunner,
        LevelData levelData,
        int evaluationTickCount,
        float baseTickSeconds)
    {
        int producedCount = simulationRunner != null ? simulationRunner.ProducedCount : 0;
        int producedValue = simulationRunner != null ? simulationRunner.ProducedValue : 0;
        int requiredTargetCount = levelData != null ? Mathf.Max(1, levelData.minimumTargetItemsForSuccess) : 1;

        float evaluationMinutes = evaluationTickCount > 0 && baseTickSeconds > 0f ? evaluationTickCount * baseTickSeconds / 60f : 0f;

        float producedPerMinute = evaluationMinutes > 0f ? producedCount / evaluationMinutes : 0f;

        float valuePerMinute = evaluationMinutes > 0f ? producedValue / evaluationMinutes : 0f;

        int producedPerMinuteRounded = Mathf.RoundToInt(producedPerMinute);
        int valuePerMinuteRounded = Mathf.RoundToInt(valuePerMinute);

        EvaluationOutcome outcome = DetermineOutcome(producedCount, requiredTargetCount);

        return new EvaluationResult(
            outcome,
            producedCount,
            producedValue,
            requiredTargetCount,
            producedPerMinute,
            valuePerMinute,
            producedPerMinuteRounded,
            valuePerMinuteRounded);
    }

    public static EvaluationRewardResult ApplySuccessfulEvaluation(
        PlayerData playerData,
        LevelRegistry levelRegistry,
        LevelData levelData,
        string levelId,
        EvaluationResult evaluationResult,
        List<PlacedComponentSaveEntry> placedComponents,
        List<WireSaveEntry> wireEntries)
    {
        if (playerData == null)
            return EvaluationRewardResult.NotApplied();

        if (string.IsNullOrEmpty(levelId))
            return EvaluationRewardResult.NotApplied();

        if (!evaluationResult.Passed)
            return EvaluationRewardResult.NotApplied();

        int firstCompletionRewardBits = ApplyFirstCompletionReward(playerData, levelData, levelId);

        bool newBest = playerData.RegisterLevelEvaluationResult(
            levelId,
            evaluationResult.ProducedPerMinuteRounded,
            evaluationResult.ValuePerMinuteRounded,
            placedComponents,
            wireEntries);

        string unlockedLevelId = null;

        if (levelRegistry != null)
        {
            string nextLevelId = levelRegistry.GetNextLevelId(levelId);
            if (!string.IsNullOrEmpty(nextLevelId))
            {
                playerData.UnlockLevel(nextLevelId);
                unlockedLevelId = nextLevelId;
            }
        }

        playerData.SaveData();

        return new EvaluationRewardResult(
            wasApplied: true,
            newBest: newBest,
            unlockedLevelId: unlockedLevelId,
            firstCompletionRewardBits: firstCompletionRewardBits);
    }

    public static EvaluationDisplayMessage BuildDisplayMessage(
        EvaluationResult result,
        bool newBest,
        int bestIncomePerMinute,
        int firstCompletionRewardBits)
    {
        string title = GetTitle(result.Outcome);

        string body =
            $"Target items produced: {result.ProducedCount} / {result.RequiredTargetCount}\n";

        string outcomeExplanation = GetOutcomeExplanation(result.Outcome);
        if (!string.IsNullOrEmpty(outcomeExplanation))
            body = $"{outcomeExplanation}\n\n{body}";

        if (result.Passed)
        {
            body +=
                $"Produced/min: {result.ProducedPerMinuteRounded}\n" +
                $"Bits/min: {result.ValuePerMinuteRounded}\n";

            if (firstCompletionRewardBits > 0)
                body += $"First completion reward: +{firstCompletionRewardBits} Bits\n";
            else if (newBest)
                body += "New best successful run!\n";
            else
                body += $"Best Bits/min: {bestIncomePerMinute}\n";
        }

        return new EvaluationDisplayMessage(
            title,
            body,
            GetPopupMessageType(result.Outcome));
    }

    private static int ApplyFirstCompletionReward(PlayerData playerData, LevelData levelData, string levelId)
    {
        if (playerData == null || levelData == null || string.IsNullOrEmpty(levelId))
            return 0;

        int rewardBits = Mathf.Max(0, levelData.firstCompletionRewardBits);
        if (rewardBits <= 0)
            return 0;

        if (!playerData.TryClaimFirstCompletionReward(levelId))
            return 0;

        playerData.AddMoney(rewardBits);
        return rewardBits;
    }

    private static EvaluationOutcome DetermineOutcome(int producedCount, int requiredTargetCount)
    {
        if (producedCount <= 0)
            return EvaluationOutcome.NoProduction;

        if (producedCount < requiredTargetCount)
            return EvaluationOutcome.UnderTarget;

        return EvaluationOutcome.Passed;
    }

    private static string GetTitle(EvaluationOutcome outcome)
    {
        return outcome switch
        {
            EvaluationOutcome.Passed => "Evaluation Passed",
            EvaluationOutcome.UnderTarget => "Evaluation Under Target",
            EvaluationOutcome.NoProduction => "Evaluation Failed",
            _ => "Evaluation Complete"
        };
    }

    private static string GetOutcomeExplanation(EvaluationOutcome outcome)
    {
        return outcome switch
        {
            EvaluationOutcome.Passed => "The factory met the target threshold.",
            EvaluationOutcome.UnderTarget => "The factory did not meet the success threshold.",
            EvaluationOutcome.NoProduction => "The factory finished evaluation without producing any items.",
            _ => "The factory encountered an error."
        };
    }

    private static PopupMessageType GetPopupMessageType(EvaluationOutcome outcome)
    {
        return outcome switch
        {
            EvaluationOutcome.Passed => PopupMessageType.Success,
            EvaluationOutcome.UnderTarget => PopupMessageType.Warning,
            EvaluationOutcome.NoProduction => PopupMessageType.Error,
            _ => PopupMessageType.Info
        };
    }
}