using UnityEngine;
using TMPro;

public sealed class PlayerMetricsUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text moneyText;
    [SerializeField] private TMP_Text incomePerMinuteText;

    private int lastDisplayedMoney = int.MinValue;
    private int lastDisplayedIncomePerMinute = int.MinValue;

    private void Awake()
    {
        if (moneyText != null)
            moneyText.richText = true;

        if (incomePerMinuteText != null)
            incomePerMinuteText.richText = true;
    }

    private void OnEnable()
    {
        Refresh(force: true);
    }

    private void Update()
    {
        Refresh(force: false);
    }

    private void Refresh(bool force)
    {
        if (PlayerData.Instance == null)
        {
            SetMoneyText(0);
            SetIncomePerMinuteText(0);

            lastDisplayedMoney = 0;
            lastDisplayedIncomePerMinute = 0;
            return;
        }

        int money = PlayerData.Instance.Money;
        int incomePerMinute = PlayerData.Instance.GetTotalIncomePerMinute();

        if (force || money != lastDisplayedMoney)
        {
            SetMoneyText(money);
            lastDisplayedMoney = money;
        }

        if (force || incomePerMinute != lastDisplayedIncomePerMinute)
        {
            SetIncomePerMinuteText(incomePerMinute);
            lastDisplayedIncomePerMinute = incomePerMinute;
        }
    }

    private void SetMoneyText(int money)
    {
        if (moneyText == null)
            return;

        moneyText.text = $"<color=#{UIColourUtility.TextPrimaryHex}>Bits:</color>" + $"<color=#{UIColourUtility.ValueGoldHex}>{money}</color>";
    }

    private void SetIncomePerMinuteText(int incomePerMinute)
    {
        if (incomePerMinuteText == null)
            return;

        incomePerMinuteText.text = $"<color=#{UIColourUtility.TextPrimaryHex}>B/min:</color>" + $"<color=#{UIColourUtility.PositiveGreenHex}>{incomePerMinute}</color>";
    }
}