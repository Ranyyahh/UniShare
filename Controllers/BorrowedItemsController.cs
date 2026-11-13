using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using UniShare.Models;
using UniShare.Services;

namespace UniShare.Controllers
{
    [Authorize]
    [VerifiedStudent]
    public class BorrowedItemsController : Controller
    {
        [HttpGet]
        public ActionResult Index(string status, string side)
        {
            ObjectId borrowerId;
            if (!ObjectId.TryParse(User.Identity.Name, out borrowerId)) return new HttpStatusCodeResult(401);
            ViewBag.Status = NormalizeTab(status);
            var ownerSide = string.Equals(side, "lent", StringComparison.OrdinalIgnoreCase);
            ViewBag.Side = ownerSide ? "lent" : "borrowed";
            return View(ownerSide ? BorrowedItemsService.GetForOwner(borrowerId) : BorrowedItemsService.GetForBorrower(borrowerId));
        }

        [HttpGet]
        public ActionResult Details(string id)
        {
            ObjectId borrowerId;
            if (!ObjectId.TryParse(User.Identity.Name, out borrowerId)) return new HttpStatusCodeResult(401);
            var record = BorrowedItemsService.GetDetails(borrowerId, id);
            return record == null ? (ActionResult)HttpNotFound() : Json(record, JsonRequestBehavior.AllowGet);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult ConfirmHandover(string id, bool received)
        {
            if (!received) return Json(new { ok = false, message = "Confirm that you received the item before continuing." });
            ObjectId borrowerId;
            if (!ObjectId.TryParse(User.Identity.Name, out borrowerId)) return Json(new { ok = false, message = "Please sign in again." });
            return Run(() => BorrowedItemsService.ConfirmHandover(borrowerId, id), "Your handover confirmation has been recorded.", "This handover is no longer awaiting your confirmation.");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult ConfirmHandoverByOwner(string id)
        {
            ObjectId ownerId;
            if (!ObjectId.TryParse(User.Identity.Name, out ownerId)) return new HttpStatusCodeResult(401);
            var confirmed = BorrowedItemsService.ConfirmHandoverByOwner(ownerId, id);
            TempData[confirmed ? "Success" : "Error"] = confirmed
                ? "Your handover confirmation has been recorded. The rental is active once both parties confirm."
                : "This rental is no longer awaiting your handover confirmation.";
            return RedirectToAction("Ongoing", "MyListings");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult ReturnItem(string id, bool returned)
        {
            if (!returned) return Json(new { ok = false, message = "Confirm that you returned the item before continuing." });
            ObjectId borrowerId;
            if (!ObjectId.TryParse(User.Identity.Name, out borrowerId)) return Json(new { ok = false, message = "Please sign in again." });
            return Run(() => BorrowedItemsService.RequestReturn(borrowerId, id), "Return request sent to the item owner.", "This rental cannot be returned from its current status.");
        }

        // Owner confirmation is kept separate from the borrower's return request.
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult ConfirmReturn(string id)
        {
            ObjectId ownerId;
            if (!ObjectId.TryParse(User.Identity.Name, out ownerId)) return Json(new { ok = false, message = "Please sign in again." });
            return Run(() => BorrowedItemsService.ConfirmReturnByOwner(ownerId, id), "Return confirmed.", "This record is not awaiting your confirmation.");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Rate(string id, int itemRating, int ownerRating, string review)
        {
            ObjectId borrowerId;
            if (!ObjectId.TryParse(User.Identity.Name, out borrowerId)) return Json(new { ok = false, message = "Please sign in again." });
            if (itemRating < 1 || itemRating > 5 || ownerRating < 1 || ownerRating > 5 || (review ?? "").Length > 300)
                return Json(new { ok = false, message = "Choose a rating from one to five stars." });
            return Run(() => BorrowedItemsService.SubmitRating(borrowerId, id, itemRating, ownerRating, review), "Thanks for reviewing this rental.", "Only completed, unrated rentals can be reviewed.");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Dispute(string id, string reason, string details, System.Web.HttpPostedFileBase[] evidenceFiles)
        {
            ObjectId participantId;
            if (!ObjectId.TryParse(User.Identity.Name, out participantId)) return Json(new { ok = false, message = "Please sign in again." });
            if (string.IsNullOrWhiteSpace(reason) || reason.Length > 100 || (details ?? "").Length > 400)
                return Json(new { ok = false, message = "Select a reason and check the details." });
            var evidence = new List<string>();
            try
            {
                evidence = SaveEvidencePhotos(evidenceFiles);
                var saved = BorrowedItemsService.SubmitDispute(participantId, id, reason, details);
                ObjectId bookingId;
                if (saved && evidence.Count > 0 && ObjectId.TryParse(id, out bookingId))
                    MongoDatabase.Disputes.UpdateOne(Builders<BsonDocument>.Filter.Eq("bookingId", bookingId),
                        Builders<BsonDocument>.Update.PushEach("evidenceUrls", evidence.Select(x => "~/Uploads/" + x)));
                if (!saved) DeleteConditionPhotos(evidence);
                return Json(new { ok = saved, message = saved ? "Dispute sent for review." : "A dispute cannot be submitted for this rental." });
            }
            catch (InvalidDataException ex)
            {
                DeleteConditionPhotos(evidence);
                return Json(new { ok = false, message = ex.Message });
            }
            catch (MongoException)
            {
                DeleteConditionPhotos(evidence);
                Response.StatusCode = 503;
                return Json(new { ok = false, message = "The dispute could not be saved." });
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult RecordCondition(string id, string phase, string condition, string notes, System.Web.HttpPostedFileBase[] photos)
        {
            ObjectId actorId;
            if (!ObjectId.TryParse(User.Identity.Name, out actorId)) return Json(new { ok = false, message = "Please sign in again." });
            if ((notes ?? "").Length > 1000 || (photos ?? new System.Web.HttpPostedFileBase[0]).Length > 5)
                return Json(new { ok = false, message = "Check the notes and photo count." });
            var savedPhotos = new List<string>();
            try
            {
                savedPhotos = SaveEvidencePhotos(photos);
                var saved = BorrowedItemsService.RecordCondition(actorId, id, phase, condition, notes, savedPhotos);
                if (!saved) DeleteConditionPhotos(savedPhotos);
                return Json(new { ok = saved, message = saved ? "Condition record saved." : "Condition records can only be added by a booking participant during handover or return." });
            }
            catch (InvalidDataException ex)
            {
                DeleteConditionPhotos(savedPhotos);
                return Json(new { ok = false, message = ex.Message });
            }
            catch (IOException)
            {
                DeleteConditionPhotos(savedPhotos);
                Response.StatusCode = 500;
                return Json(new { ok = false, message = "The condition photos could not be saved." });
            }
            catch (MongoException)
            {
                DeleteConditionPhotos(savedPhotos);
                Response.StatusCode = 503;
                return Json(new { ok = false, message = "The condition record could not be saved." });
            }
        }

        private void DeleteConditionPhotos(IEnumerable<string> photos)
        {
            var folder = Server.MapPath("~/Uploads");
            foreach (var photo in photos)
            {
                try { System.IO.File.Delete(Path.Combine(folder, Path.GetFileName(photo))); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        private List<string> SaveEvidencePhotos(System.Web.HttpPostedFileBase[] files)
        {
            if ((files ?? new System.Web.HttpPostedFileBase[0]).Length > 5) throw new InvalidDataException("Upload up to five photos.");
            var saved = new List<string>();
            try
            {
                foreach (var file in files ?? new System.Web.HttpPostedFileBase[0])
                {
                    if (file == null || file.ContentLength == 0) continue;
                    var ext = Path.GetExtension(Path.GetFileName(file.FileName)).ToLowerInvariant();
                    if (file.ContentLength > 5 * 1024 * 1024 || (ext != ".jpg" && ext != ".jpeg" && ext != ".png"))
                        throw new InvalidDataException("Use JPG or PNG photos up to 5 MB each.");
                    var header = new byte[8];
                    var read = file.InputStream.Read(header, 0, header.Length);
                    file.InputStream.Position = 0;
                    var isJpeg = read >= 3 && header[0] == 0xff && header[1] == 0xd8 && header[2] == 0xff;
                    var isPng = read == 8 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4e && header[3] == 0x47 && header[4] == 0x0d && header[5] == 0x0a && header[6] == 0x1a && header[7] == 0x0a;
                    if (!isJpeg && !isPng) throw new InvalidDataException("The uploaded file is not a valid JPG or PNG image.");
                    var fileName = Guid.NewGuid().ToString("N") + ext;
                    var folder = Server.MapPath("~/Uploads");
                    Directory.CreateDirectory(folder);
                    file.SaveAs(Path.Combine(folder, fileName));
                    saved.Add(fileName);
                }
                return saved;
            }
            catch { DeleteConditionPhotos(saved); throw; }
        }

        private JsonResult Run(Func<bool> action, string success, string failure)
        {
            try
            {
                var changed = action();
                return Json(new { ok = changed, message = changed ? success : failure });
            }
            catch
            {
                Response.StatusCode = 409;
                return Json(new { ok = false, message = "This rental changed while you were viewing it. Refresh the page and try again." });
            }
        }

        private static string NormalizeTab(string status)
        {
            var allowed = new[] { "Pending", "Ongoing", "Completed", "Disputed" };
            return allowed.FirstOrDefault(x => string.Equals(x, status, StringComparison.OrdinalIgnoreCase)) ?? "Pending";
        }
    }
}
