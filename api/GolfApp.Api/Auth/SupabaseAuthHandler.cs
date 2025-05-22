using System;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using GolfApp.Api.Services;

namespace GolfApp.Api.Auth
{
    public class SupabaseAuthHandler
    {
        private readonly SupabaseOptions _options;
        private readonly ILogger<SupabaseAuthHandler> _logger;

        public SupabaseAuthHandler(IOptions<SupabaseOptions> options, ILogger<SupabaseAuthHandler> logger)
        {
            _options = options.Value;
            _logger = logger;
        }

        public async Task<ClaimsPrincipal?> ValidateTokenAsync(HttpRequestData req)
        {
            try
            {
                // Extract the JWT token from the Authorization header
                if (!req.Headers.TryGetValues("Authorization", out var authValues))
                {
                    return null;
                }

                var authHeader = authValues.FirstOrDefault();
                if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                {
                    return null;
                }

                var token = authHeader.Substring("Bearer ".Length);

                // Validate the JWT token using Supabase's JWT key
                var tokenHandler = new JwtSecurityTokenHandler();
                
                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.ASCII.GetBytes(_options.JwtSecret)),
                    ValidateIssuer = true,
                    ValidIssuer = _options.JwtIssuer,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                var principal = tokenHandler.ValidateToken(token, validationParameters, out _);
                return principal;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Token validation failed");
                return null;
            }
        }
    }
}
