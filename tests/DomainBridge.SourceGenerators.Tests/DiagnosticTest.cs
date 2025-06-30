using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TUnit.Assertions;
using TUnit.Core;

namespace DomainBridge.SourceGenerators.Tests
{
    public class DiagnosticTest
    {
        [Test]
        public async Task ReportsDBG201ForValueTypes()
        {
            var source = @"
using DomainBridge;
using System;

namespace TestNamespace
{
    public struct ValueType
    {
        public string Name { get; set; }
    }

    // Try to bridge a value type directly - this should generate a diagnostic
    [DomainBridge(typeof(ValueType))]
    public partial class ValueTypeBridge { }
}
";

            var compilation = await TestHelper.CreateCompilation(source);
            var generator = new DomainBridgePatternGenerator();
            var driver = CSharpGeneratorDriver.Create(generator);

            var result = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

            // Debug output
            Console.WriteLine($"Total diagnostics: {diagnostics.Length}");
            foreach (var diag in diagnostics)
            {
                Console.WriteLine($"Diagnostic: {diag.Id} - {diag.GetMessage()}");
            }

            // Check for DBG201 (value types)
            var dbg201 = diagnostics.FirstOrDefault(d => d.Id == "DBG201");
            await Assert.That(dbg201).IsNotNull();
            await Assert.That(dbg201!.GetMessage()).Contains("ValueType");
        }
    }
}