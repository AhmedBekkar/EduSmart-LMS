using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using EduSmart.Models;

namespace EduSmart.Controllers
{
    public class KayitlarController : BaseController
    {
        private readonly EduSmart_DBEntities db = new EduSmart_DBEntities();

        public ActionResult Index()
        {
            var redirect = RequireLogin();
            if (redirect != null) return redirect;

            var query = db.Kayitlars
                .Include(k => k.Kullanicilar)
                .Include(k => k.Dersler);

            if (IsStudent())
            {
                var userId = (int)Session["UserId"];
                query = query.Where(k => k.OgrenciID == userId);
            }

            var kayitlar = query
                .OrderByDescending(k => k.KayitID)
                .ToList();
            return View(kayitlar);
        }

        public ActionResult Create()
        {
            var redirect = RequireTeacherOrAdmin("Kayıt ekleme yetkisi yalnızca öğretmen veya yöneticilerdedir.");
            if (redirect != null) return redirect;

            PopulateDropdowns();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Kayitlar model)
        {
            var redirect = RequireTeacherOrAdmin("Kayıt ekleme yetkisi yalnızca öğretmen veya yöneticilerdedir.");
            if (redirect != null) return redirect;

            if (db.Kayitlars.Any(k => k.OgrenciID == model.OgrenciID && k.DersID == model.DersID))
            {
                ModelState.AddModelError("", "Bu ogrenci secilen derse zaten kayitli.");
            }

            if (ModelState.IsValid)
            {
                if (!model.KayitTarihi.HasValue)
                {
                    model.KayitTarihi = System.DateTime.Now;
                }

                db.Kayitlars.Add(model);
                db.SaveChanges();
                TempData["Flash"] = "Kayıt eklendi.";
                return RedirectToAction("Index");
            }

            PopulateDropdowns(model.OgrenciID, model.DersID);
            return View(model);
        }

        public ActionResult Delete(int? id)
        {
            var redirect = RequireTeacherOrAdmin("Kayıt silme yetkisi yalnızca öğretmen veya yöneticilerdedir.");
            if (redirect != null) return redirect;

            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            var kayit = db.Kayitlars.Include(k => k.Kullanicilar).Include(k => k.Dersler).FirstOrDefault(k => k.KayitID == id);
            if (kayit == null) return HttpNotFound();

            return View(kayit);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            var redirect = RequireTeacherOrAdmin("Kayıt silme yetkisi yalnızca öğretmen veya yöneticilerdedir.");
            if (redirect != null) return redirect;

            try
            {
                var kayit = db.Kayitlars.Find(id);
                if (kayit == null) return HttpNotFound();

                db.Kayitlars.Remove(kayit);
                db.SaveChanges();
                TempData["Flash"] = "Kayıt silindi.";
                return RedirectToAction("Index");
            }
            catch (System.Data.Entity.Infrastructure.DbUpdateException)
            {
                TempData["Flash"] = "Hata: Bu kayıt silinemez çünkü sistemde buna bağlı başka veriler bulunmaktadır.";
                return RedirectToAction("Index");
            }
        }

        private void PopulateDropdowns(int? ogrenciId = null, int? dersId = null)
        {
            var ogrenciler = db.Kullanicilars.Where(k => k.Rol == "Ogrenci").OrderBy(k => k.AdSoyad).ToList();
            
            // Filter out subjects that the student already has
            var dersler = db.Derslers.OrderBy(d => d.DersAdi).ToList();
            if (ogrenciId.HasValue)
            {
                var enrolledDersIds = db.Kayitlars
                    .Where(k => k.OgrenciID == ogrenciId.Value)
                    .Select(k => k.DersID)
                    .ToList();
                dersler = dersler.Where(d => !enrolledDersIds.Contains(d.DersID)).ToList();
            }
            
            ViewBag.OgrenciID = new SelectList(ogrenciler, "KullaniciID", "AdSoyad", ogrenciId);
            ViewBag.DersID = new SelectList(dersler, "DersID", "DersAdi", dersId);
        }

        [HttpGet]
        public JsonResult GetAvailableSubjects(int ogrenciId)
        {
            var enrolledDersIds = db.Kayitlars
                .Where(k => k.OgrenciID == ogrenciId)
                .Select(k => k.DersID)
                .ToList();
            
            var availableDersler = db.Derslers
                .Where(d => !enrolledDersIds.Contains(d.DersID))
                .OrderBy(d => d.DersAdi)
                .Select(d => new { d.DersID, d.DersAdi })
                .ToList();
            
            return Json(availableDersler, JsonRequestBehavior.AllowGet);
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
