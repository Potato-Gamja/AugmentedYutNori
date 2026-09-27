using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// GameFlowController가 생기기 전까지 쓰는 ResultPopup 임시 테스트 스크립트.
/// - 1~4: 해당 플레이어 승리로 팝업 표시
/// - H: 팝업 숨기기
/// - C: Controller 연결 켜기/끄기 (RestartClicked 구독 여부)
///      켜짐 → 다시 하기 누르면 로그만 출력 (Controller가 처리하는 경우)
///      꺼짐 → 다시 하기 누르면 씬 다시 로드 (MVP 기본 동작)
/// </summary>
public class ResultPopupTester : MonoBehaviour
{
    [SerializeField] private ResultPopup resultPopup;

    private static readonly string[] Names = { "player 1", "player 2", "player 3", "player 4" };
    private static readonly Color[] Colors = { Color.red, Color.blue, Color.green, Color.yellow };

    private bool _controllerConnected;

    private void Start()
    {
        if (resultPopup == null)
        {
            resultPopup = FindFirstObjectByType<ResultPopup>();
            if (resultPopup == null) { Debug.LogError("[PopupTester] 씬에 ResultPopup이 없습니다."); enabled = false; return; }
        }

        Debug.Log("[PopupTester] 1~4: 승자 표시 / H: 숨기기 / C: Controller 연결 전환 (현재: 꺼짐 → 씬 재로드)");
    }

    private void OnDestroy()
    {
        if (resultPopup != null) resultPopup.RestartClicked -= OnRestart;
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
            resultPopup.Hide();
            Debug.Log("[PopupTester] 숨기기");
        }

        if (kb.cKey.wasPressedThisFrame) ToggleController();
    }

    private void ShowWinner(int index)
    {
        resultPopup.Show(Names[index], Colors[index]);
        Debug.Log($"[PopupTester] {Names[index]} 승리 표시 (IsShowing: {resultPopup.IsShowing})");
    }

    /// <summary>Controller가 RestartClicked를 구독한 상황을 흉내낸다.</summary>
    private void ToggleController()
    {
        _controllerConnected = !_controllerConnected;

        if (_controllerConnected) resultPopup.RestartClicked += OnRestart;
        else resultPopup.RestartClicked -= OnRestart;

        Debug.Log(_controllerConnected
            ? "[PopupTester] Controller 연결: 켜짐 → 다시 하기 누르면 로그만 출력"
            : "[PopupTester] Controller 연결: 꺼짐 → 다시 하기 누르면 씬 재로드");
    }

    private void OnRestart()
    {
        Debug.Log("[PopupTester] RestartClicked 받음 (Controller라면 여기서 게임 재시작)");
    }
}