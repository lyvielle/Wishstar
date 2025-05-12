using Microsoft.JSInterop;
using Wishstar.Components.Pages.Context;
using Wishstar.Extensions;
using Wishstar.Models;

namespace Wishstar.Components.Pages {
    public partial class Category {
        public CategoryPageContext? Context { get; set; } = null;
        public WishCategory CategoryItem { get; set; } = new(string.Empty);
        public PageContextAction Action { get; set; } = PageContextAction.Add;

        protected override void OnInitialized() {
            base.OnInitialized();
            if (HttpContextAccessor.HttpContext == null || !HttpContextAccessor.HttpContext.Request.TryValidateLogin(out _, out User? user) || user == null) {
                NavigationManager.NavigateTo("/login");
                return;
            }

            NavigationManager.GetUriContext<CategoryPageContext>(out _, out CategoryPageContext? context, out Dictionary<string, string>? queryParameters);
            if (context == null) {
                CategoryItem = new(string.Empty);
                Action = PageContextAction.Add;
                return;
            }

            Context = context;
            Action = Context.Action;
            if (Context.Action == PageContextAction.Update) {
                if (queryParameters != null && queryParameters.TryGetValue("id", out string? categoryId) && !string.IsNullOrWhiteSpace(categoryId)) {
                    var category = WishDatabase.Load().GetCategoryByName(categoryId);
                    if (category != null) {
                        CategoryItem = category;
                    } else {
                        CategoryItem = new(Context.CategoryName);
                    }
                }
            } else if (Context.Action == PageContextAction.Add) {
                CategoryItem = new(Context.CategoryName);
            } else {
                throw new InvalidOperationException("Invalid action type.");
            }

            StateHasChanged();
        }

        [JSInvokable]
        public void NavigateBack() {
            string url = Context?.ParentContext?.GetFullUrl() ?? "/";
            NavigationManager.NavigateTo(url);
        }
    }
}