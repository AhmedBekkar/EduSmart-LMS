using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using EduSmart.Models;

namespace EduSmart.Controllers
{
    public class AnalyticsController : BaseController
    {
        private readonly EduSmart_DBEntities db = new EduSmart_DBEntities();

        public ActionResult Index()
        {
            var redirect = RequireLogin();
            if (redirect != null) return redirect;

            var userId = (int)Session["UserId"];
            var role = Session["UserRole"] as string ?? string.Empty;
            var isStudent = role.Equals("Ogrenci", StringComparison.OrdinalIgnoreCase);

            var model = new AnalyticsDashboardViewModel();
            model.IsStudent = isStudent;

            if (isStudent)
            {
                // STUDENT METRICS
                var enrolledCourses = db.Kayitlars.Where(k => k.OgrenciID == userId).Select(k => k.DersID).ToList();
                model.TotalCourses = enrolledCourses.Count;

                var studentExams = db.SinavSonuclaris.Where(s => s.OgrenciID == userId && s.Ortalama.HasValue).Select(s => s.Ortalama.Value).ToList();
                model.AverageExamScore = studentExams.Any() ? Math.Round(studentExams.Average(), 1) : 0;

                var studentAssignments = db.Teslimlers.Where(t => t.OgrenciID == userId).SelectMany(t => t.Notlars).Where(n => n.FinalPuani.HasValue).Select(n => n.FinalPuani.Value).ToList();
                model.AverageAssignmentScore = studentAssignments.Any() ? Math.Round(studentAssignments.Average(), 1) : 0;

                var totalAssignmentsRequired = db.Odevlers.Count(o => enrolledCourses.Contains(o.DersID));
                var totalAssignmentsSubmitted = db.Teslimlers.Count(t => t.OgrenciID == userId);
                
                model.SubmissionRate = totalAssignmentsRequired > 0 
                    ? Math.Round(((decimal)totalAssignmentsSubmitted / totalAssignmentsRequired) * 100, 1) 
                    : 100;

                // Chart Data: Recent Exams
                var recentExams = db.SinavSonuclaris
                    .Where(s => s.OgrenciID == userId && s.Ortalama.HasValue)
                    .OrderByDescending(s => s.SinavSonucID)
                    .Take(5)
                    .Select(s => new ChartData { Label = s.Dersler.DersAdi, Value = s.Ortalama.Value })
                    .ToList();
                
                model.RecentExamsData = recentExams;
            }
            else
            {
                // TEACHER / ADMIN METRICS
                model.TotalStudents = db.Kullanicilars.Count(k => k.Rol == "Ogrenci");
                model.TotalTeachers = db.Kullanicilars.Count(k => k.Rol == "Ogretmen");
                model.TotalCourses = db.Derslers.Count();

                var allExams = db.SinavSonuclaris.Where(s => s.Ortalama.HasValue).Select(s => s.Ortalama.Value).ToList();
                model.AverageExamScore = allExams.Any() ? Math.Round(allExams.Average(), 1) : 0;

                var allAssignments = db.Notlars.Where(n => n.FinalPuani.HasValue).Select(n => n.FinalPuani.Value).ToList();
                model.AverageAssignmentScore = allAssignments.Any() ? Math.Round(allAssignments.Average(), 1) : 0;

                // Submission Rate Platform Wide
                var totalExpected = db.Odevlers.Select(o => o.DersID).Join(db.Kayitlars, dId => dId, k => k.DersID, (dId, k) => k).Count();
                var totalActual = db.Teslimlers.Count();
                model.SubmissionRate = totalExpected > 0 ? Math.Round(((decimal)totalActual / totalExpected) * 100, 1) : 0;

                // Top 5 Courses by Avg Exam Score
                var courseAverages = db.SinavSonuclaris
                    .Where(s => s.Ortalama.HasValue)
                    .GroupBy(s => s.Dersler.DersAdi)
                    .Select(g => new ChartData { Label = g.Key, Value = g.Average(s => s.Ortalama.Value) })
                    .OrderByDescending(c => c.Value)
                    .Take(5)
                    .ToList();

                model.CourseAveragesData = courseAverages;
            }

            return View(model);
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

    public class AnalyticsDashboardViewModel
    {
        public bool IsStudent { get; set; }
        public int TotalStudents { get; set; }
        public int TotalTeachers { get; set; }
        public int TotalCourses { get; set; }
        public decimal AverageExamScore { get; set; }
        public decimal AverageAssignmentScore { get; set; }
        public decimal SubmissionRate { get; set; }

        public List<ChartData> RecentExamsData { get; set; } = new List<ChartData>();
        public List<ChartData> CourseAveragesData { get; set; } = new List<ChartData>();
    }

    public class ChartData
    {
        public string Label { get; set; }
        public decimal Value { get; set; }
    }
}
