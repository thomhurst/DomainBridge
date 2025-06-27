using System.Threading.Tasks;
using TUnit.Core;
using TUnit.Assertions;

namespace DomainBridge.SourceGenerators.Tests;

public class BasicGenerationTests
{
    [Test]
    public async Task GeneratesSimpleBridge()
    {
        var source = """
            using DomainBridge;
            
            namespace TestNamespace
            {
                [DomainBridge(typeof(SimpleService))]
                public partial class SimpleServiceBridge { }
                
                public class SimpleService
                {
                    public string GetMessage() => "Hello";
                    public int Count { get; set; }
                }
            }
            """;

        var result = TestHelper.RunGenerator(source);
        var output = TestHelper.GetGeneratedOutput(result);
        
        // Verify the bridge was generated
        await Assert.That(output).Contains("public partial class SimpleServiceBridge : global::System.MarshalByRefObject, global::System.IDisposable");
        await Assert.That(output).Contains("public string GetMessage()");
        await Assert.That(output).Contains("public int Count");
    }

    [Test]
    public async Task GeneratesBridgeWithInterfaces()
    {
        var source = """
            using DomainBridge;
            using System;
            
            namespace TestNamespace
            {
                [DomainBridge(typeof(ServiceWithInterfaces))]
                public partial class ServiceWithInterfacesBridge { }
                
                public interface IService
                {
                    void DoWork();
                }
                
                public interface IDisposableService : IDisposable
                {
                    void Cleanup();
                }
                
                public class ServiceWithInterfaces : IService, IDisposableService
                {
                    public void DoWork() { }
                    public void Cleanup() { }
                    public void Dispose() { }
                }
            }
            """;

        var result = TestHelper.RunGenerator(source);
        var output = TestHelper.GetGeneratedOutput(result);
        
        // Verify interfaces are implemented
        await Assert.That(output).Contains("IService");
        await Assert.That(output).Contains("IDisposableService");
        await Assert.That(output).Contains("public void DoWork()");
        await Assert.That(output).Contains("public void Cleanup()");
    }

    [Test]
    public async Task GeneratesBridgeWithEvents()
    {
        var source = """
            using DomainBridge;
            using System;
            
            namespace TestNamespace
            {
                [DomainBridge(typeof(EventService))]
                public partial class EventServiceBridge { }
                
                public class EventService
                {
                    public event EventHandler? DataChanged;
                    public event Action<string>? StatusUpdated;
                    
                    protected virtual void OnDataChanged(EventArgs e)
                    {
                        DataChanged?.Invoke(this, e);
                    }
                }
            }
            """;

        var result = TestHelper.RunGenerator(source);
        var output = TestHelper.GetGeneratedOutput(result);
        
        // Verify events are generated (without nullable annotations in current generator)
        await Assert.That(output).Contains("public event global::System.EventHandler DataChanged");
        await Assert.That(output).Contains("public event global::System.Action<string> StatusUpdated");
    }

    [Test]
    public async Task GeneratesBridgeWithConfiguration()
    {
        var source = """
            using DomainBridge;
            
            namespace TestNamespace
            {
                [DomainBridge(typeof(ConfiguredService), EnableShadowCopy = true)]
                public partial class ConfiguredServiceBridge { }
                
                public class ConfiguredService
                {
                    public void Execute() { }
                }
            }
            """;

        var result = TestHelper.RunGenerator(source);
        var output = TestHelper.GetGeneratedOutput(result);
        
        // Verify shadow copy configuration is applied
        await Assert.That(output).Contains("if (config.EnableShadowCopy)");
        await Assert.That(output).Contains("setup.ShadowCopyFiles = \"true\"");
    }

    [Test]
    public async Task GeneratesBridgeWithStaticInstance()
    {
        var source = """
            using DomainBridge;
            
            namespace TestNamespace
            {
                [DomainBridge(typeof(SingletonService))]
                public partial class SingletonServiceBridge { }
                
                public class SingletonService
                {
                    public static SingletonService Instance { get; } = new SingletonService();
                    
                    private SingletonService() { }
                    
                    public void DoWork() { }
                }
            }
            """;

        var result = TestHelper.RunGenerator(source);
        var output = TestHelper.GetGeneratedOutput(result);
        
        // Verify static Instance property is generated
        await Assert.That(output).Contains("public static SingletonServiceBridge Instance");
        await Assert.That(output).Contains("global::TestNamespace.SingletonService.Instance");
    }
}