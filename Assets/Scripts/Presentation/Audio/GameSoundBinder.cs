using System;

// 게임 이벤트(GameEvents)를 구독해 효과음을 재생한다
// GameFlowController가 GameEvents를 만들 때 함께 만들고, 파괴될 때 Dispose한다
// 로컬·온라인 모두 GameRules가 같은 이벤트를 발생시키므로 양쪽에서 똑같이 동작한다
public sealed class GameSoundBinder : IDisposable
{
    private readonly GameEvents events;

    public GameSoundBinder(GameEvents events)
    {
        this.events = events;
        events.Thrown += OnThrown;
        events.PieceMoved += OnPieceMoved;
    }

    public void Dispose()
    {
        events.Thrown -= OnThrown;
        events.PieceMoved -= OnPieceMoved;
    }

    // 던지기·다시 던지기 모두 Thrown으로 온다
    private void OnThrown(YutThrowOutcome outcome)
    {
        SoundManager.Play(SoundId.YutThrow);
    }

    // 이동 소리 + 잡았으면 잡기 소리 (여러 마리를 잡아도 한 번)
    private void OnPieceMoved(MoveResult result)
    {
        SoundManager.Play(SoundId.PieceMove);
        if (result.CapturedPieces.Count > 0) SoundManager.Play(SoundId.PieceCapture);
    }
}
