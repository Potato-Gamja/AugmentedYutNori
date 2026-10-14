// GameSetup.CheckScenePieces가 팀별 말 수 기준으로 검사하는지 확인하는 테스트
using System.Collections.Generic;
using NUnit.Framework;

public class GameSetupTests
{
    private static List<(int, int)> Pieces(int team0, int team1)
    {
        var list = new List<(int, int)>();
        for (int s = 0; s < team0; s++) list.Add((0, s));
        for (int s = 0; s < team1; s++) list.Add((1, s));
        return list;
    }

    [Test]
    public void 함수가_없으면_모든_팀_piecesPerTeam_기준()
    {
        var setup = new GameSetup { teamCount = 2, piecesPerTeam = 4 };
        Assert.IsEmpty(setup.CheckScenePieces(Pieces(4, 4)));
    }

    [Test]
    public void 노장_팀은_말_3개면_정상()
    {
        var setup = new GameSetup { teamCount = 2, piecesPerTeam = 4 };
        Assert.IsEmpty(setup.CheckScenePieces(Pieces(4, 3), t => t == 1 ? 3 : 4));
    }

    [Test]
    public void 노장_팀에_4번째_말이_있으면_범위_밖()
    {
        var setup = new GameSetup { teamCount = 2, piecesPerTeam = 4 };
        var errors = setup.CheckScenePieces(Pieces(4, 4), t => t == 1 ? 3 : 4);
        Assert.AreEqual(1, errors.Count);
    }
}