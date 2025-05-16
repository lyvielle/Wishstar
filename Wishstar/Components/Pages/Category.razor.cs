using Microsoft.JSInterop;
using Wishstar.Components.Pages.Context;
using Wishstar.Extensions;
using Wishstar.Models;

namespace Wishstar.Components.Pages {
    public partial class Category {
        public CategoryPageContext? Context { get; set; } = null;
        public WishCategory CategoryItem { get; set; } = WishCategory.CreateDefault();
        public PageContextAction Action { get; set; } = PageContextAction.Add;

        protected override void OnInitialized() {
            base.OnInitialized();
            if (HttpContextAccessor.HttpContext == null || !HttpContextAccessor.HttpContext.Request.TryValidateLogin(out _, out User? user) || user == null) {
                NavigationManager.NavigateTo("/login", true);
                return;
            }

            NavigationManager.GetUriContext(out _, out CategoryPageContext? context, out Dictionary<string, string>? queryParameters);
            if (context == null) {
                CategoryItem = WishCategory.CreateDefault();
                Action = PageContextAction.Add;
                StateHasChanged();
                return;
            }

            Context = context;
            Action = Context.Action;
            if (Context.Action == PageContextAction.Update) {
                if (queryParameters != null && queryParameters.TryGetValue("id", out string? categoryId) && int.TryParse(categoryId, out int id)) {
                    var category = WishDatabase.Load().GetCategoryById(id);
                    if (category != null) {
                        CategoryItem = category;
                    } else {
                        CategoryItem = WishCategory.CreateDefault();
                    }
                }
            } else if (Context.Action == PageContextAction.Add) {
                CategoryItem = new(Context.CategoryId == 0 ? IdGenerator.GetNumericalId() : Context.CategoryId, Context.CategoryName, Context.CategoryDescription);
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