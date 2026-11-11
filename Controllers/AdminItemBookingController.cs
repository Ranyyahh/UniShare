using System.Web.Mvc;
using System.Collections.Generic;

namespace UniShare.Controllers
{
    [Authorize(Roles = "ADMIN")]
    public class AdminItemBookingController : Controller
    {
        [HttpGet]
        public ActionResult Index()
        {
            return View();
        }

        // ---------------------------------------------------------
        // PLACEHOLDER API: Replace this with your actual DB query
        // ---------------------------------------------------------
        [HttpGet]
        public JsonResult GetListings()
        {
            var listings = new List<object>
            {
                new {
                    id = "ITM-2026-0891",
                    name = "Casio fx-991EX ClassWiz",
                    category = "Electronics",
                    owner = "Maria Santos",
                    status = "pending",
                    active = "inactive",
                    price = "₱25/hr",
                    meta = "Uploaded Oct 24, 2026 • 2.4 MB",
                    desc = "Official QCU Department of Mathematics midterm/board approved non-programmable scientific calculator. Includes original protective slide cover with quick reference formula card.",
                    course = "BS Math, BSIT, BSEd",
                    condition = "Like New (9.5-10/10)",
                    handover = "07:00 AM - 09:00 AM",
                    returnTime = "05:00 PM - 06:00 PM",
                    days = "Mon,Tue,Wed,Thu,Fri"
                },
                new {
                    id = "ITM-2026-0740",
                    name = "Logitech MX Master 3S Mouse",
                    category = "Electronics",
                    owner = "Lester David",
                    status = "approved",
                    active = "active",
                    price = "₱50/hr",
                    meta = "Uploaded Oct 23, 2026 • 1.8 MB",
                    desc = "Ergonomic wireless mouse with 8K DPI tracking. Includes Bolt USB receiver and charging cable.",
                    course = "BSIT, BSCS",
                    condition = "Good (8-9/10)",
                    handover = "08:00 AM - 10:00 AM",
                    returnTime = "04:00 PM - 05:00 PM",
                    days = "Mon,Tue,Wed,Thu,Fri"
                },
                new {
                    id = "ITM-2026-0612",
                    name = "Arduino Uno R3 Starter Kit",
                    category = "Other Items",
                    owner = "Kenneth Dave Ramirez",
                    status = "rejected",
                    active = "inactive",
                    price = "₱40/hr",
                    meta = "Uploaded Oct 22, 2026 • 3.1 MB",
                    desc = "Complete starter kit with breadboard, jumper wires, and sensors. Contains 2x LiPo cells (unapproved).",
                    course = "BSECE, BSCS",
                    condition = "Fair (6.5-7.5/10)",
                    handover = "09:00 AM - 11:00 AM",
                    returnTime = "03:00 PM - 04:00 PM",
                    days = "Mon,Tue,Wed"
                },
                new {
                    id = "ITM-2026-0554",
                    name = "Staedtler Mars 700 Pen Set",
                    category = "School Supplies",
                    owner = "Chloe Anne Cruz",
                    status = "approved",
                    active = "active",
                    price = "₱30/hr",
                    meta = "Uploaded Oct 21, 2026 • 1.2 MB",
                    desc = "Professional drafting pen set with 0.1, 0.3, and 0.5 mm tips. Recently refilled with black ink.",
                    course = "BS Architecture, BSIE",
                    condition = "Like New (9.5-10/10)",
                    handover = "10:00 AM - 12:00 PM",
                    returnTime = "02:00 PM - 03:00 PM",
                    days = "Mon,Tue,Wed,Thu,Fri,Sat"
                }
            };

            return Json(new { success = true, data = listings }, JsonRequestBehavior.AllowGet);
        }
        // ---------------------------------------------------------

        [HttpPost]
        public JsonResult Approve(string id)
        {
            // TODO: Update DB status to 'approved'
            return Json(new { success = true, id = id, status = "Approved" });
        }

        [HttpPost]
        public JsonResult Reject(string id, string reason)
        {
            // TODO: Update DB status to 'rejected'
            return Json(new { success = true, id = id, reason = reason, status = "Rejected" });
        }

        [HttpPost]
        public JsonResult Delete(string id)
        {
            // TODO: Delete from DB
            return Json(new { success = true, id = id, status = "Deleted" });
        }
    }
}
