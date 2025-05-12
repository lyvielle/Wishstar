namespace Wishstar.Components.Pages.Context {
    public class VendorPageContext : IPageContext {
        public string Page { get; set; } = $"{AppConfig.FullCurrentDomain}/vendor";

        [PageContextItem]
        public int Id { get; set; } = 0;

        [PageContextItem]
        public string VendorName { get; set; } = string.Empty;

        [PageContextItem]
        public string Website { get; set; } = string.Empty;

        [PageContextItem]
        public PageContextAction Action { get; set; } = PageContextAction.Add;

        [PageContextItem]
        public IPageContext? ParentContext { get; set; } = null;

        public static VendorPageContext FromUri(string uri) {
            int contextIndex = uri.IndexOf("context=", StringComparison.OrdinalIgnoreCase);
            if (contextIndex == -1) {
                return new VendorPageContext();
            }

            string contextContent = uri[(contextIndex + 8)..];
            return IPageContextSerializer.Deconstruct<VendorPageContext>(contextContent);
        }

        public static string ToUri(VendorPageContext context) {
            string contextString = IPageContextSerializer.Serialize(context);
            return $"context={contextString}";
        }

        public static VendorPageContext CreateDefaultWith(IPageContext parentContext) {
            return new VendorPageContext() {
                ParentContext = parentContext
            };
        }

        public static VendorPageContext FromVendor(Models.Vendor vendor, IPageContext? parentContext = null) {
            return new VendorPageContext() {
                VendorName = vendor.VendorName,
                Website = vendor.Website,
                ParentContext = parentContext,
                Action = PageContextAction.Update
            };
        }
    }
}