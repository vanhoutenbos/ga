using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;

namespace GolfApp.Api.Observability
{
    /// <summary>
    /// Middleware for handling distributed tracing and correlation IDs
    /// </summary>
    public class CorrelationMiddleware : IFunctionsWorkerMiddleware
    {
        private readonly ILogger<CorrelationMiddleware> _logger;
        private readonly TelemetryClient _telemetryClient;

        public CorrelationMiddleware(
            ILogger<CorrelationMiddleware> logger,
            TelemetryClient telemetryClient)
        {
            _logger = logger;
            _telemetryClient = telemetryClient;
        }

        public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
        {
            // Get the correlation ID from the request or generate a new one
            var correlationId = GetOrCreateCorrelationId(context);
            var requestId = Guid.NewGuid().ToString();
            
            using var operation = new TelemetryClientExtensions(context, _telemetryClient)
                .StartOperation<RequestTelemetry>("HttpRequest", correlationId, requestId);
            
            // Add standard properties to the telemetry
            operation.Telemetry.Properties["FunctionName"] = context.FunctionDefinition.Name;
            operation.Telemetry.Properties["RequestId"] = requestId;
            
            try
            {
                // Set correlation ID in Activity
                if (Activity.Current != null)
                {
                    Activity.Current.SetBaggage("CorrelationId", correlationId);
                    Activity.Current.SetTag("CorrelationId", correlationId);
                }

                // Log request received
                _logger.LogInformation("Request received. CorrelationId: {CorrelationId}, RequestId: {RequestId}, Function: {FunctionName}",
                    correlationId, requestId, context.FunctionDefinition.Name);
                
                // Add correlation ID to the response headers
                await AddHttpResponseHeaderAsync(context, "X-Correlation-ID", correlationId);
                await AddHttpResponseHeaderAsync(context, "X-Request-ID", requestId);
                
                // Continue middleware pipeline
                await next(context);
            }
            catch (Exception ex)
            {
                // Log exception with correlation ID
                _logger.LogError(ex, "Exception during request processing. CorrelationId: {CorrelationId}, RequestId: {RequestId}",
                    correlationId, requestId);
                
                // Track exception in App Insights with correlation ID
                var exceptionTelemetry = new ExceptionTelemetry(ex)
                {
                    Properties = { ["CorrelationId"] = correlationId, ["RequestId"] = requestId }
                };
                _telemetryClient.TrackException(exceptionTelemetry);
                
                throw; // Rethrow to let the exception middleware handle the response
            }
            finally
            {
                // Log request completed
                _logger.LogInformation("Request completed. CorrelationId: {CorrelationId}, RequestId: {RequestId}",
                    correlationId, requestId);
            }
        }

        private string GetOrCreateCorrelationId(FunctionContext context)
        {
            // Try to get HTTP request data
            var httpRequestData = GetHttpRequestData(context);
            var correlationId = httpRequestData?.Headers.TryGetValues("X-Correlation-ID", out var values) == true
                ? values.First()
                : Guid.NewGuid().ToString();
            
            // Enrich the function context with the correlation ID
            context.Items["CorrelationId"] = correlationId;
            
            return correlationId;
        }

        private HttpRequestData GetHttpRequestData(FunctionContext context)
        {
            var keyValuePair = context.Features.SingleOrDefault(f => f.Key.Name == "IFunctionBindingsFeature");
            if (keyValuePair.Key == null)
            {
                return null;
            }
            
            var functionBindingsFeature = keyValuePair.Value;
            var type = functionBindingsFeature.GetType();
            var inputData = type.GetProperty("InputData")?.GetValue(functionBindingsFeature) as IReadOnlyDictionary<string, object>;
            
            return inputData?.Values.OfType<HttpRequestData>().FirstOrDefault();
        }

        private async Task AddHttpResponseHeaderAsync(FunctionContext context, string headerName, string headerValue)
        {
            var httpResponseData = await GetHttpResponseDataAsync(context);
            if (httpResponseData != null)
            {
                httpResponseData.Headers.Add(headerName, headerValue);
            }
        }

        private async Task<HttpResponseData> GetHttpResponseDataAsync(FunctionContext context)
        {
            // Try to get the response directly from the invocation result
            var invocationResult = context.GetInvocationResult();
            if (invocationResult?.Value is HttpResponseData responseData)
            {
                return responseData;
            }
            
            // If not available yet, get the request data and create a new response
            var requestData = GetHttpRequestData(context);
            return requestData?.CreateResponse();
        }
    }

    /// <summary>
    /// Extension methods for telemetry client
    /// </summary>
    public class TelemetryClientExtensions
    {
        private readonly FunctionContext _context;
        private readonly TelemetryClient _telemetryClient;

        public TelemetryClientExtensions(FunctionContext context, TelemetryClient telemetryClient)
        {
            _context = context;
            _telemetryClient = telemetryClient;
        }

        public IOperationHolder<T> StartOperation<T>(string operationName, string correlationId, string operationId) where T : OperationTelemetry, new()
        {
            var operation = _telemetryClient.StartOperation<T>(operationName);
            
            // Set operation IDs for correlation
            operation.Telemetry.Context.Operation.Id = correlationId;
            operation.Telemetry.Context.Operation.ParentId = operationId;
            operation.Telemetry.Context.Operation.Name = $"{_context.FunctionDefinition.Name}/{operationName}";
            
            return operation;
        }
    }

    /// <summary>
    /// Application Insights configuration for Functions
    /// </summary>
    public static class ApplicationInsightsConfiguration
    {
        public static IFunctionsWorkerApplicationBuilder ConfigureApplicationInsights(
            this IFunctionsWorkerApplicationBuilder builder)
        {
            // Configure telemetry initializer to add common properties
            builder.Services.Configure<TelemetryConfiguration>(config =>
            {
                config.TelemetryInitializers.Add(new EnvironmentTelemetryInitializer());
            });
            
            // Add correlation middleware
            builder.UseMiddleware<CorrelationMiddleware>();
            
            return builder;
        }
    }

    /// <summary>
    /// Telemetry initializer to add environment information to all telemetry
    /// </summary>
    public class EnvironmentTelemetryInitializer : ITelemetryInitializer
    {
        public void Initialize(ITelemetry telemetry)
        {
            if (telemetry == null) return;
            
            var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown";
            
            telemetry.Context.GlobalProperties["Environment"] = environmentName;
            telemetry.Context.GlobalProperties["ApplicationVersion"] = GetApplicationVersion();
            
            // For Azure-specific telemetry
            telemetry.Context.Cloud.RoleName = "GolfApp.Api";
        }
        
        private string GetApplicationVersion()
        {
            return Environment.GetEnvironmentVariable("WEBSITE_RUN_FROM_PACKAGE_HASH") ??
                   Environment.GetEnvironmentVariable("FUNCTIONS_EXTENSION_VERSION") ??
                   "1.0.0"; // Default version if not deployed to Azure
        }
    }
}
