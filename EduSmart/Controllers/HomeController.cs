using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using EduSmart.Models;
using EduSmart.Services;

namespace EduSmart.Controllers
{
    public class HomeController : BaseController
    {
        private readonly EduSmart_DBEntities db = new EduSmart_DBEntities();

        public ActionResult Index()
        {
            var userId = Session["UserId"];
            var role = Session["UserRole"] as string ?? string.Empty;
            var isStudent = role.Equals("Ogrenci", StringComparison.OrdinalIgnoreCase);

            // For students, calculate prioritized assignments using TOPSIS and get unsubmitted assignments
            if (userId != null && isStudent)
            {
                try
                {
                    int ogrenciId = (int)userId;
                    var prioritizedAssignments = TopsisPriorityService.CalculatePriorities(ogrenciId, db);
                    ViewBag.PrioritizedAssignments = prioritizedAssignments;

                    // Get unsubmitted assignments (assignments with no submission or not completed)
                    var studentCourseIds = db.Kayitlars
                        .Where(k => k.OgrenciID == ogrenciId)
                        .Select(k => k.DersID)
                        .ToList();

                    // Fetch assignments from the last 30 days to ensure overdue ones are not hidden in the UI
                    DateTime thirtyDaysAgo = DateTime.Now.AddDays(-30);
                    var allAssignments = db.Odevlers
                        .Where(o => studentCourseIds.Contains(o.DersID) && o.SonTeslimTarihi >= thirtyDaysAgo)
                        .Include(o => o.Dersler)
                        .ToList();

                    var submissions = db.Teslimlers
                        .Where(t => t.OgrenciID == ogrenciId)
                        .ToList();

                    var unsubmittedAssignments = allAssignments
                        .Where(o => 
                        {
                            var submission = submissions.FirstOrDefault(s => s.OdevID == o.OdevID);
                            return submission == null || !IsCompletedSubmission(submission.Durum);
                        })
                        .OrderBy(o => o.SonTeslimTarihi)
                        .ToList();

                    ViewBag.UnsubmittedAssignments = unsubmittedAssignments;
                }
                catch (Exception ex)
                {
                    // Log error but don't crash the page
                    System.Diagnostics.Debug.WriteLine($"Error calculating TOPSIS or fetching assignments: {ex}");
                    ViewBag.PrioritizedAssignments = new List<PrioritizedAssignment>();
                    ViewBag.UnsubmittedAssignments = new List<Odevler>();
                    TempData["Flash"] = "Ödev önceliklendirme hesaplanırken bir hata oluştu.";
                }
            }

            return View();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }

        public ActionResult About()
        {
            ViewBag.Message = "Your application description page.";

            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";

            return View();
        }

        private static bool IsCompletedSubmission(string status)
        {
            return !string.IsNullOrWhiteSpace(status)
                && status.StartsWith("Tamamland", StringComparison.OrdinalIgnoreCase);
        }
    }
}
