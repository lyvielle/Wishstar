using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Wishstar.Extensions;
using Wishstar.Models;
using Wishstar.Services;

namespace Wishstar.Controllers {
    [ApiController]
    [Route("/api")]
    public class LoginController(ILogger<LoginController> logger) : ControllerBase {
        private readonly ILogger<LoginController> _Logger = logger;

        public record class LoginRequest(string Email, string Password);

        [HttpPost("login")]
        [EnableRateLimiting(RateLimitPolicies.Login)]
        public IActionResult Login([FromBody] LoginRequest request) {
            if (Request.TryValidateLogin(out _, out User? currentUser) && currentUser != null) {
                return Ok();
            }

            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password)) {
                return BadRequest("Email and password are required");
            }

            var database = WishDatabase.Load();
            User? user = database.GetUserByEmail(request.Email);
            if (user == null) {
                PasswordHasher.SimulateVerify();
                _Logger.LogWarning("Failed login attempt (unknown email) from {Ip}", HttpContext.Connection.RemoteIpAddress);
                return Unauthorized("Invalid email or password");
            }

            if (!PasswordHasher.Verify(request.Password, user.Token, out bool needsRehash)) {
                _Logger.LogWarning("Failed login attempt for user {UserId} from {Ip}", user.UserId, HttpContext.Connection.RemoteIpAddress);
                return Unauthorized("Invalid email or password");
            }

            if (needsRehash) {
                database.SetPasswordHash(user.UserId, PasswordHasher.Hash(request.Password));
                _Logger.LogInformation("Upgraded password hash of user {UserId} to PBKDF2", user.UserId);
            }

            Response.Cookies.Append(AppConfig.AuthCookieName, AuthTokens.Create(user), AuthTokens.CreateCookieOptions());
            DeleteLegacyCookies();

            _Logger.LogInformation("User {UserId} logged in", user.UserId);
            return Ok();
        }

        [HttpPost("logout")]
        public IActionResult Logout() {
            Response.Cookies.Delete(AppConfig.AuthCookieName, new CookieOptions { Path = "/", Secure = true, SameSite = SameSiteMode.Strict });
            DeleteLegacyCookies();
            return Ok();
        }

        private void DeleteLegacyCookies() {
            Response.Cookies.Delete("token", new CookieOptions { Path = "/" });
            Response.Cookies.Delete("uid", new CookieOptions { Path = "/" });
        }
    }

    public static class RateLimitPolicies {
        public const string Login = "login";
        public const string Upload = "upload";
    }
}
