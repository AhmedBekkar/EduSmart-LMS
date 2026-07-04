using System;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using EduSmart.Models;

namespace EduSmart.Controllers
{
    public class OdevlerController : BaseController
    {
        private readonly EduSmart_DBEntities db = new EduSmart_DBEntities();

        public ActionResult Index(int? dersId)
        {
            var redirect = RequireLogin();
            if (redirect != null) return redirect;

            var userId = Session["UserId"];
            var role = Session["UserRole"] as string ?? string.Empty;
            var isStudent = role.Equals("Ogrenci", StringComparison.OrdinalIgnoreCase);

            // Populate Ders dropdown
            var dersler = db.Derslers.OrderBy(d => d.DersAdi).ToList();
            var studentCourseIds = new System.Collections.Generic.List<int>();
            if (isStudent && userId != null)
            {
                studentCourseIds = db.Kayitlars
                    .Where(k => k.OgrenciID == (int)userId)
                    .Select(k => k.DersID)
                    .ToList();
                dersler = dersler.Where(d => studentCourseIds.Contains(d.DersID)).ToList();
            }
            ViewBag.DersID = new SelectList(dersler, "DersID", "DersAdi", dersId);

            IQueryable<Odevler> query = db.Odevlers.Include(o => o.Dersler);

            if (isStudent && userId != null)
            {
                query = query.Where(o => studentCourseIds.Contains(o.DersID));
            }

            // Filter by Ders if selected
            if (dersId.HasValue)
            {
                query = query.Where(o => o.DersID == dersId.Value);
            }

            var odevler = query.OrderByDescending(o => o.OdevID).ToList();

            // For students, check submission status
            if (isStudent && userId != null)
            {
                ViewBag.StudentId = (int)userId;
                var submissions = db.Teslimlers
                    .Where(t => t.OgrenciID == (int)userId)
                    .ToList();
                ViewBag.Submissions = submissions;
            }

            ViewBag.SelectedDersID = dersId;
            return View(odevler);
        }

        [HttpGet]
        public ActionResult Submit(int? id)
        {
            var redirect = RequireLogin();
            if (redirect != null) return redirect;

            if (!IsStudent())
            {
                TempData["Flash"] = "Bu işlem yalnızca öğrenciler için.";
                return RedirectToAction("Index");
            }

            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            var userId = (int)Session["UserId"];
            var odev = db.Odevlers.Include(o => o.Dersler).FirstOrDefault(o => o.OdevID == id);
            if (odev == null) return HttpNotFound();

            // Check if student is registered in this course
            var isRegistered = db.Kayitlars
                .Any(k => k.OgrenciID == userId && k.DersID == odev.DersID);
            
            if (!isRegistered)
            {
                TempData["Flash"] = "Bu ödeve erişim yetkiniz yok.";
                return RedirectToAction("Index");
            }

            // Check if deadline has passed
            if (DateTime.Now > odev.SonTeslimTarihi)
            {
                TempData["Flash"] = "Teslim tarihi geçmiş. Ödevi artık teslim edemez veya güncelleyemezsiniz.";
                return RedirectToAction("Index");
            }

            // Check if already submitted
            var existingSubmission = db.Teslimlers
                .FirstOrDefault(t => t.OdevID == id && t.OgrenciID == userId);

            ViewBag.Odev = odev;
            ViewBag.ExistingSubmission = existingSubmission;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Submit(int odevId, string icerikMetni, HttpPostedFileBase dosya)
        {
            var redirect = RequireLogin();
            if (redirect != null) return redirect;

            if (!IsStudent())
            {
                TempData["Flash"] = "Bu işlem yalnızca öğrenciler için.";
                return RedirectToAction("Index");
            }

            var userId = (int)Session["UserId"];
            var odev = db.Odevlers.Find(odevId);
            if (odev == null)
            {
                TempData["Flash"] = "Ödev bulunamadı.";
                return RedirectToAction("Index");
            }

            // Check if student is registered
            var isRegistered = db.Kayitlars
                .Any(k => k.OgrenciID == userId && k.DersID == odev.DersID);
            
            if (!isRegistered)
            {
                TempData["Flash"] = "Bu ödeve erişim yetkiniz yok.";
                return RedirectToAction("Index");
            }

            // Check if deadline has passed before accepting or writing uploaded files.
            if (DateTime.Now > odev.SonTeslimTarihi)
            {
                TempData["Flash"] = "Teslim tarihi geçmiş. Ödevi artık teslim edemez veya güncelleyemezsiniz.";
                return RedirectToAction("Index");
            }

            // Handle file upload
            string dosyaYolu = null;
            if (dosya != null && dosya.ContentLength > 0)
            {
                var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".txt", ".zip", ".rar" };
                var extension = Path.GetExtension(dosya.FileName)?.ToLowerInvariant();
                
                if (!allowedExtensions.Contains(extension))
                {
                    TempData["Flash"] = "Geçersiz dosya formatı. İzin verilen formatlar: PDF, DOC, DOCX, TXT, ZIP, RAR";
                    return RedirectToAction("Submit", new { id = odevId });
                }

                var uploadsFolder = "~/Uploads/Submissions";
                var uploadsPath = Server.MapPath(uploadsFolder);
                if (!Directory.Exists(uploadsPath))
                {
                    Directory.CreateDirectory(uploadsPath);
                }

                var fileName = $"{userId}_{odevId}_{DateTime.Now:yyyyMMddHHmmss}{extension}";
                var filePath = Path.Combine(uploadsPath, fileName);
                dosya.SaveAs(filePath);
                dosyaYolu = $"{uploadsFolder.Replace("~", "")}/{fileName}";
            }

            // Check if submission exists
            var existingSubmission = db.Teslimlers
                .FirstOrDefault(t => t.OdevID == odevId && t.OgrenciID == userId);

            if (existingSubmission != null)
            {
                // Update existing submission
                existingSubmission.IcerikMetni = icerikMetni;
                if (!string.IsNullOrEmpty(dosyaYolu))
                {
                    // Delete old file if exists
                    if (!string.IsNullOrEmpty(existingSubmission.DosyaYolu))
                    {
                        var oldPath = Server.MapPath(existingSubmission.DosyaYolu);
                        if (System.IO.File.Exists(oldPath))
                        {
                            System.IO.File.Delete(oldPath);
                        }
                    }
                    existingSubmission.DosyaYolu = dosyaYolu;
                }
                existingSubmission.TeslimTarihi = DateTime.Now;
                existingSubmission.Durum = "Beklemede";
            }
            else
            {
                // Create new submission
                var teslim = new Teslimler
                {
                    OdevID = odevId,
                    OgrenciID = userId,
                    IcerikMetni = icerikMetni,
                    DosyaYolu = dosyaYolu,
                    TeslimTarihi = DateTime.Now,
                    Durum = "Beklemede"
                };
                db.Teslimlers.Add(teslim);
            }

            db.SaveChanges();
            TempData["Flash"] = "Ödev başarıyla teslim edildi.";

            // NOTIFICATION: Notify teacher (Admin/Yonetici might be the creator, so let's notify the Yonetici/Ogretmen)
            // We can broadcast to Role "Ogretmen" or specifically find the course teacher if assigned.
            EduSmart.Services.NotificationService.AddNotification(new EduSmart.Models.Notification
            {
                Role = "Ogretmen",
                Title = "Yeni Ödev Teslimi",
                Message = $"'{odev.Baslik}' ödevi için yeni bir teslim yapıldı.",
                Icon = "fa-solid fa-file-pen text-primary"
            });

            return RedirectToAction("Index");
        }

        public ActionResult Create()
        {
            var redirect = RequireTeacherOrAdmin("Ödev ekleme yetkisi yalnızca öğretmen veya yöneticilerdedir.");
            if (redirect != null) return redirect;

            PopulateDersler();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Odevler model)
        {
            var redirect = RequireTeacherOrAdmin("Ödev ekleme yetkisi yalnızca öğretmen veya yöneticilerdedir.");
            if (redirect != null) return redirect;

            // Validate Agirlik (required, 0-100)
            if (!model.Agirlik.HasValue)
            {
                ModelState.AddModelError("Agirlik", "Ağırlık zorunludur.");
            }
            else if (model.Agirlik.Value < 0 || model.Agirlik.Value > 100)
            {
                ModelState.AddModelError("Agirlik", "Ağırlık 0 ile 100 arasında olmalıdır.");
            }

            // Validate ZorlukSeviyesi (required, 1-5)
            if (!model.ZorlukSeviyesi.HasValue)
            {
                ModelState.AddModelError("ZorlukSeviyesi", "Zorluk seviyesi zorunludur.");
            }
            else if (model.ZorlukSeviyesi.Value < 1 || model.ZorlukSeviyesi.Value > 5)
            {
                ModelState.AddModelError("ZorlukSeviyesi", "Zorluk seviyesi 1 ile 5 arasında olmalıdır.");
            }

            if (ModelState.IsValid)
            {
                db.Odevlers.Add(model);
                db.SaveChanges();
                TempData["Flash"] = "Ödev eklendi.";

                // NOTIFICATION: Notify students registered in this course
                var ders = db.Derslers.Find(model.DersID);
                var registeredStudents = db.Kayitlars.Where(k => k.DersID == model.DersID).Select(k => k.OgrenciID).ToList();
                foreach (var studentId in registeredStudents)
                {
                    EduSmart.Services.NotificationService.AddNotification(new EduSmart.Models.Notification
                    {
                        UserId = studentId,
                        Title = "Yeni Ödev",
                        Message = $"'{ders?.DersAdi}' dersi için yeni bir ödev eklendi: {model.Baslik}",
                        Icon = "fa-solid fa-book-open text-warning"
                    });
                }

                return RedirectToAction("Index");
            }
            PopulateDersler(model.DersID);
            return View(model);
        }

        public ActionResult Edit(int? id)
        {
            var redirect = RequireTeacherOrAdmin("Ödev düzenleme yetkisi yalnızca öğretmen veya yöneticilerdedir.");
            if (redirect != null) return redirect;

            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            var odev = db.Odevlers.Find(id);
            if (odev == null) return HttpNotFound();

            PopulateDersler(odev.DersID);
            return View(odev);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Odevler model)
        {
            var redirect = RequireTeacherOrAdmin("Ödev düzenleme yetkisi yalnızca öğretmen veya yöneticilerdedir.");
            if (redirect != null) return redirect;

            // Validate Agirlik (required, 0-100)
            if (!model.Agirlik.HasValue)
            {
                ModelState.AddModelError("Agirlik", "Ağırlık zorunludur.");
            }
            else if (model.Agirlik.Value < 0 || model.Agirlik.Value > 100)
            {
                ModelState.AddModelError("Agirlik", "Ağırlık 0 ile 100 arasında olmalıdır.");
            }

            // Validate ZorlukSeviyesi (required, 1-5)
            if (!model.ZorlukSeviyesi.HasValue)
            {
                ModelState.AddModelError("ZorlukSeviyesi", "Zorluk seviyesi zorunludur.");
            }
            else if (model.ZorlukSeviyesi.Value < 1 || model.ZorlukSeviyesi.Value > 5)
            {
                ModelState.AddModelError("ZorlukSeviyesi", "Zorluk seviyesi 1 ile 5 arasında olmalıdır.");
            }

            if (ModelState.IsValid)
            {
                db.Entry(model).State = EntityState.Modified;
                db.SaveChanges();
                TempData["Flash"] = "Ödev güncellendi.";
                return RedirectToAction("Index");
            }
            PopulateDersler(model.DersID);
            return View(model);
        }

        public ActionResult Delete(int? id)
        {
            var redirect = RequireTeacherOrAdmin("Ödev silme yetkisi yalnızca öğretmen veya yöneticilerdedir.");
            if (redirect != null) return redirect;

            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            var odev = db.Odevlers.Include(o => o.Dersler).FirstOrDefault(o => o.OdevID == id);
            if (odev == null) return HttpNotFound();
            return View(odev);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            var redirect = RequireTeacherOrAdmin("Ödev silme yetkisi yalnızca öğretmen veya yöneticilerdedir.");
            if (redirect != null) return redirect;

            try
            {
                var odev = db.Odevlers.Find(id);
                if (odev == null) return HttpNotFound();

                db.Odevlers.Remove(odev);
                db.SaveChanges();
                TempData["Flash"] = "Ödev silindi.";
                return RedirectToAction("Index");
            }
            catch (System.Data.Entity.Infrastructure.DbUpdateException)
            {
                TempData["Flash"] = "Hata: Bu ödev silinemez çünkü öğrenciler tarafından yapılmış teslimler bulunmaktadır.";
                return RedirectToAction("Index");
            }
        }

        private void PopulateDersler(int? selected = null)
        {
            var dersler = db.Derslers.OrderBy(d => d.DersAdi).ToList();
            ViewBag.DersID = new SelectList(dersler, "DersID", "DersAdi", selected);
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
