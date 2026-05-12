using UnityEngine;
using UnityEngine.UI;
using System;
using TMPro;

public class LevelSelectEntryUI : MonoBehaviour
{
    [Header("Unlocked Text")]
    [SerializeField] private TextMeshProUGUI levelNameText;
    [SerializeField] private TextMeshProUGUI levelTargetItemText;
    [SerializeField] private TextMeshProUGUI levelIncomeText;

    [Header("Locked Text")]
    [SerializeField] private TextMeshProUGUI levelLockedText;

    [Header("Button")]
    [SerializeField] private Button button;

    private LevelData boundLevel;
    private Action<LevelData> onClicked;

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>() ?? GetComponentInChildren<Button>();

        if (levelIncomeText != null)
            levelIncomeText.richText = true;
    }

    public void Bind(
        LevelData levelData,
        bool isUnlocked,
        int incomePerMinute,
        string targetItemDisplayName,
        Action<LevelData> onClickedCallback)
    {
        boundLevel = levelData;
        onClicked = onClickedCallback;

        SetUnlockedTextVisible(isUnlocked);
        SetLockedTextVisible(!isUnlocked);

        if (isUnlocked)
        {
            SetText(levelNameText, levelData != null ? levelData.displayName : null, UIColourUtility.TextPrimary);
            SetText(levelTargetItemText, targetItemDisplayName, UIColourUtility.TextSecondary);
            SetIncomeText(incomePerMinute);
        }
        else
        {
            SetText(levelLockedText, "Locked", UIColourUtility.TextDisabled);
        }

        bool canClick = isUnlocked && levelData != null && !string.IsNullOrEmpty(levelData.sceneName);

        if (button != null)
        {
            button.interactable = canClick;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnPressed);

            UIColourUtility.ApplySelectableColours(button, isUnlocked ? UIColourUtility.InformationBlue : UIColourUtility.DisabledGrey);
        }
    }

    private void SetUnlockedTextVisible(bool visible)
    {
        if (levelNameText != null)
            levelNameText.gameObject.SetActive(visible);

        if (levelTargetItemText != null)
            levelTargetItemText.gameObject.SetActive(visible);

        if (levelIncomeText != null)
            levelIncomeText.gameObject.SetActive(visible);
    }

    private void SetLockedTextVisible(bool visible)
    {
        if (levelLockedText != null)
            levelLockedText.gameObject.SetActive(visible);
    }

    private void SetText(TextMeshProUGUI textField, string value, Color colour)
    {
        if (textField == null)
            return;

        textField.text = string.IsNullOrEmpty(value) ? "Null" : value;
        textField.color = colour;
    }

    private void SetIncomeText(int incomePerMinute)
    {
        if (levelIncomeText == null)
            return;

        levelIncomeText.richText = true;

        if (incomePerMinute <= 0)
        {
            levelIncomeText.text = "Incomplete";
            levelIncomeText.color = UIColourUtility.ErrorRed;
            return;
        }

        levelIncomeText.color = UIColourUtility.TextPrimary;
        levelIncomeText.text = $"<color=#{UIColourUtility.TextPrimaryHex}>B/m:</color>" + $"<color=#{UIColourUtility.PositiveGreenHex}>{incomePerMinute}</color>";
    }

    private void OnPressed()
    {
        if (boundLevel == null)
            return;

        onClicked?.Invoke(boundLevel);
    }
}