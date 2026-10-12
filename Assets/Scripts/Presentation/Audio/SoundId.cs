// 소리 종류. SoundLibrarySO에서 종류별로 오디오 파일을 연결한다
// 새 소리가 필요하면 여기에 추가하고 SoundLibrary 에셋에 클립을 넣으면 된다 (중간 삽입 대신 끝에 추가: 에셋에 숫자로 저장됨)
public enum SoundId
{
    None = 0,

    // BGM
    LobbyBgm = 1,
    GameBgm = 2,

    // SFX
    YutThrow = 10,      // 윷 던지기
    PieceMove = 11,     // 말 이동
    PieceCapture = 12,  // 상대 말을 잡았을 때
    ResultPopup = 13,   // 결과창이 열릴 때
    ButtonClick = 14,   // 버튼 클릭 (필요한 곳에서 SoundManager.Play로 사용)
}
