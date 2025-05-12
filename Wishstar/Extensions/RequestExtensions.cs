using Microsoft.AspNetCore.Mvc;
using Wishstar.Models;

namespace Wishstar.Extensions {
    public static class RequestExtensions {
        public static bool TryValidateLogin(this HttpRequest request, out IActionResult? errorResult, out User? user) {
            user = null;
            errorResult = null;

            if(request.Cookies.TryGetValue("token", out string? token) && request.Cookies.TryGetValue("uid", out string? userId)) {
                string? userAgent = request.Headers.UserAgent;
                if(string.IsNullOrWhiteSpace(userAgent)) {
                    errorResult = new BadRequestObjectResult("User-Agent header is required");
                    return false;
                }

                if(!string.IsNullOrWhiteSpace(token) && !string.IsNullOrWhiteSpace(userId)) {
                    if(PasswordCookie.Validate(token, userId, userAgent, out user)) {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
