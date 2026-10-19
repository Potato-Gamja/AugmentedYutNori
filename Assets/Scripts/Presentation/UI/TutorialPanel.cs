using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>로비에서 읽는 기본 규칙 안내. 게임 상태나 서버에 영향을 주지 않는다.</summary>
public sealed class TutorialPanel : UIBase
{
    [SerializeField] private Button closeButton;
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private TMP_Text nextButtonText;
    [SerializeField] private TMP_Text pageTitle;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private TMP_Text pageNumber;
    [SerializeField] private Image pageImage;
    [SerializeField] private Sprite[] pageImages;
    [SerializeField] private TMP_Text imageCaption;

    private static readonly string[] Titles =
    {
        "01  대기실에서 준비하기", "02  나만의 직업 선택하기", "03  내 차례에 윷 던지기",
        "04  말과 목적지 선택하기", "05  업기·잡기·골인 알아보기"
    };
    private static readonly string[] Descriptions =
    {
        "2~4명이 함께 플레이할 수 있어요.\n\n친구에게 방 코드를 알려 주세요.\n‘코드 복사’ 버튼으로 쉽게 공유할 수 있어요.\n\n‘준비’를 누르고 다른 참가자를 기다리세요.\n전원 준비 후 방장이 ‘게임 시작’을 눌러요.",
        "내 화면에 표시된 후보 중\n원하는 직업 카드 하나를 선택하세요.\n\n카드에는 이름과 효과가 적혀 있어요.\n선택한 직업은 이번 경기의 규칙에 영향을 줘요.\n\n위쪽 타이머에서 남은 시간을 확인하세요.\n자세한 효과는 로비의 ‘직업’에서 볼 수 있어요.",
        "위쪽 안내창에서 내 차례인지 확인하세요.\n그림의 빨간 표시 안에 있는\n‘윷 던지기’ 버튼을 누르세요.\n\n도 1칸 · 개 2칸 · 걸 3칸\n윷 4칸 · 모 5칸 · 빽도는 뒤로 1칸이에요.\n\n윷·모는 한 번 더 던질 수 있어요.\n직업에 따라 결과와 이동 규칙이 달라져요.",
        "왼쪽 패널의 대기 중인 말을 선택하세요.\n판 위에 있는 내 말도 선택할 수 있어요.\n\n강조된 목적지를 누르면 이동해요.\nPC에서는 클릭, 모바일에서는 터치하세요.\n\n한 위치에 여러 선택지가 있으면\n원하는 이동을 골라 주세요.\n출발 칸 도착과 골인은 서로 다른 선택이에요.",
        "내 말이 같은 칸에 모이면 업어서 함께 이동해요.\n상대 말이 있는 칸에 도착하면 잡을 수 있어요.\n잡힌 말은 대기석으로 돌아가요.\n\n내 말을 모두 골인시키는 것이 목표예요.\n왼쪽 플레이어 패널에서 완주 수를 확인하세요.\n\n말은 기본 4개, 노장 직업은 3개예요.\n잡기와 추가 던지기도 직업에 따라 달라져요."
    };
    private static readonly string[] Captions =
    {
        "대기실의 방 코드와 준비·게임 시작 버튼을 확인하세요.",
        "설명을 읽고 원하는 직업 카드를 선택하세요.",
        "빨간 표시: 내 차례에 누르는 윷 던지기 버튼",
        "빨간 표시: 대기 중인 말 · 판 위 원형 강조: 이동 목적지",
        "플레이어 패널에서 완주 수와 보유 직업을 확인할 수 있어요."
    };

    public int PageIndex { get; private set; }
    public int PageCount => Titles.Length;

    public override void Init()
    {
        closeButton?.onClick.AddListener(Close);
        previousButton?.onClick.AddListener(Previous);
        nextButton?.onClick.AddListener(Next);
    }

    private void OnDestroy()
    {
        closeButton?.onClick.RemoveListener(Close);
        previousButton?.onClick.RemoveListener(Previous);
        nextButton?.onClick.RemoveListener(Next);
    }

    protected override void OnOpen()
    {
        PageIndex = 0;
        Refresh();
    }

    private void Previous()
    {
        PageIndex = Mathf.Max(0, PageIndex - 1);
        Refresh();
    }

    private void Next()
    {
        if (PageIndex == PageCount - 1) { Close(); return; }
        PageIndex++;
        Refresh();
    }

    private void Refresh()
    {
        if (pageTitle != null) pageTitle.text = Titles[PageIndex];
        if (pageImage != null)
        {
            pageImage.sprite = pageImages != null && PageIndex < pageImages.Length ? pageImages[PageIndex] : null;
            pageImage.enabled = pageImage.sprite != null;
            pageImage.preserveAspect = true;
        }
        if (imageCaption != null) imageCaption.text = Captions[PageIndex];
        if (bodyText != null) bodyText.text = Descriptions[PageIndex];
        if (pageNumber != null) pageNumber.text = $"{PageIndex + 1} / {PageCount}";
        if (previousButton != null) previousButton.interactable = PageIndex > 0;
        if (nextButtonText != null) nextButtonText.text = PageIndex == PageCount - 1 ? "닫기" : "다음";
    }
}
