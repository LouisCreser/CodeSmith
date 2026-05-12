using UnityEngine;
using UnityEngine.UI;
using System;
using TMPro;

public enum PopupMessageType
{
    Info,
    Success,
    Warning,
    Error
}

public sealed class MessagePopupUI : MonoBehaviour
{
    public static bool IsAnyPopupOpen => openPopupCount > 0;

    private static int openPopupCount;

    [Header("Text")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI bodyText;

    [Header("Controls")]
    [SerializeField] private Button closeButton;

    [Header("Defaults")]
    [SerializeField] private string defaultTitle = "Message";
    [SerializeField] private string defaultBody = "";

    private Action onClosed;
    private bool isOpen;

    private void Awake()
    {
        ApplyStaticColours();

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Close);
        }

        Hide();
    }

    private void OnDisable()
    {
        MarkClosed();
    }

    public void Show(string title, string body)
    {
        Show(title, body, PopupMessageType.Info, null);
    }

    public void Show(string title, string body, Action onClosedCallback)
    {
        Show(title, body, PopupMessageType.Info, onClosedCallback);
    }

    public void Show(string title, string body, PopupMessageType messageType)
    {
        Show(title, body, messageType, null);
    }

    public void Show(string title, string body, PopupMessageType messageType, Action onClosedCallback)
    {
        onClosed = onClosedCallback;

        if (titleText != null)
        {
            titleText.text = string.IsNullOrWhiteSpace(title) ? defaultTitle : title;
            titleText.color = GetTitleColour(messageType);
        }

        if (bodyText != null)
        {
            bodyText.text = string.IsNullOrWhiteSpace(body) ? defaultBody : body;
            bodyText.color = UIColourUtility.TextPrimary;
        }

        gameObject.SetActive(true);
        MarkOpen();
    }

    public void ShowMessage(string body)
    {
        Show(defaultTitle, body, PopupMessageType.Info, null);
    }

    public void ShowMessage(string body, Action onClosedCallback)
    {
        Show(defaultTitle, body, PopupMessageType.Info, onClosedCallback);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        onClosed = null;
        MarkClosed();
    }

    private void Close()
    {
        gameObject.SetActive(false);
        MarkClosed();

        Action callback = onClosed;
        onClosed = null;

        callback?.Invoke();
    }

    private void MarkOpen()
    {
        if (isOpen)
            return;

        isOpen = true;
        openPopupCount++;
    }

    private void MarkClosed()
    {
        if (!isOpen)
            return;

        isOpen = false;
        openPopupCount = Mathf.Max(0, openPopupCount - 1);
    }

    private void ApplyStaticColours()
    {
        if (bodyText != null)
            bodyText.color = UIColourUtility.TextPrimary;

        UIColourUtility.ApplySelectableColours(closeButton, UIColourUtility.ActionTeal);
    }

    private static Color GetTitleColour(PopupMessageType messageType)
    {
        return messageType switch
        {
            PopupMessageType.Success => UIColourUtility.PositiveGreen,
            PopupMessageType.Warning => UIColourUtility.ValueGold,
            PopupMessageType.Error => UIColourUtility.ErrorRed,
            _ => UIColourUtility.InformationBlue.Normal
        };
    }
}