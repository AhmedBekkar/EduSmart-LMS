using System;
using System.Web.Mvc;

namespace EduSmart.Controllers
{
    public abstract class BaseController : Controller
    {
        protected bool IsSignedIn()
        {
            return Session["UserId"] != null;
        }

        protected string CurrentRole()
        {
            return Session["UserRole"] as string ?? string.Empty;
        }

        protected bool IsStudent()
        {
            return CurrentRole().Equals("Ogrenci", StringComparison.OrdinalIgnoreCase);
        }

        protected bool IsTeacher()
        {
            return CurrentRole().Equals("Ogretmen", StringComparison.OrdinalIgnoreCase);
        }

        protected bool IsAdmin()
        {
            return CurrentRole().Equals("Yonetici", StringComparison.OrdinalIgnoreCase);
        }

        protected ActionResult RequireLogin(string message = null)
        {
            if (!IsSignedIn())
            {
                TempData["Flash"] = message ?? "Lütfen önce giriş yapın.";
                return RedirectToAction("Login", "Account");
            }

            return null;
        }

        protected ActionResult RequireTeacherOrAdmin(string message = null)
        {
            var redirect = RequireLogin(message);
            if (redirect != null) return redirect;

            if (IsStudent())
            {
                TempData["Flash"] = message ?? "Bu işlem yalnızca öğretmen veya yöneticiler için.";
                return RedirectToAction("Index", "Home");
            }

            return null;
        }
    }
}

