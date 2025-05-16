namespace Wishstar.Components.Pages.Context {
    public interface IPageContext {
        public string Page { get; set; }
        public string Serialize();
    }
}