using Wishstar.Models;

namespace Wishstar.Components.Pages.Context {
    public class CategoryPageContext : IPageContext {
        public string Page { get; set; } = $"{AppConfig.FullCurrentDomain}/category";

        [PageContextItem]
        public int CategoryId { get; set; } = 0;

        [PageContextItem]
        public string CategoryName { get; set; } = string.Empty;

        [PageContextItem]
        public string CategoryDescription { get; set; } = string.Empty;

        [PageContextItem]
        public PageContextAction Action { get; set; } = PageContextAction.Add;

        [PageContextItem]
        public IPageContext? ParentContext { get; set; } = null;

        public static CategoryPageContext FromUri(string uri) {
            int contextIndex = uri.IndexOf("context=", StringComparison.OrdinalIgnoreCase);
            if (contextIndex == -1) {
                return new CategoryPageContext();
            }

            string contextContent = uri[(contextIndex + 8)..];
            return IPageContextSerializer.Deconstruct<CategoryPageContext>(contextContent);
        }

        public static string ToUri(CategoryPageContext context) {
            string contextString = IPageContextSerializer.Serialize(context);
            return $"context={contextString}";
        }

        public static CategoryPageContext CreateDefaultWith(IPageContext parentContext) {
            return new CategoryPageContext() {
                ParentContext = parentContext
            };
        }

        public static CategoryPageContext FromCategory(WishCategory category, IPageContext? parentContext = null) {
            return new CategoryPageContext() {
                CategoryId = category.CategoryId,
                CategoryName = category.CategoryName,
                CategoryDescription = category.CategoryDescription,
                Action = PageContextAction.Update,
                ParentContext = parentContext
            };
        }
    }
}
