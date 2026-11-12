using System.Web.Mvc;
using System.Collections.Generic;

namespace UniShare.Controllers
{
    [Authorize(Roles = "ADMIN")]
    public class AdminStudentVerificationController : Controller
    {
        [HttpGet]
        public ActionResult Index()
        {
            return View();
        }

        // Placeholder API — replace with DB query later
        [HttpGet]
        public JsonResult GetStudents()
        {
            var students = new List<object>
            {
                new {
                    id = "2024-03482-SB",
                    kyc = "KYC-2024-03482",
                    name = "Angela Mae Torres",
                    initials = "AT",
                    course = "BS Information Technology • SBIT-3D (3rd Year, Regular)",
                    campus = "San Bartolome (Main Campus)",
                    email = "a.torres.03482@qcu.edu.ph",
                    phone = "+63 917 849 2011",
                    date = "Oct 24, 2026",
                    status = "pending",
                    meta = "ID Scan • 1.8 MB • Uploaded Oct 24, 2026"
                },
                new {
                    id = "2023-01944-BC",
                    kyc = "KYC-2023-01944",
                    name = "Joshua Miguel Santos",
                    initials = "JS",
                    course = "BS Computer Science • 2A (2nd Year, Regular)",
                    campus = "Batasan Campus",
                    email = "j.santos.01944@qcu.edu.ph",
                    phone = "+63 918 320 4491",
                    date = "Oct 24, 2026",
                    status = "pending",
                    meta = "ID Scan • 2.1 MB • Uploaded Oct 24, 2026"
                },
                new {
                    id = "2025-06129-SB",
                    kyc = "KYC-2025-06129",
                    name = "Princess Joy Perez",
                    initials = "PP",
                    course = "BS Industrial Engineering • 1B (1st Year, Regular)",
                    campus = "San Bartolome (Main Campus)",
                    email = "p.perez.00129@qcu.edu.ph",
                    phone = "+63 927 501 8820",
                    date = "Oct 23, 2026",
                    status = "pending",
                    meta = "Freshmen ID • 1.4 MB • Uploaded Oct 23, 2026"
                },
                new {
                    id = "2024-05512-SB",
                    kyc = "KYC-2024-05512",
                    name = "Kenneth Dave Ramirez",
                    initials = "KR",
                    course = "BS Information Technology • SBIT-3D (3rd Year, Regular)",
                    campus = "San Bartolome (Main Campus)",
                    email = "k.ramirez.05512@qcu.edu.ph",
                    phone = "+63 905 119 7731",
                    date = "Oct 23, 2026",
                    status = "approved",
                    meta = "ID Scan • 1.9 MB • Uploaded Oct 23, 2026"
                },
                new {
                    id = "2022-08819-BC",
                    kyc = "KYC-2022-08819",
                    name = "Maria Cristina Gomez",
                    initials = "MG",
                    course = "BS Electronics Engineering • 4A (4th Year, Regular)",
                    campus = "Batasan Campus",
                    email = "m.gomez.08819@qcu.edu.ph",
                    phone = "+63 915 889 0122",
                    date = "Oct 22, 2026",
                    status = "pending",
                    meta = "CoR Document • 2.6 MB • Uploaded Oct 22, 2026"
                },
                new {
                    id = "2024-01123-SB",
                    kyc = "KYC-2024-01123",
                    name = "John Patrick Reyes",
                    initials = "JR",
                    course = "BS Information Technology • 3C (3rd Year, Regular)",
                    campus = "San Bartolome (Main Campus)",
                    email = "j.reyes.01123@qcu.edu.ph",
                    phone = "+63 949 712 5590",
                    date = "Oct 21, 2026",
                    status = "rejected",
                    meta = "ID Scan • 0.9 MB • Uploaded Oct 21, 2026"
                }
            };

            return Json(new { success = true, data = students }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public JsonResult Approve(string id)
        {
            return Json(new { success = true, id = id, status = "Approved" });
        }

        [HttpPost]
        public JsonResult Reject(string id, string reason)
        {
            return Json(new { success = true, id = id, reason = reason, status = "Rejected" });
        }
    }
}
