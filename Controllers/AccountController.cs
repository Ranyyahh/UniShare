using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Text.RegularExpressions;
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

        [AllowAnonymous, HttpGet]
        public ActionResult Restricted() { return View(); }

        public static void RefreshUserSession(HttpSessionStateBase session, string userIdString)
        {
            if (session == null || string.IsNullOrWhiteSpace(userIdString)) return;
            ObjectId userId;
            if (!ObjectId.TryParse(userIdString, out userId)) return;
            try
            {
                var user = MongoDatabase.Users.Find(x => x.Id == userId).FirstOrDefault();
                if (user != null)
                {
                    session["UserStatus"] = user.Status;
                    session["VerificationStatus"] = user.VerificationStatus;
                }
            }
            catch { }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Login(LoginViewModel model, string returnUrl)
        {
            if (!ModelState.IsValid) return View(model);
            try
            {
                var identifier = (model.SchoolEmail ?? "").Trim();
                if (string.IsNullOrWhiteSpace(identifier))
                {
                    ModelState.AddModelError("", "The email or username is required.");
                    return View(model);
                }

                var regex = new BsonRegularExpression("^" + Regex.Escape(identifier) + "$", "i");
                var filter = Builders<UserDocument>.Filter.Or(
                    Builders<UserDocument>.Filter.Regex(x => x.SchoolEmail, regex),
                    Builders<UserDocument>.Filter.Regex(x => x.Username, regex)
                );
                var user = MongoDatabase.Users.Find(filter).FirstOrDefault();

                if (user != null)
                {
                    AdminAccountabilityService.IsUserRestricted(user.Id);
                    user = MongoDatabase.Users.Find(x => x.Id == user.Id).FirstOrDefault();
                }
                if (user == null || (user.Status != "ACTIVE" && user.Status != "PENDING") || String.IsNullOrEmpty(user.PasswordHash) || !PasswordHasher.Verify(model.Password, user.PasswordHash))
                {
                    ModelState.AddModelError("", "The email/username or password is incorrect.");
                    return View(model);
                }
                Session["UserStatus"] = user.Status;
                Session["VerificationStatus"] = user.VerificationStatus;
                var ticket = new FormsAuthenticationTicket(1, user.Id.ToString(), DateTime.Now,
                    DateTime.Now.Add(model.RememberMe ? TimeSpan.FromDays(14) : TimeSpan.FromHours(8)), model.RememberMe, user.Role);
                Response.Cookies.Add(new HttpCookie(FormsAuthentication.FormsCookieName, FormsAuthentication.Encrypt(ticket))
                { HttpOnly = true, Secure = Request.IsSecureConnection, SameSite = SameSiteMode.Lax });
                if (!String.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
                if (user.Role == "ADMIN") return RedirectToAction("Index", "AdminDashboard");
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
                ModelState.AddModelError("SchoolEmail", "Must be @qcu.edu.ph email");

            var dateFormats = new[] { "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "MM/dd/yyyy", "M/d/yyyy", "dd-MM-yyyy" };
            DateTime birthdate;
            if (!DateTime.TryParseExact((model.Birthdate ?? "").Trim(), dateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out birthdate) || birthdate.Date >= DateTime.Today)
                ModelState.AddModelError("Birthdate", "Use dd/mm/yyyy");
            ValidateDocument(model.StudentDocument, "StudentDocument");
            if (!ModelState.IsValid) return View(model);

            ObjectId userId = ObjectId.Empty;
            var userInserted = false;
            string documentPath = null;
            try
            {
                if (MongoDatabase.Users.Find(x => x.SchoolEmail == email).Any())
                {
                    ModelState.AddModelError("SchoolEmail", "Email already exists");
                    return View(model);
                }
                var studentNo = model.StudentIdNumber.Trim();
                if (MongoDatabase.Users.Find(x => x.StudentIdNumber == studentNo).Any())
                {
                    ModelState.AddModelError("StudentIdNumber", "Student ID already exists");
                    return View(model);
                }
                userId = ObjectId.GenerateNewId();
                var now = DateTime.UtcNow;
                documentPath = SaveDocument(model.StudentDocument, userId, "student-doc");
                MongoDatabase.Users.InsertOne(new UserDocument
                {
                    Id = userId, SchoolEmail = email, Username = model.Username.Trim(),
                    FirstName = model.FirstName.Trim(), LastName = model.LastName.Trim(), Birthdate = birthdate,
                    Phone = model.Phone.Trim(), Section = model.Section.Trim(), StudentIdNumber = model.StudentIdNumber.Trim(),
                    Program = model.Program.Trim(), YearLevel = model.YearLevel,
                    SchoolIdPhoto = documentPath, RegistrationForm = documentPath,
                    Role = "STUDENT", Status = "PENDING", PasswordHash = PasswordHasher.Hash(model.Password),
                    VerificationStatus = "PENDING", CreatedAt = now, UpdatedAt = now
                });
                userInserted = true;
                MongoDatabase.Verifications.InsertOne(new VerificationApplicationDocument
                {
                    Id = ObjectId.GenerateNewId(), UserId = userId, SchoolEmail = email,
                    StudentIdNumber = model.StudentIdNumber.Trim(), Program = model.Program.Trim(), YearLevel = model.YearLevel,
                    SchoolIdPath = documentPath, RegistrationFormPath = documentPath, SubmittedDocument = documentPath,
                    Status = "PENDING", SubmittedAt = now
                });
                return RedirectToAction("Login", new { registered = true });
            }
            catch (Exception ex) when (ex is MongoException || ex is InvalidOperationException || ex is IOException)
            {
                if (userInserted) MongoDatabase.Users.DeleteOne(x => x.Id == userId);
                DeleteUpload(documentPath);
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

                if (user.Role == "ADMIN")
                {
                    var adminName = !string.IsNullOrWhiteSpace(user.Username) ? user.Username : "Administrator";
                    return Json(new
                    {
                        displayName       = adminName,
                        initials          = "A",
                        email             = user.SchoolEmail,
                        role              = user.Role,
                        isAdmin           = true,
                        username          = user.Username ?? "admin",
                        contactNumber     = user.ContactNumber ?? user.Phone ?? "",
                        registered        = user.CreatedAt != default(DateTime)
                                              ? user.CreatedAt.ToLocalTime().ToString("MMM d, yyyy")
                                              : "Active"
                    }, JsonRequestBehavior.AllowGet);
                }

                var verif = MongoDatabase.Verifications.Find(x => x.UserId == user.Id).SortByDescending(x => x.SubmittedAt).FirstOrDefault();
                var studentName = string.Join(" ", new[] { user.FirstName, user.MiddleName, user.LastName }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
                var sFn = (user.FirstName ?? "").Trim();
                var sLn = (user.LastName ?? "").Trim();
                var studentInitials = "ST";
                if (!string.IsNullOrEmpty(sFn) && !string.IsNullOrEmpty(sLn))
                {
                    studentInitials = (sFn.Substring(0, 1) + sLn.Substring(0, 1)).ToUpperInvariant();
                }
                else if (!string.IsNullOrEmpty(sFn))
                {
                    studentInitials = sFn.Substring(0, Math.Min(2, sFn.Length)).ToUpperInvariant();
                }
                else if (!string.IsNullOrEmpty(sLn))
                {
                    studentInitials = sLn.Substring(0, Math.Min(2, sLn.Length)).ToUpperInvariant();
                }

                return Json(new
                {
                    displayName        = string.IsNullOrWhiteSpace(studentName) ? "Student" : studentName,
                    initials           = studentInitials,
                    email              = user.SchoolEmail,
                    role               = user.Role,
                    isAdmin            = false,
                    verificationStatus = user.VerificationStatus ?? "PENDING",
                    studentId          = user.StudentIdNumber ?? (verif != null ? verif.StudentIdNumber : ""),
                    program            = (verif != null && !string.IsNullOrWhiteSpace(verif.Program)) ? verif.Program : (user.Program ?? ""),
                    yearLevel          = (verif != null && !string.IsNullOrWhiteSpace(verif.YearLevel)) ? verif.YearLevel : (user.YearLevel ?? ""),
                    section            = user.Section ?? "",
                    phone              = user.ContactNumber ?? user.Phone ?? "",
                    registered         = user.CreatedAt != default(DateTime)
                                           ? user.CreatedAt.ToLocalTime().ToString("MMM d, yyyy")
                                           : "Active"
                }, JsonRequestBehavior.AllowGet);
            }
            catch { return new HttpUnauthorizedResult(); }
        }

        [HttpGet, Authorize]
        public ActionResult Settings()
        {
            ObjectId userId;
            if (!ObjectId.TryParse(User.Identity.Name, out userId)) return new HttpUnauthorizedResult();
            var user = MongoDatabase.Users.Find(x => x.Id == userId).FirstOrDefault();
            if (user == null) return new HttpUnauthorizedResult();

            if (User.IsInRole("ADMIN"))
            {
                ViewBag.ShowAdminSidebar = true;
                return View("AdminSettings", new AdminSettingsViewModel
                {
                    SchoolEmail  = user.SchoolEmail,
                    Username     = user.Username
                });
            }

            return View(new OwnerProfileEditViewModel
            {
                Program   = user.Program,
                YearLevel = user.YearLevel,
                Campus    = user.Campus,
                Bio       = user.Bio
            });
        }

        [HttpPost, Authorize, ValidateAntiForgeryToken]
        public ActionResult AdminSettings(AdminSettingsViewModel model)
        {
            ViewBag.ShowAdminSidebar = true;
            ObjectId userId;
            if (!ObjectId.TryParse(User.Identity.Name, out userId)) return new HttpUnauthorizedResult();
            var user = MongoDatabase.Users.Find(x => x.Id == userId).FirstOrDefault();
            if (user == null) return new HttpUnauthorizedResult();

            if (string.IsNullOrWhiteSpace(model.NewPassword))
            {
                ModelState.AddModelError("NewPassword", "Please provide a new password to update.");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(model.CurrentPassword) || !PasswordHasher.Verify(model.CurrentPassword, user.PasswordHash))
                    ModelState.AddModelError("CurrentPassword", "Current password is incorrect.");
            }

            if (!ModelState.IsValid)
            {
                model.SchoolEmail = user.SchoolEmail;
                model.Username    = user.Username;
                return View(model);
            }

            var update = Builders<UserDocument>.Update
                .Set(x => x.PasswordHash, PasswordHasher.Hash(model.NewPassword))
                .Set(x => x.UpdatedAt, DateTime.UtcNow);

            MongoDatabase.Users.UpdateOne(x => x.Id == userId, update);
            TempData["ProfileSaved"] = "Password updated successfully.";
            return RedirectToAction("Settings");
        }

        [HttpPost, Authorize, ValidateAntiForgeryToken]
        public ActionResult Settings(OwnerProfileEditViewModel model)
        {
            if (!ModelState.IsValid) return View(model);
            ObjectId userId;
            if (!ObjectId.TryParse(User.Identity.Name, out userId)) return new HttpUnauthorizedResult();
            var update = Builders<UserDocument>.Update
                .Set(x => x.Program,   (model.Program   ?? "").Trim())
                .Set(x => x.YearLevel, (model.YearLevel ?? "").Trim())
                .Set(x => x.Campus,    (model.Campus    ?? "").Trim())
                .Set(x => x.Bio,       (model.Bio       ?? "").Trim())
                .Set(x => x.UpdatedAt, DateTime.UtcNow);
            var result = MongoDatabase.Users.UpdateOne(x => x.Id == userId, update);
            if (result.MatchedCount == 0) return new HttpUnauthorizedResult();
            TempData["ProfileSaved"] = "Your owner profile has been updated.";
            return RedirectToAction("Settings");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Logout()
        {
            FormsAuthentication.SignOut(); Session.Clear(); Session.Abandon();
            Response.Cache.SetCacheability(HttpCacheability.NoCache); Response.Cache.SetNoStore();
            return RedirectToAction("Login", "Account");
        }

        private void ValidateDocument(HttpPostedFileBase file, string key, bool allowPdf = true)
        {
            if (file == null || file.ContentLength == 0) { ModelState.AddModelError(key, "Document required"); return; }
            var extension = Path.GetExtension(Path.GetFileName(file.FileName)).ToLowerInvariant();
            if (file.ContentLength > 8 * 1024 * 1024) ModelState.AddModelError(key, "Max 8 MB allowed");
            var allowed = allowPdf ? new[] { ".jpg", ".jpeg", ".png", ".pdf" } : new[] { ".jpg", ".jpeg", ".png" };
            if (!allowed.Contains(extension)) ModelState.AddModelError(key, allowPdf ? "JPG, PNG, or PDF only" : "JPG or PNG only");
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
