using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TUnit.Core;
using TUnit.Assertions;

namespace DomainBridge.SourceGenerators.Tests
{
    public class AsyncMethodTest
    {
        [Test]
        public async Task GeneratesCleanAsyncMethodsWithoutSyncWrappers()
        {
            var source = @"
using DomainBridge;
using System.Threading.Tasks;

namespace TestNamespace
{
    public class AsyncService
    {
        public async Task DoWorkAsync()
        {
            await Task.Delay(1);
        }
        
        public async Task<string> GetDataAsync()
        {
            await Task.Delay(1);
            return ""data"";
        }
    }
    
    [DomainBridge(typeof(AsyncService))]
    public partial class AsyncServiceBridge { }
}";

            var compilation = await TestHelper.CreateCompilation(source);
            var generator = new DomainBridgePatternGenerator();
            var driver = CSharpGeneratorDriver.Create(generator);

            var result = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

            var bridgeTree = outputCompilation.SyntaxTrees.FirstOrDefault(t => t.FilePath.Contains("AsyncServiceBridge"));
            await Assert.That(bridgeTree).IsNotNull();

            var bridgeSource = bridgeTree.ToString();
            Console.WriteLine("Generated bridge source:");
            Console.WriteLine(bridgeSource);

            // Verify async methods are generated directly
            await Assert.That(bridgeSource).Contains("public async global::System.Threading.Tasks.Task DoWorkAsync()");
            await Assert.That(bridgeSource).Contains("public async global::System.Threading.Tasks.Task<string> GetDataAsync()");
            
            // Verify direct async calls without sync wrappers
            await Assert.That(bridgeSource).Contains("await _instance.DoWorkAsync()");
            await Assert.That(bridgeSource).Contains("await _instance.GetDataAsync()");
            
            // Verify NO sync wrapper methods are generated
            await Assert.That(bridgeSource).DoesNotContain("__DomainBridge_Sync_");
        }
    }
}