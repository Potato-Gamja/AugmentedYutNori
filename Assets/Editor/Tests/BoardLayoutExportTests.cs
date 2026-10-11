using NUnit.Framework;
using UnityEditor;

// BoardLayoutSO → 서버 판 json 변환 테스트
// 서버 저장소 파일과의 실제 비교는 Tools/Yutnori/Board Layout/서버 json과 비교 메뉴로 한다
public class BoardLayoutExportTests
{
    [TestCase("BoardLayout_Default", "default")]
    [TestCase("BoardLayout_LongDiagonal", "long-diagonal")]
    [TestCase("BoardLayout_Clockwise", "clockwise")]
    [TestCase("BoardLayout_Half", "half")]
    [TestCase("MyBoard", "my-board")]
    public void ServerBoardId_FromAssetName(string assetName, string expected)
    {
        Assert.AreEqual(expected, BoardLayoutSO.ToServerBoardId(assetName));
    }

    [Test]
    public void DefaultBoard_ServerJsonFormat()
    {
        var layout = AssetDatabase.LoadAssetAtPath<BoardLayoutSO>("Assets/Data/BoardLayouts/BoardLayout_Default.asset");
        string json = layout.ToServerJson();

        StringAssert.StartsWith("{\n  \"startNodeId\": 0,\n  \"nodes\": [\n", json);
        StringAssert.Contains("    {\"id\": 0, \"type\": \"START\", \"next\": [1], \"prev\": [20], \"shortcut\": []},\n", json);
        StringAssert.Contains("    {\"id\": 23, \"type\": \"CENTER\", \"next\": [24, 28], \"prev\": [22, 27], \"shortcut\": [28]},\n", json);
        StringAssert.EndsWith("}\n  ]\n}\n", json);
        StringAssert.DoesNotContain("\r", json);
    }

    [Test]
    public void AllBoards_ValidAndUniqueIds()
    {
        var ids = new System.Collections.Generic.HashSet<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:BoardLayoutSO"))
        {
            var layout = AssetDatabase.LoadAssetAtPath<BoardLayoutSO>(AssetDatabase.GUIDToAssetPath(guid));
            Assert.IsTrue(layout.Definition.Validate(out string error), $"{layout.name}: {error}");
            Assert.IsTrue(ids.Add(layout.ServerBoardId), $"{layout.name}: 서버 id '{layout.ServerBoardId}'가 겹칩니다.");
        }
        Assert.IsTrue(ids.Contains("default"), "서버 기본 판(default)이 있어야 합니다.");
    }
}
