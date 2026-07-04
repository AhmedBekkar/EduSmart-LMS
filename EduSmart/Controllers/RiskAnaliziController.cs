using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using EduSmart.Models;

namespace EduSmart.Controllers
{
    public class RiskAnaliziController : BaseController
    {
        private readonly EduSmart_DBEntities db = new EduSmart_DBEntities();

        public ActionResult Index(int? dersId)
        {
            var redirect = RequireLogin();
            if (redirect != null) return redirect;

            // Only teachers and admins can view risk analysis
            var redirectRole = RequireTeacherOrAdmin("Risk analizi görüntüleme yetkisi yalnızca öğretmen veya yöneticilerdedir.");
            if (redirectRole != null) return redirectRole;

            // Populate Ders dropdown
            var dersler = db.Derslers.OrderBy(d => d.DersAdi).ToList();
            ViewBag.DersID = new SelectList(dersler, "DersID", "DersAdi", dersId);

            // Get students - filter by Ders if selected
            IQueryable<Kullanicilar> studentsQuery = db.Kullanicilars.Where(k => k.Rol == "Ogrenci");
            
            if (dersId.HasValue)
            {
                var enrolledStudentIds = db.Kayitlars
                    .Where(k => k.DersID == dersId.Value)
                    .Select(k => k.OgrenciID)
                    .ToList();
                studentsQuery = studentsQuery.Where(k => enrolledStudentIds.Contains(k.KullaniciID));
            }

            var students = studentsQuery.OrderBy(k => k.AdSoyad).ToList();

            var riskResults = new List<RiskAnalysisViewModel>();

            foreach (var student in students)
            {
                var riskScore = CalculateRiskScore(student.KullaniciID);
                var riskLevel = DetermineRiskLevel(riskScore);
                
                // Save or update risk analysis
                var existingRisk = db.RiskAnalizis
                    .FirstOrDefault(r => r.OgrenciID == student.KullaniciID);

                if (existingRisk != null)
                {
                    existingRisk.RiskSeviyesi = riskLevel;
                    existingRisk.RiskPuani = riskScore;
                    existingRisk.HesaplanmaTarihi = DateTime.Now;
                }
                else
                {
                    var newRisk = new RiskAnalizi
                    {
                        OgrenciID = student.KullaniciID,
                        RiskSeviyesi = riskLevel,
                        RiskPuani = riskScore,
                        HesaplanmaTarihi = DateTime.Now
                    };
                    db.RiskAnalizis.Add(newRisk);
                }

                var detaylar = GetRiskDetails(student.KullaniciID);
                var aiData = GenerateAiInsights(detaylar, riskScore);

                riskResults.Add(new RiskAnalysisViewModel
                {
                    OgrenciID = student.KullaniciID,
                    OgrenciAdi = student.AdSoyad,
                    RiskPuani = riskScore,
                    RiskSeviyesi = riskLevel,
                    Detaylar = detaylar,
                    YapayZekaOnerileri = aiData.Oneriler,
                    Egilim = aiData.Egilim
                });
            }

            db.SaveChanges();

            ViewBag.SelectedDersID = dersId;
            return View(riskResults.OrderByDescending(r => r.RiskPuani).ToList());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Recalculate(int? ogrenciId)
        {
            var redirect = RequireTeacherOrAdmin();
            if (redirect != null) return Json(new { success = false, message = "Yetkisiz işlem." });

            if (ogrenciId == null)
            {
                return Json(new { success = false, message = "Geçersiz öğrenci ID." });
            }

            var student = db.Kullanicilars.Find(ogrenciId);
            if (student == null)
            {
                return Json(new { success = false, message = "Öğrenci bulunamadı." });
            }

            var riskScore = CalculateRiskScore(ogrenciId.Value);
            var riskLevel = DetermineRiskLevel(riskScore);

            var existingRisk = db.RiskAnalizis
                .FirstOrDefault(r => r.OgrenciID == ogrenciId.Value);

            if (existingRisk != null)
            {
                existingRisk.RiskSeviyesi = riskLevel;
                existingRisk.RiskPuani = riskScore;
                existingRisk.HesaplanmaTarihi = DateTime.Now;
            }
            else
            {
                var newRisk = new RiskAnalizi
                {
                    OgrenciID = ogrenciId.Value,
                    RiskSeviyesi = riskLevel,
                    RiskPuani = riskScore,
                    HesaplanmaTarihi = DateTime.Now
                };
                db.RiskAnalizis.Add(newRisk);
            }

            db.SaveChanges();
            return Json(new { success = true, message = $"{student.AdSoyad} için analiz güncellendi." });
        }

        private decimal CalculateRiskScore(int ogrenciId)
        {
            decimal riskScore = 0;
            decimal maxScore = 100;

            // Rule 1: Average Exam Score (0-30 points)
            var examScores = db.SinavSonuclaris
                .Where(s => s.OgrenciID == ogrenciId && s.Ortalama.HasValue)
                .Select(s => s.Ortalama.Value)
                .ToList();

            if (examScores.Any())
            {
                var avgExamScore = examScores.Average();
                if (avgExamScore < 50) riskScore += 30;
                else if (avgExamScore < 60) riskScore += 20;
                else if (avgExamScore < 70) riskScore += 10;
            }
            else
            {
                // No exam scores - medium risk
                riskScore += 15;
            }

            // Rule 2: Average Assignment Grade (0-25 points)
            var assignmentGrades = db.Teslimlers
                .Where(t => t.OgrenciID == ogrenciId)
                .SelectMany(t => t.Notlars)
                .Where(n => n.FinalPuani.HasValue)
                .Select(n => n.FinalPuani.Value)
                .ToList();

            if (assignmentGrades.Any())
            {
                var avgAssignmentGrade = assignmentGrades.Average();
                if (avgAssignmentGrade < 50) riskScore += 25;
                else if (avgAssignmentGrade < 60) riskScore += 15;
                else if (avgAssignmentGrade < 70) riskScore += 8;
            }
            else
            {
                // No assignment grades - check if there are ungraded submissions
                var ungradedCount = db.Teslimlers
                    .Where(t => t.OgrenciID == ogrenciId && !t.Notlars.Any())
                    .Count();
                if (ungradedCount > 0) riskScore += 10;
            }

            // Rule 3: Late Submissions (0-20 points)
            var lateSubmissions = db.Teslimlers
                .Where(t => t.OgrenciID == ogrenciId && t.TeslimTarihi.HasValue)
                .Join(db.Odevlers, t => t.OdevID, o => o.OdevID, (t, o) => new { t, o })
                .Where(x => x.t.TeslimTarihi.Value > x.o.SonTeslimTarihi)
                .Count();

            var totalSubmissions = db.Teslimlers
                .Where(t => t.OgrenciID == ogrenciId)
                .Count();

            if (totalSubmissions > 0)
            {
                var latePercentage = (decimal)lateSubmissions / totalSubmissions * 100;
                if (latePercentage > 50) riskScore += 20;
                else if (latePercentage > 30) riskScore += 15;
                else if (latePercentage > 10) riskScore += 8;
            }

            // Rule 4: Submission Rate (0-15 points)
            var enrolledCourses = db.Kayitlars
                .Where(k => k.OgrenciID == ogrenciId)
                .Count();

            if (enrolledCourses > 0)
            {
                var totalAssignments = db.Odevlers
                    .Where(o => db.Kayitlars
                        .Any(k => k.OgrenciID == ogrenciId && k.DersID == o.DersID))
                    .Count();

                if (totalAssignments > 0)
                {
                    var submissionRate = (decimal)totalSubmissions / totalAssignments * 100;
                    if (submissionRate < 50) riskScore += 15;
                    else if (submissionRate < 70) riskScore += 10;
                    else if (submissionRate < 85) riskScore += 5;
                }
                else
                {
                    // No assignments yet - low risk
                    riskScore += 2;
                }
            }

            // Rule 5: Missing Submissions for Past Due Assignments (0-10 points)
            var pastDueAssignments = db.Odevlers
                .Where(o => o.SonTeslimTarihi < DateTime.Now &&
                           db.Kayitlars.Any(k => k.OgrenciID == ogrenciId && k.DersID == o.DersID) &&
                           !db.Teslimlers.Any(t => t.OgrenciID == ogrenciId && t.OdevID == o.OdevID))
                .Count();

            if (pastDueAssignments > 0)
            {
                riskScore += Math.Min(10, pastDueAssignments * 2);
            }

            return Math.Min(riskScore, maxScore);
        }

        private string DetermineRiskLevel(decimal riskScore)
        {
            if (riskScore >= 70) return "Yuksek";
            if (riskScore >= 40) return "Orta";
            return "Dusuk";
        }

        private RiskDetailsViewModel GetRiskDetails(int ogrenciId)
        {
            var examScores = db.SinavSonuclaris
                .Where(s => s.OgrenciID == ogrenciId && s.Ortalama.HasValue)
                .Select(s => s.Ortalama.Value)
                .ToList();

            var assignmentGrades = db.Teslimlers
                .Where(t => t.OgrenciID == ogrenciId)
                .SelectMany(t => t.Notlars)
                .Where(n => n.FinalPuani.HasValue)
                .Select(n => n.FinalPuani.Value)
                .ToList();

            var lateSubmissions = db.Teslimlers
                .Where(t => t.OgrenciID == ogrenciId && t.TeslimTarihi.HasValue)
                .Join(db.Odevlers, t => t.OdevID, o => o.OdevID, (t, o) => new { t, o })
                .Where(x => x.t.TeslimTarihi.Value > x.o.SonTeslimTarihi)
                .Count();

            var totalSubmissions = db.Teslimlers
                .Where(t => t.OgrenciID == ogrenciId)
                .Count();

            var enrolledCourses = db.Kayitlars
                .Where(k => k.OgrenciID == ogrenciId)
                .Count();

            var totalAssignments = db.Odevlers
                .Where(o => db.Kayitlars
                    .Any(k => k.OgrenciID == ogrenciId && k.DersID == o.DersID))
                .Count();

            var pastDueAssignments = db.Odevlers
                .Where(o => o.SonTeslimTarihi < DateTime.Now &&
                           db.Kayitlars.Any(k => k.OgrenciID == ogrenciId && k.DersID == o.DersID) &&
                           !db.Teslimlers.Any(t => t.OgrenciID == ogrenciId && t.OdevID == o.OdevID))
                .Count();

            return new RiskDetailsViewModel
            {
                OrtalamaSinavPuani = examScores.Any() ? (decimal?)examScores.Average() : null,
                OrtalamaOdevPuani = assignmentGrades.Any() ? (decimal?)assignmentGrades.Average() : null,
                GecTeslimSayisi = lateSubmissions,
                ToplamTeslimSayisi = totalSubmissions,
                KayitliDersSayisi = enrolledCourses,
                ToplamOdevSayisi = totalAssignments,
                GecmisOdevSayisi = pastDueAssignments
            };
        }

        private (List<string> Oneriler, string Egilim) GenerateAiInsights(RiskDetailsViewModel details, decimal riskScore)
        {
            var oneriler = new List<string>();
            string egilim = "Stabil ➡️";

            // Trend logic
            if (riskScore > 60) egilim = "Düşüşte 📉";
            else if (riskScore < 30) egilim = "Yükselişte 📈";

            // AI Rules
            if (details.GecmisOdevSayisi > 2)
            {
                oneriler.Add("⚠️ Çok sayıda teslim edilmemiş ödev var. Veli görüşmesi önerilir.");
            }
            
            if (details.GecTeslimSayisi > 3)
            {
                oneriler.Add("⏱️ Ödevleri sürekli geç teslim ediyor. Zaman yönetimi problemi yaşıyor olabilir.");
            }

            if (details.OrtalamaSinavPuani.HasValue && details.OrtalamaSinavPuani.Value < 50)
            {
                oneriler.Add("📉 Sınav ortalaması kritik seviyede. Birebir etüt planlanmalı.");
            }
            else if (details.OrtalamaSinavPuani.HasValue && details.OrtalamaSinavPuani.Value >= 80 && details.GecmisOdevSayisi == 0)
            {
                oneriler.Add("🌟 Mükemmel performans. Başarısı takdir edilerek motive edilmeli.");
            }
            
            if (details.OrtalamaOdevPuani.HasValue && details.OrtalamaSinavPuani.HasValue)
            {
                if (details.OrtalamaOdevPuani.Value > 85 && details.OrtalamaSinavPuani.Value < 50)
                {
                    oneriler.Add("🔍 Sınav stresi veya kopya şüphesi: Ödev notları çok yüksekken sınav notları çok düşük.");
                }
            }

            if (!oneriler.Any())
            {
                oneriler.Add("✅ Öğrencinin genel durumu normal seyrediyor, olağandışı bir risk tespit edilmedi.");
            }

            return (oneriler, egilim);
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

    public class RiskAnalysisViewModel
    {
        public int OgrenciID { get; set; }
        public string OgrenciAdi { get; set; }
        public decimal RiskPuani { get; set; }
        public string RiskSeviyesi { get; set; }
        public RiskDetailsViewModel Detaylar { get; set; }
        public List<string> YapayZekaOnerileri { get; set; } = new List<string>();
        public string Egilim { get; set; }
    }

    public class RiskDetailsViewModel
    {
        public decimal? OrtalamaSinavPuani { get; set; }
        public decimal? OrtalamaOdevPuani { get; set; }
        public int GecTeslimSayisi { get; set; }
        public int ToplamTeslimSayisi { get; set; }
        public int KayitliDersSayisi { get; set; }
        public int ToplamOdevSayisi { get; set; }
        public int GecmisOdevSayisi { get; set; }
    }
}

