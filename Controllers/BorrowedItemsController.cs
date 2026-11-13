using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
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
        public ActionResult Index(string status)
        {
            ObjectId borrowerId;
            if (!ObjectId.TryParse(User.Identity.Name, out borrowerId)) return new HttpStatusCodeResult(401);
            ViewBag.Status = NormalizeTab(status);
            return View(BorrowedItemsService.GetForBorrower(borrowerId));
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
        public ActionResult Dispute(string id, string reason, string details, HttpPostedFileBase[] evidenceFiles)
        {
            ObjectId borrowerId;
            if (!ObjectId.TryParse(User.Identity.Name, out borrowerId)) return Json(new { ok = false, message = "Please sign in again." });
            if (string.IsNullOrWhiteSpace(reason) || reason.Length > 100 || (details ?? "").Length > 400)
                return Json(new { ok = false, message = "Select a reason and check the details." });
            var files = (evidenceFiles ?? new HttpPostedFileBase[0]).Where(x => x != null && x.ContentLength > 0).ToList();
            if (files.Count > 2) return Json(new { ok = false, message = "Upload no more than two evidence images." });
            var savedUrls = new List<string>();
            try
            {
                foreach (var file in files) savedUrls.Add(SaveEvidence(file));
                var saved = BorrowedItemsService.SubmitDispute(borrowerId, id, reason, details, savedUrls);
                if (!saved) { DeleteEvidence(savedUrls); return Json(new { ok = false, message = "A dispute cannot be submitted for this rental." }); }
                return Json(new { ok = true, message = "Dispute sent for admin review." });
            }
            catch (InvalidDataException ex)
            {
                DeleteEvidence(savedUrls);
                return Json(new { ok = false, message = ex.Message });
            }
            catch (Exception ex) when (ex is MongoException || ex is IOException || ex is InvalidOperationException)
            {
                DeleteEvidence(savedUrls);
                Response.StatusCode = 409;
                return Json(new { ok = false, message = "The dispute could not be saved. Refresh and try again." });
            }
        }

        private string SaveEvidence(HttpPostedFileBase file)
        {
            if (file.ContentLength > 5 * 1024 * 1024) throw new InvalidDataException("Each image must be 5 MB or smaller.");
            var extension = Path.GetExtension(file.FileName ?? "").ToLowerInvariant();
            if (!new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" }.Contains(extension) || !IsImageSignature(file, extension))
                throw new InvalidDataException("Evidence must be a valid JPG, PNG, GIF, or WEBP image.");
            var folder = Server.MapPath("~/Uploads/disputes");
            Directory.CreateDirectory(folder);
            var name = Guid.NewGuid().ToString("N") + extension;
            file.SaveAs(Path.Combine(folder, name));
            return "/Uploads/disputes/" + name;
        }

        private static bool IsImageSignature(HttpPostedFileBase file, string extension)
        {
            var bytes = new byte[12];
            var read = file.InputStream.Read(bytes, 0, bytes.Length);
            if (file.InputStream.CanSeek) file.InputStream.Position = 0;
            if ((extension == ".jpg" || extension == ".jpeg") && read >= 3) return bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff;
            if (extension == ".png" && read >= 8) return bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4e && bytes[3] == 0x47 && bytes[4] == 0x0d && bytes[5] == 0x0a && bytes[6] == 0x1a && bytes[7] == 0x0a;
            if (extension == ".gif" && read >= 6) return System.Text.Encoding.ASCII.GetString(bytes, 0, 6) == "GIF87a" || System.Text.Encoding.ASCII.GetString(bytes, 0, 6) == "GIF89a";
            if (extension == ".webp" && read >= 12) return System.Text.Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF" && System.Text.Encoding.ASCII.GetString(bytes, 8, 4) == "WEBP";
            return false;
        }

        private void DeleteEvidence(IEnumerable<string> urls)
        {
            foreach (var url in urls ?? Enumerable.Empty<string>())
            {
                try
                {
                    var path = Server.MapPath(url);
                    if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
                }
                catch { }
            }
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
