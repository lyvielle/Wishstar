using Wishstar.Components.Pages.Context;

namespace Wishstar.Extensions {
    public static class IPageContextExtensions {
        public static string GetFullUrl<T>(this T pageContext) where T : IPageContext {
            string contextContent = IPageContextSerializer.Serialize(pageContext, typeof(T));
            string url = pageContext.Page.TrimEnd('/');
            if(!url.Contains('?')) {
                url += "?";
            } else {
                url += "&";
            }

            url += $"context={contextContent}";
            return url;
        }
    }
}