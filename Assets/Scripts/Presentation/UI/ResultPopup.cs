using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;


public class ResultPopup : UIBase
{
    [Header("UI연결")]
    [SerializeField] private TextMeshProUGUI winnerText;
    [SerializeField] private Image winnerColorImage;
    [SerializeField] private TextMeshProUGUI rankingsText;
    [FormerlySerializedAs("restartButton")]
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private Button returnToRoomButton;
    [SerializeField] private Image winnerProfileIcon;

    public event Action MainMenuClicked;
    public event Action ReturnToRoomClicked;

    public override void Init()
    {
        mainMenuButton?.onClick.AddListener(HandleMainMenu);
        returnToRoomButton?.onClick.AddListener(HandleReturnToRoom);
    }

    private void OnDestroy()
    {
        mainMenuButton?.onClick.RemoveListener(HandleMainMenu);
        returnToRoomButton?.onClick.RemoveListener(HandleReturnToRoom);
    }

    protected override void OnOpen()
    {
        if (mainMenuButton != null) mainMenuButton.interactable = true;
        if (returnToRoomButton != null) returnToRoomButton.interactable = true;
        SoundManager.Play(SoundId.ResultPopup);
    }

    public ResultPopup SetWinner(string winnername, Color winnerColor, Sprite profileIcon = null)
    {
        if (winnerText != null) winnerText.text = string.Format("{0} win", winnername);
        if (winnerColorImage != null) winnerColorImage.color = winnerColor;
        if (winnerProfileIcon != null)
        {
            winnerProfileIcon.sprite = profileIcon;
            winnerProfileIcon.enabled = profileIcon != null;
            winnerProfileIcon.preserveAspect = true;
        }
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

    private void HandleMainMenu()
    {
        DisableReturnButtons();
        Close();

        if (MainMenuClicked != null) MainMenuClicked.Invoke();
        else
        {
            LobbyNetworkCoordinator coordinator = FindFirstObjectByType<LobbyNetworkCoordinator>();
            if (coordinator != null) coordinator.LeaveGameToMainMenu();
            else SceneManager.LoadScene("Lobby");
        }
    }

    private void HandleReturnToRoom()
    {
        DisableReturnButtons();
        Close();
        if (ReturnToRoomClicked != null) ReturnToRoomClicked.Invoke();
        else
        {
            LobbyNetworkCoordinator coordinator = FindFirstObjectByType<LobbyNetworkCoordinator>();
            if (coordinator != null) coordinator.ReturnToWaitingRoom();
            else SceneManager.LoadScene("Lobby");
        }
    }

    private void DisableReturnButtons()
    {
        if (mainMenuButton != null) mainMenuButton.interactable = false;
        if (returnToRoomButton != null) returnToRoomButton.interactable = false;
    }

}
