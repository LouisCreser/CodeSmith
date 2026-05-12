using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;
using TMPro;

public class RowItemUI : MonoBehaviour
{
    [Header("Text")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI priceText;
    [SerializeField] private TextMeshProUGUI lockedText;

    [Header("Button")]
    [SerializeField] private Button button;

    private bool isComponentRow;

    private void Awake()
    {
        ResolveReferences();
    }

    public void BindComponent(FactoryComponentData componentData, int currentPrice, int storedCount, bool selected, bool canAfford, Action<FactoryComponentData> onSelected)
    {
        if (componentData == null || componentData.component == null)
        {
            Debug.LogWarning("RowItemUI: cannot bind null component data", this);
            ClearText();
            return;
        }

        isComponentRow = true;

        FactoryComponent component = componentData.component;

        SetName(component.name, UIColourUtility.TextPrimary);
        SetLockedTextVisible(false);
        SetComponentPriceState(currentPrice, storedCount, canAfford);
        SetComponentSelected(selected);

        ConfigureButton(() =>
        {
            onSelected?.Invoke(componentData);
            ClearButtonSelection();
        });
    }

    public void BindManual(ManualData manual, bool isUnlocked, bool canAfford, Action<ManualData> onSelected)
    {
        if (manual == null)
        {
            Debug.LogWarning("RowItemUI: cannot bind null manual", this);
            ClearText();
            return;
        }

        isComponentRow = false;

        SetName(manual.displayName, UIColourUtility.TextPrimary);
        SetManualState(manual.unlockPrice, isUnlocked, canAfford);

        ConfigureButton(() =>
        {
            onSelected?.Invoke(manual);
            ClearButtonSelection();
        });
    }

    public void SetComponentSelected(bool selected)
    {
        if (!isComponentRow)
            return;

        if (selected)
            ApplySelectedComponentAppearance();
        else
            ApplyComponentAppearance();
    }

    public void SetComponentPriceState(int price, int storedCount, bool canAfford)
    {
        int safeStoredCount = Mathf.Max(0, storedCount);

        if (safeStoredCount > 0)
        {
            SetPrice($"({safeStoredCount})", UIColourUtility.PositiveGreen);
            return;
        }

        SetPrice(Mathf.Max(0, price).ToString(), canAfford ? UIColourUtility.ValueGold : UIColourUtility.ErrorRed);
    }

    public void SetManualState(int price, bool isUnlocked, bool canAfford)
    {
        SetManualLockedState(isUnlocked);
        SetManualPriceState(price, isUnlocked, canAfford);

        if (isUnlocked)
            ApplyUnlockedManualAppearance();
        else
            ApplyLockedManualAppearance();
    }

    public void SetManualPriceState(int price, bool isUnlocked, bool canAfford)
    {
        if (isUnlocked)
        {
            SetPrice("", UIColourUtility.TextSecondary);
            return;
        }

        SetPrice(Mathf.Max(0, price).ToString(), canAfford ? UIColourUtility.ValueGold : UIColourUtility.ErrorRed);
    }

    private void SetManualLockedState(bool isUnlocked)
    {
        if (isUnlocked)
        {
            SetLockedTextVisible(false);
            return;
        }

        SetLockedText("Locked", UIColourUtility.ErrorRed);
        SetLockedTextVisible(true);
    }

    private void ResolveReferences()
    {
        TextMeshProUGUI[] texts = GetComponentsInChildren<TextMeshProUGUI>(true);

        if (texts.Length > 0 && nameText == null)
            nameText = texts[0];

        if (texts.Length > 1 && priceText == null)
            priceText = texts[1];

        if (texts.Length > 2 && lockedText == null)
            lockedText = texts[2];

        if (button == null)
            button = GetComponent<Button>() ?? GetComponentInChildren<Button>(true);
    }

    private void SetName(string text, Color colour)
    {
        if (nameText == null)
            return;

        nameText.text = string.IsNullOrEmpty(text) ? "Unnamed" : text;
        nameText.color = colour;
    }

    private void SetPrice(string text, Color colour)
    {
        if (priceText == null)
            return;

        priceText.text = text ?? "";
        priceText.color = colour;
    }

    private void SetLockedText(string text, Color colour)
    {
        if (lockedText == null)
            return;

        lockedText.text = text ?? "";
        lockedText.color = colour;
    }

    private void SetLockedTextVisible(bool visible)
    {
        if (lockedText != null)
            lockedText.gameObject.SetActive(visible);
    }

    private void ClearText()
    {
        SetName("", UIColourUtility.TextPrimary);
        SetPrice("", UIColourUtility.TextSecondary);
        SetLockedText("", UIColourUtility.ErrorRed);
        SetLockedTextVisible(false);
    }

    private void ConfigureButton(Action onClicked)
    {
        if (button == null)
            return;

        button.interactable = true;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClicked?.Invoke());
    }

    private void ApplyComponentAppearance()
    {
        UIColourUtility.ApplySelectableColours(button, UIColourUtility.BuildOrange);
    }

    private void ApplySelectedComponentAppearance()
    {
        UIColourUtility.ApplySelectableColours(button, UIColourUtility.Lightened(UIColourUtility.BuildOrange));
    }

    private void ApplyLockedManualAppearance()
    {
        UIColourUtility.ApplySelectableColours(button, UIColourUtility.Darkened(UIColourUtility.InformationBlue));
    }

    private void ApplyUnlockedManualAppearance()
    {
        UIColourUtility.ApplySelectableColours(button, UIColourUtility.InformationBlue);
    }

    private void ClearButtonSelection()
    {
        if (EventSystem.current == null || button == null)
            return;

        if (EventSystem.current.currentSelectedGameObject == button.gameObject)
            EventSystem.current.SetSelectedGameObject(null);
    }
}