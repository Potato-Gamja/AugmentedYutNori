using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ResultRankRowView : UIBase
{
    [Header("UI연결")]
    [SerializeField] private TextMeshProUGUI rankText;
    [SerializeField] private Image profileIcon;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI goldText;

    // 한 줄 내용 채우기. left: 기권·끊김으로 나간 플레이어 (보상 0)
    public ResultRankRowView Bind(int rank, string playerName, int rewardGold, Sprite icon,
                                  Color teamColor, bool isMine, bool left)
    {
        SetText(rankText, $"{rank}위");
        SetText(nameText, playerName);
        SetText(goldText, left ? "-" : $"+{rewardGold} 골드");

        if (profileIcon != null)
        {
            profileIcon.sprite = icon;
            profileIcon.enabled = icon != null;
            profileIcon.preserveAspect = true;
        }
        return this;
    }

    private static void SetText(TextMeshProUGUI text, string value)
    {
        if (text != null) text.text = value;
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null) target.SetActive(active);
    }
}