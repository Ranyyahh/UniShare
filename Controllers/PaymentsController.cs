using System;
using System.Linq;
using System.Web.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using UniShare.Services;

namespace UniShare.Controllers
{
    [Authorize]
    public class PaymentsController : Controller
    {
        [HttpGet]
        public ActionResult History()
        {
            ObjectId userId;
            if (!ObjectId.TryParse(User.Identity.Name, out userId)) return new HttpStatusCodeResult(401);
            var filter = Builders<BsonDocument>.Filter.Eq("payerId", userId) | Builders<BsonDocument>.Filter.Eq("ownerId", userId);
            var rows = MongoDatabase.Payments.Find(filter).SortByDescending(x => x["createdAt"]).Limit(500).ToList();
            return Json(rows.Select(x => new {
                id = x.GetValue("_id", BsonNull.Value).ToString(),
                bookingId = x.GetValue("bookingId", BsonNull.Value).ToString(),
                amount = x.GetValue("amount", BsonNull.Value).IsNumeric ? x["amount"].ToDouble() : 0d,
                method = "CASH",
                status = x.GetValue("voidedAt", BsonNull.Value).IsValidDateTime ? "CANCELLED" : x.GetValue("status", "UNPAID").ToString(),
                paidAt = x.GetValue("paidAt", BsonNull.Value).ToString()
            }), JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public ActionResult Details(string bookingId)
        {
            ObjectId userId;
            if (!ObjectId.TryParse(User.Identity.Name, out userId)) return new HttpStatusCodeResult(401);
            var payment = PaymentFeeService.GetPayment(bookingId, userId);
            if (payment == null) return HttpNotFound();
            ObjectId id;
            if (!ObjectId.TryParse(bookingId, out id)) return HttpNotFound();
            var lateFee = MongoDatabase.LateFees.Find(Builders<BsonDocument>.Filter.Eq("bookingId", id)).FirstOrDefault();
            return Json(new {
                bookingId = id.ToString(), status = payment.GetValue("status", "UNPAID").ToString(),
                method = "CASH", amount = payment.GetValue("amount", 0).ToDouble(),
                paidAt = payment.GetValue("paidAt", BsonNull.Value).ToString(),
                lateFee = lateFee == null ? (object)null : new {
                    estimatedAmount = lateFee.GetValue("amount", 0).ToDouble(),
                    finalAmount = lateFee.GetValue("finalAmount", BsonNull.Value).IsNumeric ? (double?)lateFee["finalAmount"].ToDouble() : null,
                    status = lateFee.GetValue("status", "").ToString()
                }
            }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost, ValidateAntiForgeryToken, VerifiedStudent]
        public ActionResult SelectMethod(string bookingId)
        {
            ObjectId borrowerId;
            if (!ObjectId.TryParse(User.Identity.Name, out borrowerId)) return new HttpStatusCodeResult(401);
            try
            {
                var saved = PaymentFeeService.SelectMethod(bookingId, borrowerId);
                return Json(new { ok = saved, message = saved ? "Payment method saved. Payment remains unpaid until the owner confirms receipt." : "Payment method could not be saved for this booking." });
            }
            catch (MongoException) { Response.StatusCode = 503; return Json(new { ok = false, message = "Payment details are temporarily unavailable." }); }
        }

        [HttpPost, ValidateAntiForgeryToken, VerifiedStudent]
        public ActionResult ConfirmReceipt(string bookingId)
        {
            ObjectId ownerId;
            if (!ObjectId.TryParse(User.Identity.Name, out ownerId)) return new HttpStatusCodeResult(401);
            try
            {
                var saved = PaymentFeeService.ConfirmReceipt(bookingId, ownerId);
                return Json(new { ok = saved, message = saved ? "Payment receipt confirmed." : "No unpaid payment for this booking was found." });
            }
            catch (MongoException) { Response.StatusCode = 503; return Json(new { ok = false, message = "Payment could not be updated." }); }
        }

        [HttpPost, ValidateAntiForgeryToken, VerifiedStudent]
        public ActionResult Dispute(string bookingId, string reason)
        {
            ObjectId participantId;
            if (!ObjectId.TryParse(User.Identity.Name, out participantId)) return new HttpStatusCodeResult(401);
            try
            {
                var saved = PaymentFeeService.SetDisputed(bookingId, participantId, reason);
                return Json(new { ok = saved, message = saved ? "Payment flagged for review." : "The payment could not be disputed." });
            }
            catch (MongoException) { Response.StatusCode = 503; return Json(new { ok = false, message = "Payment could not be updated." }); }
        }

        [Authorize(Roles = "ADMIN"), HttpPost, ValidateAntiForgeryToken]
        public ActionResult Refund(string bookingId, string reason)
        {
            ObjectId adminId;
            if (!ObjectId.TryParse(User.Identity.Name, out adminId)) return new HttpStatusCodeResult(401);
            try
            {
                var saved = PaymentFeeService.Refund(bookingId, adminId, reason);
                if (Request.IsAjaxRequest())
                {
                    Response.StatusCode = saved ? 200 : 409;
                    return Json(new { ok = saved, message = saved ? "Refund status recorded." : "Only paid or disputed payments can be refunded." });
                }
                TempData[saved ? "Success" : "Error"] = saved ? "Refund status recorded." : "Only paid or disputed payments can be refunded.";
            }
            catch (MongoException)
            {
                if (Request.IsAjaxRequest()) { Response.StatusCode = 503; return Json(new { ok = false, message = "The refund record could not be saved." }); }
                TempData["Error"] = "The refund record could not be saved.";
            }
            return RedirectToAction("Index", "TransactionPaymentRecords");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult FinalizeLateFee(string bookingId, decimal finalAmount, string notes)
        {
            ObjectId actorId;
            if (!ObjectId.TryParse(User.Identity.Name, out actorId)) return new HttpStatusCodeResult(401);
            try
            {
                var saved = PaymentFeeService.FinalizeLateFee(bookingId, actorId, User.IsInRole("ADMIN"), finalAmount, notes);
                return Json(new { ok = saved, message = saved ? "Late fee finalized." : "Only an estimated late fee can be finalized by the owner or an administrator." });
            }
            catch (MongoException) { Response.StatusCode = 503; return Json(new { ok = false, message = "Late fee could not be updated." }); }
        }
    }
}
