using System;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using EduSmart.Models;

namespace EduSmart.Controllers
{
    public class TeslimlerController : BaseController
    {
        private readonly EduSmart_DBEntities db = new EduSmart_DBEntities();

        public ActionResult Index(int? dersId)
        {
            var redirect = RequireLogin();
            if (redirect != null) return redirect;

            // Only teachers and admins can view submissions
            var redirectRole = RequireTeacherOrAdmin("Teslimleri görüntüleme yetkisi yalnızca öğretmen veya yöneticilerdedir.");
            if (redirectRole != null) return redirectRole;

            // Populate Ders dropdown
            var dersler = db.Derslers.OrderBy(d => d.DersAdi).ToList();
            ViewBag.DersID = new SelectList(dersler, "DersID", "DersAdi", dersId);

            var query = db.Teslimlers
                .Include(t => t.Odevler)
                .Include(t => t.Kullanicilar)
                .Include(t => t.Notlars);

            // Filter by Ders if selected - through Odevler.DersID
            if (dersId.HasValue)
            {
                query = query.Where(t => t.Odevler.DersID == dersId.Value);
            }

            var teslimler = query.OrderByDescending(t => t.TeslimTarihi).ToList();
            ViewBag.SelectedDersID = dersId;
            return View(teslimler);
        }

        public ActionResult Create()
        {
            // Only admins can create submissions manually
            var redirect = RequireLogin();
            if (redirect != null) return redirect;

            if (!IsAdmin())
            {
                TempData["Flash"] = "Teslim ekleme yetkisi yalnızca yöneticilerdedir.";
                return RedirectToAction("Index");
            }

            PopulateDropdowns();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Teslimler model)
        {
            // Only admins can create submissions manually
            var redirect = RequireLogin();
            if (redirect != null) return redirect;

            if (!IsAdmin())
            {
                TempData["Flash"] = "Teslim ekleme yetkisi yalnızca yöneticilerdedir.";
                return RedirectToAction("Index");
            }

            if (ModelState.IsValid)
            {
                db.Teslimlers.Add(model);
                db.SaveChanges();
                TempData["Flash"] = "Teslim eklendi.";
                return RedirectToAction("Index");
            }

            PopulateDropdowns(model.OgrenciID, model.OdevID);
            return View(model);
        }

        public ActionResult Grade(int? id)
        {
            var redirect = RequireTeacherOrAdmin("Not verme yetkisi yalnızca öğretmen veya yöneticilerdedir.");
            if (redirect != null) return redirect;

            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            var teslim = db.Teslimlers
                .Include(t => t.Odevler)
                .Include(t => t.Kullanicilar)
                .Include(t => t.Notlars)
                .FirstOrDefault(t => t.TeslimID == id);
            
            if (teslim == null) return HttpNotFound();

            var existingGrade = teslim.Notlars?.FirstOrDefault();
            ViewBag.ExistingGrade = existingGrade;
            return View(teslim);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Grade(int teslimId, decimal? finalPuani)
        {
            var redirect = RequireTeacherOrAdmin("Not verme yetkisi yalnızca öğretmen veya yöneticilerdedir.");
            if (redirect != null) return redirect;

            var teslim = db.Teslimlers
                .Include(t => t.Notlars)
                .FirstOrDefault(t => t.TeslimID == teslimId);
            
            if (teslim == null) return HttpNotFound();

            if (!finalPuani.HasValue || finalPuani.Value < 0 || finalPuani.Value > 100)
            {
                TempData["Flash"] = "Not 0-100 arasında olmalıdır.";
                return RedirectToAction("Grade", new { id = teslimId });
            }

            var existingGrade = teslim.Notlars?.FirstOrDefault();
            
            if (existingGrade != null)
            {
                // Update existing grade
                existingGrade.FinalPuani = finalPuani.Value;
                existingGrade.PuanlamaTarihi = DateTime.Now;
            }
            else
            {
                // Create new grade
                var not = new Notlar
                {
                    TeslimID = teslimId,
                    FinalPuani = finalPuani.Value,
                    PuanlamaTarihi = DateTime.Now
                };
                db.Notlars.Add(not);
            }

            // Update submission status
            teslim.Durum = "Tamamlandı";
            
            db.SaveChanges();
            TempData["Flash"] = "Not başarıyla kaydedildi.";

            // NOTIFICATION: Notify the student about the grade
            EduSmart.Services.NotificationService.AddNotification(new EduSmart.Models.Notification
            {
                UserId = teslim.OgrenciID,
                Title = "Ödeviniz Notlandırıldı",
                Message = $"'{teslim.Odevler?.Baslik}' ödevi için {finalPuani.Value} puan aldınız.",
                Icon = "fa-solid fa-star text-success"
            });

            return RedirectToAction("Index");
        }

        public ActionResult Edit(int? id)
        {
            // Only admins can edit submissions
            var redirect = RequireLogin();
            if (redirect != null) return redirect;

            if (!IsAdmin())
            {
                TempData["Flash"] = "Teslim düzenleme yetkisi yalnızca yöneticilerdedir.";
                return RedirectToAction("Index");
            }

            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            var teslim = db.Teslimlers.Find(id);
            if (teslim == null) return HttpNotFound();

            PopulateDropdowns(teslim.OgrenciID, teslim.OdevID);
            return View(teslim);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Teslimler model)
        {
            // Only admins can edit submissions
            var redirect = RequireLogin();
            if (redirect != null) return redirect;

            if (!IsAdmin())
            {
                TempData["Flash"] = "Teslim düzenleme yetkisi yalnızca yöneticilerdedir.";
                return RedirectToAction("Index");
            }

            if (ModelState.IsValid)
            {
                db.Entry(model).State = EntityState.Modified;
                db.SaveChanges();
                TempData["Flash"] = "Teslim güncellendi.";
                return RedirectToAction("Index");
            }

            PopulateDropdowns(model.OgrenciID, model.OdevID);
            return View(model);
        }

        public ActionResult Delete(int? id)
        {
            // Only admins can delete submissions
            var redirect = RequireLogin();
            if (redirect != null) return redirect;

            if (!IsAdmin())
            {
                TempData["Flash"] = "Teslim silme yetkisi yalnızca yöneticilerdedir.";
                return RedirectToAction("Index");
            }

            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            var teslim = db.Teslimlers.Include(t => t.Odevler).Include(t => t.Kullanicilar).FirstOrDefault(t => t.TeslimID == id);
            if (teslim == null) return HttpNotFound();
            return View(teslim);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            // Only admins can delete submissions
            var redirect = RequireLogin();
            if (redirect != null) return redirect;

            if (!IsAdmin())
            {
                TempData["Flash"] = "Teslim silme yetkisi yalnızca yöneticilerdedir.";
                return RedirectToAction("Index");
            }

            try
            {
                var teslim = db.Teslimlers.Find(id);
                if (teslim == null) return HttpNotFound();

                db.Teslimlers.Remove(teslim);
                db.SaveChanges();
                TempData["Flash"] = "Teslim silindi.";
                return RedirectToAction("Index");
            }
            catch (System.Data.Entity.Infrastructure.DbUpdateException)
            {
                TempData["Flash"] = "Hata: Bu teslim silinemez çünkü bu teslime ait bir notlandırma (Grade) bulunmaktadır.";
                return RedirectToAction("Index");
            }
        }

        private void PopulateDropdowns(int? ogrenciId = null, int? odevId = null)
        {
            var ogrenciler = db.Kullanicilars.Where(k => k.Rol == "Ogrenci").OrderBy(k => k.AdSoyad).ToList();
            var odevler = db.Odevlers.OrderBy(o => o.Baslik).ToList();
            ViewBag.OgrenciID = new SelectList(ogrenciler, "KullaniciID", "AdSoyad", ogrenciId);
            ViewBag.OdevID = new SelectList(odevler, "OdevID", "Baslik", odevId);
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

