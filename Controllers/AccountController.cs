using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using MongoDB.Bson;
using MongoDB.Driver;
using UniShare.Models;
using UniShare.Services;

namespace UniShare.Controllers
{
    public class AccountController : Controller
    {
        [HttpGet] public ActionResult Login(string returnUrl) { ViewBag.ReturnUrl = returnUrl; return View(); }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Login(LoginViewModel model, string returnUrl)
        {
            if (!ModelState.IsValid) return View(model);
            try
            {
                var email = model.SchoolEmail.Trim().ToLowerInvariant();
                var user = MongoDatabase.Users.Find(x => x.SchoolEmail == email).FirstOrDefault();
                if (user == null || (user.Status != "ACTIVE" && user.Status != "PENDING") || String.IsNullOrEmpty(user.PasswordHash) || !PasswordHasher.Verify(model.Password, user.PasswordHash))
                {
                    ModelState.AddModelError("", "The email or password is incorrect.");
                    return View(model);
                }
                var ticket = new FormsAuthenticationTicket(1, user.Id.ToString(), DateTime.Now,
                    DateTime.Now.Add(model.RememberMe ? TimeSpan.FromDays(14) : TimeSpan.FromHours(8)), model.RememberMe, user.Role);
                Response.Cookies.Add(new HttpCookie(FormsAuthentication.FormsCookieName, FormsAuthentication.Encrypt(ticket))
                { HttpOnly = true, Secure = Request.IsSecureConnection, SameSite = SameSiteMode.Lax });
                if (!String.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
                if (user.Role == "ADMIN") return RedirectToAction("StudentVerification", "Admin");
                if (user.VerificationStatus == "APPROVED") return RedirectToAction("Index", "Dashboard");
                return RedirectToAction("Status", "Verification");
            }
            catch (Exception ex) when (ex is MongoException || ex is InvalidOperationException)
            {
                ModelState.AddModelError("", "Sign in is temporarily unavailable. Check the database connection and try again.");
                return View(model);
            }
        }

        [HttpGet] public ActionResult Register() { return View(new RegisterViewModel()); }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Register(RegisterViewModel model)
        {
            var email = (model.SchoolEmail ?? "").Trim().ToLowerInvariant();
            if (!email.EndsWith("@unishare.qcu.edu.ph") && !email.EndsWith("@qcu.edu.ph"))
                ModelState.AddModelError("SchoolEmail", "Use your official @qcu.edu.ph or @unishare.qcu.edu.ph school email.");

            var dateFormats = new[] { "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "MM/dd/yyyy", "M/d/yyyy", "dd-MM-yyyy" };
            DateTime birthdate;
            if (!DateTime.TryParseExact((model.Birthdate ?? "").Trim(), dateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out birthdate) || birthdate.Date >= DateTime.Today)
                ModelState.AddModelError("Birthdate", "Enter a valid birthdate (e.g. dd/mm/yyyy).");
            ValidateDocument(model.SchoolIdPhoto, "School ID photo");
            ValidateDocument(model.OneByOnePhoto, "1×1 photo", false);
            ValidateDocument(model.RegistrationForm, "Registration form");
            if (!ModelState.IsValid) return View(model);

            ObjectId userId = ObjectId.Empty;
            var userInserted = false;
            string schoolIdPath = null, oneByOnePath = null, registrationFormPath = null;
            try
            {
                if (MongoDatabase.Users.Find(x => x.SchoolEmail == email).Any())
                {
                    ModelState.AddModelError("SchoolEmail", "An account with this school email already exists.");
                    return View(model);
                }
                userId = ObjectId.GenerateNewId();
                var now = DateTime.UtcNow;
                schoolIdPath = SaveDocument(model.SchoolIdPhoto, userId, "school-id");
                oneByOnePath = SaveDocument(model.OneByOnePhoto, userId, "one-by-one");
                registrationFormPath = SaveDocument(model.RegistrationForm, userId, "registration-form");
                MongoDatabase.Users.InsertOne(new UserDocument
                {
                    Id = userId, SchoolEmail = email, Username = model.Username.Trim(),
                    FirstName = model.FirstName.Trim(), LastName = model.LastName.Trim(), Birthdate = birthdate,
                    Phone = model.Phone.Trim(), Section = model.Section.Trim(), StudentIdNumber = model.StudentIdNumber.Trim(),
                    SchoolIdPhoto = schoolIdPath, OneByOnePhoto = oneByOnePath, RegistrationForm = registrationFormPath,
                    Role = "STUDENT", Status = "PENDING", PasswordHash = PasswordHasher.Hash(model.Password),
                    VerificationStatus = "PENDING", CreatedAt = now, UpdatedAt = now
                });
                userInserted = true;
                MongoDatabase.Verifications.InsertOne(new VerificationApplicationDocument
                {
                    Id = ObjectId.GenerateNewId(), UserId = userId, SchoolEmail = email,
                    StudentIdNumber = model.StudentIdNumber.Trim(), Program = model.Program.Trim(), YearLevel = model.YearLevel,
                    SchoolIdPath = schoolIdPath, OneByOnePhotoPath = oneByOnePath, RegistrationFormPath = registrationFormPath,
                    Status = "PENDING", SubmittedAt = now
                });
                return RedirectToAction("Login", new { registered = true });
            }
            catch (Exception ex) when (ex is MongoException || ex is InvalidOperationException || ex is IOException)
            {
                if (userInserted) MongoDatabase.Users.DeleteOne(x => x.Id == userId);
                DeleteUpload(schoolIdPath); DeleteUpload(oneByOnePath); DeleteUpload(registrationFormPath);
                ModelState.AddModelError("", "Registration could not be saved. Check the database connection and try again.");
                return View(model);
            }
        }

        [HttpGet, Authorize, OutputCache(NoStore = true, Duration = 0, VaryByParam = "*")]
        public new ActionResult Profile()
        {
            try
            {
                var user = MongoDatabase.Users.Find(x => x.Id == ObjectId.Parse(User.Identity.Name)).FirstOrDefault();
                if (user == null) return new HttpUnauthorizedResult();
                var verif = MongoDatabase.Verifications.Find(x => x.UserId == user.Id).SortByDescending(x => x.SubmittedAt).FirstOrDefault();
                return Json(new
                {
                    displayName = user.FirstName + " " + user.LastName,
                    email = user.SchoolEmail,
                    role = user.Role,
                    verificationStatus = user.VerificationStatus ?? "PENDING",
                    studentId = user.StudentIdNumber ?? (verif != null ? verif.StudentIdNumber : ""),
                    program = verif != null ? verif.Program : "",
                    yearLevel = verif != null ? verif.YearLevel : "",
                    section = user.Section ?? "",
                    phone = user.Phone ?? user.ContactNumber ?? "",
                    registered = user.CreatedAt != default(DateTime) ? user.CreatedAt.ToLocalTime().ToString("MMM d, yyyy") : "Active"
                }, JsonRequestBehavior.AllowGet);
            }
            catch { return new HttpUnauthorizedResult(); }
        }

        [HttpGet] public ActionResult Settings() { return View(); }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Logout()
        {
            FormsAuthentication.SignOut(); Session.Clear(); Session.Abandon();
            Response.Cache.SetCacheability(HttpCacheability.NoCache); Response.Cache.SetNoStore();
            return RedirectToAction("Login", "Account");
        }

        private void ValidateDocument(HttpPostedFileBase file, string label, bool allowPdf = true)
        {
            if (file == null || file.ContentLength == 0) { ModelState.AddModelError("", label + " is required."); return; }
            var extension = Path.GetExtension(Path.GetFileName(file.FileName)).ToLowerInvariant();
            if (file.ContentLength > 8 * 1024 * 1024) ModelState.AddModelError("", label + " must be 8 MB or smaller.");
            var allowed = allowPdf ? new[] { ".jpg", ".jpeg", ".png", ".pdf" } : new[] { ".jpg", ".jpeg", ".png" };
            if (!allowed.Contains(extension)) ModelState.AddModelError("", label + (allowPdf ? " must be a JPG, PNG, or PDF." : " must be a JPG or PNG image."));
        }

        private string SaveDocument(HttpPostedFileBase file, ObjectId userId, string prefix)
        {
            var extension = Path.GetExtension(Path.GetFileName(file.FileName)).ToLowerInvariant();
            var directory = Server.MapPath("~/App_Data/Verification");
            Directory.CreateDirectory(directory);
            var filename = userId + "-" + prefix + "-" + Guid.NewGuid().ToString("N") + extension;
            file.SaveAs(Path.Combine(directory, filename));
            return filename;
        }

        private void DeleteUpload(string name)
        {
            if (String.IsNullOrEmpty(name)) return;
            var path = Server.MapPath("~/App_Data/Verification/" + Path.GetFileName(name));
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        }

    }
}
