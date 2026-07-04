using System.Linq;
using System.Web.Mvc;
using EduSmart.Models;
using EduSmart.Services;

namespace EduSmart.Controllers
{
    public class AccountController : Controller
    {
        private readonly EduSmart_DBEntities db = new EduSmart_DBEntities();

        [HttpGet]
        public ActionResult Login()
        {
            if (Session["UserId"] != null)
            {
                return RedirectToAction("Index", "Home");
            }

            return View(new LoginViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = db.Kullanicilars.FirstOrDefault(u => u.Eposta == model.Email);
            if (user == null || !PasswordService.VerifyPassword(model.Password, user.SifreHash))
            {
                ModelState.AddModelError("", "E-posta veya şifre hatalı.");
                return View(model);
            }

            if (!PasswordService.IsHashed(user.SifreHash))
            {
                user.SifreHash = PasswordService.HashPassword(model.Password);
                db.SaveChanges();
            }

            Session["UserId"] = user.KullaniciID;
            Session["UserName"] = user.AdSoyad;
            Session["UserRole"] = user.Rol;
            TempData["Flash"] = $"Hoş geldiniz, {user.AdSoyad}!";

            return RedirectToAction("Index", "Home");
        }

        public ActionResult Logout()
        {
            Session.Clear();
            TempData["Flash"] = "Çıkış yapıldı.";
            return RedirectToAction("Login");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
