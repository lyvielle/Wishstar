namespace Wishstar.Components.Pages.Context {
    public class ParentPageContext : IPageContext {
        public string Page { get; set; } = $"{AppConfig.FullCurrentDomain}/";

        public static ParentPageContext Create(string url) {
            return new ParentPageContext() {
                Page = url
            };
        }

        public static ParentPageContext FromUri(string uri) {
            int contextIndex = uri.IndexOf("context=", StringComparison.OrdinalIgnoreCase);
            if(contextIndex == -1) {
                return new ParentPageContext();
            }

            string contextContent = uri[(contextIndex + 8)..];
            return IPageContextSerializer.Deconstruct<ParentPageContext>(contextContent);
        }

        public static string ToUri(ParentPageContext context) {
            string contextString = IPageContextSerializer.Serialize(context);
            return $"context={contextString}";
        }
    }
}