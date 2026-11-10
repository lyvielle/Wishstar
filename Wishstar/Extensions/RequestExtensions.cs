using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Wishstar.Models;
using Wishstar.Services;

namespace Wishstar.Extensions {
    public static class RequestExtensions {
        private const string UserItemKey = "Wishstar.User";

        public static bool TryValidateLogin(this HttpRequest request, out IActionResult? errorResult, out User? user) {
            errorResult = null;
            user = request.GetCurrentUser();
            return user != null;
        }

        /// <summary>Returns the logged-in user for this request (validated once and cached per request).</summary>
        public static User? GetCurrentUser(this HttpRequest request) {
            var items = request.HttpContext.Items;
            if (items.TryGetValue(UserItemKey, out object? cached)) {
                return cached as User;
            }

            User? user = null;
            if (request.Cookies.TryGetValue(AppConfig.AuthCookieName, out string? token)
                && AuthTokens.TryValidate(token, out User? validated, out _)) {
                user = validated;
            }

            items[UserItemKey] = user;
            return user;
        }

        public static string GetDomain(this HttpRequest request) {
            if (request.Headers.TryGetValue("X-Forwarded-Host", out var forwardedHost)) {
                return forwardedHost.ToString();
            }

            if (request.Host.HasValue) {
                return request.Host.Value;
            }

            if (Debugger.IsAttached) {
                return "localhost";
            } else {
                return AppConfig.CurrentDomain;
            }
        }
    }
}
