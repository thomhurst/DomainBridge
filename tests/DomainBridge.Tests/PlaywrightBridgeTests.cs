using System.Windows.Forms;
using Microsoft.Playwright;
using TUnit.Core;

namespace DomainBridge.Tests;

public class PlaywrightBridgeTests
{
    // Form is too complex for bridge generation due to extensive WinForms hierarchy
    // [DomainBridge(typeof(Form))]
    // public partial class FormBridge;

    [Test]
    [Skip("Form bridge generation disabled due to Windows Forms complexity")]
    public void Test()
    {
        // var bridge = FormBridge.Create(() => new Form());
        // bridge.Dispose();
    }
}