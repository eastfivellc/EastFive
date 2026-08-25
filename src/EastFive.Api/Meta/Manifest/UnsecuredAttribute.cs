using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace EastFive.Api
{
    /// <summary>
    /// Marks an endpoint as intentionally unsecured (no token validation required).
    /// This attribute requires documentation explaining why the endpoint must be publicly accessible.
    /// </summary>
    /// <remarks>
    /// ⚠️ SECURITY WARNING: This attribute disables all authentication checks.
    /// Only use this for endpoints that absolutely must be publicly accessible, such as:
    /// - Health check endpoints
    /// - OAuth callback handlers
    /// - Webhook receivers with alternative authentication (e.g., HMAC signatures)
    /// - Public documentation endpoints
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class UnsecuredAttribute : Attribute, IHandleMethodInvocation
    {
        /// <summary>
        /// Gets the documented reason why this endpoint is unsecured.
        /// </summary>
        public string Reason { get; }

        /// <summary>
        /// Creates a new UnsecuredAttribute with a required explanation.
        /// </summary>
        /// <param name="reason">
        /// A clear explanation of why this endpoint must be publicly accessible without token validation.
        /// This will be logged and may be audited for security compliance.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown when reason is null or whitespace.</exception>
        public UnsecuredAttribute(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentNullException(nameof(reason), 
                    "Unsecured endpoints must document why they are publicly accessible.");
            
            Reason = reason;
        }

        /// <summary>
        /// Validates the HTTP request by explicitly allowing it without any authentication checks.
        /// </summary>
        /// <remarks>
        /// This implementation intentionally performs no validation and always calls the bound callback,
        /// allowing the request to proceed without authentication.
        /// 
        /// The reason for unsecuring this endpoint is: {Reason}
        /// </remarks>
        public Task<IHttpResponse> HandleMethodInvocationAsync(
            KeyValuePair<ParameterInfo, object>[] parameters,
            IReadOnlyDictionary<ParameterInfo, object> bindingContexts,
            MethodInfo method,
            IApplication httpApp,
            IHttpRequest request,
            InvokeMethodDelegate continueInvocation)
        {
            // No validation performed - endpoint is intentionally unsecured
            // Reason: {this.Reason}
            
            // Optional: Log unsecured access for security auditing
            // Logger.LogWarning($"Unsecured endpoint accessed: {method.DeclaringType.FullName}.{method.Name} - Reason: {Reason}");
            
            return continueInvocation(parameters, bindingContexts, method, httpApp, request);
        }
    }
}
