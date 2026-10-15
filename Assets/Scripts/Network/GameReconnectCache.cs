using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

// 이 기기가 실제 참가했던 경기 정보만 보관한다. 로그인 토큰은 저장하지 않는다.
public sealed class GameReconnectData
{
    public string playerId;
    public string serverUrl;
    public GameStartMessage setup;
    public List<PlayerDisplayInfo> profiles = new();

    public bool Matches(string player, string server)
    {
        if (playerId != player || string.IsNullOrEmpty(player) || setup == null ||
            string.IsNullOrWhiteSpace(setup.roomCode) ||
            !string.Equals(serverUrl?.TrimEnd('/'), server?.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)) return false;
        try { setup.ToGameSetup().Validate(); }
        catch (Exception) { return false; }
        return setup.localTeam >= 0 && setup.localTeam < setup.teamCount && profiles != null &&
               profiles.Any(profile => profile != null && profile.playerId == player && profile.team == setup.localTeam);
    }

    public bool CanRestore(GameSnapshotMessage snapshot) => snapshot != null && setup != null &&
        (snapshot.phase == "PLAYING" || snapshot.phase == "AUGMENT_SELECT") &&
        snapshot.myTeam == setup.localTeam && snapshot.pieces != null &&
        // 증강 선택 중에는 아직 말을 생성하지 않는다. 참가 여부·퇴장은 서버가 검증한다.
        (snapshot.phase == "AUGMENT_SELECT" ||
         snapshot.pieces.Any(piece => piece != null && piece.team == snapshot.myTeam));
}

public sealed class GameReconnectCache
{
    private readonly string key;
    public GameReconnectCache(string key) => this.key = key;
    public void Save(GameReconnectData data)
    {
        PlayerPrefs.SetString(key, JsonConvert.SerializeObject(data));
        PlayerPrefs.Save();
    }
    public GameReconnectData Load(string playerId, string serverUrl)
    {
        if (!PlayerPrefs.HasKey(key)) return null;
        try
        {
            var data = JsonConvert.DeserializeObject<GameReconnectData>(PlayerPrefs.GetString(key));
            return data != null && data.Matches(playerId, serverUrl) ? data : null;
        }
        catch (Exception) { return null; }
    }
    public void Clear()
    {
        PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();
    }
}

// 서버의 복귀 불가 ERROR 코드. 일시적인 통신 실패와 구분해 저장 정보를 지운다.
public static class ReconnectFailure
{
    public static bool IsTerminal(string code) => code == "REMOVED_FROM_GAME" ||
        code == "GAME_ALREADY_ENDED" || code == "GAME_NOT_FOUND" || code == "PLAYER_NOT_IN_GAME";
}
