using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using EduSmart.Models;

namespace EduSmart.Controllers
{
    public class SinavSonuclariController : BaseController
    {
        private readonly EduSmart_DBEntities db = new EduSmart_DBEntities();

        public ActionResult Index(int? dersId)
        {
            var redirect = RequireLogin();
            if (redirect != null) return redirect;

            var isStudent = IsStudent();
            var userId = isStudent ? (int?)Session["UserId"] : null;

            // Populate Ders dropdown
            var dersler = db.Derslers.OrderBy(d => d.DersAdi).ToList();
            if (isStudent && userId.HasValue)
            {
                var studentCourseIds = db.Kayitlars
                    .Where(k => k.OgrenciID == userId.Value)
                    .Select(k => k.DersID)
                    .ToList();
                dersler = dersler.Where(d => studentCourseIds.Contains(d.DersID)).ToList();
            }
            ViewBag.DersID = new SelectList(dersler, "DersID", "DersAdi", dersId);

            var query = db.SinavSonuclaris
                .Include(s => s.Kullanicilar)
                .Include(s => s.Dersler);

            if (isStudent && userId.HasValue)
            {
                query = query.Where(s => s.OgrenciID == userId.Value);
            }

            // Filter by Ders if selected
            if (dersId.HasValue)
            {
                query = query.Where(s => s.DersID == dersId.Value);
            }

            var sonuclar = query.OrderByDescending(s => s.SinavSonucID).ToList();
            ViewBag.SelectedDersID = dersId;
            return View(sonuclar);
        }

        public ActionResult Create()
        {
            var redirect = RequireTeacherOrAdmin("Sınav sonucu ekleme yetkisi yalnızca öğretmen veya yöneticilerdedir.");
            if (redirect != null) return redirect;

            PopulateDropdowns();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(SinavSonuclari model)
        {
            var redirect = RequireTeacherOrAdmin("Sınav sonucu ekleme yetkisi yalnızca öğretmen veya yöneticilerdedir.");
            if (redirect != null) return redirect;

            if (ModelState.IsValid)
            {
                db.SinavSonuclaris.Add(model);
                db.SaveChanges();
                TempData["Flash"] = "Sınav sonucu eklendi.";
                return RedirectToAction("Index");
            }
            PopulateDropdowns(model.OgrenciID, model.DersID);
            return View(model);
        }

        public ActionResult Edit(int? id)
        {
            var redirect = RequireTeacherOrAdmin("Sınav sonucu düzenleme yetkisi yalnızca öğretmen veya yöneticilerdedir.");
            if (redirect != null) return redirect;

            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            var sonuc = db.SinavSonuclaris.Find(id);
            if (sonuc == null) return HttpNotFound();

            PopulateDropdowns(sonuc.OgrenciID, sonuc.DersID);
            return View(sonuc);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(SinavSonuclari model)
        {
            var redirect = RequireTeacherOrAdmin("Sınav sonucu düzenleme yetkisi yalnızca öğretmen veya yöneticilerdedir.");
            if (redirect != null) return redirect;

            if (ModelState.IsValid)
            {
                db.Entry(model).State = EntityState.Modified;
                db.SaveChanges();
                TempData["Flash"] = "Sınav sonucu güncellendi.";
                return RedirectToAction("Index");
            }
            PopulateDropdowns(model.OgrenciID, model.DersID);
            return View(model);
        }

        public ActionResult Delete(int? id)
        {
            var redirect = RequireTeacherOrAdmin("Sınav sonucu silme yetkisi yalnızca öğretmen veya yöneticilerdedir.");
            if (redirect != null) return redirect;

            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            var sonuc = db.SinavSonuclaris.Include(s => s.Kullanicilar).Include(s => s.Dersler).FirstOrDefault(s => s.SinavSonucID == id);
            if (sonuc == null) return HttpNotFound();
            return View(sonuc);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            var redirect = RequireTeacherOrAdmin("Sınav sonucu silme yetkisi yalnızca öğretmen veya yöneticilerdedir.");
            if (redirect != null) return redirect;

            try
            {
                var sonuc = db.SinavSonuclaris.Find(id);
                if (sonuc == null) return HttpNotFound();
                
                db.SinavSonuclaris.Remove(sonuc);
                db.SaveChanges();
                TempData["Flash"] = "Sınav sonucu başarıyla silindi.";
            }
            catch (System.Exception)
            {
                TempData["Error"] = "Bu kayıt silinemiyor çünkü sistemde başka bir tabloyla bağlantılı olabilir.";
            }

            return RedirectToAction("Index");
        }

        private void PopulateDropdowns(int? ogrenciId = null, int? dersId = null)
        {
            var ogrenciler = db.Kullanicilars.Where(k => k.Rol == "Ogrenci").OrderBy(k => k.AdSoyad).ToList();
            var dersler = db.Derslers.OrderBy(d => d.DersAdi).ToList();
            ViewBag.OgrenciID = new SelectList(ogrenciler, "KullaniciID", "AdSoyad", ogrenciId);
            ViewBag.DersID = new SelectList(dersler, "DersID", "DersAdi", dersId);
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
