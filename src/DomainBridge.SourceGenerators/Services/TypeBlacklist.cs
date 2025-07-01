using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace DomainBridge.SourceGenerators.Services
{
    /// <summary>
    /// Maintains a list of types and namespaces that cannot or should not be bridged across AppDomain boundaries.
    /// This class implements the Single Responsibility Principle by focusing solely on type filtering logic.
    /// </summary>
    internal static class TypeBlacklist
    {
        /// <summary>
        /// Namespaces that contain types unsuitable for AppDomain bridging.
        /// These typically include UI frameworks, database contexts, and other complex framework types.
        /// </summary>
        private static readonly HashSet<string> BlacklistedNamespaces = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Entity Framework - Complex ORM contexts with internal state
            "System.Data.Entity",
            "Microsoft.EntityFrameworkCore",
            "Microsoft.Data.Entity",
            
            // Windows Forms - UI controls with native handles and complex state
            "System.Windows.Forms",
            "System.Drawing",
            "System.Windows.Controls",
            
            // WPF - UI framework with complex dependency injection and threading
            "System.Windows",
            "System.Windows.Media",
            "System.Windows.Data",
            
            // ASP.NET - Web framework types with complex request/response lifecycles
            "Microsoft.AspNetCore",
            "System.Web",
            "Microsoft.AspNetCore.Http",
            
            // Threading primitives - Not suitable for cross-domain marshaling
            "System.Threading.Tasks",
            "System.Threading",
            
            // Stream and IO - Often tied to specific AppDomain resources
            "System.IO",
            "System.Net.Sockets",
            
            // Reflection types - Can cause security and stability issues across domains
            "System.Reflection.Emit",
            
            // Native interop - Unsafe across domain boundaries
            "System.Runtime.InteropServices"
        };

        /// <summary>
        /// Specific type names that are problematic for bridging, regardless of namespace.
        /// </summary>
        private static readonly HashSet<string> BlacklistedTypeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Task types - Not serializable and tied to specific execution contexts
            "Task",
            "Task`1", // Task<T>
            "ValueTask",
            "ValueTask`1", // ValueTask<T>
            
            // Delegates - Complex marshaling behavior and potential security issues
            "Delegate",
            "MulticastDelegate",
            "Action",
            "Func",
            
            // Stream types - Often represent domain-specific resources
            "Stream",
            "FileStream",
            "MemoryStream",
            
            // Exception types - Already handled by AppDomain marshaling mechanisms
            "Exception",
            
            // Threading primitives
            "Thread",
            "ThreadStart",
            "CancellationToken", // Special case - often used in interfaces but needs careful handling
            
            // Native handles
            "IntPtr",
            "UIntPtr"
        };

        /// <summary>
        /// Base types that indicate a type should not be bridged.
        /// Types inheriting from these are typically complex framework types.
        /// </summary>
        private static readonly HashSet<string> BlacklistedBaseTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "System.Windows.Forms.Control",
            "System.Windows.Forms.Form",
            "System.Data.Entity.DbContext",
            "Microsoft.EntityFrameworkCore.DbContext",
            "System.ComponentModel.Component",
            "System.MarshalByRefObject", // Already handles cross-domain scenarios
            "System.Windows.DependencyObject"
        };

        /// <summary>
        /// Determines if a type should be excluded from bridge generation.
        /// </summary>
        /// <param name="type">The type symbol to check</param>
        /// <returns>True if the type should be blacklisted, false otherwise</returns>
        public static bool IsBlacklisted(ITypeSymbol type)
        {
            if (type == null)
                return true;

            // Check namespace
            var namespaceName = type.ContainingNamespace?.ToDisplayString();
            if (!string.IsNullOrEmpty(namespaceName) && IsNamespaceBlacklisted(namespaceName!))
                return true;

            // Check type name
            if (BlacklistedTypeNames.Contains(type.Name))
                return true;

            // Check fully qualified name for generic types
            var fullName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (BlacklistedTypeNames.Any(blacklisted => fullName.Contains(blacklisted)))
                return true;

            // Check base types
            return HasBlacklistedBaseType(type);
        }

        /// <summary>
        /// Checks if a namespace or any of its parent namespaces are blacklisted.
        /// </summary>
        /// <param name="namespaceName">The namespace to check</param>
        /// <returns>True if the namespace should be blacklisted</returns>
        private static bool IsNamespaceBlacklisted(string namespaceName)
        {
            return BlacklistedNamespaces.Any(blacklisted => 
                namespaceName.StartsWith(blacklisted, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Recursively checks if a type inherits from any blacklisted base types.
        /// </summary>
        /// <param name="type">The type to check</param>
        /// <returns>True if the type has a blacklisted base type</returns>
        private static bool HasBlacklistedBaseType(ITypeSymbol type)
        {
            var currentType = type.BaseType;
            while (currentType != null)
            {
                var baseTypeName = currentType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                if (BlacklistedBaseTypes.Any(blacklisted => 
                    baseTypeName.IndexOf(blacklisted, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    return true;
                }
                currentType = currentType.BaseType;
            }
            return false;
        }

        /// <summary>
        /// Gets a descriptive reason why a type is blacklisted.
        /// This is useful for generating helpful diagnostic messages.
        /// </summary>
        /// <param name="type">The blacklisted type</param>
        /// <returns>A human-readable explanation of why the type cannot be bridged</returns>
        public static string GetBlacklistReason(ITypeSymbol type)
        {
            if (type == null)
                return "Type is null or invalid";

            var namespaceName = type.ContainingNamespace?.ToDisplayString();
            if (!string.IsNullOrEmpty(namespaceName) && IsNamespaceBlacklisted(namespaceName!))
            {
                return $"Type belongs to namespace '{namespaceName}' which contains complex framework types unsuitable for AppDomain bridging";
            }

            if (BlacklistedTypeNames.Contains(type.Name))
            {
                return $"Type '{type.Name}' is a known problematic type for cross-domain marshaling";
            }

            if (HasBlacklistedBaseType(type))
            {
                return $"Type inherits from a framework base type that cannot be safely bridged across AppDomain boundaries";
            }

            return "Type is blacklisted for AppDomain bridging";
        }
    }
}