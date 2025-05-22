using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;

namespace GolfApp.Api.Auth
{
    public class SupabaseAuthMiddleware : IFunctionsWorkerMiddleware
    {
        private readonly SupabaseAuthHandler _authHandler;
        private readonly ILogger<SupabaseAuthMiddleware> _logger;

        public SupabaseAuthMiddleware(SupabaseAuthHandler authHandler, ILogger<SupabaseAuthMiddleware> logger)
        {
            _authHandler = authHandler;
            _logger = logger;
        }

        public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
        {
            _logger.LogInformation("Authentication middleware executing");
            
            // Check if this is an HTTP trigger
            var httpReqData = await context.GetHttpRequestDataAsync();
            if (httpReqData != null)
            {
                // Try to validate JWT token
                var user = await _authHandler.ValidateTokenAsync(httpReqData);
                
                // Store the user in the function context for later use
                if (user != null)
                {
                    _logger.LogInformation("Valid user authentication found");
                    context.Items.Add("User", user);
                }
                else
                {
                    _logger.LogInformation("No valid authentication found");
                }
            }
            
            // Continue processing the request
            await next(context);
        }
    }
}
