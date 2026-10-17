using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>서버 프로필과 보유 아이콘을 표시하고 선택한 아이콘을 저장한다.</summary>
public sealed class ProfilePanel : UIBase
{
    [SerializeField] private Button closeButton;
    [SerializeField] private Image profileIcon;
    [SerializeField] private TMP_Text iconPlaceholder;
    [SerializeField] private TMP_Text nicknameText;
    [SerializeField] private TMP_Text playerIdText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Button logoutButton;
    [SerializeField] private Button withdrawButton;
    [SerializeField] private TMP_Text goldText;
    [SerializeField] private TextAsset profileIconsJson;
    [SerializeField] private Button iconOptionTemplate;
    [SerializeField] private Transform iconOptionContainer;
    [SerializeField] private ScrollRect iconScrollRect;

    private ProfileIconOption[] iconOptions = Array.Empty<ProfileIconOption>();

    [Serializable]
    private sealed class ProfileIconOption
    {
        public string iconId;
        public Button button;
        public Image icon;
        public TMP_Text stateText;
        [NonSerialized] public UnityEngine.Events.UnityAction listener;
    }

    private readonly HashSet<string> ownedIconIds = new();
    private LobbyNetworkCoordinator coordinator;
    private string selectedIconId;
    private bool busy;
    private int requestVersion;

    public event Action LogoutRequested;
    // 탈퇴 버튼을 두 번(확인) 눌렀을 때
    public event Action WithdrawRequested;

    // 탈퇴는 되돌릴 수 없어서, 첫 번째 누름은 경고만 보여 주고 이 시간 안에 한 번 더 눌러야 탈퇴한다
    private const float WithdrawConfirmSeconds = 5f;
    private const string WithdrawConfirmLabel = "한 번 더 누르면 탈퇴";
    private float withdrawArmedUntil = -1f;
    private TMP_Text withdrawLabel;
    private string withdrawLabelDefault;

    public override void Init()
    {
        BuildIconOptions();
        closeButton?.onClick.AddListener(Close);
        logoutButton?.onClick.AddListener(OnLogoutClicked);
        withdrawButton?.onClick.AddListener(OnWithdrawClicked);
        withdrawLabel = withdrawButton != null ? withdrawButton.GetComponentInChildren<TMP_Text>(true) : null;
        withdrawLabelDefault = withdrawLabel != null ? withdrawLabel.text : null;
        foreach (ProfileIconOption option in iconOptions)
        {
            if (option.icon != null)
                option.icon.sprite = Resources.Load<Sprite>(ProfileIconCatalog.SpritePath(option.iconId));
            option.listener = () => OnIconClicked(option.iconId);
            option.button?.onClick.AddListener(option.listener);
        }
        RefreshIconOptions();
    }

    private void BuildIconOptions()
    {
        if (profileIconsJson == null || iconOptionTemplate == null || iconOptionContainer == null)
        {
            Debug.LogWarning("[Profile] 아이콘 카탈로그 또는 카드 템플릿 참조가 없습니다.", this);
            return;
        }

        ProfileIconCatalog catalog = new ProfileIconCatalog(
            JsonUtility.FromJson<ProfileIconDefinitionFile>(profileIconsJson.text));
        var options = new List<ProfileIconOption>();
        foreach (ProfileIconDefinition definition in catalog.Definitions)
        {
            Button button = Instantiate(iconOptionTemplate, iconOptionContainer);
            button.name = $"ProfileIconOption_{definition.id}";
            button.gameObject.SetActive(true);
            TMP_Text name = button.transform.Find("IconName").GetComponent<TMP_Text>();
            name.text = definition.name;
            options.Add(new ProfileIconOption
            {
                iconId = definition.id,
                button = button,
                icon = button.transform.Find("Icon").GetComponent<Image>(),
                stateText = button.transform.Find("SelectionState").GetComponent<TMP_Text>(),
            });
        }
        iconOptions = options.ToArray();
    }

    private void OnDestroy()
    {
        closeButton?.onClick.RemoveListener(Close);
        logoutButton?.onClick.RemoveListener(OnLogoutClicked);
        withdrawButton?.onClick.RemoveListener(OnWithdrawClicked);
        foreach (ProfileIconOption option in iconOptions)
            if (option.listener != null) option.button?.onClick.RemoveListener(option.listener);
    }

    protected override async void OnOpen()
    {
        DisarmWithdraw();
        if (iconScrollRect != null) iconScrollRect.verticalNormalizedPosition = 1f;
        int version = ++requestVersion;
        coordinator = FindFirstObjectByType<LobbyNetworkCoordinator>();
        ownedIconIds.Clear();
        selectedIconId = null;
        ShowProfile(coordinator?.LocalPlayerId, coordinator?.LocalNickname);
        if (goldText != null) goldText.text = "—";
        SetBusy(true);
        ShowStatus("프로필과 보유 아이콘을 불러오는 중입니다.");
        try
        {
            if (coordinator == null) throw new InvalidOperationException("로그인 정보를 찾을 수 없습니다.");
            Task<PlayerProfileResponse> profileTask = coordinator.GetProfileAsync();
            Task<OwnedIconsResponse> ownedTask = coordinator.GetOwnedIconsAsync();
            await Task.WhenAll(profileTask, ownedTask);
            if (this == null || !IsOpen || version != requestVersion) return;
            foreach (string id in ownedTask.Result.iconIds) ownedIconIds.Add(id);
            ApplyProfile(profileTask.Result);
            ShowStatus(ownedIconIds.Count == 0
                ? "보유한 아이콘이 없습니다. 뽑기에서 아이콘을 획득해 주세요."
                : "보유한 아이콘을 선택하면 프로필에 적용됩니다.");
        }
        catch (Exception exception)
        {
            if (this != null && IsOpen && version == requestVersion)
                ShowStatus(ToUserMessage(exception));
        }
        finally
        {
            if (this != null && version == requestVersion) SetBusy(false);
        }
    }

    protected override void OnClose() => requestVersion++;

    public void ShowProfile(string playerId, string nickname, Sprite icon = null)
    {
        if (nicknameText != null)
            nicknameText.text = string.IsNullOrWhiteSpace(nickname) ? "닉네임 미설정" : nickname;
        if (playerIdText != null)
            playerIdText.text = string.IsNullOrWhiteSpace(playerId) ? "플레이어 ID 없음"
                : playerId.Length > 12 ? $"{playerId[..4]}…{playerId[^4..]}" : playerId;
        if (profileIcon != null)
        {
            profileIcon.sprite = icon;
            profileIcon.enabled = icon != null;
            profileIcon.preserveAspect = true;
            profileIcon.color = Color.white;
        }
        if (iconPlaceholder != null) iconPlaceholder.gameObject.SetActive(icon == null);
        if (statusText != null)
            statusText.text = string.Empty;
    }

    private void ApplyProfile(PlayerProfileResponse profile)
    {
        selectedIconId = profile.iconId;
        Sprite icon = string.IsNullOrWhiteSpace(profile.iconId)
            ? null : Resources.Load<Sprite>(ProfileIconCatalog.SpritePath(profile.iconId));
        ShowProfile(coordinator?.LocalPlayerId, profile.nickname, icon);
        if (goldText != null) goldText.text = $"{profile.gold:N0}";
        RefreshIconOptions();
    }

    private async void OnIconClicked(string iconId)
    {
        if (busy || coordinator == null || iconId == selectedIconId || !ownedIconIds.Contains(iconId)) return;
        int version = requestVersion;
        SetBusy(true);
        ShowStatus("프로필 아이콘을 저장하는 중입니다.");
        try
        {
            PlayerProfileResponse profile = await coordinator.ChangeProfileIconAsync(iconId);
            if (this == null || !IsOpen || version != requestVersion) return;
            ApplyProfile(profile);
            ShowStatus("프로필 아이콘을 변경했습니다.");
        }
        catch (Exception exception)
        {
            if (this != null && IsOpen && version == requestVersion)
                ShowStatus(ToUserMessage(exception));
        }
        finally
        {
            if (this != null && version == requestVersion) SetBusy(false);
        }
    }

    private void SetBusy(bool value)
    {
        busy = value;
        if (closeButton != null) closeButton.interactable = !value;
        if (logoutButton != null) logoutButton.interactable = !value;
        if (withdrawButton != null) withdrawButton.interactable = !value;
        RefreshIconOptions();
    }

    private void RefreshIconOptions()
    {
        foreach (ProfileIconOption option in iconOptions)
        {
            bool owned = ownedIconIds.Contains(option.iconId);
            bool selected = option.iconId == selectedIconId;
            if (option.button != null)
            {
                option.button.interactable = !busy && owned && !selected;
                if (option.button.targetGraphic != null)
                    option.button.targetGraphic.color = Color.white;
            }
            if (option.icon != null) option.icon.color = owned || selected ? Color.white : new Color(0.3f, 0.3f, 0.3f);
            if (option.stateText != null)
                option.stateText.text = selected ? "사용 중" : owned ? "선택" : "미보유";
            option.button?.GetComponent<YutNori.UI.Common.ProfileIconOptionPresentation>()?.Show(selected, owned);
        }
    }

    public void SetLogoutBusy(bool busy)
    {
        SetBusy(busy);
    }

    public void ShowStatus(string message)
    {
        if (statusText != null) statusText.text = message ?? string.Empty;
    }

    private void OnWithdrawClicked()
    {
        if (busy) return;

        if (Time.unscaledTime > withdrawArmedUntil)
        {
            // 첫 번째 누름: 버튼 글자를 바꿔서 확인을 받는다 (경고 문구는 아래 상태 줄에도 남긴다)
            withdrawArmedUntil = Time.unscaledTime + WithdrawConfirmSeconds;
            if (withdrawLabel != null) withdrawLabel.text = WithdrawConfirmLabel;
            ShowStatus("탈퇴하면 닉네임·골드·아이콘·업적이 모두 삭제되고 되돌릴 수 없어요. 계속하려면 버튼을 한 번 더 눌러 주세요.");
            return;
        }

        DisarmWithdraw();
        WithdrawRequested?.Invoke();
    }

    // 확인 시간이 지나면 버튼 글자를 되돌린다
    private void Update()
    {
        if (withdrawArmedUntil > 0f && Time.unscaledTime > withdrawArmedUntil) DisarmWithdraw();
    }

    private void DisarmWithdraw()
    {
        withdrawArmedUntil = -1f;
        if (withdrawLabel != null && withdrawLabelDefault != null) withdrawLabel.text = withdrawLabelDefault;
    }

    // 탈퇴 요청 중 버튼 잠금 (로그아웃과 같은 처리)
    public void SetWithdrawBusy(bool value)
    {
        SetBusy(value);
    }

    private void OnLogoutClicked() => LogoutRequested?.Invoke();

    private static string ToUserMessage(Exception exception)
    {
        if (exception is not ApiException api) return exception.Message;
        return api.StatusCode switch
        {
            0 => "서버에 연결할 수 없습니다.",
            401 => "로그인이 만료되었습니다. 다시 로그인해 주세요.",
            404 => "서버의 프로필 기능이 아직 반영되지 않았습니다.",
            _ => $"프로필 요청에 실패했습니다. ({api.StatusCode})",
        };
    }
}
