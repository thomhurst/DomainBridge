using System.Windows.Forms;
using Microsoft.Playwright;

namespace DomainBridge.Tests;

public class PlaywrightBridgeTests
{
    [DomainBridge(typeof(Form))]
    public partial class FormBridge;

    [Test]
    public void Test()
    {
        var bridge = Tests.FormBridge.Create(() => null);
    }
}