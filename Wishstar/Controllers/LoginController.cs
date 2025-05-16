using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Wishstar.Extensions;
using Wishstar.Models;

namespace Wishstar.Controllers {
    [ApiController]
    [Route("/api/login")]
    public class LoginController(ILogger<LoginController> logger) : ControllerBase {
        private readonly ILogger<LoginController> _Logger = logger;

        public record class LoginRequest(string Email, string Password);

        [HttpPost]
        public IActionResult Login([FromBody] LoginRequest request) {
            try {
                if (!Request.TryValidateLogin(out IActionResult? errorResult, out User? user)) {
                    if (errorResult != null) {
                        return errorResult;
                    }

                    if (string.IsNullOrWhiteSpace(request.Email)) {
                        return BadRequest("Email is required");
                    }

                    if (string.IsNullOrWhiteSpace(request.Password)) {
                        return BadRequest("Password is required");
                    }

                    string passwordHash = SHA256.HashData(Encoding.UTF8.GetBytes(request.Password)).ByteArrayToString();
                    user = WishDatabase.Load().GetUsers().FirstOrDefault(u => u.Email == request.Email && u.Token == passwordHash);
                    if (user == null) {
                        _Logger.LogWarning("Failed login attempt for email {Email}", request.Email);
                        return Unauthorized("Invalid email or password");
                    }

                    Response.Cookies.Append("token", PasswordCookie.CreateCookie(user.UserId, Request.Headers.UserAgent!, passwordHash), new CookieOptions {
                        HttpOnly = true,
                        Secure = true,
                        SameSite = SameSiteMode.Strict,
                        Path = "/",
                        Expires = DateTimeOffset.UtcNow.AddDays(30)
                    });

                    Response.Cookies.Append("uid", user.UserId.ToString(), new CookieOptions {
                        HttpOnly = true,
                        Secure = true,
                        SameSite = SameSiteMode.Strict,
                        Path = "/",
                        Expires = DateTimeOffset.UtcNow.AddDays(30)
                    });

                    _Logger.LogInformation("User {UserId} logged in", user.UserId);

                    return Ok();
                } else {
                    if (user == null) {
                        return StatusCode(StatusCodes.Status500InternalServerError, "Missing user in database");
                    }

                    return Ok();
                }
            } catch (Exception ex) {
                _Logger.LogError(ex, "Error during login");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred during login");
            }
        }
    }
}
