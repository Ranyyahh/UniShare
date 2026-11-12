using System;
using System.Linq;
using System.Web.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using UniShare.Models;
using UniShare.Services;

namespace UniShare.Controllers
{
    [Authorize(Roles = "ADMIN")]
    public class AdminController : Controller
    {
        public ActionResult Index() { return View(); }

        [HttpGet]
        public ActionResult StudentVerification(string status = "PENDING")
        {
            return RedirectToAction("Index", "AdminStudentVerification");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult ReviewVerification(string id, string decision, string reviewNotes)
        {
            ObjectId verificationId;
            var allowed = new[] { "APPROVED", "REJECTED" };
            if (!ObjectId.TryParse(id, out verificationId) || !allowed.Contains((decision ?? "").ToUpperInvariant())) return new HttpStatusCodeResult(400);
            try
            {
                if (reviewNotes != null && reviewNotes.Length > 1000) reviewNotes = reviewNotes.Substring(0, 1000);
                var application = MongoDatabase.Verifications.Find(x => x.Id == verificationId && x.Status == "PENDING").FirstOrDefault();
                if (application == null) return HttpNotFound();
                var now = DateTime.UtcNow;
                var adminId = ObjectId.Parse(User.Identity.Name);
                var status = decision.ToUpperInvariant();
                var result = MongoDatabase.Verifications.UpdateOne(x => x.Id == verificationId && x.Status == "PENDING",
                    Builders<VerificationApplicationDocument>.Update.Set(x => x.Status, status).Set(x => x.ReviewNotes, reviewNotes).Set(x => x.ReviewedAt, now).Set(x => x.ReviewedBy, adminId));
                if (result.ModifiedCount != 1) return new HttpStatusCodeResult(409, "This application is no longer pending review.");
                MongoDatabase.Users.UpdateOne(x => x.Id == application.UserId,
                    Builders<UserDocument>.Update.Set(x => x.VerificationStatus, status)
                        .Set(x => x.Status, status == "APPROVED" ? "ACTIVE" : "PENDING")
                        .Set(x => x.UpdatedAt, now));
                try
                {
                    MongoDatabase.AuditLogs.InsertOne(new AuditLogDocument
                    {
                        Id = ObjectId.GenerateNewId(),
                        UserId = adminId,
                        Action = status == "APPROVED" ? "APPROVE_STUDENT" : "REJECT_STUDENT",
                        EntityType = "USER",
                        EntityId = application.UserId,
                        Details = new BsonDocument { { "verificationId", verificationId }, { "status", status }, { "notes", reviewNotes ?? "" } },
                        CreatedAt = now
                    });
                }
                catch { }
                return RedirectToAction("StudentVerification", new { status = "PENDING" });
            }
            catch (FormatException) { return new HttpStatusCodeResult(401); }
            catch (Exception ex) when (ex is MongoException || ex is InvalidOperationException)
            { TempData["ReviewError"] = "The decision could not be saved. Check the database connection and try again."; return RedirectToAction("StudentVerification"); }
        }

        public ActionResult ItemListings() { return View(); }
        public ActionResult DisputeReports() { return View(); }
        public ActionResult ReportsAndAnalytics() { return RedirectToAction("Index", "AdminReportsAnalytics"); }
    }
}
