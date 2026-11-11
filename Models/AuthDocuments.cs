using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UniShare.Models
{
    [BsonIgnoreExtraElements]
    public class UserDocument
    {
        [BsonId] public ObjectId Id { get; set; }
        [BsonElement("schoolEmail")] public string SchoolEmail { get; set; }
        [BsonElement("firstName")] public string FirstName { get; set; }
        [BsonElement("lastName")] public string LastName { get; set; }
        [BsonElement("middleName")] public string MiddleName { get; set; }
        [BsonElement("username")] public string Username { get; set; }
        [BsonElement("role")] public string Role { get; set; }
        [BsonElement("status")] public string Status { get; set; }
        [BsonElement("passwordHash")] public string PasswordHash { get; set; }
        [BsonElement("password")] public string LegacyPassword { set { if (string.IsNullOrEmpty(PasswordHash)) PasswordHash = value; } }
        [BsonElement("birthdate")] public DateTime? Birthdate { get; set; }
        [BsonElement("phone")] public string Phone { get; set; }
        [BsonElement("contactNumber")] public string ContactNumber { get; set; }
        [BsonElement("section")] public string Section { get; set; }
        [BsonElement("studentIdNumber")] public string StudentIdNumber { get; set; }
        [BsonElement("studentNumber")] public string StudentNumber { set { if (string.IsNullOrEmpty(StudentIdNumber)) StudentIdNumber = value; } }
        [BsonElement("schoolIdPhoto")] public string SchoolIdPhoto { get; set; }
        [BsonElement("oneByOnePhoto")] public string OneByOnePhoto { get; set; }
        [BsonElement("registrationForm")] public string RegistrationForm { get; set; }
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
        [BsonElement("studentIdNumber")] public string StudentIdNumber { get; set; }
        [BsonElement("studentNumber")] public string StudentNumber { set { if (string.IsNullOrEmpty(StudentIdNumber)) StudentIdNumber = value; } }
        [BsonElement("program")] public string Program { get; set; }
        [BsonElement("yearLevel")] public string YearLevel { get; set; }
        [BsonElement("schoolIdPath")] public string SchoolIdPath { get; set; }
        [BsonElement("submittedDocument")] public string SubmittedDocument { set { if (string.IsNullOrEmpty(SchoolIdPath)) SchoolIdPath = value; } }
        [BsonElement("oneByOnePhotoPath")] public string OneByOnePhotoPath { get; set; }
        [BsonElement("registrationFormPath")] public string RegistrationFormPath { get; set; }
        [BsonElement("status")] public string Status { get; set; }
        [BsonElement("reviewNotes")] public string ReviewNotes { get; set; }
        [BsonElement("remarks")] public string Remarks { set { if (string.IsNullOrEmpty(ReviewNotes)) ReviewNotes = value; } }
        [BsonElement("submittedAt")] public DateTime SubmittedAt { get; set; }
        [BsonElement("createdAt")] public DateTime CreatedAt { set { if (SubmittedAt == default(DateTime)) SubmittedAt = value; } }
        [BsonElement("reviewedAt")] public DateTime? ReviewedAt { get; set; }
        [BsonElement("reviewedBy")] public ObjectId? ReviewedBy { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class AuditLogDocument
    {
        [BsonId] public ObjectId Id { get; set; }
        [BsonElement("userId")] public ObjectId UserId { get; set; }
        [BsonElement("action")] public string Action { get; set; }
        [BsonElement("entityType")] public string EntityType { get; set; }
        [BsonElement("entityId")] public ObjectId? EntityId { get; set; }
        [BsonElement("details")] public BsonDocument Details { get; set; }
        [BsonElement("createdAt")] public DateTime CreatedAt { get; set; }
    }
}
