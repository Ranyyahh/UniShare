using System.Collections.Generic;
using System.Dynamic;
using System.Web.Mvc;

namespace UniShare.Controllers
{
    public class DisputeManagementController : Controller
    {
        public ActionResult Index()
        {
            ViewBag.Title = "Dispute Management";
            ViewBag.TotalDisputes = 28;
            ViewBag.OpenDisputes = 7;
            ViewBag.UnderReview = 12;
            ViewBag.ResolvedDisputes = 89;

            var disputes = new List<dynamic>();

            dynamic d1 = new ExpandoObject();
            d1.DisputeId = "DSP-2026-0042";
            d1.BookingId = "BK-2026-084";
            d1.ItemName = "Casio fx-991EX ClassWiz";
            d1.Reporter = "Maria Santos";
            d1.Respondent = "Juan Dela Cruz";
            d1.Priority = "High";
            d1.Status = "Under Review";
            disputes.Add(d1);

            dynamic d2 = new ExpandoObject();
            d2.DisputeId = "DSP-2026-0039";
            d2.BookingId = "BK-2026-075";
            d2.ItemName = "Arduino Uno R3 Kit";
            d2.Reporter = "Kenneth Ramirez";
            d2.Respondent = "Angelo Reyes";
            d2.Priority = "Urgent";
            d2.Status = "Open";
            disputes.Add(d2);

            dynamic d3 = new ExpandoObject();
            d3.DisputeId = "DSP-2026-0036";
            d3.BookingId = "BK-2026-064";
            d3.ItemName = "Staedtler Mars 700";
            d3.Reporter = "Chloe Anne Cruz";
            d3.Respondent = "Patricia Gomez";
            d3.Priority = "Medium";
            d3.Status = "Under Review";
            disputes.Add(d3);

            dynamic d4 = new ExpandoObject();
            d4.DisputeId = "DSP-2026-0031";
            d4.BookingId = "BK-2026-052";
            d4.ItemName = "TI-84 Plus CE";
            d4.Reporter = "Jeric Alcantara";
            d4.Respondent = "Jerome Bautista";
            d4.Priority = "Low";
            d4.Status = "Resolved";
            disputes.Add(d4);

            return View("DisputeManagement", disputes);
        }

        public ActionResult DisputeManagement()
        {
            return Index();
        }

        [HttpGet]
        public ActionResult GetDisputeDetails(string id)
        {
            dynamic details = new ExpandoObject();

            if (id == "DSP-2026-0039")
            {
                details.DisputeId = "DSP-2026-0039";
                details.BookingId = "BK-2026-075";
                details.ItemName = "Arduino Uno R3 Kit";
                details.SerialNumber = "ARD-UNO-48192";
                details.ReporterName = "Kenneth Ramirez";
                details.ReporterId = "2023-0112-SB";
                details.RespondentName = "Angelo Reyes";
                details.RespondentId = "2024-0089-SB";
                details.IncidentStatement = "The Arduino kit was returned missing 3 jumper wire bundles and the ultrasonic sensor pins were bent. Borrower stated it was already loose upon pickup, but the initial kit inventory sheet verified complete components upon dispatch.";
                details.Priority = "Urgent";
                details.Status = "Open";
            }
            else if (id == "DSP-2026-0036")
            {
                details.DisputeId = "DSP-2026-0036";
                details.BookingId = "BK-2026-064";
                details.ItemName = "Staedtler Mars 700";
                details.SerialNumber = "STM-700-0918";
                details.ReporterName = "Chloe Anne Cruz";
                details.ReporterId = "2023-0891-SB";
                details.RespondentName = "Patricia Gomez";
                details.RespondentId = "2024-0312-SB";
                details.IncidentStatement = "Technical pen was returned dry and clogged with dried drawing ink contrary to care guidelines. Nib tip shows distortion.";
                details.Priority = "Medium";
                details.Status = "Under Review";
            }
            else if (id == "DSP-2026-0031")
            {
                details.DisputeId = "DSP-2026-0031";
                details.BookingId = "BK-2026-052";
                details.ItemName = "TI-84 Plus CE";
                details.SerialNumber = "TI84-99120-CE";
                details.ReporterName = "Jeric Alcantara";
                details.ReporterId = "2022-0941-SB";
                details.RespondentName = "Jerome Bautista";
                details.RespondentId = "2023-0518-SB";
                details.IncidentStatement = "Payment discrepancy resolved via mutual GCash reference confirmation after verification by Student Affairs.";
                details.Priority = "Low";
                details.Status = "Resolved";
            }
            else
            {
                // Default: DSP-2026-0042
                details.DisputeId = !string.IsNullOrEmpty(id) ? id : "DSP-2026-0042";
                details.BookingId = "BK-2026-084";
                details.ItemName = "Casio fx-991EX ClassWiz";
                details.SerialNumber = "CW-98218-PH";
                details.ReporterName = "Maria Santos";
                details.ReporterId = "2023-0455-SB";
                details.RespondentName = "Juan Dela Cruz";
                details.RespondentId = "2024-0022-SB";
                details.IncidentStatement = "Item was returned with a visible hairline crack across the lower solar cell display matrix and the protective hard slide cover is missing. Borrower claimed it was already loose upon pickup, but Guardhouse 1 inspection log showed pristine Grade A condition at handover.";
                details.Priority = "High";
                details.Status = "Under Review";
            }

            return PartialView("DisputeDetails", (object)details);
        }
    }
}
