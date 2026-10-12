using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;


public class ResultPopup : UIBase
{
    [Header("UI연결")]
    [SerializeField] private TextMeshProUGUI winnerText;
    [SerializeField] private Image winnerColorImage;
    [SerializeField] private TextMeshProUGUI rankingsText;
    [SerializeField] private Button restartButton;

    public event Action RestartClicked;

    public override void Init()
    {
        restartButton.onClick.AddListener(HandleRestart);
    }

    protected override void OnOpen()
    {
        SoundManager.Play(SoundId.ResultPopup);
    }

    public ResultPopup SetWinner(string winnername, Color winnerColor)
    {
        if (winnerText != null) winnerText.text = string.Format("{0} win", winnername);
        if (winnerColorImage != null) winnerColorImage.color = winnerColor;
        return this;
    }

    public ResultPopup SetRankings(
        IReadOnlyList<RankingEntry> rankings,
        IReadOnlyList<PlayerDisplayInfo> profiles)
    {
        if (rankingsText == null) return this;
        if (rankings == null || rankings.Count == 0)
        {
            rankingsText.text = string.Empty;
            return this;
        }

        var builder = new StringBuilder();
        foreach (RankingEntry entry in rankings.OrderBy(value => value.rank).ThenBy(value => value.team))
        {
            PlayerDisplayInfo profile = profiles?.FirstOrDefault(value =>
                value != null && (value.playerId == entry.playerId || value.team == entry.team));
            string name = !string.IsNullOrWhiteSpace(profile?.nickname)
                ? profile.nickname
                : $"팀 {entry.team + 1}";
            if (builder.Length > 0) builder.AppendLine();
            builder.Append($"{entry.rank}위  {name}  +{entry.rewardGold} 골드");
        }

        rankingsText.text = builder.ToString();
        return this;
    }

    private void HandleRestart()
    {
        Close();

        if (RestartClicked != null) RestartClicked.Invoke();
        else SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

}
