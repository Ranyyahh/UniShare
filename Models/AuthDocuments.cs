using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;

namespace UniShare.Models
{
    [BsonIgnoreExtraElements]
    public class UserDocument
    {
        [BsonId] public ObjectId Id { get; set; }
        [BsonElement("schoolEmail")] public string SchoolEmail { get; set; }
        [BsonElement("firstName")] public string FirstName { get; set; }
        [BsonElement("lastName")] public string LastName { get; set; }
        [BsonIgnoreIfNull] [BsonElement("middleName")] public string MiddleName { get; set; }
        [BsonIgnoreIfNull] [BsonElement("username")] public string Username { get; set; }
        [BsonElement("role")] public string Role { get; set; }
        [BsonElement("status")] public string Status { get; set; }
        [BsonElement("passwordHash")] public string PasswordHash { get; set; }
        [BsonIgnoreIfNull] [BsonElement("birthdate")] public DateTime? Birthdate { get; set; }
        [BsonIgnoreIfNull] [BsonElement("phone")] public string Phone { get; set; }
        [BsonIgnoreIfNull] [BsonElement("contactNumber")] public string ContactNumber { get; set; }
        [BsonIgnoreIfNull] [BsonElement("section")] public string Section { get; set; }
        [BsonIgnoreIfNull] [BsonElement("program")] public string Program { get; set; }
        [BsonIgnoreIfNull] [BsonElement("yearLevel")] public string YearLevel { get; set; }
        [BsonIgnoreIfNull] [BsonElement("campus")] public string Campus { get; set; }
        [BsonIgnoreIfNull] [BsonElement("bio")] public string Bio { get; set; }
        [BsonIgnoreIfNull] [BsonElement("profilePhoto")] public string ProfilePhoto { get; set; }
        [BsonElement("studentNumber")] public string StudentIdNumber { get; set; }
        [BsonIgnoreIfNull] [BsonElement("schoolIdPhoto")] public string SchoolIdPhoto { get; set; }
        [BsonIgnoreIfNull] [BsonElement("oneByOnePhoto")] public string OneByOnePhoto { get; set; }
        [BsonIgnoreIfNull] [BsonElement("registrationForm")] public string RegistrationForm { get; set; }
        [BsonIgnoreIfNull] [BsonElement("submittedDocument")] public string SubmittedDocument { get; set; }
        [BsonElement("verificationStatus")] public string VerificationStatus { get; set; }
        [BsonElement("createdAt")] public DateTime CreatedAt { get; set; }
        [BsonElement("updatedAt")] public DateTime UpdatedAt { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class VerificationApplicationDocument
    {
        [BsonId] public ObjectId Id { get; set; }
        [BsonElement("userId")] public ObjectId UserId { get; set; }
        [BsonElement("schoolEmail")] public string SchoolEmail { get; set; }
        [BsonElement("studentNumber")] public string StudentIdNumber { get; set; }
        [BsonIgnoreIfNull] [BsonElement("program")] public string Program { get; set; }
        [BsonIgnoreIfNull] [BsonElement("yearLevel")] public string YearLevel { get; set; }
        [BsonIgnoreIfNull] [BsonElement("schoolIdPath")] public string SchoolIdPath { get; set; }
        [BsonIgnoreIfNull] [BsonElement("submittedDocument")] public string SubmittedDocument { get; set; }
        [BsonIgnoreIfNull] [BsonElement("oneByOnePhotoPath")] public string OneByOnePhotoPath { get; set; }
        [BsonIgnoreIfNull] [BsonElement("registrationFormPath")] public string RegistrationFormPath { get; set; }
        [BsonElement("status")] public string Status { get; set; }
        [BsonIgnoreIfNull] [BsonElement("reviewNotes")] public string ReviewNotes { get; set; }
        [BsonIgnoreIfNull] [BsonElement("remarks")] public string Remarks { get; set; }
        [BsonElement("submittedAt")] public DateTime SubmittedAt { get; set; }
        [BsonIgnoreIfNull] [BsonElement("createdAt")] public DateTime? CreatedAt { get; set; }
        [BsonIgnoreIfNull] [BsonElement("reviewedAt")] public DateTime? ReviewedAt { get; set; }
        [BsonIgnoreIfNull] [BsonElement("reviewedBy")] public ObjectId? ReviewedBy { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class AuditLogDocument
    {
        [BsonId] public ObjectId Id { get; set; }
        [BsonElement("userId")] public ObjectId UserId { get; set; }
        [BsonElement("action")] public string Action { get; set; }
        [BsonElement("entityType")] public string EntityType { get; set; }
        [BsonIgnoreIfNull] [BsonElement("entityId")] public ObjectId? EntityId { get; set; }
        [BsonIgnoreIfNull] [BsonElement("details")] public BsonDocument Details { get; set; }
        [BsonElement("createdAt")] public DateTime CreatedAt { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class ItemDocument
    {
        [BsonId] public ObjectId Id { get; set; }
        [BsonElement("ownerId")] public ObjectId OwnerId { get; set; }
        [BsonElement("name")] public string Name { get; set; }
        [BsonElement("description")] public string Description { get; set; }
        [BsonElement("category")] public string Category { get; set; }
        [BsonElement("photos")] public List<string> Photos { get; set; } = new List<string>();
        [BsonElement("condition")] public string Condition { get; set; }
        [BsonElement("rentalDetails")] public ItemRentalDetailsDocument RentalDetails { get; set; } = new ItemRentalDetailsDocument();
        [BsonElement("location")] public ItemLocationDocument Location { get; set; } = new ItemLocationDocument();
        [BsonElement("availabilityStatus")] public string AvailabilityStatus { get; set; } = "AVAILABLE";
        [BsonElement("approvalStatus")] public string ApprovalStatus { get; set; } = "PENDING";
        [BsonIgnoreIfNull] [BsonElement("approvalRejectionReason")] public string ApprovalRejectionReason { get; set; }
        [BsonIgnoreIfNull] [BsonElement("approvalReviewedAt")] public DateTime? ApprovalReviewedAt { get; set; }
        [BsonIgnoreIfNull] [BsonElement("approvalReviewedBy")] public ObjectId? ApprovalReviewedBy { get; set; }
        [BsonElement("availableDays")] public List<string> AvailableDays { get; set; } = new List<string>();
        [BsonElement("earliestHandoverTime")] public string EarliestHandoverTime { get; set; }
        [BsonElement("latestReturnTime")] public string LatestReturnTime { get; set; }
        [BsonElement("checkpoint")] public string Checkpoint { get; set; }
        [BsonElement("accessories")][BsonIgnoreIfNull] public string Accessories { get; set; }
        [BsonElement("createdAt")] public DateTime CreatedAt { get; set; }
        [BsonElement("updatedAt")] public DateTime UpdatedAt { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class ItemRentalDetailsDocument
    {
        [BsonElement("pricePerDay")] public double PricePerDay { get; set; }
        [BsonElement("hourlyRate")] public double HourlyRate { get; set; }
        [BsonElement("dailyRate")] public double DailyRate { get; set; }
        [BsonElement("pricingUnitHours")] public int PricingUnitHours { get; set; }
        [BsonElement("securityDeposit")] public double SecurityDeposit { get; set; }
        [BsonElement("replacementValue")] public double ReplacementValue { get; set; }
        [BsonElement("minimumRentalDays")] public int MinimumRentalDays { get; set; }
        [BsonElement("maximumRentalDays")] public int MaximumRentalDays { get; set; }
        [BsonElement("enableFullDayBundle")] public bool EnableFullDayBundle { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class ItemLocationDocument
    {
        [BsonElement("campus")] public string Campus { get; set; }
        [BsonElement("building")] public string Building { get; set; }
        [BsonElement("room")] public string Room { get; set; }
        [BsonElement("handoverLocation")] public string HandoverLocation { get; set; }
    }
}
