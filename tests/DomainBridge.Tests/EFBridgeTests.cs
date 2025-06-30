using System.Data.Entity;

namespace DomainBridge.Tests;

public class EFBridgeTests
{
    [DomainBridge(typeof(DbContext))]
    public partial class EFBridge;

    [Test]
    public void Test()
    {
        var bridge = Tests.EFBridge.Create(() => null);
    }
}