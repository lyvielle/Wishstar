using Microsoft.JSInterop;
using Wishstar.Components.Pages.Context;
using Wishstar.Extensions;
using Wishstar.Models;

namespace Wishstar.Components.Pages {
    public partial class Vendor : IDisposable {
        public VendorPageContext? Context { get; set; } = null;
        public Models.Vendor VendorItem { get; set; } = Models.Vendor.CreateDefault();
        public PageContextAction Action { get; set; } = PageContextAction.Add;

        protected DotNetObjectReference<Vendor> ObjectReference { get; set; } = null!;

        protected override async Task OnAfterRenderAsync(bool firstRender) {
            if (firstRender) {
                if (HttpContextAccessor.HttpContext == null || !HttpContextAccessor.HttpContext.Request.TryValidateLogin(out _, out User? user) || user == null) {
                    NavigationManager.NavigateTo("/login", true);
                    return;
                }

                await JSRuntime.InvokeVoidAsync("registerComponentInstance", ObjectReference = DotNetObjectReference.Create(this));

                NavigationManager.GetUriContext(out _, out VendorPageContext? context, out Dictionary<string, string>? queryParameters);
                if (context == null) {
                    VendorItem = Models.Vendor.CreateDefault();
                    Action = PageContextAction.Add;
                    StateHasChanged();
                    return;
                }

                Context = context;
                Action = Context.Action;
                if (Context.Action == PageContextAction.Update) {
                    if (queryParameters != null && queryParameters.TryGetValue("id", out string? vendorId) && int.TryParse(vendorId, out int id)) {
                        var vendor = WishDatabase.Load().GetVendorById(id);
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
        }

        [JSInvokable]
        public void NavigateBack() {
            string url = Context?.ParentContext?.GetFullUrl() ?? "/";
            NavigationManager.NavigateTo(url, true);
        }

        public void Dispose() {
            ObjectReference?.Dispose();
            ObjectReference = null!;
            GC.SuppressFinalize(this);
        }
    }
}