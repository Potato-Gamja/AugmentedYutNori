using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 프로필 아이콘 뽑기 화면. 보유 골드와 뽑기(1회·10연차) 버튼을 보여 주고, 뽑으면 GachaResultView로 결과를 띄운다.
/// 10연차는 할인 없이 가격 × 10이고, 골드가 모자라면 버튼이 꺼진다.
/// 서버 호출은 LobbyNetworkCoordinator, 아이콘 이름은 profile_icons.json, 그림은 Resources/ProfileIcons/{id}.png
/// 보유 아이콘 목록은 도감, 프로필 아이콘 변경은 내 정보 화면 담당
/// </summary>
public sealed class GachaPanel : UIBase
{
    // 화면을 처음 열 때 보여 줄 가격. 뽑은 뒤에는 서버 응답의 price로 맞춘다 (서버 GachaService.PRICE)
    private const int DefaultPrice = 200;

    [SerializeField] private TextAsset profileIconsJson;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button drawButton;
    [SerializeField] private TMP_Text drawButtonLabel;
    [SerializeField] private Button drawTenButton;
    [SerializeField] private TMP_Text drawTenButtonLabel;
    [SerializeField] private TMP_Text goldText;
    [SerializeField] private TMP_Text statusText;

    private ProfileIconCatalog catalog;
    private LobbyNetworkCoordinator coordinator;
    private int price = DefaultPrice;
    private int gold;
    private bool busy;

    public override void Init()
    {
        closeButton?.onClick.AddListener(Close);
        drawButton?.onClick.AddListener(OnDrawClicked);
        drawTenButton?.onClick.AddListener(OnDrawTenClicked);
    }

    private void OnDestroy()
    {
        closeButton?.onClick.RemoveListener(Close);
        drawButton?.onClick.RemoveListener(OnDrawClicked);
        drawTenButton?.onClick.RemoveListener(OnDrawTenClicked);
    }

    protected override async void OnOpen()
    {
        catalog ??= LoadCatalog();
        coordinator = FindFirstObjectByType<LobbyNetworkCoordinator>();

        ShowGold(coordinator != null ? coordinator.LocalGold : 0);
        ShowStatus(coordinator != null ? string.Empty : "서버 연결 정보가 없습니다. Lobby 씬에서 열어 주세요.");
        SetBusy(busy);
        if (coordinator != null)
        {
            await coordinator.WaitForLoginAsync();
            if (this != null && IsOpen && coordinator != null)
            {
                ShowGold(coordinator.LocalGold);
                SetBusy(busy);
            }
        }
    }

    private ProfileIconCatalog LoadCatalog()
    {
        if (profileIconsJson == null)
        {
            Debug.LogError("[Gacha] profileIconsJson(Assets/Data/profile_icons.json)을 연결해 주세요.", this);
            return null;
        }
        return new ProfileIconCatalog(JsonUtility.FromJson<ProfileIconDefinitionFile>(profileIconsJson.text));
    }

    private void OnDrawClicked() => Draw(1);

    private void OnDrawTenClicked() => Draw(GachaApi.TenDrawCount);

    private async void Draw(int count)
    {
        if (busy || coordinator == null) return;

        SetBusy(true);
        ShowStatus(string.Empty);
        try
        {
            GachaResultResponse result = await coordinator.DrawGachaAsync(count);
            if (this == null) return;

            int drawn = result.results?.Count ?? 1;
            if (result.price > 0 && drawn > 0) price = result.price / drawn;
            ShowGold(result.gold);

            UIManager uiManager = FindFirstObjectByType<UIManager>();
            GachaResultView view = uiManager != null ? uiManager.Open<GachaResultView>() : null;
            view?.Show(result, FindName, LoadSprite);
        }
        catch (Exception exception)
        {
            if (this == null) return;
            ShowStatus(ToUserMessage(exception));
        }
        finally
        {
            if (this != null) SetBusy(false);
        }
    }

    private void SetBusy(bool value)
    {
        busy = value;
        if (drawButton != null) drawButton.interactable = !value && coordinator != null;
        if (drawButtonLabel != null) drawButtonLabel.text = $"뽑기 ({price}골드)";

        int tenPrice = price * GachaApi.TenDrawCount;
        if (drawTenButton != null) drawTenButton.interactable = !value && coordinator != null && gold >= tenPrice;
        if (drawTenButtonLabel != null) drawTenButtonLabel.text = $"10연차 ({tenPrice:N0}골드)";
    }

    private void ShowGold(int value)
    {
        gold = value;
        if (goldText != null) goldText.text = $"{value:N0}";
    }

    private void ShowStatus(string message)
    {
        if (statusText != null) statusText.text = message ?? string.Empty;
    }

    private string FindName(string iconId)
    {
        return catalog != null && catalog.Contains(iconId) ? catalog.Find(iconId).name : iconId;
    }

    private static Sprite LoadSprite(string iconId)
    {
        Sprite sprite = Resources.Load<Sprite>(ProfileIconCatalog.SpritePath(iconId));
        if (sprite == null) Debug.LogWarning($"[Gacha] 아이콘 그림이 없습니다: Resources/{ProfileIconCatalog.SpritePath(iconId)}.png");
        return sprite;
    }

    private static string ToUserMessage(Exception exception)
    {
        if (exception is not ApiException api) return exception.Message;

        return api.StatusCode switch
        {
            0 => "서버에 연결할 수 없어요.",
            409 when api.ResponseBody != null && api.ResponseBody.Contains("NOT_ENOUGH_GOLD") => "골드가 부족해요.",
            401 => "로그인이 만료됐어요. 다시 로그인해 주세요.",
            _ => $"요청에 실패했어요. ({api.StatusCode})",
        };
    }
}
