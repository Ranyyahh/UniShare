using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Mvc;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using UniShare.Models;
using UniShare.Services;

namespace UniShare.Controllers
{
    [Authorize(Roles = "ADMIN")]
    public class AdminStudentVerificationController : Controller
    {
        private const int DefaultPageSize = 10;

        [HttpGet]
        public ActionResult Index()
        {
            ViewBag.Title = "Student Account Verification and Management";
            ViewBag.ShowAdminSidebar = true;
            return View();
        }

        [HttpGet]
        public ActionResult GetStudents(string status = "PENDING", string search = "", int page = 1, int pageSize = DefaultPageSize)
        {
            status = NormalizeStatus(status);
            if (status == null || page < 1 || pageSize < 1 || pageSize > 100)
                return new HttpStatusCodeResult(400, "Invalid status or page parameters.");
            try
            {
                SyncStudentVerifications();

                var requestedPage = page;
                var searchTerm = (search ?? "").Trim();
                if (searchTerm.Length > 120) searchTerm = searchTerm.Substring(0, 120);
                var pagePipeline = BuildPagePipeline(status, searchTerm, page, pageSize);
                var pageResult = MongoDatabase.Verifications.Aggregate<BsonDocument>(pagePipeline).FirstOrDefault() ?? new BsonDocument();
                var totalArray = pageResult.GetValue("total", new BsonArray()).AsBsonArray;
                var total = totalArray.Count == 0 ? 0 : totalArray[0].AsBsonDocument.GetValue("count", 0).ToInt32();
                var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
                if (page > totalPages) page = totalPages;
                if (page != Math.Max(1, requestedPage))
                {
                    pageResult = MongoDatabase.Verifications.Aggregate<BsonDocument>(BuildPagePipeline(status, searchTerm, page, pageSize)).FirstOrDefault() ?? new BsonDocument();
                }
                var rows = pageResult.GetValue("data", new BsonArray()).AsBsonArray.Select(doc =>
                {
                    var row = doc.AsBsonDocument;
                    var application = BsonSerializer.Deserialize<VerificationApplicationDocument>(row);
                    var user = BsonSerializer.Deserialize<UserDocument>(row["student"].AsBsonDocument);
                    return ToRow(application, user);
                }).ToList();

                var totalStudents = (int)MongoDatabase.Users.CountDocuments(x => x.Role == "STUDENT");
                var pendingCount = (int)MongoDatabase.Users.CountDocuments(x => x.Role == "STUDENT" && (x.VerificationStatus == "PENDING" || x.VerificationStatus == null || x.VerificationStatus == ""));
                var approvedCount = (int)MongoDatabase.Users.CountDocuments(x => x.Role == "STUDENT" && x.VerificationStatus == "APPROVED");
                var rejectedCount = (int)MongoDatabase.Users.CountDocuments(x => x.Role == "STUDENT" && x.VerificationStatus == "REJECTED");

                var stats = new
                {
                    total = totalStudents,
                    pending = pendingCount,
                    approved = approvedCount,
                    rejected = rejectedCount
                };
                return Json(new { success = true, data = rows, total, page, pageSize, totalPages, stats }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex) when (ex is MongoException || ex is InvalidOperationException || ex is FormatException)
            {
                Response.StatusCode = 503;
                return Json(new { success = false, message = "Student verification records are unavailable. Check the database connection and try again." }, JsonRequestBehavior.AllowGet);
            }
        }

        private static void SyncStudentVerifications()
        {
            try
            {
                var students = MongoDatabase.Users.Find(x => x.Role == "STUDENT").ToList();
                if (students.Count == 0) return;

                var studentIds = students.Select(s => s.Id).ToList();
                var existingApps = MongoDatabase.Verifications.Find(x => studentIds.Contains(x.UserId)).ToList();
                var appByUserId = existingApps
                    .GroupBy(x => x.UserId)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.SubmittedAt != default(DateTime) ? x.SubmittedAt : (x.CreatedAt ?? DateTime.MinValue)).First());

                var now = DateTime.UtcNow;
                var toInsert = new List<VerificationApplicationDocument>();

                foreach (var student in students)
                {
                    var rawStatus = string.IsNullOrWhiteSpace(student.VerificationStatus) ? "PENDING" : student.VerificationStatus.Trim().ToUpperInvariant();
                    if (!new[] { "PENDING", "APPROVED", "REJECTED" }.Contains(rawStatus)) rawStatus = "PENDING";

                    VerificationApplicationDocument app;
                    if (!appByUserId.TryGetValue(student.Id, out app))
                    {
                        var submitted = student.CreatedAt != default(DateTime) ? student.CreatedAt : now;
                        toInsert.Add(new VerificationApplicationDocument
                        {
                            Id = ObjectId.GenerateNewId(),
                            UserId = student.Id,
                            SchoolEmail = student.SchoolEmail,
                            StudentIdNumber = student.StudentIdNumber,
                            Program = student.Program,
                            YearLevel = student.YearLevel,
                            SchoolIdPath = student.SchoolIdPhoto,
                            OneByOnePhotoPath = student.OneByOnePhoto,
                            RegistrationFormPath = student.RegistrationForm,
                            SubmittedDocument = student.SubmittedDocument ?? student.SchoolIdPhoto ?? student.RegistrationForm,
                            Status = rawStatus,
                            SubmittedAt = submitted,
                            CreatedAt = submitted,
                            ReviewedAt = (rawStatus == "APPROVED" || rawStatus == "REJECTED") ? (student.UpdatedAt != default(DateTime) ? (DateTime?)student.UpdatedAt : submitted) : null
                        });
                    }
                    else
                    {
                        if (!string.Equals(app.Status, rawStatus, StringComparison.OrdinalIgnoreCase))
                        {
                            MongoDatabase.Verifications.UpdateOne(
                                x => x.Id == app.Id,
                                Builders<VerificationApplicationDocument>.Update.Set(x => x.Status, rawStatus));
                        }
                    }
                }

                if (toInsert.Count > 0)
                {
                    MongoDatabase.Verifications.InsertMany(toInsert);
                }
            }
            catch
            {
                // Silently ignore background sync issues so normal queries can proceed
            }
        }

        private static PipelineDefinition<VerificationApplicationDocument, BsonDocument> BuildPagePipeline(string status, string search, int page, int pageSize)
        {
            var stages = new List<BsonDocument>
            {
                new BsonDocument("$addFields", new BsonDocument("_sortDate", new BsonDocument("$ifNull", new BsonArray { "$submittedAt", "$createdAt" }))),
                new BsonDocument("$sort", new BsonDocument("_sortDate", -1)),
                new BsonDocument("$group", new BsonDocument { { "_id", "$userId" }, { "application", new BsonDocument("$first", "$$ROOT") } }),
                new BsonDocument("$replaceRoot", new BsonDocument("newRoot", "$application")),
                new BsonDocument("$lookup", new BsonDocument { { "from", "users" }, { "localField", "userId" }, { "foreignField", "_id" }, { "as", "student" } }),
                new BsonDocument("$unwind", "$student"),
                new BsonDocument("$match", new BsonDocument("student.role", "STUDENT")),
                new BsonDocument("$sort", new BsonDocument { { "_sortDate", -1 }, { "_id", 1 } })
            };
            if (status != "ALL") stages.Add(new BsonDocument("$match", new BsonDocument("status", status)));
            if (!string.IsNullOrWhiteSpace(search))
            {
                var regex = new BsonRegularExpression(Regex.Escape(search), "i");
                stages.Add(new BsonDocument("$match", new BsonDocument("$or", new BsonArray
                {
                    new BsonDocument("student.firstName", regex), new BsonDocument("student.lastName", regex),
                    new BsonDocument("student.studentNumber", regex), new BsonDocument("student.schoolEmail", regex),
                    new BsonDocument("studentIdNumber", regex), new BsonDocument("schoolEmail", regex),
                    new BsonDocument("$expr", new BsonDocument("$regexMatch", new BsonDocument
                    {
                        { "input", new BsonDocument("$concat", new BsonArray { new BsonDocument("$ifNull", new BsonArray { "$student.firstName", "" }), " ", new BsonDocument("$ifNull", new BsonArray { "$student.lastName", "" }) }) },
                        { "regex", Regex.Escape(search) }, { "options", "i" }
                    }))
                })));
            }
            stages.Add(new BsonDocument("$facet", new BsonDocument
            {
                { "data", new BsonArray { new BsonDocument("$skip", (page - 1) * pageSize), new BsonDocument("$limit", pageSize) } },
                { "total", new BsonArray { new BsonDocument("$count", "count") } }
            }));
            return PipelineDefinition<VerificationApplicationDocument, BsonDocument>.Create(stages);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Approve(string id)
        {
            return Review(id, "APPROVED", null);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Reject(string id, string reason)
        {
            reason = (reason ?? "").Trim();
            if (reason.Length > 1000) return Json(new { success = false, message = "Rejection notes must be 1,000 characters or fewer." });
            return Review(id, "REJECTED", reason);
        }

        private ActionResult Review(string id, string decision, string notes)
        {
            ObjectId parsedId, adminId;
            if (!ObjectId.TryParse(id, out parsedId)) return new HttpStatusCodeResult(400, "Invalid application ID.");
            if (!ObjectId.TryParse(User.Identity.Name, out adminId)) return new HttpUnauthorizedResult();
            try
            {
                var application = MongoDatabase.Verifications.Find(x => x.Id == parsedId).FirstOrDefault();
                if (application == null)
                {
                    application = MongoDatabase.Verifications.Find(x => x.UserId == parsedId).SortByDescending(x => x.SubmittedAt).FirstOrDefault();
                }
                if (application == null)
                {
                    var studentUser = MongoDatabase.Users.Find(x => x.Id == parsedId && x.Role == "STUDENT").FirstOrDefault();
                    if (studentUser != null)
                    {
                        var createTime = studentUser.CreatedAt != default(DateTime) ? studentUser.CreatedAt : DateTime.UtcNow;
                        application = new VerificationApplicationDocument
                        {
                            Id = ObjectId.GenerateNewId(),
                            UserId = studentUser.Id,
                            SchoolEmail = studentUser.SchoolEmail,
                            StudentIdNumber = studentUser.StudentIdNumber,
                            Program = studentUser.Program,
                            YearLevel = studentUser.YearLevel,
                            SchoolIdPath = studentUser.SchoolIdPhoto,
                            OneByOnePhotoPath = studentUser.OneByOnePhoto,
                            RegistrationFormPath = studentUser.RegistrationForm,
                            SubmittedDocument = studentUser.SubmittedDocument ?? studentUser.SchoolIdPhoto ?? studentUser.RegistrationForm,
                            Status = "PENDING",
                            SubmittedAt = createTime,
                            CreatedAt = createTime
                        };
                        MongoDatabase.Verifications.InsertOne(application);
                    }
                }
                if (application == null) return HttpNotFound();

                var student = MongoDatabase.Users.Find(x => x.Id == application.UserId && x.Role == "STUDENT").FirstOrDefault();
                if (student == null) return HttpNotFound("Student account not found.");

                var now = DateTime.UtcNow;
                var update = Builders<VerificationApplicationDocument>.Update.Set(x => x.Status, decision)
                    .Set(x => x.ReviewNotes, notes).Set(x => x.ReviewedAt, now).Set(x => x.ReviewedBy, adminId);
                var result = MongoDatabase.Verifications.UpdateOne(
                    x => x.Id == application.Id && x.UserId == application.UserId && x.Status == "PENDING", update);
                if (result.ModifiedCount != 1)
                    return new HttpStatusCodeResult(409, "This application is no longer pending review.");

                MongoDatabase.Users.UpdateOne(x => x.Id == application.UserId && x.Role == "STUDENT",
                    Builders<UserDocument>.Update.Set(x => x.VerificationStatus, decision)
                        .Set(x => x.Status, decision == "APPROVED" ? "ACTIVE" : "PENDING")
                        .Set(x => x.UpdatedAt, now));

                try
                {
                    MongoDatabase.AuditLogs.InsertOne(new AuditLogDocument
                    {
                        Id = ObjectId.GenerateNewId(), UserId = adminId,
                        Action = decision == "APPROVED" ? "APPROVE_STUDENT" : "REJECT_STUDENT",
                        EntityType = "USER", EntityId = application.UserId,
                        Details = new BsonDocument { { "verificationId", application.Id }, { "status", decision }, { "notes", notes ?? "" } },
                        CreatedAt = now
                    });
                }
                catch { }
                return Json(new { success = true, id = application.Id.ToString(), status = decision, message = decision == "APPROVED" ? "Student account approved." : "Application rejected." });
            }
            catch (Exception ex) when (ex is MongoException || ex is InvalidOperationException || ex is FormatException)
            {
                Response.StatusCode = 503;
                return Json(new { success = false, message = "The verification decision could not be saved. Check the database connection and try again." });
            }
        }

        private object ToRow(VerificationApplicationDocument app, UserDocument user)
        {
            var status = (!string.IsNullOrWhiteSpace(app.Status) ? app.Status : (user.VerificationStatus ?? "PENDING")).ToUpperInvariant();
            var submitted = app.SubmittedAt != default(DateTime) ? app.SubmittedAt : (app.CreatedAt ?? user.CreatedAt);
            var name = string.Join(" ", new[] { user.FirstName, user.MiddleName, user.LastName }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
            var registrationFile = !string.IsNullOrWhiteSpace(app.RegistrationFormPath) ? app.RegistrationFormPath : (app.SubmittedDocument ?? user.RegistrationForm ?? user.SubmittedDocument);
            var idFile = !string.IsNullOrWhiteSpace(app.SchoolIdPath) ? app.SchoolIdPath : (user.SchoolIdPhoto ?? app.SubmittedDocument ?? user.SubmittedDocument);
            var photoFile = !string.IsNullOrWhiteSpace(app.OneByOnePhotoPath) ? app.OneByOnePhotoPath : user.OneByOnePhoto;
            return new
            {
                id = app.Id.ToString(), userId = user.Id.ToString(), studentId = app.StudentIdNumber ?? user.StudentIdNumber ?? "",
                name = string.IsNullOrWhiteSpace(name) ? "Student" : name,
                initials = Initials(name), program = app.Program ?? user.Program ?? "", yearLevel = app.YearLevel ?? user.YearLevel ?? "",
                campus = user.Campus ?? "", section = user.Section ?? "", email = app.SchoolEmail ?? user.SchoolEmail ?? "",
                phone = user.ContactNumber ?? user.Phone ?? "", accountStatus = user.Status ?? "", status = status,
                date = user.CreatedAt == default(DateTime) ? "" : user.CreatedAt.ToLocalTime().ToString("MMM d, yyyy", CultureInfo.InvariantCulture),
                submittedAt = submitted == default(DateTime) ? "" : submitted.ToLocalTime().ToString("MMM d, yyyy h:mm tt", CultureInfo.InvariantCulture),
                reviewNotes = app.ReviewNotes ?? app.Remarks ?? "",
                documents = new
                {
                    id = DocumentInfo(idFile, app.Id.ToString(), "id"),
                    photo = DocumentInfo(photoFile, app.Id.ToString(), "photo"),
                    form = DocumentInfo(registrationFile, app.Id.ToString(), "form")
                }
            };
        }

        private object DocumentInfo(string fileName, string applicationId, string kind)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return null;
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            if (!new[] { ".jpg", ".jpeg", ".png", ".pdf" }.Contains(extension)) return null;
            var safeName = Path.GetFileName(fileName);
            if (string.IsNullOrWhiteSpace(safeName) || !System.IO.File.Exists(Server.MapPath("~/App_Data/Verification/" + safeName))) return null;
            return new { url = Url.Action("Document", "Verification", new { id = applicationId, kind }), type = extension == ".pdf" ? "pdf" : "image" };
        }

        private static string NormalizeStatus(string status)
        {
            var value = (status ?? "PENDING").Trim().ToUpperInvariant();
            return new[] { "PENDING", "APPROVED", "REJECTED", "ALL" }.Contains(value) ? value : null;
        }
        private static string Initials(string name)
        {
            var words = (name ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return words.Length == 0 ? "ST" : string.Concat(words.Take(2).Select(x => x.Substring(0, 1))).ToUpperInvariant();
        }
    }
}
