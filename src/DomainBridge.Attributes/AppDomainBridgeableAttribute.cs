using System;

namespace DomainBridge
{
    /// <summary>
    /// Marks an interface or simple data transfer object as bridgeable across AppDomain boundaries.
    /// This attribute indicates that the source generator should create a MarshalByRefObject-based
    /// proxy implementation for cross-domain communication.
    /// 
    /// Best Practices:
    /// - Prefer interfaces over concrete classes for better separation of concerns
    /// - Keep interface contracts simple and focused on cross-domain operations
    /// - Use serializable DTOs for data transfer rather than complex entities
    /// - Avoid interfaces with generic constraints on unbridgeable types (e.g., DbContext, Form)
    /// </summary>
    /// <example>
    /// <code>
    /// [AppDomainBridgeable]
    /// public interface IUserService
    /// {
    ///     Task&lt;UserDto&gt; GetUserAsync(int id);
    ///     Task SaveUserAsync(UserDto user);
    /// }
    /// 
    /// [Serializable]
    /// public class UserDto
    /// {
    ///     public int Id { get; set; }
    ///     public string Name { get; set; }
    /// }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class AppDomainBridgeableAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the AppDomainBridgeableAttribute.
        /// </summary>
        public AppDomainBridgeableAttribute()
        {
        }
    }
}