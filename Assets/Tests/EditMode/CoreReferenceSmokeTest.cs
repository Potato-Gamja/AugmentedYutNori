using NUnit.Framework;

public class CoreReferenceSmokeTest
{
    [Test]
    public void CanSeeCoreTypes()
    {
        Assert.IsTrue(typeof(PieceState).IsEnum);
    }
}
