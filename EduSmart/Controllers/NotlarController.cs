using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using EduSmart.Models;

namespace EduSmart.Controllers
{
    public class NotlarController : BaseController
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

            var query = db.Notlars.Include(n => n.Teslimler).Include(n => n.Teslimler.Odevler).Include(n => n.Teslimler.Kullanicilar);

            if (isStudent && userId.HasValue)
            {
                query = query.Where(n => n.Teslimler.OgrenciID == userId.Value);
            }

            // Filter by Ders if selected - through Teslimler->Odevler.DersID
            if (dersId.HasValue)
            {
                query = query.Where(n => n.Teslimler.Odevler.DersID == dersId.Value);
            }

            var notlar = query.OrderByDescending(n => n.NotID).ToList();
            ViewBag.SelectedDersID = dersId;
            return View(notlar);
        }

        public ActionResult Create()
        {
            var redirect = RequireTeacherOrAdmin("Not ekleme yetkisi yalnızca öğretmen veya yöneticilerdedir.");
            if (redirect != null) return redirect;

            PopulateTeslimler();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Notlar model)
        {
            var redirect = RequireTeacherOrAdmin("Not ekleme yetkisi yalnızca öğretmen veya yöneticilerdedir.");
            if (redirect != null) return redirect;

            if (ModelState.IsValid)
            {
                db.Notlars.Add(model);
                db.SaveChanges();
                TempData["Flash"] = "Not eklendi.";

                // NOTIFICATION: Get student ID via Teslim
                var teslim = db.Teslimlers.Include(t => t.Odevler).FirstOrDefault(t => t.TeslimID == model.TeslimID);
                if (teslim != null)
                {
                    EduSmart.Services.NotificationService.AddNotification(new EduSmart.Models.Notification
                    {
                        UserId = teslim.OgrenciID,
                        Title = "Yeni Not Eklendi",
                        Message = $"'{teslim.Odevler?.Baslik}' ödeviniz için {model.FinalPuani} puan aldınız.",
                        Icon = "fa-solid fa-star text-success"
                    });
                }

                return RedirectToAction("Index");
            }
            PopulateTeslimler(model.TeslimID);
            return View(model);
        }

        public ActionResult Edit(int? id)
        {
            var redirect = RequireTeacherOrAdmin("Not düzenleme yetkisi yalnızca öğretmen veya yöneticilerdedir.");
            if (redirect != null) return redirect;

            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            var not = db.Notlars.Find(id);
            if (not == null) return HttpNotFound();

            PopulateTeslimler(not.TeslimID);
            return View(not);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Notlar model)
        {
            var redirect = RequireTeacherOrAdmin("Not düzenleme yetkisi yalnızca öğretmen veya yöneticilerdedir.");
            if (redirect != null) return redirect;

            if (ModelState.IsValid)
            {
                db.Entry(model).State = EntityState.Modified;
                db.SaveChanges();
                TempData["Flash"] = "Not güncellendi.";
                return RedirectToAction("Index");
            }
            PopulateTeslimler(model.TeslimID);
            return View(model);
        }

        public ActionResult Delete(int? id)
        {
            var redirect = RequireTeacherOrAdmin("Not silme yetkisi yalnızca öğretmen veya yöneticilerdedir.");
            if (redirect != null) return redirect;

            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            var not = db.Notlars.Include(n => n.Teslimler).Include(n => n.Teslimler.Odevler).Include(n => n.Teslimler.Kullanicilar).FirstOrDefault(n => n.NotID == id);
            if (not == null) return HttpNotFound();
            return View(not);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            var redirect = RequireTeacherOrAdmin("Not silme yetkisi yalnızca öğretmen veya yöneticilerdedir.");
            if (redirect != null) return redirect;

            try
            {
                var not = db.Notlars.Find(id);
                if (not == null) return HttpNotFound();
                db.Notlars.Remove(not);
                db.SaveChanges();
                TempData["Flash"] = "Not silindi.";
            }
            catch (System.Exception)
            {
                TempData["Error"] = "Bu not silinemiyor çünkü sistemde başka bir kayıtla bağlantılı olabilir.";
            }
            return RedirectToAction("Index");
        }

        private void PopulateTeslimler(int? selected = null)
        {
            var teslimler = db.Teslimlers
                .Include(t => t.Kullanicilar)
                .Include(t => t.Odevler)
                .OrderByDescending(t => t.TeslimID)
                .ToList()
                .Select(t => new
                {
                    t.TeslimID,
                    Display = $"{t.TeslimID} - {t.Odevler?.Baslik ?? "Odev"} - {t.Kullanicilar?.AdSoyad ?? "Ogrenci"}"
                });
            ViewBag.TeslimID = new SelectList(teslimler, "TeslimID", "Display", selected);
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
