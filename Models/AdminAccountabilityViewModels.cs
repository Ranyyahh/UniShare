using System;
using System.Collections.Generic;

namespace UniShare.Models
{
    public class AdminDashboardViewModel
    {
        public long RegisteredUsers { get; set; }
        public long ActiveBookings { get; set; }
        public long OpenDisputes { get; set; }
        public long PendingNoShowReports { get; set; }
        public long ReportedReviews { get; set; }
        public long ActiveRestrictions { get; set; }
        public IList<AuditLogRowViewModel> RecentActivity { get; set; } = new List<AuditLogRowViewModel>();
    }

    public class NoShowIncidentViewModel
    {
        public string Id { get; set; }
        public string BookingId { get; set; }
        public string RequestId { get; set; }
        public string ItemName { get; set; }
        public string ReporterName { get; set; }
        public string ReporterRole { get; set; }
        public string SubjectName { get; set; }
        public string Status { get; set; }
        public string Reason { get; set; }
        public string AdminNotes { get; set; }
        public DateTime ReportedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public bool CanReview { get { return Status == "PENDING"; } }
    }

    public class UserManagementViewModel
    {
        public string Search { get; set; }
        public IList<UserAccountRowViewModel> Users { get; set; } = new List<UserAccountRowViewModel>();
    }

    public class UserAccountRowViewModel
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string StudentNumber { get; set; }
        public string Role { get; set; }
        public string Status { get; set; }
        public string VerificationStatus { get; set; }
        public string RestrictionSummary { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class AccountRestrictionPageViewModel
    {
        public IList<UserAccountRowViewModel> Users { get; set; } = new List<UserAccountRowViewModel>();
        public IList<AccountRestrictionRowViewModel> Restrictions { get; set; } = new List<AccountRestrictionRowViewModel>();
    }

    public class AccountRestrictionRowViewModel
    {
        public string Id { get; set; }
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string Type { get; set; }
        public string Status { get; set; }
        public string Reason { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public bool CanRevoke { get { return Status == "ACTIVE" && Type != "WARNING"; } }
    }

    public class RatingsReviewsViewModel
    {
        public string Filter { get; set; }
        public IList<RatingReviewRowViewModel> Reviews { get; set; } = new List<RatingReviewRowViewModel>();
    }

    public class RatingReviewRowViewModel
    {
        public string Id { get; set; }
        public string BookingId { get; set; }
        public string ItemName { get; set; }
        public string ReviewerName { get; set; }
        public string ReviewedUserName { get; set; }
        public int? ItemRating { get; set; }
        public int? OwnerRating { get; set; }
        public string Review { get; set; }
        public string Status { get; set; }
        public string ReportReason { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class AuditLogRowViewModel
    {
        public string Id { get; set; }
        public string ActorName { get; set; }
        public string Action { get; set; }
        public string EntityType { get; set; }
        public string EntityId { get; set; }
        public string Summary { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
