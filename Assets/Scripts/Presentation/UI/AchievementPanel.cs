using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 업적 화면. 열 때마다 서버에서 내 업적 목록을 받아 AchievementItemView로 그리고, 달성한 업적은 받기 버튼으로 골드를 받는다.
/// 업적 이름·설명·기준은 서버 achievements.json에서 오므로 클라에 따로 목록이 없다.
/// 서버 호출은 LobbyNetworkCoordinator. 도감 업적 탭과 메인 메뉴 업적 버튼에서 같은 화면을 사용한다.
/// </summary>
public sealed class AchievementPanel : UIBase
{
    [SerializeField] private Button closeButton;
    [SerializeField] private AchievementItemView itemPrefab;
    [SerializeField] private Transform itemContainer;
    [SerializeField] private TMP_Text goldText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private ScrollRect scrollRect;

    private readonly List<AchievementItemView> items = new();
    private LobbyNetworkCoordinator coordinator;
    private bool loading;

    public override void Init()
    {
        closeButton?.onClick.AddListener(Close);
    }

    private void OnDestroy()
    {
        closeButton?.onClick.RemoveListener(Close);
    }

    protected override void OnOpen()
    {
        if (scrollRect != null) scrollRect.verticalNormalizedPosition = 1f;
        coordinator = FindFirstObjectByType<LobbyNetworkCoordinator>();
        ShowGold(coordinator != null ? coordinator.LocalGold : 0);
        Refresh();
    }

    private async void Refresh()
    {
        if (loading) return;
        if (coordinator == null)
        {
            ShowStatus("서버 연결 정보가 없습니다. Lobby 씬에서 열어 주세요.");
            return;
        }
        if (itemPrefab == null || itemContainer == null)
        {
            Debug.LogError("[Achievement] itemPrefab / itemContainer를 연결해 주세요.", this);
            return;
        }

        loading = true;
        ShowStatus("불러오는 중...");
        try
        {
            await coordinator.WaitForLoginAsync();
            AchievementListResponse response = await coordinator.GetAchievementsAsync();
            if (this == null || !IsOpen) return;

            Build(response.achievements);
            ShowGold(coordinator.LocalGold);
            ShowStatus(response.achievements.Count == 0 ? "업적이 없습니다." : string.Empty);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[Achievement] 업적 목록 조회 실패: {exception.Message}", this);
            if (this != null) ShowStatus(ToUserMessage(exception));
        }
        finally
        {
            loading = false;
        }
    }

    // 받을 수 있는 것 → 진행 중 → 받음 순으로, 같은 묶음 안에서는 서버 순서 그대로
    private void Build(List<AchievementEntry> entries)
    {
        var sorted = entries.OrderBy(Order).ToList();

        for (int i = 0; i < sorted.Count; i++)
        {
            AchievementItemView item;
            if (i < items.Count)
            {
                item = items[i];
            }
            else
            {
                item = Instantiate(itemPrefab, itemContainer);
                item.ClaimClicked += OnClaimClicked;
                items.Add(item);
            }
            item.gameObject.SetActive(true);
            item.Bind(sorted[i]);
        }
        for (int i = sorted.Count; i < items.Count; i++) items[i].gameObject.SetActive(false);
    }

    private static int Order(AchievementEntry entry)
    {
        if (entry.achieved && !entry.claimed) return 0;
        return entry.claimed ? 2 : 1;
    }

    private async void OnClaimClicked(AchievementItemView item)
    {
        if (coordinator == null || item == null || item.Entry == null) return;

        item.SetBusy(true);
        ShowStatus(string.Empty);
        try
        {
            ClaimAchievementResponse result = await coordinator.ClaimAchievementAsync(item.Entry.id);
            if (this == null) return;

            item.MarkClaimed();
            Build(items.Where(view => view.gameObject.activeSelf).Select(view => view.Entry).ToList());
            ShowGold(result.gold);
            ShowStatus($"{result.rewardGold:N0} 골드를 받았어요!");
        }
        catch (Exception exception)
        {
            if (this == null) return;
            item.SetBusy(false);
            ShowStatus(ToUserMessage(exception));
            // 이미 받은 경우 등 서버와 상태가 다르면 목록을 다시 받는다
            if (exception is ApiException { StatusCode: 409 }) Refresh();
        }
    }

    private void ShowGold(int gold)
    {
        if (goldText != null) goldText.text = $"{gold:N0}";
    }

    private void ShowStatus(string message)
    {
        if (statusText != null) statusText.text = message ?? string.Empty;
    }

    private static string ToUserMessage(Exception exception)
    {
        if (exception is not ApiException api) return exception.Message;

        string body = api.ResponseBody ?? string.Empty;
        return api.StatusCode switch
        {
            0 => "서버에 연결할 수 없어요.",
            401 => "로그인이 만료됐어요. 다시 로그인해 주세요.",
            404 => "서버의 업적 기능이 아직 반영되지 않았습니다.",
            409 when body.Contains("ACHIEVEMENT_ALREADY_CLAIMED") => "이미 받은 보상이에요.",
            409 when body.Contains("ACHIEVEMENT_NOT_ACHIEVED") => "아직 달성하지 않은 업적이에요.",
            _ => $"요청에 실패했어요. ({api.StatusCode})",
        };
    }
}
