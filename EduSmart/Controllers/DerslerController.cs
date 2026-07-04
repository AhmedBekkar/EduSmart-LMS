using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using EduSmart.Models;

namespace EduSmart.Controllers
{
    public class DerslerController : BaseController
    {
        private readonly EduSmart_DBEntities db = new EduSmart_DBEntities();

        public ActionResult Index()
        {
            var redirect = RequireLogin();
            if (redirect != null) return redirect;

            var dersler = db.Derslers.Include(d => d.Kullanicilar)
                .OrderBy(d => d.DersAdi)
                .ToList();
            return View(dersler);
        }

        public ActionResult Create()
        {
            var redirect = RequireTeacherOrAdmin("Ders ekleme yetkisi yalnızca öğretmen veya yöneticilerdedir.");
            if (redirect != null) return redirect;

            PopulateEgitmen();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Dersler model)
        {
            var redirect = RequireTeacherOrAdmin("Ders ekleme yetkisi yalnızca öğretmen veya yöneticilerdedir.");
            if (redirect != null) return redirect;

            if (ModelState.IsValid)
            {
                db.Derslers.Add(model);
                db.SaveChanges();
                TempData["Flash"] = "Ders eklendi.";
                return RedirectToAction("Index");
            }

            PopulateEgitmen(model.EgitmenID);
            return View(model);
        }

        public ActionResult Edit(int? id)
        {
            var redirect = RequireTeacherOrAdmin("Ders düzenleme yetkisi yalnızca öğretmen veya yöneticilerdedir.");
            if (redirect != null) return redirect;

            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            var ders = db.Derslers.Find(id);
            if (ders == null) return HttpNotFound();

            PopulateEgitmen(ders.EgitmenID);
            return View(ders);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Dersler model)
        {
            var redirect = RequireTeacherOrAdmin("Ders düzenleme yetkisi yalnızca öğretmen veya yöneticilerdedir.");
            if (redirect != null) return redirect;

            if (ModelState.IsValid)
            {
                db.Entry(model).State = EntityState.Modified;
                db.SaveChanges();
                TempData["Flash"] = "Ders güncellendi.";
                return RedirectToAction("Index");
            }

            PopulateEgitmen(model.EgitmenID);
            return View(model);
        }

        public ActionResult Delete(int? id)
        {
            var redirect = RequireTeacherOrAdmin("Ders silme yetkisi yalnızca öğretmen veya yöneticilerdedir.");
            if (redirect != null) return redirect;

            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            var ders = db.Derslers.Include(d => d.Kullanicilar).FirstOrDefault(d => d.DersID == id);
            if (ders == null) return HttpNotFound();
            return View(ders);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            var redirect = RequireTeacherOrAdmin("Ders silme yetkisi yalnızca öğretmen veya yöneticilerdedir.");
            if (redirect != null) return redirect;

            try
            {
                var ders = db.Derslers.Find(id);
                if (ders == null) return HttpNotFound();

                db.Derslers.Remove(ders);
                db.SaveChanges();
                TempData["Flash"] = "Ders silindi.";
                return RedirectToAction("Index");
            }
            catch (System.Data.Entity.Infrastructure.DbUpdateException)
            {
                TempData["Flash"] = "Hata: Bu ders silinemez çünkü derse kayıtlı öğrenciler veya atanmış ödevler bulunmaktadır.";
                return RedirectToAction("Index");
            }
        }

        private void PopulateEgitmen(int? selected = null)
        {
            var ogretmenler = db.Kullanicilars
                .Where(k => k.Rol == "Ogretmen" || k.Rol == "Yonetici")
                .OrderBy(k => k.AdSoyad)
                .ToList();
            ViewBag.EgitmenID = new SelectList(ogretmenler, "KullaniciID", "AdSoyad", selected);
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

