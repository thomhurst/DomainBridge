using System;
using System.Threading.Tasks;
using TUnit.Core;
using TUnit.Assertions;

namespace DomainBridge.Tests
{
    /// <summary>
    /// Test the new interface-first approach with [AppDomainBridgeable]
    /// </summary>
    public class InterfaceFirstTests
    {
        [AppDomainBridgeable]
        public interface ISimpleService
        {
            string GetMessage();
            int Add(int a, int b);
        }

        [AppDomainBridgeable]
        public interface IUserService
        {
            Task<string> GetUserDataAsync();
            Task ProcessUserAsync(string data);
        }

        public class SimpleServiceImpl : MarshalByRefObject, ISimpleService
        {
            public string GetMessage() => "Hello from isolated domain!";
            public int Add(int a, int b) => a + b;
        }

        public class UserServiceImpl : MarshalByRefObject, IUserService
        {
            public async Task<string> GetUserDataAsync()
            {
                await Task.Delay(10);
                return "User data from isolated domain";
            }

            public async Task ProcessUserAsync(string data)
            {
                await Task.Delay(10);
                // Processing complete
            }
        }

        [Test]
        public async Task SimpleInterfaceBridge_ShouldWork()
        {
            using var bridge = SimpleServiceBridge.Create(() => new SimpleServiceImpl());
            
            var message = bridge.GetMessage();
            var sum = bridge.Add(5, 3);
            
            await Assert.That(message).IsEqualTo("Hello from isolated domain!");
            await Assert.That(sum).IsEqualTo(8);
        }

        [Test]
        [Skip("Async methods cannot work across AppDomain boundaries - Tasks are not serializable")]
        public async Task UserInterfaceBridge_ShouldWork()
        {
            // This test documents a known limitation with async methods across AppDomains
            // The interface-first approach still suffers from the same limitation as the legacy approach
            using var bridge = UserServiceBridge.Create(() => new UserServiceImpl());
            
            var data = await bridge.GetUserDataAsync();
            await bridge.ProcessUserAsync("test data");
            
            await Assert.That(data).IsEqualTo("User data from isolated domain");
        }
    }
}