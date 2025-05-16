using System;
using Microsoft.AspNetCore.Components;
using Wishstar.Components.Pages.Context;

namespace Wishstar.Extensions {
    public static class NavigationManagerExtensions {
        public static void NavigateToWithContext<T>(this NavigationManager navigationManager, string url, PageContextAction contextAction, T? context = default, Dictionary<string, string>? queryParameters = null) where T : class, IPageContext {
            if (!url.Contains('?')) {
                url += "?";
            } else {
                url += "&";
            }

            url += $"action={contextAction}";
            if (queryParameters != null && queryParameters.Count > 0) {
                foreach (var queryParameter in queryParameters) {
                    url += $"&{Uri.EscapeDataString(queryParameter.Key)}={Uri.EscapeDataString(queryParameter.Value)}";
                }
            }

            if (context != null) {
                string contextString = IPageContextSerializer.Serialize(context, context.GetType());
                url += $"&context=contained&{contextString}";
            }

            navigationManager.NavigateTo(url, forceLoad: true);
        }

        public static void GetUriContext<T>(this NavigationManager navigationManager, out PageContextAction? action, out T? context, out Dictionary<string, string>? queryParameters) where T : class, IPageContext {
            GetUriContext(navigationManager, out action, out queryParameters);
            string uri = navigationManager.Uri;
            int contextIndex = uri.IndexOf("context=contained", StringComparison.OrdinalIgnoreCase);
            if (contextIndex == -1) {
                context = null;
                return;
            }

            string contextContent = uri[(contextIndex + 18)..];
            context = (T?)IPageContextSerializer.Deconstruct(contextContent, typeof(T));
        }

        public static void GetUriContext(this NavigationManager navigationManager, out PageContextAction? action, out Dictionary<string, string>? queryParameters) {
            var uri = navigationManager.ToAbsoluteUri(navigationManager.Uri);
            var query = uri.Query;
            var queryItems = query.TrimStart('?').Split('&');

            action = null;
            queryParameters = [];

            foreach (var item in queryItems) {
                var keyValue = item.Split('=');
                if (keyValue.Length == 2) {
                    var key = keyValue[0];
                    var value = Uri.UnescapeDataString(keyValue[1]);
                    if (key == "action") {
                        action = Enum.Parse<PageContextAction>(value);
                    } else if (key == "context" && value == "contained") {
                        // Skip context parsing
                        break;
                    } else {
                        queryParameters[Uri.UnescapeDataString(key)] = Uri.UnescapeDataString(value);
                    }
                }
            }
        }
    }
}
