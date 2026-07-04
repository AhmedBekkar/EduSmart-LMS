using System;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using EduSmart.Models;
using EduSmart.Services;

namespace EduSmart.Controllers
{
    public class UsersController : BaseController
    {
        private readonly EduSmart_DBEntities db = new EduSmart_DBEntities();

        public ActionResult Index(int? dersId)
        {
            var redirect = RequireLogin();
            if (redirect != null) return redirect;

            if (IsStudent())
            {
                TempData["Flash"] = "Öğrenciler kullanıcı listesine erişemez.";
                return RedirectToAction("Index", "Home");
            }

            // Populate Ders dropdown
            var dersler = db.Derslers.OrderBy(d => d.DersAdi).ToList();
            ViewBag.DersID = new SelectList(dersler, "DersID", "DersAdi", dersId);

            var query = db.Kullanicilars.AsQueryable();

            // Teachers cannot see admins
            if (IsTeacher())
            {
                query = query.Where(u => !u.Rol.Equals("Yonetici", StringComparison.OrdinalIgnoreCase));
            }

            // Filter by Ders if selected - show students enrolled in that Ders
            if (dersId.HasValue)
            {
                var enrolledStudentIds = db.Kayitlars
                    .Where(k => k.DersID == dersId.Value)
                    .Select(k => k.OgrenciID)
                    .ToList();
                query = query.Where(u => enrolledStudentIds.Contains(u.KullaniciID));
            }

            var users = query
                .OrderByDescending(u => u.KullaniciID)
                .ToList();

            ViewBag.SelectedDersID = dersId;
            return View(users);
        }

        public ActionResult Create()
        {
            var redirect = RequireTeacherOrAdmin("Kullanıcı ekleme yetkisi yalnızca öğretmen veya yöneticilerdedir.");
            if (redirect != null) return redirect;

            return View(new UserInputViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(UserInputViewModel model)
        {
            var redirect = RequireTeacherOrAdmin("Kullanıcı ekleme yetkisi yalnızca öğretmen veya yöneticilerdedir.");
            if (redirect != null) return redirect;

            if (string.IsNullOrWhiteSpace(model.Password))
            {
                ModelState.AddModelError("Password", "Şifre zorunludur.");
            }

            // SECURITY FIX: Teachers can only create students
            if (IsTeacher() && !string.Equals(model.Rol, "Ogrenci", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("Rol", "Öğretmenler yalnızca Öğrenci rolünde kullanıcı oluşturabilir.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = new Kullanicilar
            {
                AdSoyad = model.AdSoyad,
                Eposta = model.Eposta,
                Rol = model.Rol,
                SifreHash = PasswordService.HashPassword(model.Password),
                OlusturulmaTarihi = DateTime.Now
            };

            db.Kullanicilars.Add(user);
            db.SaveChanges();

            TempData["Flash"] = "Kullanıcı başarıyla oluşturuldu.";
            return RedirectToAction("Index");
        }

        public ActionResult Edit(int? id)
        {
            var redirect = RequireLogin();
            if (redirect != null) return redirect;

            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            var user = db.Kullanicilars.Find(id);
            if (user == null)
            {
                return HttpNotFound();
            }

            var block = GuardEditOrDelete(user, isDelete: false);
            if (block != null) return block;

            var model = new UserInputViewModel
            {
                Id = user.KullaniciID,
                AdSoyad = user.AdSoyad,
                Eposta = user.Eposta,
                Rol = user.Rol
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(int id, UserInputViewModel model)
        {
            var redirect = RequireLogin();
            if (redirect != null) return redirect;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = db.Kullanicilars.Find(id);
            if (user == null)
            {
                return HttpNotFound();
            }

            var block = GuardEditOrDelete(user, isDelete: false);
            if (block != null) return block;

            // SECURITY FIX: Teachers cannot elevate a student's role to Admin or Teacher
            if (IsTeacher() && !string.Equals(model.Rol, "Ogrenci", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("Rol", "Öğretmenler, kullanıcı rolünü yalnızca Öğrenci olarak ayarlayabilir.");
                return View(model);
            }

            user.AdSoyad = model.AdSoyad;
            user.Eposta = model.Eposta;
            user.Rol = model.Rol;

            if (!string.IsNullOrWhiteSpace(model.Password))
            {
                user.SifreHash = PasswordService.HashPassword(model.Password);
            }

            db.Entry(user).State = EntityState.Modified;
            db.SaveChanges();

            TempData["Flash"] = "Kullanıcı güncellendi.";
            return RedirectToAction("Index");
        }

        public ActionResult Delete(int? id)
        {
            var redirect = RequireTeacherOrAdmin("Kullanıcı silme yetkisi yalnızca öğretmen veya yöneticilerdedir.");
            if (redirect != null) return redirect;

            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            var user = db.Kullanicilars.Find(id);
            if (user == null)
            {
                return HttpNotFound();
            }

            var block = GuardEditOrDelete(user, isDelete: true);
            if (block != null) return block;

            return View(user);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            var redirect = RequireTeacherOrAdmin("Kullanıcı silme yetkisi yalnızca öğretmen veya yöneticilerdedir.");
            if (redirect != null) return redirect;

            var user = db.Kullanicilars.Find(id);
            if (user == null)
            {
                return HttpNotFound();
            }

            var block = GuardEditOrDelete(user, isDelete: true);
            if (block != null) return block;

            db.Kullanicilars.Remove(user);
            db.SaveChanges();

            TempData["Flash"] = "Kullanıcı silindi.";
            return RedirectToAction("Index");
        }

        private ActionResult GuardEditOrDelete(Kullanicilar target, bool isDelete)
        {
            // Students cannot edit/delete anyone
            if (IsStudent())
            {
                TempData["Flash"] = "Öğrenciler bu işlemi yapamaz.";
                return RedirectToAction("Index");
            }

            // Teachers cannot edit/delete other teachers or admins
            if (IsTeacher() && (string.Equals(target.Rol, "Ogretmen", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(target.Rol, "Yonetici", StringComparison.OrdinalIgnoreCase)))
            {
                TempData["Flash"] = "Öğretmenler diğer öğretmen veya yöneticileri değiştiremez.";
                return RedirectToAction("Index");
            }

            // Admin cannot delete self
            var currentUserId = Session["UserId"] as int?;
            if (isDelete && IsAdmin() && currentUserId.HasValue && target.KullaniciID == currentUserId.Value)
            {
                TempData["Flash"] = "Yönetici kendi hesabını silemez.";
                return RedirectToAction("Index");
            }

            return null;
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