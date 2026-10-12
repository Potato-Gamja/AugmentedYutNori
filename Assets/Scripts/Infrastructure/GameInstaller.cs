using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum GameSessionMode
{
    Local,
    Remote,
}

/// <summary>
/// 게임 씬이 시작되기 전에 GameSetup과 세션 생성 방식을 GameFlowController에 전달한다.
/// </summary>
public class GameInstaller : MonoBehaviour, IGameSessionFactory
{
    [SerializeField] private GameFlowController gameFlowController;
    [SerializeField] private GameSessionMode defaultMode = GameSessionMode.Local;
    [SerializeField] private GameSetup localSetup = new GameSetup();

    private static GameStartMessage pendingGameStart;
    private static List<PlayerDisplayInfo> pendingPlayerProfiles = new();

    // RemoteGameSession 계약:
    // Network 담당이 RemoteGameSession 구현을 끝내면 앱 시작 시 이 Factory를 등록한다.
    // 입력으로 현재 Core 상태와 GAME_START 설정을 받고 IGameSession을 반환해야 한다.
    // GameState/TurnManager는 서버가 확정한 MOVE_APPLIED/TURN_CHANGED를 같은 Core에 적용할 때 필요하다.
    private static Func<GameRules, GameEvents, GameState, TurnManager, GameSetup, IGameSession> remoteSessionFactory;

    private GameSessionMode activeMode;
    private GameSetup activeSetup;

    public static void PrepareOnlineGame(GameStartMessage message)
    {
        pendingGameStart = message ?? throw new ArgumentNullException(nameof(message));
    }

    public static void PreparePlayerProfiles(IEnumerable<PlayerDisplayInfo> profiles)
    {
        pendingPlayerProfiles = profiles?.Select(profile => new PlayerDisplayInfo
        {
            playerId = profile.playerId,
            nickname = profile.nickname,
            team = profile.team,
            iconId = profile.iconId,
        }).ToList() ?? new List<PlayerDisplayInfo>();
    }

    public static void RegisterRemoteSessionFactory(
        Func<GameRules, GameEvents, GameState, TurnManager, GameSetup, IGameSession> factory)
    {
        remoteSessionFactory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    private void Awake()
    {
        if (gameFlowController == null) gameFlowController = FindFirstObjectByType<GameFlowController>();
        if (gameFlowController == null)
        {
            Debug.LogError("[GameInstaller] GameFlowController가 없습니다.", this);
            enabled = false;
            return;
        }

        if (pendingGameStart != null)
        {
            activeMode = GameSessionMode.Remote;
            activeSetup = pendingGameStart.ToGameSetup();
            pendingGameStart = null; // 이전 방 설정이 다음 게임에 재사용되지 않도록 한 번만 소비한다.
        }
        else
        {
            activeMode = defaultMode;
            activeSetup = localSetup ?? GameSetup.CreateLocalDefault();
        }

        if (activeMode == GameSessionMode.Remote &&
            (activeSetup.localTeam < 0 || activeSetup.localTeam >= activeSetup.teamCount))
        {
            Debug.LogError("[GameInstaller] 온라인 게임의 내 팀 정보가 없습니다. 로비에서 GAME_START를 받은 뒤 입장해 주세요.", this);
            gameFlowController.enabled = false;
            enabled = false;
            return;
        }

        if (activeMode == GameSessionMode.Remote && remoteSessionFactory == null)
        {
            Debug.LogError(
                "[GameInstaller] RemoteGameSession Factory가 등록되지 않았습니다. " +
                "Network 초기화 코드에서 RegisterRemoteSessionFactory를 호출해 주세요.", this);
            gameFlowController.enabled = false;
            enabled = false;
            return;
        }

        gameFlowController.Configure(activeSetup, this);
        gameFlowController.ConfigurePlayerProfiles(pendingPlayerProfiles);
        pendingPlayerProfiles.Clear();
    }

    public IGameSession Create(
        GameRules rules,
        GameEvents gameEvents,
        GameState gameState,
        TurnManager turnManager)
    {
        if (activeMode == GameSessionMode.Local)
            return new LocalGameSession(rules, gameEvents);

        return remoteSessionFactory(rules, gameEvents, gameState, turnManager, activeSetup);
    }
}
