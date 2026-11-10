using Wishstar.Extensions;
using Wishstar.Models;

namespace Wishstar.Services {
    /// <summary>
    /// The logged-in user for the current prerender request or interactive circuit. Resolved once from the
    /// login cookie of the request that created the scope.
    /// </summary>
    public class UserSession(IHttpContextAccessor httpContextAccessor) {
        private bool _Resolved;
        private User? _User;

        public User? User {
            get {
                if (!_Resolved) {
                    _User = httpContextAccessor.HttpContext?.Request.GetCurrentUser();
                    _Resolved = true;
                }

                return _User;
            }
        }

        public bool IsLoggedIn => User != null;
    }
}
