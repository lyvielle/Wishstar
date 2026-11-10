using Microsoft.AspNetCore.Components;

namespace Wishstar.Components.Shared {
    public static class NavigationHelper {
        public const string Home = "/wishlist";

        /// <summary>Only allows local paths, so a crafted ?returnUrl= cannot send users to another site.</summary>
        public static string SafeReturnUrl(string? returnUrl, string fallback = Home) {
            if (string.IsNullOrWhiteSpace(returnUrl)
                || !returnUrl.StartsWith('/')
                || returnUrl.StartsWith("//", StringComparison.Ordinal)
                || returnUrl.StartsWith("/\\", StringComparison.Ordinal)
                || returnUrl.Contains("://", StringComparison.Ordinal)) {
                return fallback;
            }

            return returnUrl;
        }

        /// <summary>Redirects to the login page (full page load, since login is not an interactive page).</summary>
        public static void RedirectToLogin(this NavigationManager navigation) {
            string returnUrl = "/" + navigation.ToBaseRelativePath(navigation.Uri);
            navigation.NavigateTo($"/login?returnUrl={Uri.EscapeDataString(returnUrl)}", forceLoad: true);
        }
    }
}
