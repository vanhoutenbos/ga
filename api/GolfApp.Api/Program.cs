using System;
using GolfApp.Api.Auth;
using GolfApp.Api.Repositories;
using GolfApp.Api.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GolfApp.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var host = new HostBuilder()
                .ConfigureFunctionsWorkerDefaults(workerBuilder =>
                {
                    workerBuilder.UseMiddleware<SupabaseAuthMiddleware>();
                })
                .ConfigureServices((context, services) =>
                {
                    // Configure Supabase options
                    services.Configure<SupabaseOptions>(context.Configuration.GetSection("Supabase"));
                    
                    // Register authentication services
                    services.AddSingleton<SupabaseAuthHandler>();
                    services.AddScoped<IUserContext>(sp => 
                    {
                        var functionContextAccessor = sp.GetRequiredService<Microsoft.Azure.Functions.Worker.FunctionContextAccessor>();
                        return new SupabaseUserContext(functionContextAccessor.FunctionContext);
                    });
                    
                    // Register Redis cache
                    services.AddStackExchangeRedisCache(options =>
                    {
                        options.Configuration = context.Configuration["Redis:ConnectionString"];
                    });
                    
                    // Register services
                    services.AddSingleton<ISupabaseService, SupabaseService>();
                    services.AddSingleton<ICacheService, RedisCacheService>();
                    services.AddScoped<ILeaderboardService, LeaderboardService>();
                    services.AddScoped<IPlayerStatisticsService, PlayerStatisticsService>();
                    
                    // Register repositories
                    services.AddScoped<ITournamentRepository, TournamentRepository>();
                    services.AddScoped<IPlayerRepository, PlayerRepository>();
                    services.AddScoped<IScoreRepository, ScoreRepository>();
                    
                    // Register typed HttpClient
                    services.AddHttpClient();
                    
                    // Configure application insights telemetry
                    if (!string.IsNullOrEmpty(context.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
                    {
                        services.AddApplicationInsightsTelemetry(options =>
                        {
                            options.ConnectionString = context.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
                        });
                    }
                })
                .ConfigureLogging((context, logging) =>
                {
                    logging.AddConsole();
                    
                    // Configure logging
                    if (!string.IsNullOrEmpty(context.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
                    {
                        logging.AddApplicationInsights(
                            configureTelemetryConfiguration: (config) => 
                                config.ConnectionString = context.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"],
                            configureApplicationInsightsLoggerOptions: (options) => { });
                    }
                })
                .Build();

            host.Run();
        }
    }
}
