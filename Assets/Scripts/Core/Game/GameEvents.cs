using UnityEngine;
using System;
    
public class GameEvents
{
    // 윷 던졌을 때 YutResult 하나 전달하는 이벤트
    public event Action<YutResult> Thrown;
    // 말 이동, 아웃, 완주 시 발생 이벤트
    public event Action<MoveResult> PieceMoved;
    public event Action<Piece> PieceCaptured;
    public event Action<Piece> PieceFinished;
    public event Action<int> TurnChanged;
    // 증강을 선택했을 때 발생하는 이벤트
    public event Action<IAugment> AugmentSelected;
    // 게임이 끝났을 때 발생하는 이벤트 (int는 승리팀 번호)
    public event Action<int> GameEnded;

    // Thrown 이벤트 직접 호출하는 메서드
    // events.RaiseThrown(result)로 여러 곳에서 직접 호출하는 대신 통일해서 사용 가능
    public void RaiseThrown(YutResult result)
    {
        Thrown?.Invoke(result);
    }

    public void RaisePieceMoved(MoveResult result)
    {
        PieceMoved?.Invoke(result);
    }

    public void RaisePieceCaptured(Piece piece)
    {
        PieceCaptured?.Invoke(piece);
    }

    public void RaisePieceFinished(Piece piece)
    {
        PieceFinished?.Invoke(piece);
    }

    public void RaiseTurnChanged(int team)
    {
        TurnChanged?.Invoke(team);
    }

    // 증강 선택이 끝났을 때 어떤 증강을 골랐는지
    public void RaiseAugmentSelected(IAugment augment)
    {
        AugmentSelected?.Invoke(augment);
    }

    // 게임이 끝났을 때 승리 팀 번호를 전달
    public void RaiseGameEnded(int winnerTeam)
    {
        GameEnded?.Invoke(winnerTeam);
    }
}
