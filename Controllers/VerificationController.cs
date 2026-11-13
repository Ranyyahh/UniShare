using System;
using System.IO;
using System.Web;
using System.Linq;
using System.Web.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using UniShare.Models;
using UniShare.Services;

namespace UniShare.Controllers
{
    [Authorize]
    public class VerificationController : Controller
    {
        [HttpGet]
        public ActionResult Apply()
        {
            try
            {
                var userId = ObjectId.Parse(User.Identity.Name);
                var application = MongoDatabase.Verifications.Find(x => x.UserId == userId).SortByDescending(x => x.SubmittedAt).FirstOrDefault();
                if (application != null && application.Status != "RETURNED_FOR_CORRECTION") return RedirectToAction("Status");
                ViewBag.ReviewNotes = application == null ? null : application.ReviewNotes;
                return View(application == null ? new VerificationApplicationViewModel() : new VerificationApplicationViewModel
                { StudentIdNumber = application.StudentIdNumber, Program = application.Program, YearLevel = application.YearLevel });
            }
            catch (Exception ex) when (ex is MongoException || ex is InvalidOperationException)
            { return new HttpStatusCodeResult(503, "Verification is temporarily unavailable."); }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Apply(VerificationApplicationViewModel model)
        {
            ValidateDocument(model.SchoolIdFile, "School ID");
            ValidateDocument(model.RegistrationFormFile, "Registration form");
            ValidateDocument(model.OneByOnePhotoFile, "1×1 photo", false);
            if (!ModelState.IsValid) return View(model);
            string schoolIdPath = null, formPath = null;
            string oneByOnePath = null;
            var applicationSaved = false;
            string oldSchoolIdPath = null, oldOneByOnePath = null, oldFormPath = null;
            try
            {
                var userId = ObjectId.Parse(User.Identity.Name);
                var user = MongoDatabase.Users.Find(x => x.Id == userId).FirstOrDefault();
                if (user == null) return new HttpUnauthorizedResult();
                var application = MongoDatabase.Verifications.Find(x => x.UserId == userId).SortByDescending(x => x.SubmittedAt).FirstOrDefault();
                if (application != null && application.Status != "RETURNED_FOR_CORRECTION") return RedirectToAction("Status");
                if (application != null) { oldSchoolIdPath = application.SchoolIdPath; oldOneByOnePath = application.OneByOnePhotoPath; oldFormPath = application.RegistrationFormPath; }
                schoolIdPath = SaveFile(model.SchoolIdFile, userId, "school-id");
                oneByOnePath = SaveFile(model.OneByOnePhotoFile, userId, "one-by-one");
                formPath = SaveFile(model.RegistrationFormFile, userId, "registration-form");
                var now = DateTime.UtcNow;
                if (application == null)
                {
                    application = new VerificationApplicationDocument { Id = ObjectId.GenerateNewId(), UserId = userId, SchoolEmail = user.SchoolEmail };
                    application.StudentIdNumber = model.StudentIdNumber.Trim();
                    application.Program = model.Program.Trim(); application.YearLevel = model.YearLevel;
                    application.SchoolIdPath = schoolIdPath; application.OneByOnePhotoPath = oneByOnePath; application.RegistrationFormPath = formPath;
                    application.Status = "PENDING"; application.SubmittedAt = now;
                    MongoDatabase.Verifications.InsertOne(application);
                    applicationSaved = true;
                }
                else
                {
                    MongoDatabase.Verifications.UpdateOne(x => x.Id == application.Id,
                        Builders<VerificationApplicationDocument>.Update.Set(x => x.StudentIdNumber, model.StudentIdNumber.Trim())
                            .Set(x => x.Program, model.Program.Trim()).Set(x => x.YearLevel, model.YearLevel)
                            .Set(x => x.SchoolIdPath, schoolIdPath).Set(x => x.OneByOnePhotoPath, oneByOnePath).Set(x => x.RegistrationFormPath, formPath)
                            .Set(x => x.Status, "PENDING").Set(x => x.ReviewNotes, null).Set(x => x.SubmittedAt, now)
                            .Set(x => x.ReviewedAt, null).Set(x => x.ReviewedBy, null));
                    applicationSaved = true;
                }
                MongoDatabase.Users.UpdateOne(x => x.Id == userId,
                    Builders<UserDocument>.Update.Set(x => x.StudentIdNumber, model.StudentIdNumber.Trim())
                        .Set(x => x.SchoolIdPhoto, schoolIdPath).Set(x => x.OneByOnePhoto, oneByOnePath).Set(x => x.RegistrationForm, formPath)
                        .Set(x => x.VerificationStatus, "PENDING").Set(x => x.UpdatedAt, now));
                DeleteFile(oldSchoolIdPath); DeleteFile(oldOneByOnePath); DeleteFile(oldFormPath);
                return RedirectToAction("Status");
            }
            catch (Exception ex) when (ex is MongoException || ex is InvalidOperationException || ex is IOException)
            {
                if (!applicationSaved) { DeleteFile(schoolIdPath); DeleteFile(oneByOnePath); DeleteFile(formPath); }
                ModelState.AddModelError("", "We could not save your application. Check the database connection and try again.");
                return View(model);
            }
        }

        [HttpGet]
        public ActionResult Status()
        {
            try
            {
                var userId = ObjectId.Parse(User.Identity.Name);
                var application = MongoDatabase.Verifications.Find(x => x.UserId == userId).SortByDescending(x => x.SubmittedAt).FirstOrDefault();
                var user = MongoDatabase.Users.Find(x => x.Id == userId).FirstOrDefault();
                if (application == null) return View("Status", new VerificationReviewViewModel { Status = user == null ? "UNKNOWN" : user.VerificationStatus ?? "NOT_SUBMITTED" });
                return View("Status", ToViewModel(application, user));
            }
            catch (Exception ex) when (ex is MongoException || ex is InvalidOperationException)
            { return View("Status", new VerificationReviewViewModel { Status = "UNAVAILABLE" }); }
        }

        [HttpGet, Authorize(Roles = "ADMIN"), OutputCache(NoStore = true, Duration = 0, VaryByParam = "*")]
        public ActionResult Document(string id, string kind)
        {
            ObjectId objectId;
            if (!ObjectId.TryParse(id, out objectId) || (kind != "id" && kind != "photo" && kind != "form")) return HttpNotFound();
            var application = MongoDatabase.Verifications.Find(x => x.Id == objectId).FirstOrDefault();
            if (application == null)
            {
                application = MongoDatabase.Verifications.Find(x => x.UserId == objectId).SortByDescending(x => x.SubmittedAt).FirstOrDefault();
            }
            var user = application != null
                ? MongoDatabase.Users.Find(x => x.Id == application.UserId).FirstOrDefault()
                : MongoDatabase.Users.Find(x => x.Id == objectId).FirstOrDefault();
            if (application == null && user == null) return HttpNotFound();

            var name = kind == "id" ? ((application != null ? application.SchoolIdPath : null) ?? (user != null ? (user.SchoolIdPhoto ?? user.SubmittedDocument) : null))
                : kind == "photo" ? ((application != null ? application.OneByOnePhotoPath : null) ?? (user != null ? user.OneByOnePhoto : null))
                : ((application != null ? (application.RegistrationFormPath ?? application.SubmittedDocument) : null) ?? (user != null ? (user.RegistrationForm ?? user.SubmittedDocument) : null));
            var path = Server.MapPath("~/App_Data/Verification/" + Path.GetFileName(name ?? ""));
            if (!System.IO.File.Exists(path)) return HttpNotFound();
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext != ".pdf" && ext != ".png" && ext != ".jpg" && ext != ".jpeg") return HttpNotFound();
            var contentType = ext == ".pdf" ? "application/pdf" : ext == ".png" ? "image/png" : "image/jpeg";
            return File(path, contentType);
        }

        private string SaveFile(HttpPostedFileBase file, ObjectId userId, string prefix)
        {
            var ext = Path.GetExtension(Path.GetFileName(file.FileName)).ToLowerInvariant();
            var directory = Server.MapPath("~/App_Data/Verification");
            Directory.CreateDirectory(directory);
            var name = userId + "-" + prefix + "-" + Guid.NewGuid().ToString("N") + ext;
            file.SaveAs(Path.Combine(directory, name));
            return name;
        }

        private void DeleteFile(string name)
        {
            if (String.IsNullOrEmpty(name)) return;
            var path = Server.MapPath("~/App_Data/Verification/" + Path.GetFileName(name));
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        }

        private void ValidateDocument(HttpPostedFileBase file, string label, bool allowPdf = true)
        {
            if (file == null || file.ContentLength == 0) { ModelState.AddModelError("", label + " is required."); return; }
            var extension = Path.GetExtension(Path.GetFileName(file.FileName)).ToLowerInvariant();
            if (file.ContentLength > 8 * 1024 * 1024) ModelState.AddModelError("", label + " must be 8 MB or smaller.");
            var allowed = allowPdf ? new[] { ".jpg", ".jpeg", ".png", ".pdf" } : new[] { ".jpg", ".jpeg", ".png" };
            if (!allowed.Contains(extension)) ModelState.AddModelError("", label + (allowPdf ? " must be a JPG, PNG, or PDF." : " must be a JPG or PNG image."));
        }

        internal static VerificationReviewViewModel ToViewModel(VerificationApplicationDocument item, UserDocument user)
        {
            return new VerificationReviewViewModel
            {
                Id = item.Id.ToString(), UserId = item.UserId.ToString(),
                StudentName = user == null ? "Unknown student" : user.FirstName + " " + user.LastName,
                SchoolEmail = item.SchoolEmail ?? (user != null ? user.SchoolEmail : ""),
                StudentIdNumber = item.StudentIdNumber ?? (user != null ? user.StudentIdNumber : ""),
                Program = item.Program, YearLevel = item.YearLevel, Status = item.Status,
                ReviewNotes = item.ReviewNotes ?? item.Remarks,
                SubmittedAt = item.SubmittedAt != default(DateTime) ? item.SubmittedAt : (item.CreatedAt ?? DateTime.UtcNow),
                SchoolIdUrl = item.Id.ToString() + "/id", OneByOneUrl = item.Id.ToString() + "/photo",
                RegistrationFormUrl = item.Id.ToString() + "/form"
            };
        }
    }
}
