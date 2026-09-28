using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

/// <summary>
/// GameFlowController가 생기기 전까지 쓰는 UIManager + ResultPopup 임시 테스트 스크립트.
/// - 1~4: 해당 플레이어 승리로 팝업 열기
/// - H: 팝업 닫기
/// - A: 모든 UI 닫기 (CloseAll)
/// - C: Controller 연결 켜기/끄기 (RestartClicked 구독 여부)
///      켜짐 → 다시 하기 누르면 로그만 출력
///      꺼짐 → 다시 하기 누르면 씬 다시 로드 (MVP 기본 동작)
/// </summary>
public class ResultPopupTester : MonoBehaviour
{
    [SerializeField] private UIManager uiManager;

    private static readonly string[] Names = { "player 1", "player 2", "player 3", "player 4" };
    private static readonly Color[] Colors = { Color.red, Color.blue, Color.green, Color.yellow };

    private ResultPopup _popup;   // 이벤트 구독·해제용 캐시
    private bool _controllerConnected;

    private void Start()
    {
        if (uiManager == null)
        {
            uiManager = FindFirstObjectByType<UIManager>();
            if (uiManager == null) { Debug.LogError("[PopupTester] 씬에 UIManager가 없습니다."); enabled = false; return; }
        }

        // 열지 않고 인스턴스만 가져온다 → 프리팹 등록·생성이 제대로 되는지 확인
        _popup = uiManager.Get<ResultPopup>();
        if (_popup == null) { enabled = false; return; }   // 에러 로그는 UIManager가 출력

        _popup.Closed += OnPopupClosed;

        Debug.Log($"[PopupTester] 생성 확인: {_popup.name}, IsOpen: {_popup.IsOpen} (false여야 정상)");
        Debug.Log("[PopupTester] 1~4: 승자 표시 / H: 닫기 / A: 전체 닫기 / C: Controller 연결 전환 (현재: 꺼짐)");
    }

    private void OnDestroy()
    {
        if (_popup == null) return;
        _popup.Closed -= OnPopupClosed;
        _popup.RestartClicked -= OnRestart;
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.digit1Key.wasPressedThisFrame) ShowWinner(0);
        if (kb.digit2Key.wasPressedThisFrame) ShowWinner(1);
        if (kb.digit3Key.wasPressedThisFrame) ShowWinner(2);
        if (kb.digit4Key.wasPressedThisFrame) ShowWinner(3);

        if (kb.hKey.wasPressedThisFrame)
        {
            uiManager.Close<ResultPopup>();
            Debug.Log($"[PopupTester] Close<ResultPopup> → IsOpen: {uiManager.IsOpen<ResultPopup>()}");
        }

        if (kb.aKey.wasPressedThisFrame)
        {
            uiManager.CloseAll();
            Debug.Log("[PopupTester] CloseAll");
        }

        if (kb.cKey.wasPressedThisFrame) ToggleController();
    }

    private void ShowWinner(int index)
    {
        // Open은 매번 같은 인스턴스를 반환해야 한다 (캐싱 확인)
        var popup = uiManager.Open<ResultPopup>().SetWinner(Names[index], Colors[index]);
        Debug.Log($"[PopupTester] {Names[index]} 승리 표시 / 같은 인스턴스: {popup == _popup} / IsOpen: {popup.IsOpen}");
    }

    /// <summary>Controller가 RestartClicked를 구독한 상황을 흉내낸다.</summary>
    private void ToggleController()
    {
        _controllerConnected = !_controllerConnected;

        if (_controllerConnected) _popup.RestartClicked += OnRestart;
        else _popup.RestartClicked -= OnRestart;

        Debug.Log(_controllerConnected
            ? "[PopupTester] Controller 연결: 켜짐 → 다시 하기 누르면 로그만 출력"
            : "[PopupTester] Controller 연결: 꺼짐 → 다시 하기 누르면 씬 재로드");
    }

    private void OnRestart()
    {
        Debug.Log("[PopupTester] RestartClicked 받음 (Controller라면 여기서 게임 재시작)");
    }

    private void OnPopupClosed(UIBase ui)
    {
        Debug.Log($"[PopupTester] Closed 이벤트 받음: {ui.name}");
    }
}