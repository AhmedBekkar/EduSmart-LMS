using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using EduSmart.Models;

namespace EduSmart.Services
{
    public class TopsisPriorityService
    {
        /// <summary>
        /// Calculates priority scores for student assignments using TOPSIS algorithm
        /// </summary>
        public static List<PrioritizedAssignment> CalculatePriorities(int ogrenciId, EduSmart_DBEntities db)
        {
            // Get all assignments for courses the student is registered in
            var studentCourseIds = db.Kayitlars
                .Where(k => k.OgrenciID == ogrenciId)
                .Select(k => k.DersID)
                .ToList();

            // Get incomplete assignments (not submitted or not completed)
            var allAssignments = db.Odevlers
                .Where(o => studentCourseIds.Contains(o.DersID) && o.SonTeslimTarihi >= DateTime.Now)
                .Include(o => o.Dersler)
                .ToList();

            var incompleteAssignments = new List<Odevler>();
            foreach (var odev in allAssignments)
            {
                var submission = db.Teslimlers
                    .FirstOrDefault(t => t.OdevID == odev.OdevID && t.OgrenciID == ogrenciId);
                
                // Include if not submitted or if submitted but not completed
                if (submission == null || !IsCompletedSubmission(submission.Durum))
                {
                    incompleteAssignments.Add(odev);
                }
            }

            if (incompleteAssignments.Count == 0)
            {
                return new List<PrioritizedAssignment>();
            }

            // Prepare decision matrix
            var decisionMatrix = new List<AssignmentCriteria>();
            var now = DateTime.Now;

            foreach (var odev in incompleteAssignments)
            {
                // C1: Time remaining in hours (negative = overdue, but we filter those out)
                var timeRemaining = (odev.SonTeslimTarihi - now).TotalHours;
                if (timeRemaining < 0) timeRemaining = 0; // Safety check

                // C2: Weight (Agirlik) - default to 50 if null
                var weight = (double)(odev.Agirlik ?? 50);

                // C3: Difficulty (ZorlukSeviyesi) - default to 3 if null, invert for TOPSIS (lower is better)
                var difficulty = odev.ZorlukSeviyesi ?? 3;
                var invertedDifficulty = 6 - difficulty; // Invert: 5 becomes 1, 1 becomes 5

                decisionMatrix.Add(new AssignmentCriteria
                {
                    Assignment = odev,
                    TimeRemainingHours = timeRemaining,
                    Weight = weight,
                    InvertedDifficulty = invertedDifficulty
                });
            }

            // Apply TOPSIS
            return ApplyTopsis(decisionMatrix);
        }

        private static List<PrioritizedAssignment> ApplyTopsis(List<AssignmentCriteria> criteria)
        {
            if (criteria.Count == 0) return new List<PrioritizedAssignment>();

            int n = criteria.Count; // Number of alternatives (assignments)
            int m = 3; // Number of criteria (Time, Weight, Difficulty)

            // Step 1: Normalize the decision matrix
            var normalizedMatrix = new double[n, m];

            // Extract values for normalization
            var timeValues = criteria.Select(c => c.TimeRemainingHours).ToArray();
            var weightValues = criteria.Select(c => c.Weight).ToArray();
            var difficultyValues = criteria.Select(c => c.InvertedDifficulty).ToArray();

            // Calculate sum of squares for each criterion
            double sumSqTime = timeValues.Sum(x => x * x);
            double sumSqWeight = weightValues.Sum(x => x * x);
            double sumSqDifficulty = difficultyValues.Sum(x => x * x);

            // Normalize (avoid division by zero)
            for (int i = 0; i < n; i++)
            {
                normalizedMatrix[i, 0] = sumSqTime > 0 ? timeValues[i] / Math.Sqrt(sumSqTime) : 0;
                normalizedMatrix[i, 1] = sumSqWeight > 0 ? weightValues[i] / Math.Sqrt(sumSqWeight) : 0;
                normalizedMatrix[i, 2] = sumSqDifficulty > 0 ? difficultyValues[i] / Math.Sqrt(sumSqDifficulty) : 0;
            }

            // Step 2: Apply weights (Time: 50%, Weight: 30%, Difficulty: 20%)
            double[] weights = { 0.50, 0.30, 0.20 };
            var weightedMatrix = new double[n, m];

            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < m; j++)
                {
                    weightedMatrix[i, j] = normalizedMatrix[i, j] * weights[j];
                }
            }

            // Step 3: Determine ideal and negative-ideal solutions
            // For all criteria, higher is better (more time, higher weight, lower difficulty = higher inverted)
            var idealSolution = new double[m];
            var negativeIdealSolution = new double[m];

            for (int j = 0; j < m; j++)
            {
                var columnValues = new List<double>();
                for (int i = 0; i < n; i++)
                {
                    columnValues.Add(weightedMatrix[i, j]);
                }
                idealSolution[j] = columnValues.Max();
                negativeIdealSolution[j] = columnValues.Min();
            }

            // Step 4: Calculate distances
            var distancesToIdeal = new double[n];
            var distancesToNegativeIdeal = new double[n];

            for (int i = 0; i < n; i++)
            {
                double sumIdeal = 0;
                double sumNegative = 0;

                for (int j = 0; j < m; j++)
                {
                    double diffIdeal = weightedMatrix[i, j] - idealSolution[j];
                    double diffNegative = weightedMatrix[i, j] - negativeIdealSolution[j];
                    sumIdeal += diffIdeal * diffIdeal;
                    sumNegative += diffNegative * diffNegative;
                }

                distancesToIdeal[i] = Math.Sqrt(sumIdeal);
                distancesToNegativeIdeal[i] = Math.Sqrt(sumNegative);
            }

            // Step 5: Calculate relative closeness (TOPSIS score)
            var results = new List<PrioritizedAssignment>();

            for (int i = 0; i < n; i++)
            {
                double denominator = distancesToIdeal[i] + distancesToNegativeIdeal[i];
                double score = denominator > 0 
                    ? distancesToNegativeIdeal[i] / denominator 
                    : 0;

                results.Add(new PrioritizedAssignment
                {
                    Assignment = criteria[i].Assignment,
                    PriorityScore = Math.Round(score * 100, 2), // Scale to 0-100
                    TimeRemainingHours = criteria[i].TimeRemainingHours,
                    Weight = criteria[i].Weight,
                    Difficulty = (int)Math.Round(6 - criteria[i].InvertedDifficulty) // Convert back to original
                });
            }

            // Step 6: Sort by priority score (descending)
            return results.OrderByDescending(r => r.PriorityScore).ToList();
        }

        private static bool IsCompletedSubmission(string status)
        {
            return !string.IsNullOrWhiteSpace(status)
                && status.StartsWith("Tamamland", StringComparison.OrdinalIgnoreCase);
        }

        private class AssignmentCriteria
        {
            public Odevler Assignment { get; set; }
            public double TimeRemainingHours { get; set; }
            public double Weight { get; set; }
            public double InvertedDifficulty { get; set; }
        }
    }

    public class PrioritizedAssignment
    {
        public Odevler Assignment { get; set; }
        public double PriorityScore { get; set; }
        public double TimeRemainingHours { get; set; }
        public double Weight { get; set; }
        public int Difficulty { get; set; }
    }
}

