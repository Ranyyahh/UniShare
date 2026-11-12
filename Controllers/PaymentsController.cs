using System;
using System.Web.Mvc;
using MongoDB.Bson;
using UniShare.Models;
using UniShare.Services;

namespace UniShare.Controllers
{
    [Authorize]
    public class PaymentsController : Controller
    {
        [HttpGet, VerifiedStudent]
        public ActionResult PaymentMethods(string bookingId) { return PaymentDetails(bookingId); }

        [HttpGet, VerifiedStudent]
        public ActionResult PaymentDetails(string bookingId)
        {
            ObjectId userId;
            if (!ObjectId.TryParse(User.Identity.Name, out userId)) return new HttpStatusCodeResult(401);
            try
            {
                var details = PaymentManagementService.GetPaymentDetails(bookingId, userId);
                return details == null ? (ActionResult)HttpNotFound() : Json(details, JsonRequestBehavior.AllowGet);
            }
            catch (Exception) { return JsonError("Payment details are temporarily unavailable.", 503, true); }
        }

        [HttpGet, VerifiedStudent]
        public ActionResult PaymentConfirmation(string id)
        {
            ObjectId userId;
            if (!ObjectId.TryParse(User.Identity.Name, out userId)) return new HttpStatusCodeResult(401);
            try
            {
                var record = PaymentManagementService.GetHistory(userId).Find(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
                return record == null ? (ActionResult)HttpNotFound() : Json(record, JsonRequestBehavior.AllowGet);
            }
            catch (Exception) { return JsonError("Payment confirmation details are temporarily unavailable.", 503, true); }
        }

        [HttpGet, VerifiedStudent]
        public ActionResult PaymentHistory()
        {
            ObjectId userId;
            if (!ObjectId.TryParse(User.Identity.Name, out userId)) return new HttpStatusCodeResult(401);
            try { return Json(PaymentManagementService.GetHistory(userId), JsonRequestBehavior.AllowGet); }
            catch (Exception) { return JsonError("Payment history is temporarily unavailable.", 503, true); }
        }

        [HttpGet, VerifiedStudent]
        public ActionResult RentalFeeDetails(string bookingId)
        {
            ObjectId userId;
            if (!ObjectId.TryParse(User.Identity.Name, out userId)) return new HttpStatusCodeResult(401);
            try
            {
                var details = PaymentManagementService.GetPaymentDetails(bookingId, userId);
                return details == null ? (ActionResult)HttpNotFound() : Json(new { details.BookingId, details.RequestId, details.ItemName, details.DurationHours, details.HourlyRate, details.RentalFee, Currency = "PHP" }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception) { return JsonError("Rental fee details are temporarily unavailable.", 503, true); }
        }

        [HttpGet, VerifiedStudent]
        public ActionResult LateFeeDetails(string rentalId)
        {
            ObjectId userId;
            if (!ObjectId.TryParse(User.Identity.Name, out userId)) return new HttpStatusCodeResult(401);
            try
            {
                var details = PaymentManagementService.GetLateFeeDetails(rentalId, userId);
                return details == null ? (ActionResult)HttpNotFound() : Json(details, JsonRequestBehavior.AllowGet);
            }
            catch (Exception) { return JsonError("Late fee details are temporarily unavailable.", 503, true); }
        }

        [HttpPost, ValidateAntiForgeryToken, VerifiedStudent]
        public ActionResult SubmitPayment(PaymentSubmissionViewModel input)
        {
            ObjectId borrowerId;
            if (!ObjectId.TryParse(User.Identity.Name, out borrowerId)) return JsonError("Please sign in again.", 401);
            if (!ModelState.IsValid) return JsonError("Check the payment details and try again.", 400);
            try { return Json(new { ok = true, payment = PaymentManagementService.SubmitPayment(borrowerId, input) }); }
            catch (PaymentWorkflowException ex) { return JsonError(ex.Message, 400); }
            catch (Exception) { return JsonError("The payment could not be saved. Try again.", 503); }
        }

        [HttpPost, ValidateAntiForgeryToken, VerifiedStudent]
        public ActionResult ConfirmReceipt(PaymentActionViewModel input)
        {
            ObjectId ownerId;
            if (!ObjectId.TryParse(User.Identity.Name, out ownerId)) return JsonError("Please sign in again.", 401);
            try { return Json(new { ok = true, payment = PaymentManagementService.ConfirmReceipt(ownerId, input == null ? null : input.PaymentId) }); }
            catch (PaymentWorkflowException ex) { return JsonError(ex.Message, 400); }
            catch (Exception) { return JsonError("Payment confirmation could not be saved. Try again.", 503); }
        }

        [HttpPost, ValidateAntiForgeryToken, VerifiedStudent]
        public ActionResult DisputePayment(PaymentActionViewModel input)
        {
            ObjectId borrowerId;
            if (!ObjectId.TryParse(User.Identity.Name, out borrowerId)) return JsonError("Please sign in again.", 401);
            try { return Json(new { ok = true, payment = PaymentManagementService.DisputePayment(borrowerId, input) }); }
            catch (PaymentWorkflowException ex) { return JsonError(ex.Message, 400); }
            catch (Exception) { return JsonError("The dispute could not be saved. Try again.", 503); }
        }

        [HttpPost, Authorize(Roles = "ADMIN"), ValidateAntiForgeryToken]
        public ActionResult RefundPayment(PaymentActionViewModel input)
        {
            ObjectId adminId;
            if (!ObjectId.TryParse(User.Identity.Name, out adminId)) return JsonError("Please sign in again.", 401);
            try { return Json(new { ok = true, payment = PaymentManagementService.RefundPayment(adminId, input) }); }
            catch (PaymentWorkflowException ex) { return JsonError(ex.Message, 400); }
            catch (Exception) { return JsonError("The refund could not be saved. Try again.", 503); }
        }

        [HttpPost, ValidateAntiForgeryToken, VerifiedStudent]
        public ActionResult RefreshLateFee(string rentalId)
        {
            ObjectId userId;
            if (!ObjectId.TryParse(User.Identity.Name, out userId)) return JsonError("Please sign in again.", 401);
            try { return Json(new { ok = true, fee = PaymentManagementService.RefreshLateFee(rentalId, userId) }); }
            catch (PaymentWorkflowException ex) { return JsonError(ex.Message, 400); }
            catch (Exception) { return JsonError("The late fee estimate could not be updated. Try again.", 503); }
        }

        [HttpPost, ValidateAntiForgeryToken, VerifiedStudent]
        public ActionResult FinalizeLateFee(LateFeeFinalizationViewModel input)
        {
            ObjectId ownerId;
            if (!ObjectId.TryParse(User.Identity.Name, out ownerId)) return JsonError("Please sign in again.", 401);
            if (!ModelState.IsValid) return JsonError("Check the finalized fee amount and try again.", 400);
            try { return Json(new { ok = true, fee = PaymentManagementService.FinalizeLateFee(ownerId, input.RentalId, input.FinalAmount) }); }
            catch (PaymentWorkflowException ex) { return JsonError(ex.Message, 400); }
            catch (Exception) { return JsonError("The finalized late fee could not be saved. Try again.", 503); }
        }

        private JsonResult JsonError(string message, int statusCode, bool allowGet = false)
        {
            Response.StatusCode = statusCode;
            return Json(new { ok = false, message = message }, allowGet ? JsonRequestBehavior.AllowGet : JsonRequestBehavior.DenyGet);
        }
    }
}
