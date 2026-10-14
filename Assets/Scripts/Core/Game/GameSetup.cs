using System;
using System.Collections;
using System.Collections.Generic;

[Serializable]
public class GameSetup
{
    public const int LocalModeTeam = -1;
    public const int TeamIdStride = 100;

    public int teamCount = 2;   //팀 수(2 이상)
    public int piecesPerTeam = 4;   // 팀당 말 수 
    public int localTeam = LocalModeTeam;   // -1이면 로컬, 아니면 기기가 조작하는 팀
    public bool allowSkipShortcut;  // 지름길 건너뛰기 허용
    public string boardLayoutId = "default";    //보드 레이아웃 키

    public static int PieceId(int team, int slot) => team * TeamIdStride + slot;
    public static int TeamOf(int pieceId) => pieceId / TeamIdStride;
    public static int SlotOf(int pieceId) => pieceId % TeamIdStride;

    public bool IsLocalMode => localTeam == LocalModeTeam;

    //이 기기가 해당 팀을 조작할 수 있는 지
    public bool CanControl(int team) => IsLocalMode || team == localTeam;

    //만들어야 할 말 목록. 팀 -> 슬롯 순. pieceCountOf가 없으면 모든 팀 piecesPerTeam
    public IEnumerable<(int id, int team, int slot)> EnumeratePieces(Func<int, int> pieceCountOf = null)
    {
        for (int team = 0; team < teamCount; team++)
        {
            int count = pieceCountOf?.Invoke(team) ?? piecesPerTeam;
            for (int slot = 0; slot < count; slot++)
                yield return (PieceId(team, slot), team, slot);
        }
    }
    
    // 설정값 검사
    public void Validate()
    {
        if (teamCount < 2) throw new ArgumentException($"teamCount는 2 이상이어야합니다 (현재 {teamCount})");
        if (piecesPerTeam < 1 || piecesPerTeam >= TeamIdStride) throw new ArgumentException($"piecesPerTeam은 1 ~ {TeamIdStride - 1} 사이여야 합니다. (현재 {piecesPerTeam})");
        if (localTeam != LocalModeTeam && (localTeam < 0 || localTeam >= teamCount)) throw new ArgumentException($"localTeam은 -1(로컬) 또는 0 ~ {teamCount - 1}이어야 합니다. (현재 {localTeam}");
        if (string.IsNullOrEmpty(boardLayoutId)) throw new ArgumentException("boardLayoutId가 비어 있습니다.");
    }

    public List<String> CheckScenePieces(IEnumerable<(int team, int slot)> scenePieces, Func<int, int> pieceCountOf = null)
    {
        var errors = new List<string>();
        var seen = new HashSet<int>();

        foreach(var (team, slot) in scenePieces)
        {
            if (team < 0 || team >= teamCount) { errors.Add($"팀 {team}은 teamCount({teamCount}) 범위 밖입니다."); continue; }

            int count = pieceCountOf?.Invoke(team) ?? piecesPerTeam;
            if (slot < 0 || slot >= count) errors.Add($"팀 {team}의 slotIndex {slot}이 0 ~ {count - 1} 범위 밖입니다.");
            else if (!seen.Add(PieceId(team, slot))) errors.Add($"팀 {team}의 slotIndex {slot}이 중복됩니다.");
        }

        foreach (var (id, team, slot) in EnumeratePieces(pieceCountOf))
        {
            if (!seen.Contains(id)) errors.Add($"팀 {team}의 slotIndex {slot} 말이 씬에 없습니다.");
        }

        return errors;
    }

    // 로컬용 기본 설정
    public static GameSetup CreateLocalDefault() => new GameSetup();
}
