# 직업 윷놀이 · 이승욱의 Unity 클라이언트 기여

2~4인 온라인 윷놀이에 직업별 특수 능력을 결합한 팀 프로젝트에서, 이승욱(Potato-Gamja / Gamja)이 담당한 클라이언트 코드와 공동 파일의 수정 내역을 정리한 브랜치입니다.

**이 브랜치는 코드 검토용입니다.** Unity에서 단독 실행하는 완성 프로젝트가 아닙니다. 팀의 규칙·데이터·네트워크 인터페이스, 씬·프리팹·아트·패키지·프로젝트 설정은 실행에 필요하지만 개인 코드 보관 범위에서 제외했습니다.

- [팀 클라이언트 원본 저장소](https://github.com/TeamYutNori/AugmentedYutNori)
- [팀 서버 저장소 · Spring Boot 멀티플레이 서버](https://github.com/TeamYutNori/AugmentedYutNori-Server)
- [전체 프로젝트 기준 소스](https://github.com/Potato-Gamja/AugmentedYutNori/tree/a4fca4d4dd238c8c4abda41502fc847fe8503d6d)
- [상세 포트폴리오](https://app.notion.com/p/400c52d0fe99805895efe66e07246260)
- [게임 플레이](https://changil.itch.io/yut)

## 담당 범위

- 게임 진행과 화면 연결: 상태 전환, 말 선택·이동과 보드·HUD 연출
- 세션 통합: 팀원이 구현한 `IGameSession`을 활용한 로컬·원격 요청과 결과 반영 흐름
- 안정성 개선: 방 참가 실패 이후 소켓 처리, 재접속 스냅샷과 이동 연출 충돌, 참가자 표시와 게임 종료 흐름
- UI·실행 환경: 공통 UI와 Editor 생성 도구, 모바일 입력, WebGL 닉네임 입력·방 코드 복사

서버·게임 규칙·증강·공통 네트워크 기반과 팀원 구현 전체를 개인 작업으로 표시하지 않습니다. 테스트 파일은 포함된 검증 코드의 사례이며, 팀 전체 테스트 수나 부하 테스트 결과를 개인 성과로 계산하지 않습니다.

팀 서버 저장소는 클라이언트 연동을 이해하기 위한 참고 자료입니다. 이 브랜치에는 클라이언트의 요청·이벤트 반영 및 오류 처리 기여를 정리했으며, 서버 구현 전체를 개인 담당 범위에 포함하지 않습니다.

## 구성

| 경로 | 내용 |
|---|---|
| [PersonalCode/](PersonalCode/) | 지정 담당 범위 중 이력상 팀원 기능 수정이 확인되지 않은 코드 64개. 원본 상대 경로와 내용을 보존 |
| [SharedChanges/](SharedChanges/) | 공동 수정 파일 41개에서 본인 관련 변경을 추린 커밋별 패치 47개 |
| [docs/CONTRIBUTIONS.md](docs/CONTRIBUTIONS.md) | 파일별 수록 방식, 원본 소스와 기여 커밋 링크 |
| [docs/manifest.json](docs/manifest.json) | 기준 커밋, 파일 목록·해시, 패치 대상, 제외한 가져오기 기록 |

## 코드 읽기

1. [말 선택 흐름](PersonalCode/Assets/Scripts/Flow/GameFlowSelectionController.cs), [포인터 입력](PersonalCode/Assets/Scripts/Flow/GamePointerInput.cs)
2. [방 UI](PersonalCode/Assets/Scripts/Presentation/UI/RoomPanel.cs), [재접속 UI](PersonalCode/Assets/Scripts/Presentation/UI/ReconnectPanel.cs), [공통 UI 연출](PersonalCode/Assets/Scripts/Presentation/UI/Common/)
3. [WebGL 닉네임 입력](PersonalCode/Assets/Plugins/WebGL/NicknameInput.jslib), [방 코드 복사](PersonalCode/Assets/Plugins/WebGL/RoomClipboard.jslib)
4. [공동 코드 변경](SharedChanges/README.md)에서 게임 진행·세션 연동 변경 확인

## 원본·작성 범위와 검증

기준은 `LSU-Client`의 [`a4fca4d4`](https://github.com/Potato-Gamja/AugmentedYutNori/commit/a4fca4d4dd238c8c4abda41502fc847fe8503d6d)입니다. 전체 팀 이력은 Git 기록에 남아 있고, 기본 브랜치의 전체 프로젝트는 유지됩니다. 이 브랜치의 최종 파일 구성만 개인 기여 열람용으로 정리했습니다.

분류는 합의한 담당 범위와 Git 이력을 근거로 한 보수적인 정리이며 독점 저작권 판정이 아닙니다. 팀원이 공동 수정한 파일은 전체 최신본 대신 원본 링크와 본인 변경 패치를 제공합니다. 동기화 커밋·가져오기·줄바꿈 정리는 구현 성과로 계산하지 않습니다.

개인 코드와 기준 커밋의 내용 일치, 패치의 원본 커밋·대상 파일, 내부 링크와 변경 파일 범위를 확인했습니다. 이 코드 발췌 브랜치에서는 Unity 컴파일·플레이 테스트를 실행하지 않았습니다. 실행 환경과 실제 검증 범위는 전체 프로젝트 및 상세 포트폴리오를 참고하세요.
