using Microsoft.JSInterop;
using Wishstar.Components.Pages.Context;
using Wishstar.Extensions;
using Wishstar.Models;

namespace Wishstar.Components.Pages {
    public partial class Vendor {
        public VendorPageContext? Context { get; set; } = null;
        public Models.Vendor VendorItem { get; set; } = new(IdGenerator.GetNumericalId(), string.Empty, string.Empty);
        public PageContextAction Action { get; set; } = PageContextAction.Add;

        protected override void OnAfterRender(bool firstRender) {
            if (HttpContextAccessor.HttpContext == null || !HttpContextAccessor.HttpContext.Request.TryValidateLogin(out _, out User? user) || user == null) {
                NavigationManager.NavigateTo("/login", true);
                return;
            }

            NavigationManager.GetUriContext<VendorPageContext>(out _, out VendorPageContext? context, out Dictionary<string, string>? queryParameters);
            if (context == null) {
                VendorItem = Models.Vendor.CreateDefault();
                Action = PageContextAction.Add;
                return;
            }

            Context = context;
            Action = Context.Action;
            if (Context.Action == PageContextAction.Update) {
                if (queryParameters != null && queryParameters.TryGetValue("id", out string? vendorId) && !string.IsNullOrWhiteSpace(vendorId)) {
                    var vendor = WishDatabase.Load().GetVendorByName(vendorId);
                    if (vendor != null) {
                        VendorItem = vendor;
                    } else {
                        VendorItem = new(Context.Id == 0 ? IdGenerator.GetNumericalId() : Context.Id, Context.VendorName, Context.Website);
                    }
                }
            } else if (Context.Action == PageContextAction.Add) {
                VendorItem = new(Context.Id == 0 ? IdGenerator.GetNumericalId() : Context.Id, Context.VendorName, Context.Website);
            } else {
                throw new InvalidOperationException("Invalid action type.");
            }

            StateHasChanged();
        }

        [JSInvokable]
        public void NavigateBack() {
            string url = Context?.ParentContext?.GetFullUrl() ?? "/";
            NavigationManager.NavigateTo(url, true);
        }
    }
}