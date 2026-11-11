using System.Collections.Generic;
using System.Web.Mvc;
using UniShare.Models;

namespace UniShare.Controllers
{
    [Authorize(Roles = "ADMIN")]
    public class DisputeManagementController : Controller
    {
        public ActionResult Index()
        {
            ViewBag.Title = "Dispute Management";
            ViewBag.TotalDisputes = 28;
            ViewBag.OpenDisputes = 7;
            ViewBag.UnderReview = 12;
            ViewBag.ResolvedDisputes = 89;

            var disputes = new List<DisputeViewModel>
            {
                new DisputeViewModel
                {
                    DisputeId = "DSP-2026-0042",
                    BookingId = "BK-2026-084",
                    ItemName = "Casio fx-991EX ClassWiz",
                    ReporterName = "Maria Santos",
                    RespondentName = "Juan Dela Cruz",
                    Priority = "High",
                    Status = "Under Review",
                    DateReported = "Nov 08, 2026",
                    IncidentStatement = "Item was returned with a visible hairline crack across the lower solar cell display matrix and the protective hard slide cover is missing."
                },
                new DisputeViewModel
                {
                    DisputeId = "DSP-2026-0039",
                    BookingId = "BK-2026-075",
                    ItemName = "Arduino Uno R3 Kit",
                    ReporterName = "Kenneth Ramirez",
                    RespondentName = "Angelo Reyes",
                    Priority = "Urgent",
                    Status = "Open",
                    DateReported = "Nov 07, 2026",
                    IncidentStatement = "The Arduino kit was returned missing 3 jumper wire bundles and the ultrasonic sensor pins were bent."
                },
                new DisputeViewModel
                {
                    DisputeId = "DSP-2026-0036",
                    BookingId = "BK-2026-064",
                    ItemName = "Staedtler Mars 700",
                    ReporterName = "Chloe Anne Cruz",
                    RespondentName = "Patricia Gomez",
                    Priority = "Medium",
                    Status = "Under Review",
                    DateReported = "Nov 06, 2026",
                    IncidentStatement = "Technical pen was returned dry and clogged with dried drawing ink contrary to care guidelines."
                },
                new DisputeViewModel
                {
                    DisputeId = "DSP-2026-0031",
                    BookingId = "BK-2026-052",
                    ItemName = "TI-84 Plus CE",
                    ReporterName = "Jeric Alcantara",
                    RespondentName = "Jerome Bautista",
                    Priority = "Low",
                    Status = "Resolved",
                    DateReported = "Nov 04, 2026",
                    IncidentStatement = "Payment discrepancy resolved via mutual GCash reference confirmation after verification by Student Affairs."
                }
            };

            return View("DisputeManagement", disputes);
        }

        public ActionResult DisputeManagement()
        {
            return Index();
        }

        [HttpGet]
        public ActionResult GetDisputeDetails(string id)
        {
            DisputeViewModel details;

            if (id == "DSP-2026-0039")
            {
                details = new DisputeViewModel
                {
                    DisputeId = "DSP-2026-0039",
                    BookingId = "BK-2026-075",
                    ItemName = "Arduino Uno R3 Kit",
                    SerialNumber = "ARD-UNO-48192",
                    ReporterName = "Kenneth Ramirez",
                    ReporterId = "2023-0112-SB",
                    RespondentName = "Angelo Reyes",
                    RespondentId = "2024-0089-SB",
                    IncidentStatement = "The Arduino kit was returned missing 3 jumper wire bundles and the ultrasonic sensor pins were bent. Borrower stated it was already loose upon pickup, but the initial kit inventory sheet verified complete components upon dispatch.",
                    Priority = "Urgent",
                    Status = "Open",
                    DateReported = "Nov 07, 2026"
                };
            }
            else if (id == "DSP-2026-0036")
            {
                details = new DisputeViewModel
                {
                    DisputeId = "DSP-2026-0036",
                    BookingId = "BK-2026-064",
                    ItemName = "Staedtler Mars 700",
                    SerialNumber = "STM-700-0918",
                    ReporterName = "Chloe Anne Cruz",
                    ReporterId = "2023-0891-SB",
                    RespondentName = "Patricia Gomez",
                    RespondentId = "2024-0312-SB",
                    IncidentStatement = "Technical pen was returned dry and clogged with dried drawing ink contrary to care guidelines. Nib tip shows distortion.",
                    Priority = "Medium",
                    Status = "Under Review",
                    DateReported = "Nov 06, 2026"
                };
            }
            else if (id == "DSP-2026-0031")
            {
                details = new DisputeViewModel
                {
                    DisputeId = "DSP-2026-0031",
                    BookingId = "BK-2026-052",
                    ItemName = "TI-84 Plus CE",
                    SerialNumber = "TI84-99120-CE",
                    ReporterName = "Jeric Alcantara",
                    ReporterId = "2022-0941-SB",
                    RespondentName = "Jerome Bautista",
                    RespondentId = "2023-0518-SB",
                    IncidentStatement = "Payment discrepancy resolved via mutual GCash reference confirmation after verification by Student Affairs.",
                    Priority = "Low",
                    Status = "Resolved",
                    DateReported = "Nov 04, 2026"
                };
            }
            else
            {
                // Default: DSP-2026-0042
                details = new DisputeViewModel
                {
                    DisputeId = !string.IsNullOrEmpty(id) ? id : "DSP-2026-0042",
                    BookingId = "BK-2026-084",
                    ItemName = "Casio fx-991EX ClassWiz",
                    SerialNumber = "CW-98218-PH",
                    ReporterName = "Maria Santos",
                    ReporterId = "2023-0455-SB",
                    RespondentName = "Juan Dela Cruz",
                    RespondentId = "2024-0022-SB",
                    IncidentStatement = "Item was returned with a visible hairline crack across the lower solar cell display matrix and the protective hard slide cover is missing. Borrower claimed it was already loose upon pickup, but Guardhouse 1 inspection log showed pristine Grade A condition at handover.",
                    Priority = "High",
                    Status = "Under Review",
                    DateReported = "Nov 08, 2026"
                };
            }

            return PartialView("DisputeDetails", details);
        }
    }
}
