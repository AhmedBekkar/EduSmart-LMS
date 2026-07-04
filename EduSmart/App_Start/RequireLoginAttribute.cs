using System.Web;
using System.Web.Mvc;

namespace EduSmart
{
    public class RequireLoginAttribute : AuthorizeAttribute
    {
        protected override bool AuthorizeCore(HttpContextBase httpContext)
        {
            // Check if user is logged in via session
            return httpContext.Session["UserId"] != null;
        }

        public override void OnAuthorization(AuthorizationContext filterContext)
        {
            // Allow access to Account controller without login
            var controllerName = filterContext.ActionDescriptor.ControllerDescriptor.ControllerName;
            if (controllerName != null && controllerName.Equals("Account", System.StringComparison.OrdinalIgnoreCase))
            {
                return; // Skip authorization for Account controller
            }

            // Call base authorization for all other controllers
            base.OnAuthorization(filterContext);
        }

        protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        {
            // Redirect to login page if not authorized
            filterContext.Result = new RedirectToRouteResult(
                new System.Web.Routing.RouteValueDictionary(
                    new { controller = "Account", action = "Login" }));
        }
    }
}

