public enum YutResult { BackDo, Do, Gae, Geol, Yut, Mo }

/*int steps   = result.Steps();
bool extra  = result.GrantsExtraThrow();
string name = result.DisplayName();*/
public static class YutResultExtensions
{
    public static int Steps(this YutResult r) => r switch
    {
        YutResult.BackDo => -1,
        YutResult.Do => 1,
        YutResult.Gae => 2,
        YutResult.Geol => 3,
        YutResult.Yut => 4,
        YutResult.Mo => 5,
        _ => 0
    };

    public static bool GrantsExtraThrow(this YutResult r) //추가 기회 체크
        => r == YutResult.Yut || r == YutResult.Mo;

    public static string DisplayName(this YutResult r) => r switch //표기할 이름 체크
    {
        YutResult.BackDo => "빽도",
        YutResult.Do => "도",
        YutResult.Gae => "개",
        YutResult.Geol => "걸",
        YutResult.Yut => "윷",
        YutResult.Mo => "모",
        _ => throw new System.ArgumentOutOfRangeException(nameof(r), r, null)
    };
}