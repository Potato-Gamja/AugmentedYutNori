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

    [Header("순위 목록 (행 프리팹이 있으면 행으로, 없으면 rankingsText로 표시)")]
    [SerializeField] private Transform rankRowContainer;       // VerticalLayoutGroup이 붙은 부모
    [SerializeField] private ResultRankRowView rankRowPrefab;

    private bool returning;
    private readonly List<ResultRankRowView> rankRows = new();

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
        ClearRankRows();
    }

    protected override void OnOpen()
    {
        returning = false;
        if (mainMenuButton != null) mainMenuButton.interactable = true;
        if (returnToRoomButton != null) returnToRoomButton.interactable = true;
        SoundManager.Play(SoundId.ResultPopup);
    }

    public ResultPopup SetWinner(string winnername, Color winnerColor, Sprite profileIcon = null)
    {
        if (winnerText != null) winnerText.text = string.Format("{0} win", winnername);
        // 기존 배경 참조는 프로필 테두리로 사용하며 팀 색을 입히지 않는다.
        if (winnerColorImage != null) winnerColorImage.color = Color.white;
        if (winnerProfileIcon != null)
        {
            winnerProfileIcon.sprite = profileIcon;
            winnerProfileIcon.enabled = profileIcon != null;
            winnerProfileIcon.preserveAspect = true;
        }
        return this;
    }

    // 순위 목록 표시 (등수 → 팀 순)
    //  iconResolver: (iconId, team) → 프로필 아이콘 스프라이트 (HUD의 ResolveProfileIcon 사용)
    //  localTeam: 내 팀 (내 줄 강조용, -1이면 강조 안 함)
    // 나간 플레이어: 서버가 보내는 leftGame (남아 있어도 보상이 0일 수 있어서 금액으로 판단하지 않는다)
    public ResultPopup SetRankings(
        IReadOnlyList<RankingEntry> rankings,
        IReadOnlyList<PlayerDisplayInfo> profiles,
        Func<string, int, Sprite> iconResolver = null,
        int localTeam = -1)
    {
        ClearRankRows();
        if (rankingsText != null) rankingsText.text = string.Empty;
        if (rankings == null || rankings.Count == 0) return this;

        var ordered = rankings.OrderBy(value => value.rank).ThenBy(value => value.team).ToList();

        // 행 프리팹이 없으면 기존처럼 텍스트로
        if (rankRowPrefab == null || rankRowContainer == null)
        {
            SetRankingsText(ordered, profiles);
            return this;
        }

        foreach (RankingEntry entry in ordered)
        {
            PlayerDisplayInfo profile = FindProfile(profiles, entry);
            string name = DisplayName(profile, entry);
            Sprite icon = iconResolver?.Invoke(profile?.iconId, entry.team);
            bool left = entry.leftGame;

            ResultRankRowView row = Instantiate(rankRowPrefab, rankRowContainer);
            row.Init();
            row.Bind(entry.rank, name, entry.rewardGold, icon,
                    GameHudPanel.GetTeamColor(entry.team), entry.team == localTeam, left)
               .Open();
            rankRows.Add(row);
        }
        return this;
    }

    private void SetRankingsText(List<RankingEntry> ordered, IReadOnlyList<PlayerDisplayInfo> profiles)
    {
        if (rankingsText == null) return;
        var builder = new StringBuilder();
        foreach (RankingEntry entry in ordered)
        {
            string name = DisplayName(FindProfile(profiles, entry), entry);
            if (builder.Length > 0) builder.AppendLine();
            builder.Append($"{entry.rank}위  {name}  +{entry.rewardGold} 골드");
        }
        rankingsText.text = builder.ToString();
    }

    private static PlayerDisplayInfo FindProfile(IReadOnlyList<PlayerDisplayInfo> profiles, RankingEntry entry) =>
        profiles?.FirstOrDefault(value =>
            value != null && (value.playerId == entry.playerId || value.team == entry.team));

    private static string DisplayName(PlayerDisplayInfo profile, RankingEntry entry) =>
        !string.IsNullOrWhiteSpace(profile?.nickname) ? profile.nickname : $"팀 {entry.team + 1}";

    private void ClearRankRows()
    {
        foreach (ResultRankRowView row in rankRows)
        {
            if (row == null) continue;
            row.Close();
            Destroy(row.gameObject);
        }
        rankRows.Clear();
    }

    private void HandleMainMenu()
    {
        if (returning) return;
        returning = true;
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
        if (returning) return;
        returning = true;
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
