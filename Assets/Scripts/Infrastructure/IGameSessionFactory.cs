/// <summary>
/// GameFlowController가 Local/Remote 구현을 알지 않고 세션을 만들기 위한 생성 계약이다.
/// RemoteGameSession 담당자는 이 계약을 바꾸기보다 GameInstaller에 생성 함수를 등록한다.
/// </summary>
public interface IGameSessionFactory
{
    IGameSession Create(
        GameRules rules,
        GameEvents gameEvents,
        GameState gameState,
        TurnManager turnManager);
}
