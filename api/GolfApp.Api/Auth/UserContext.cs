using System.Security.Claims;
using Microsoft.Azure.Functions.Worker;

namespace GolfApp.Api.Auth
{
    public interface IUserContext
    {
        ClaimsPrincipal? User { get; }
        string? GetUserId();
        bool IsInRole(string role);
    }

    public class SupabaseUserContext : IUserContext
    {
        private readonly FunctionContext _functionContext;

        public SupabaseUserContext(FunctionContext functionContext)
        {
            _functionContext = functionContext;
        }

        public ClaimsPrincipal? User => 
            _functionContext.Items.TryGetValue("User", out var user) ? user as ClaimsPrincipal : null;

        public string? GetUserId() => User?.FindFirst("sub")?.Value;

        public bool IsInRole(string role) => 
            User?.HasClaim(c => c.Type == "app_role" && c.Value == role) ?? false;
    }
}
