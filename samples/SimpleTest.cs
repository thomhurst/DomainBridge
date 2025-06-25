using DomainBridge;

namespace SimpleTest
{
    // Simple test case to isolate the generator issue
    [DomainBridge(typeof(TargetClass))]
    public partial class TargetClassBridge
    {
    }
    
    public class TargetClass
    {
        public string Name { get; set; } = "Test";
        
        public void DoSomething()
        {
            System.Console.WriteLine("Doing something");
        }
    }
}