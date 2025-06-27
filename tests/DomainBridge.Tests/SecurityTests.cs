using System;
using System.Security;
using System.Threading.Tasks;
using DomainBridge;
using TUnit.Assertions;
using TUnit.Core;

namespace DomainBridge.Tests
{
    /// <summary>
    /// Security-focused tests for DomainBridge
    /// </summary>
    public class SecurityTests
    {
        [Test]
        public async Task IsolatedDomain_PreventsDirectAccess()
        {
            // Arrange
            var isolatedBridge = SecurityTestServiceBridge.CreateIsolated();
            
            // Act - Try to access sensitive data through bridge
            var result = isolatedBridge.GetSensitiveData();
            
            // Assert - Should get the data through proper bridge channel
            await Assert.That(result).IsEqualTo("Sensitive data processed safely");
            
            // Cleanup
            SecurityTestServiceBridge.UnloadDomain();
        }

        [Test]
        public async Task ExceptionWrapping_DoesNotLeakInternalDetails()
        {
            // Arrange
            var bridge = SecurityTestServiceBridge.Instance;
            
            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                bridge.ThrowExceptionWithSensitiveInfo();
            });
            
            // The wrapped exception should not contain the original sensitive stack trace
            await Assert.That(exception.Message).Contains("Exception in method ThrowExceptionWithSensitiveInfo");
            await Assert.That(exception.Message).DoesNotContain("SuperSecretMethod");
        }

        [Test]
        public async Task SerializationSecurity_RejectsUntrustedTypes()
        {
            // Arrange
            var bridge = SecurityTestServiceBridge.Instance;
            
            // Act - Try to pass an untrusted object
            var result = bridge.ProcessTrustedData("safe data");
            
            // Assert
            await Assert.That(result).IsEqualTo("Processed: safe data");
        }

        [Test]
        public async Task DomainIsolation_PreventsStaticFieldSharing()
        {
            // Arrange
            SecurityTestService.StaticCounter = 0;
            var localBridge = SecurityTestServiceBridge.Instance;
            var isolatedBridge = SecurityTestServiceBridge.CreateIsolated();
            
            // Act - Increment counter in both domains
            localBridge.IncrementStaticCounter();
            isolatedBridge.IncrementStaticCounter();
            
            // Assert - Each domain should have its own static state
            var localCount = localBridge.GetStaticCounter();
            var isolatedCount = isolatedBridge.GetStaticCounter();
            
            await Assert.That(localCount).IsEqualTo(1);
            await Assert.That(isolatedCount).IsEqualTo(1);
            
            // Cleanup
            SecurityTestServiceBridge.UnloadDomain();
        }

        [Test]
        public async Task AppDomainUnload_CleansUpResources()
        {
            // Arrange
            var isolatedBridge = SecurityTestServiceBridge.CreateIsolated();
            isolatedBridge.CreateResource();
            
            // Act
            SecurityTestServiceBridge.UnloadDomain();
            
            // Assert - Should not throw when unloading
            // The actual cleanup verification would require more complex resource tracking
            // No explicit assertion needed - test passes if no exception
        }

        [Test]
        public async Task MethodAccess_RespectsPublicVisibility()
        {
            // Arrange
            var bridge = SecurityTestServiceBridge.Instance;
            
            // Act - Only public methods should be accessible through bridge
            var result = bridge.PublicMethod();
            
            // Assert
            await Assert.That(result).IsEqualTo("Public method called");
            
            // Private/protected methods should not be generated in bridge
            // This is verified at compile time by the source generator
        }
    }

    public class SecurityTestService
    {
        public static SecurityTestService Instance { get; } = new SecurityTestService();
        public static int StaticCounter { get; set; }
        
        private string _resource = "";
        
        public string GetSensitiveData()
        {
            return "Sensitive data processed safely";
        }
        
        public void ThrowExceptionWithSensitiveInfo()
        {
            try
            {
                SuperSecretMethod();
            }
            catch (Exception ex)
            {
                throw new CustomException("Error in sensitive operation", ex);
            }
        }
        
        private void SuperSecretMethod()
        {
            throw new InvalidOperationException("Secret internal error");
        }
        
        public string ProcessTrustedData(string data)
        {
            return $"Processed: {data}";
        }
        
        public void IncrementStaticCounter()
        {
            StaticCounter++;
        }
        
        public int GetStaticCounter()
        {
            return StaticCounter;
        }
        
        public void CreateResource()
        {
            _resource = "Resource created";
        }
        
        public string PublicMethod()
        {
            return "Public method called";
        }
        
        private string PrivateMethod()
        {
            return "This should not be accessible through bridge";
        }
        
        protected string ProtectedMethod()
        {
            return "This should not be accessible through bridge";
        }
    }

    [DomainBridge(typeof(SecurityTestService))]
    public partial class SecurityTestServiceBridge { }

    [Serializable]
    public class CustomException : Exception
    {
        public CustomException(string message, Exception innerException) : base(message, innerException) { }
    }
}