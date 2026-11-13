using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Driver;
using UniShare.Models;

namespace UniShare.Services
{
    public static class AdminAccountabilityService
    {
        private static readonly string[] OpenDisputeStatuses = { "OPEN", "UNDER_REVIEW", "INFO_REQUESTED" };
        private static readonly string[] BlockingRestrictionTypes = { "TEMPORARY_SUSPENSION", "PERMANENT_BAN" };

        public static void LogActivity(ObjectId actorId, string action, string entityType, ObjectId? entityId, BsonDocument details)
        {
            WriteAudit(actorId, action, entityType, entityId, details);
        }

        public static AdminDashboardViewModel GetDashboard()
        {
            ImportLegacyDisputes();
            var now = DateTime.UtcNow;
            var activeBookings = MongoDatabase.Bookings.CountDocuments(Builders<BsonDocument>.Filter.In("status", new[] { "ACCEPTED", "ONGOING", "ACTIVE", "OVERDUE", "RETURN_REQUESTED" }));
            var recent = MongoDatabase.AuditLogs.Find(Builders<AuditLogDocument>.Filter.Empty).SortByDescending(x => x.CreatedAt).Limit(8).ToList();
            return new AdminDashboardViewModel
            {
                RegisteredUsers = MongoDatabase.Users.CountDocuments(Builders<UserDocument>.Filter.Empty),
                ActiveBookings = activeBookings,
                OpenDisputes = MongoDatabase.Disputes.CountDocuments(Builders<BsonDocument>.Filter.In("status", OpenDisputeStatuses)),
                PendingNoShowReports = MongoDatabase.NoShowIncidents.CountDocuments(Builders<BsonDocument>.Filter.Eq("status", "PENDING")),
                ReportedReviews = MongoDatabase.Ratings.CountDocuments(Builders<BsonDocument>.Filter.Eq("isReported", true)),
                ActiveRestrictions = MongoDatabase.AccountRestrictions.CountDocuments(Builders<BsonDocument>.Filter.Eq("status", "ACTIVE") & Builders<BsonDocument>.Filter.In("type", BlockingRestrictionTypes)),
                RecentActivity = recent.Select(ToAuditRow).ToList()
            };
        }

        public static IList<DisputeViewModel> GetDisputes()
        {
            ImportLegacyDisputes();
            return MongoDatabase.Disputes.Find(Builders<BsonDocument>.Filter.Empty).SortByDescending(x => x["createdAt"]).Limit(500).ToList().Select(ToDispute).ToList();
        }

        public static DisputeViewModel GetDispute(string id)
        {
            ObjectId disputeId;
            if (!ObjectId.TryParse(id, out disputeId)) return null;
            var doc = MongoDatabase.Disputes.Find(Builders<BsonDocument>.Filter.Eq("_id", disputeId)).FirstOrDefault();
            return doc == null ? null : ToDispute(doc);
        }

        public static bool UpdateDispute(string id, ObjectId adminId, string decision, string notes)
        {
            ObjectId disputeId;
            var status = (decision ?? "").Trim().ToUpperInvariant();
            if (!ObjectId.TryParse(id, out disputeId) || !new[] { "UNDER_REVIEW", "INFO_REQUESTED", "RESOLVED" }.Contains(status)) return false;
            if (notes != null && notes.Length > 1500) notes = notes.Substring(0, 1500);
            var dispute = MongoDatabase.Disputes.Find(Builders<BsonDocument>.Filter.Eq("_id", disputeId)).FirstOrDefault();
            if (dispute == null || !OpenDisputeStatuses.Contains(ReadString(dispute, "status").ToUpperInvariant())) return false;
            var now = DateTime.UtcNow;
            var evt = new BsonDocument { { "action", status }, { "notes", notes ?? "" }, { "adminId", adminId }, { "createdAt", now } };
            var update = Builders<BsonDocument>.Update.Set("status", status).Set("adminNotes", notes ?? "").Set("updatedAt", now)
                .Set("reviewedBy", adminId).Push("events", evt);
            if (status == "RESOLVED") update = update.Set("resolvedAt", now);
            var changed = MongoDatabase.Disputes.UpdateOne(Builders<BsonDocument>.Filter.Eq("_id", disputeId) & Builders<BsonDocument>.Filter.In("status", OpenDisputeStatuses), update).ModifiedCount == 1;
            if (!changed) return false;
            if (status == "RESOLVED")
            {
                var bookingId = ReadObjectId(dispute, "bookingId");
                if (bookingId.HasValue)
                {
                    MongoDatabase.Bookings.UpdateOne(Builders<BsonDocument>.Filter.Eq("_id", bookingId.Value) & Builders<BsonDocument>.Filter.Eq("status", "DISPUTED"),
                        Builders<BsonDocument>.Update.Set("status", "COMPLETED").Set("completedAt", now).Set("disputeResolvedAt", now).Set("updatedAt", now));
                    MongoDatabase.Rentals.UpdateOne(Builders<BsonDocument>.Filter.Eq("bookingId", bookingId.Value) & Builders<BsonDocument>.Filter.Eq("status", "DISPUTED"),
                        Builders<BsonDocument>.Update.Set("status", "COMPLETED").Set("completedAt", now).Set("disputeResolvedAt", now).Set("updatedAt", now));
                }
            }
            WriteAudit(adminId, "DISPUTE_" + status, "DISPUTE", disputeId, new BsonDocument { { "notes", notes ?? "" } });
            return true;
        }

        public static IList<NoShowIncidentViewModel> GetNoShowIncidents()
        {
            return MongoDatabase.NoShowIncidents.Find(Builders<BsonDocument>.Filter.Empty).SortByDescending(x => x["reportedAt"]).Limit(500).ToList().Select(ToNoShow).ToList();
        }

        public static bool ReportNoShow(ObjectId reporterId, string bookingId, string reason)
        {
            ObjectId parsedBookingId;
            if (!ObjectId.TryParse(bookingId, out parsedBookingId) || String.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500) return false;
            var booking = MongoDatabase.Bookings.Find(Builders<BsonDocument>.Filter.Eq("_id", parsedBookingId)).FirstOrDefault();
            if (booking == null) return false;
            var ownerId = ReadObjectId(booking, "ownerId");
            var borrowerId = ReadObjectId(booking, "borrowerId") ?? ReadObjectId(booking, "renterId");
            if (!ownerId.HasValue || !borrowerId.HasValue || Bool(booking, "handoverConfirmed")) return false;
            var reporterIsOwner = reporterId == ownerId.Value;
            if (!reporterIsOwner && reporterId != borrowerId.Value) return false;
            var status = ReadString(booking, "status").ToUpperInvariant();
            if (status != "ACCEPTED" && !(status == "PENDING" && (booking.Contains("acceptedAt") || booking.Contains("approvedAt")))) return false;
            var schedule = ReadDate(booking, "handoverDate", ReadDate(booking, "startDate", DateTime.MaxValue));
            if (schedule > DateTime.UtcNow) return false;
            var existing = MongoDatabase.NoShowIncidents.Find(Builders<BsonDocument>.Filter.Eq("bookingId", parsedBookingId) &
                Builders<BsonDocument>.Filter.In("status", new[] { "PENDING", "CONFIRMED" })).FirstOrDefault();
            if (existing != null) return false;

            var now = DateTime.UtcNow;
            var incidentId = ObjectId.GenerateNewId();
            var subjectId = reporterIsOwner ? borrowerId.Value : ownerId.Value;
            MongoDatabase.NoShowIncidents.InsertOne(new BsonDocument
            {
                { "_id", incidentId }, { "bookingId", parsedBookingId }, { "requestId", ReadString(booking, "requestId") },
                { "itemName", ReadString(booking, "itemName") }, { "reportedBy", reporterId }, { "reporterRole", reporterIsOwner ? "OWNER" : "BORROWER" },
                { "subjectUserId", subjectId }, { "reason", reason.Trim() }, { "status", "PENDING" }, { "reportedAt", now },
                { "evidenceUrls", new BsonArray() }
            });
            WriteAudit(reporterId, "NO_SHOW_REPORTED", "NO_SHOW_INCIDENT", incidentId, new BsonDocument { { "bookingId", parsedBookingId }, { "subjectUserId", subjectId } });
            return true;
        }

        public static bool ReviewNoShow(string id, ObjectId adminId, bool confirm, string notes)
        {
            ObjectId incidentId;
            if (!ObjectId.TryParse(id, out incidentId)) return false;
            if (notes != null && notes.Length > 1000) notes = notes.Substring(0, 1000);
            var incident = MongoDatabase.NoShowIncidents.Find(Builders<BsonDocument>.Filter.Eq("_id", incidentId) & Builders<BsonDocument>.Filter.Eq("status", "PENDING")).FirstOrDefault();
            if (incident == null) return false;
            var bookingId = ReadObjectId(incident, "bookingId");
            if (!bookingId.HasValue) return false;
            var now = DateTime.UtcNow;
            if (confirm)
            {
                var eligible = Builders<BsonDocument>.Filter.Eq("_id", bookingId.Value) & Builders<BsonDocument>.Filter.In("status", new[] { "ACCEPTED", "PENDING" }) &
                    Builders<BsonDocument>.Filter.Ne("handoverConfirmed", true);
                var cancelled = MongoDatabase.Bookings.UpdateOne(eligible, Builders<BsonDocument>.Update.Set("status", "CANCELLED")
                    .Set("cancelReason", "CONFIRMED_NO_SHOW").Set("cancelledAt", now).Set("updatedAt", now));
                if (cancelled.ModifiedCount != 1) return false;
                MongoDatabase.Rentals.UpdateOne(Builders<BsonDocument>.Filter.Eq("bookingId", bookingId.Value),
                    Builders<BsonDocument>.Update.Set("status", "CANCELLED").Set("cancelReason", "CONFIRMED_NO_SHOW").Set("cancelledAt", now).Set("updatedAt", now));
            }
            var finalStatus = confirm ? "CONFIRMED" : "REJECTED";
            var update = Builders<BsonDocument>.Update.Set("status", finalStatus).Set("adminNotes", notes ?? "").Set("reviewedBy", adminId).Set("reviewedAt", now);
            if (MongoDatabase.NoShowIncidents.UpdateOne(Builders<BsonDocument>.Filter.Eq("_id", incidentId) & Builders<BsonDocument>.Filter.Eq("status", "PENDING"), update).ModifiedCount != 1) return false;

            if (confirm)
            {
                var subjectId = ReadObjectId(incident, "subjectUserId");
                if (subjectId.HasValue)
                {
                    var confirmedCount = MongoDatabase.NoShowIncidents.CountDocuments(Builders<BsonDocument>.Filter.Eq("subjectUserId", subjectId.Value) & Builders<BsonDocument>.Filter.Eq("status", "CONFIRMED"));
                    if (confirmedCount == 1) CreateWarning(subjectId.Value, incidentId, adminId, "First confirmed no-show incident.");
                }
            }
            WriteAudit(adminId, confirm ? "NO_SHOW_CONFIRMED" : "NO_SHOW_REJECTED", "NO_SHOW_INCIDENT", incidentId, new BsonDocument { { "notes", notes ?? "" } });
            return true;
        }

        public static UserManagementViewModel GetUsers(string search)
        {
            var users = MongoDatabase.Users.Find(Builders<UserDocument>.Filter.Empty).SortByDescending(x => x.CreatedAt).Limit(1000).ToList();
            var restrictions = MongoDatabase.AccountRestrictions.Find(Builders<BsonDocument>.Filter.Eq("status", "ACTIVE")).ToList();
            var vm = new UserManagementViewModel { Search = search ?? "" };
            foreach (var user in users)
            {
                var name = FullName(user);
                var restriction = restrictions.FirstOrDefault(x => ReadObjectId(x, "userId") == user.Id && IsRestrictionCurrent(x, DateTime.UtcNow));
                var row = new UserAccountRowViewModel
                {
                    Id = user.Id.ToString(), Name = name, Email = user.SchoolEmail ?? "", StudentNumber = user.StudentIdNumber ?? "",
                    Role = user.Role ?? "STUDENT", Status = user.Status ?? "UNKNOWN", VerificationStatus = user.VerificationStatus ?? "UNKNOWN",
                    RestrictionSummary = restriction == null ? "None" : ReadString(restriction, "type").Replace('_', ' '), CreatedAt = user.CreatedAt
                };
                if (String.IsNullOrWhiteSpace(search) || (row.Name + " " + row.Email + " " + row.StudentNumber).IndexOf(search.Trim(), StringComparison.OrdinalIgnoreCase) >= 0)
                    vm.Users.Add(row);
            }
            return vm;
        }

        public static AccountRestrictionPageViewModel GetRestrictionsPage()
        {
            var users = GetUsers(null).Users.Where(x => !String.Equals(x.Role, "ADMIN", StringComparison.OrdinalIgnoreCase)).ToList();
            var records = MongoDatabase.AccountRestrictions.Find(Builders<BsonDocument>.Filter.Empty).SortByDescending(x => x["createdAt"]).Limit(500).ToList();
            return new AccountRestrictionPageViewModel
            {
                Users = users,
                Restrictions = records.Select(ToRestriction).ToList()
            };
        }

        public static bool CreateRestriction(string userId, ObjectId adminId, string type, string reason, int? days)
        {
            ObjectId targetId;
            type = (type ?? "").Trim().ToUpperInvariant();
            if (!ObjectId.TryParse(userId, out targetId) || targetId == adminId || String.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 1000) return false;
            if (type != "WARNING" && type != "TEMPORARY_SUSPENSION" && type != "PERMANENT_BAN") return false;
            var user = MongoDatabase.Users.Find(x => x.Id == targetId).FirstOrDefault();
            if (user == null || String.Equals(user.Role, "ADMIN", StringComparison.OrdinalIgnoreCase)) return false;
            var now = DateTime.UtcNow;
            DateTime? expiresAt = null;
            if (type == "TEMPORARY_SUSPENSION")
            {
                if (!days.HasValue || days < 1 || days > 365) return false;
                expiresAt = now.AddDays(days.Value);
            }
            var previousStatus = user.Status ?? "ACTIVE";
            if (BlockingRestrictionTypes.Contains(type))
            {
                var activeBlocking = MongoDatabase.AccountRestrictions.Find(Builders<BsonDocument>.Filter.Eq("userId", targetId) &
                    Builders<BsonDocument>.Filter.Eq("status", "ACTIVE") & Builders<BsonDocument>.Filter.In("type", BlockingRestrictionTypes))
                    .SortBy(x => x["createdAt"]).FirstOrDefault();
                if (activeBlocking != null && activeBlocking.Contains("previousStatus")) previousStatus = ReadString(activeBlocking, "previousStatus", previousStatus);
            }
            var id = ObjectId.GenerateNewId();
            var record = new BsonDocument
            {
                { "_id", id }, { "userId", targetId }, { "type", type }, { "reason", reason.Trim() }, { "status", "ACTIVE" },
                { "createdBy", adminId }, { "createdAt", now }, { "source", "ADMIN_REVIEW" }
            };
            if (BlockingRestrictionTypes.Contains(type)) record.Add("previousStatus", previousStatus);
            if (expiresAt.HasValue) record.Add("expiresAt", expiresAt.Value);
            MongoDatabase.AccountRestrictions.InsertOne(record);
            if (type == "TEMPORARY_SUSPENSION") MongoDatabase.Users.UpdateOne(x => x.Id == targetId, Builders<UserDocument>.Update.Set(x => x.Status, "SUSPENDED").Set(x => x.UpdatedAt, now));
            else if (type == "PERMANENT_BAN") MongoDatabase.Users.UpdateOne(x => x.Id == targetId, Builders<UserDocument>.Update.Set(x => x.Status, "BANNED").Set(x => x.UpdatedAt, now));
            WriteAudit(adminId, "ACCOUNT_" + type, "USER", targetId, new BsonDocument { { "restrictionId", id }, { "reason", reason.Trim() }, { "expiresAt", expiresAt.HasValue ? (BsonValue)expiresAt.Value : BsonNull.Value } });
            return true;
        }

        public static bool RevokeRestriction(string id, ObjectId adminId, string notes)
        {
            ObjectId restrictionId;
            if (!ObjectId.TryParse(id, out restrictionId)) return false;
            if (notes != null && notes.Length > 1000) notes = notes.Substring(0, 1000);
            var record = MongoDatabase.AccountRestrictions.Find(Builders<BsonDocument>.Filter.Eq("_id", restrictionId) & Builders<BsonDocument>.Filter.Eq("status", "ACTIVE")).FirstOrDefault();
            if (record == null || !BlockingRestrictionTypes.Contains(ReadString(record, "type"))) return false;
            var userId = ReadObjectId(record, "userId");
            var now = DateTime.UtcNow;
            var changed = MongoDatabase.AccountRestrictions.UpdateOne(Builders<BsonDocument>.Filter.Eq("_id", restrictionId) & Builders<BsonDocument>.Filter.Eq("status", "ACTIVE"),
                Builders<BsonDocument>.Update.Set("status", "REVOKED").Set("revokedAt", now).Set("revokedBy", adminId).Set("revocationNotes", notes ?? "")).ModifiedCount == 1;
            if (!changed) return false;
            if (userId.HasValue && !HasCurrentBlockingRestriction(userId.Value)) RestoreUserStatus(userId.Value);
            WriteAudit(adminId, "ACCOUNT_RESTRICTION_REVOKED", "ACCOUNT_RESTRICTION", restrictionId, new BsonDocument { { "notes", notes ?? "" } });
            return true;
        }

        public static bool IsUserRestricted(ObjectId userId)
        {
            var user = MongoDatabase.Users.Find(x => x.Id == userId).FirstOrDefault();
            if (user == null) return false;
            var now = DateTime.UtcNow;
            var records = MongoDatabase.AccountRestrictions.Find(Builders<BsonDocument>.Filter.Eq("userId", userId) & Builders<BsonDocument>.Filter.Eq("status", "ACTIVE")).ToList();
            var expiredSuspension = false;
            foreach (var record in records.Where(x => ReadString(x, "type") == "TEMPORARY_SUSPENSION" && ReadDate(x, "expiresAt", DateTime.MaxValue) <= now))
            {
                var expired = MongoDatabase.AccountRestrictions.UpdateOne(Builders<BsonDocument>.Filter.Eq("_id", record["_id"]) & Builders<BsonDocument>.Filter.Eq("status", "ACTIVE"),
                    Builders<BsonDocument>.Update.Set("status", "EXPIRED").Set("expiredAt", now)).ModifiedCount == 1;
                expiredSuspension = expiredSuspension || expired;
                if (expired) WriteAudit(ObjectId.Empty, "ACCOUNT_SUSPENSION_EXPIRED", "USER", userId, new BsonDocument { { "restrictionId", record["_id"] } });
            }
            var blocked = records.Any(x => BlockingRestrictionTypes.Contains(ReadString(x, "type")) && IsRestrictionCurrent(x, now));
            if (blocked) return true;
            if (user.Status == "SUSPENDED" && expiredSuspension) { RestoreUserStatus(userId); return false; }
            return user.Status == "BANNED";
        }

        public static IList<RatingReviewRowViewModel> GetReviews(string filter)
        {
            var docs = MongoDatabase.Ratings.Find(Builders<BsonDocument>.Filter.Empty).SortByDescending(x => x["createdAt"]).Limit(500).ToList();
            if (String.Equals(filter, "reported", StringComparison.OrdinalIgnoreCase)) docs = docs.Where(x => Bool(x, "isReported")).ToList();
            return docs.Select(ToReview).ToList();
        }

        public static bool ReportReview(ObjectId reporterId, string id, string reason)
        {
            ObjectId ratingId;
            if (!ObjectId.TryParse(id, out ratingId) || String.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500) return false;
            var doc = MongoDatabase.Ratings.Find(Builders<BsonDocument>.Filter.Eq("_id", ratingId)).FirstOrDefault();
            if (doc == null || ReadObjectId(doc, "reviewerId") == reporterId || Bool(doc, "isHidden")) return false;
            var now = DateTime.UtcNow;
            var reports = doc.GetValue("reports", new BsonArray()).AsBsonArray;
            if (reports.Any(x => x.IsBsonDocument && ReadObjectId(x.AsBsonDocument, "reportedBy") == reporterId)) return false;
            var report = new BsonDocument { { "reportedBy", reporterId }, { "reason", reason.Trim() }, { "createdAt", now } };
            MongoDatabase.Ratings.UpdateOne(Builders<BsonDocument>.Filter.Eq("_id", ratingId), Builders<BsonDocument>.Update.Set("isReported", true).Set("reportReason", reason.Trim()).Set("reportedAt", now).Push("reports", report));
            WriteAudit(reporterId, "REVIEW_REPORTED", "RATING", ratingId, new BsonDocument { { "reason", reason.Trim() } });
            return true;
        }

        public static bool ModerateReview(string id, ObjectId adminId, string decision, string notes)
        {
            ObjectId ratingId;
            decision = (decision ?? "").Trim().ToUpperInvariant();
            if (!ObjectId.TryParse(id, out ratingId) || (decision != "HIDE" && decision != "DISMISS" && decision != "RESTORE")) return false;
            if (notes != null && notes.Length > 1000) notes = notes.Substring(0, 1000);
            var update = Builders<BsonDocument>.Update.Set("moderatedAt", DateTime.UtcNow).Set("moderatedBy", adminId).Set("moderationNotes", notes ?? "");
            if (decision == "HIDE") update = update.Set("isHidden", true).Set("moderationStatus", "HIDDEN");
            else if (decision == "RESTORE") update = update.Set("isHidden", false).Set("moderationStatus", "ACTIVE").Set("isReported", false);
            else update = update.Set("isReported", false).Set("reportReason", "");
            if (MongoDatabase.Ratings.UpdateOne(Builders<BsonDocument>.Filter.Eq("_id", ratingId), update).ModifiedCount != 1) return false;
            WriteAudit(adminId, "REVIEW_" + decision, "RATING", ratingId, new BsonDocument { { "notes", notes ?? "" } });
            return true;
        }

        public static IList<AuditLogRowViewModel> GetAuditLogs(string search)
        {
            var docs = MongoDatabase.AuditLogs.Find(Builders<AuditLogDocument>.Filter.Empty).SortByDescending(x => x.CreatedAt).Limit(1000).ToList().Select(ToAuditRow).ToList();
            return String.IsNullOrWhiteSpace(search) ? docs : docs.Where(x => (x.ActorName + " " + x.Action + " " + x.EntityType + " " + x.EntityId + " " + x.Summary).IndexOf(search.Trim(), StringComparison.OrdinalIgnoreCase) >= 0).ToList();
        }

        public static void RecordDispute(BsonDocument booking, ObjectId reporterId, string category, string details, IEnumerable<string> evidenceUrls = null)
        {
            var bookingId = ReadObjectId(booking, "bookingId") ?? ReadObjectId(booking, "_id");
            if (!bookingId.HasValue) return;
            var existing = MongoDatabase.Disputes.Find(Builders<BsonDocument>.Filter.Eq("bookingId", bookingId.Value) & Builders<BsonDocument>.Filter.In("status", OpenDisputeStatuses)).FirstOrDefault();
            if (existing != null) return;
            var ownerId = ReadObjectId(booking, "ownerId");
            var borrowerId = ReadObjectId(booking, "borrowerId") ?? ReadObjectId(booking, "renterId");
            var now = DateTime.UtcNow;
            var id = ObjectId.GenerateNewId();
            MongoDatabase.Disputes.InsertOne(new BsonDocument
            {
                { "_id", id }, { "bookingId", bookingId.Value }, { "requestId", ReadString(booking, "requestId") },
                { "itemName", ReadString(booking, "itemName") }, { "reporterId", reporterId },
                { "respondentId", ownerId.HasValue && reporterId == ownerId.Value ? (borrowerId.HasValue ? (BsonValue)borrowerId.Value : BsonNull.Value) : (ownerId.HasValue ? (BsonValue)ownerId.Value : BsonNull.Value) },
                { "category", category ?? "Other" }, { "details", details ?? "" }, { "incidentStatement", String.IsNullOrWhiteSpace(details) ? category ?? "" : (category ?? "") + ": " + details.Trim() },
                { "priority", "MEDIUM" }, { "status", "OPEN" }, { "evidenceUrls", new BsonArray((evidenceUrls ?? Enumerable.Empty<string>()).Select(x => (BsonValue)x)) }, { "events", new BsonArray() },
                { "createdAt", now }, { "updatedAt", now }
            });
            WriteAudit(reporterId, "DISPUTE_REPORTED", "DISPUTE", id, new BsonDocument { { "bookingId", bookingId.Value }, { "category", category ?? "Other" } });
        }

        private static void ImportLegacyDisputes()
        {
            var bookings = MongoDatabase.Bookings.Find(Builders<BsonDocument>.Filter.Eq("status", "DISPUTED")).Limit(500).ToList();
            foreach (var booking in bookings)
            {
                var bookingId = ReadObjectId(booking, "_id");
                if (!bookingId.HasValue || MongoDatabase.Disputes.Find(Builders<BsonDocument>.Filter.Eq("bookingId", bookingId.Value)).Limit(1).Any()) continue;
                RecordDispute(booking, ReadObjectId(booking, "borrowerId") ?? ObjectId.Empty, ReadString(booking, "disputeCategory"), ReadString(booking, "disputeDetails"));
            }
        }

        private static void CreateWarning(ObjectId userId, ObjectId incidentId, ObjectId adminId, string reason)
        {
            var id = ObjectId.GenerateNewId();
            MongoDatabase.AccountRestrictions.InsertOne(new BsonDocument
            {
                { "_id", id }, { "userId", userId }, { "type", "WARNING" }, { "reason", reason }, { "status", "ACTIVE" },
                { "createdBy", adminId }, { "createdAt", DateTime.UtcNow }, { "source", "CONFIRMED_NO_SHOW" }, { "sourceIncidentId", incidentId }
            });
            WriteAudit(adminId, "ACCOUNT_WARNING_ISSUED", "USER", userId, new BsonDocument { { "incidentId", incidentId }, { "reason", reason } });
        }

        private static bool HasCurrentBlockingRestriction(ObjectId userId)
        {
            var now = DateTime.UtcNow;
            return MongoDatabase.AccountRestrictions.Find(Builders<BsonDocument>.Filter.Eq("userId", userId) & Builders<BsonDocument>.Filter.Eq("status", "ACTIVE")).ToList()
                .Any(x => BlockingRestrictionTypes.Contains(ReadString(x, "type")) && IsRestrictionCurrent(x, now));
        }

        private static bool IsRestrictionCurrent(BsonDocument record, DateTime now)
        {
            var expires = ReadDate(record, "expiresAt", DateTime.MaxValue);
            return expires > now;
        }

        private static void RestoreUserStatus(ObjectId userId)
        {
            var user = MongoDatabase.Users.Find(x => x.Id == userId).FirstOrDefault();
            if (user == null || HasCurrentBlockingRestriction(userId)) return;
            var status = MongoDatabase.AccountRestrictions.Find(Builders<BsonDocument>.Filter.Eq("userId", userId) & Builders<BsonDocument>.Filter.In("type", BlockingRestrictionTypes))
                .SortByDescending(x => x["createdAt"]).ToList().Select(x => ReadString(x, "previousStatus")).FirstOrDefault(x => !String.IsNullOrWhiteSpace(x));
            if (String.IsNullOrWhiteSpace(status)) status = String.Equals(user.VerificationStatus, "APPROVED", StringComparison.OrdinalIgnoreCase) ? "ACTIVE" : "PENDING";
            MongoDatabase.Users.UpdateOne(x => x.Id == userId && (x.Status == "SUSPENDED" || x.Status == "BANNED"),
                Builders<UserDocument>.Update.Set(x => x.Status, status).Set(x => x.UpdatedAt, DateTime.UtcNow));
        }

        private static void WriteAudit(ObjectId actorId, string action, string entityType, ObjectId? entityId, BsonDocument details)
        {
            MongoDatabase.AuditLogs.InsertOne(new AuditLogDocument
            {
                Id = ObjectId.GenerateNewId(), UserId = actorId, Action = action, EntityType = entityType,
                EntityId = entityId, Details = details ?? new BsonDocument(), CreatedAt = DateTime.UtcNow
            });
        }

        private static DisputeViewModel ToDispute(BsonDocument doc)
        {
            var reporterId = ReadObjectId(doc, "reporterId");
            var respondentId = ReadObjectId(doc, "respondentId");
            var bookingId = ReadObjectId(doc, "bookingId");
            var booking = bookingId.HasValue ? MongoDatabase.Bookings.Find(Builders<BsonDocument>.Filter.Eq("_id", bookingId.Value)).FirstOrDefault() : null;
            var reporter = reporterId.HasValue ? MongoDatabase.Users.Find(x => x.Id == reporterId.Value).FirstOrDefault() : null;
            var respondent = respondentId.HasValue ? MongoDatabase.Users.Find(x => x.Id == respondentId.Value).FirstOrDefault() : null;
            var disputeObjectId = ReadObjectId(doc, "_id");
            return new DisputeViewModel
            {
                Id = disputeObjectId.HasValue ? disputeObjectId.Value.ToString() : "", DisputeId = disputeObjectId.HasValue ? disputeObjectId.Value.ToString() : "",
                BookingId = ReadString(doc, "requestId", booking == null ? "" : ReadString(booking, "requestId", booking.GetValue("_id", BsonNull.Value).ToString())),
                ItemName = ReadString(doc, "itemName", booking == null ? "Rental item" : ReadString(booking, "itemName", "Rental item")),
                ReporterName = reporter == null ? "Student" : FullName(reporter), ReporterId = reporter == null ? "" : reporter.StudentIdNumber,
                ReporterRole = ParticipantRole(booking, reporterId),
                RespondentName = respondent == null ? "Student" : FullName(respondent), RespondentId = respondent == null ? "" : respondent.StudentIdNumber,
                RespondentRole = ParticipantRole(booking, respondentId),
                Category = ReadString(doc, "category"), IncidentStatement = ReadString(doc, "incidentStatement", ReadString(doc, "details")),
                Priority = ReadString(doc, "priority", "MEDIUM").Replace("URGENT", "Urgent").Replace("HIGH", "High").Replace("MEDIUM", "Medium").Replace("LOW", "Low"),
                Status = ReadString(doc, "status", "OPEN").Replace("OPEN", "Open").Replace("UNDER_REVIEW", "Under Review").Replace("INFO_REQUESTED", "Info Requested").Replace("RESOLVED", "Resolved"),
                DateReported = ReadDate(doc, "createdAt", DateTime.UtcNow).ToLocalTime().ToString("MMM d, yyyy"), AdminNotes = ReadString(doc, "adminNotes"),
                EvidenceUrls = ReadStringArray(doc, "evidenceUrls")
            };
        }

        private static NoShowIncidentViewModel ToNoShow(BsonDocument doc)
        {
            var reporter = ReadObjectId(doc, "reportedBy"); var subject = ReadObjectId(doc, "subjectUserId");
            var reporterUser = reporter.HasValue ? MongoDatabase.Users.Find(x => x.Id == reporter.Value).FirstOrDefault() : null;
            var subjectUser = subject.HasValue ? MongoDatabase.Users.Find(x => x.Id == subject.Value).FirstOrDefault() : null;
            return new NoShowIncidentViewModel
            {
                Id = doc.GetValue("_id", ObjectId.Empty).ToString(), BookingId = ReadString(doc, "bookingId"), RequestId = ReadString(doc, "requestId"),
                ItemName = ReadString(doc, "itemName", "Rental item"), ReporterName = reporterUser == null ? "Student" : FullName(reporterUser),
                ReporterRole = ReadString(doc, "reporterRole"), SubjectName = subjectUser == null ? "Student" : FullName(subjectUser),
                Status = ReadString(doc, "status", "PENDING"), Reason = ReadString(doc, "reason"), AdminNotes = ReadString(doc, "adminNotes"),
                ReportedAt = ReadDate(doc, "reportedAt", DateTime.UtcNow).ToLocalTime(), ReviewedAt = ReadNullableDate(doc, "reviewedAt")
            };
        }

        private static UserAccountRowViewModel ToUserRow(UserDocument user)
        {
            return new UserAccountRowViewModel { Id = user.Id.ToString(), Name = FullName(user), Email = user.SchoolEmail ?? "", StudentNumber = user.StudentIdNumber ?? "", Role = user.Role ?? "STUDENT", Status = user.Status ?? "UNKNOWN", VerificationStatus = user.VerificationStatus ?? "UNKNOWN", CreatedAt = user.CreatedAt };
        }

        private static AccountRestrictionRowViewModel ToRestriction(BsonDocument doc)
        {
            var userId = ReadObjectId(doc, "userId"); var user = userId.HasValue ? MongoDatabase.Users.Find(x => x.Id == userId.Value).FirstOrDefault() : null;
            var createdBy = ReadObjectId(doc, "createdBy"); var admin = createdBy.HasValue ? MongoDatabase.Users.Find(x => x.Id == createdBy.Value).FirstOrDefault() : null;
            return new AccountRestrictionRowViewModel
            {
                Id = doc.GetValue("_id", ObjectId.Empty).ToString(), UserId = userId.HasValue ? userId.Value.ToString() : "",
                UserName = user == null ? "Student" : FullName(user), Type = ReadString(doc, "type"), Status = ReadString(doc, "status"),
                Reason = ReadString(doc, "reason"), CreatedBy = admin == null ? "Administrator" : FullName(admin),
                CreatedAt = ReadDate(doc, "createdAt", DateTime.UtcNow).ToLocalTime(), ExpiresAt = ReadNullableDate(doc, "expiresAt")?.ToLocalTime()
            };
        }

        private static RatingReviewRowViewModel ToReview(BsonDocument doc)
        {
            var reviewerId = ReadObjectId(doc, "reviewerId"); var reviewedId = ReadObjectId(doc, "reviewedUserId");
            var reviewer = reviewerId.HasValue ? MongoDatabase.Users.Find(x => x.Id == reviewerId.Value).FirstOrDefault() : null;
            var reviewed = reviewedId.HasValue ? MongoDatabase.Users.Find(x => x.Id == reviewedId.Value).FirstOrDefault() : null;
            var itemId = ReadObjectId(doc, "itemId"); var item = itemId.HasValue ? MongoDatabase.Items.Find(x => x.Id == itemId.Value).FirstOrDefault() : null;
            var id = doc.GetValue("_id", ObjectId.Empty).ToString();
            return new RatingReviewRowViewModel
            {
                Id = id, BookingId = ReadString(doc, "bookingId"), ItemName = item == null ? "Rental item" : item.Name,
                ReviewerName = reviewer == null ? "Student" : FullName(reviewer), ReviewedUserName = reviewed == null ? "Student" : FullName(reviewed),
                ItemRating = ReadNullableInt(doc, "itemRating"), OwnerRating = ReadNullableInt(doc, "ownerRating") ?? ReadNullableInt(doc, "rating"),
                Review = ReadString(doc, "review"), Status = Bool(doc, "isHidden") ? "HIDDEN" : (Bool(doc, "isReported") ? "REPORTED" : "VISIBLE"),
                ReportReason = ReadString(doc, "reportReason"), CreatedAt = ReadDate(doc, "createdAt", DateTime.UtcNow).ToLocalTime()
            };
        }

        private static AuditLogRowViewModel ToAuditRow(AuditLogDocument doc)
        {
            var actor = MongoDatabase.Users.Find(x => x.Id == doc.UserId).FirstOrDefault();
            return new AuditLogRowViewModel { Id = doc.Id.ToString(), ActorName = actor == null ? "System" : FullName(actor), Action = doc.Action ?? "", EntityType = doc.EntityType ?? "", EntityId = doc.EntityId.HasValue ? doc.EntityId.Value.ToString() : "", Summary = doc.Details == null ? "" : doc.Details.ToString(), CreatedAt = doc.CreatedAt.ToLocalTime() };
        }

        private static string FullName(UserDocument user)
        {
            var name = ((user.FirstName ?? "") + " " + (user.LastName ?? "")).Trim();
            return String.IsNullOrWhiteSpace(name) ? (user.Username ?? user.SchoolEmail ?? "Student") : name;
        }

        private static string ParticipantRole(BsonDocument booking, ObjectId? userId)
        {
            var ownerId = ReadObjectId(booking, "ownerId");
            if (!ownerId.HasValue || !userId.HasValue) return "Student";
            return ownerId.Value == userId.Value ? "Lender" : "Borrower";
        }

        private static string ReadString(BsonDocument doc, string key, string fallback = "")
        {
            if (doc == null || !doc.Contains(key) || doc[key].IsBsonNull) return fallback;
            return doc[key].ToString();
        }

        private static ObjectId? ReadObjectId(BsonDocument doc, string key)
        {
            if (doc == null || !doc.Contains(key) || doc[key].IsBsonNull) return null;
            if (doc[key].IsObjectId) return doc[key].AsObjectId;
            ObjectId id; return ObjectId.TryParse(doc[key].ToString(), out id) ? (ObjectId?)id : null;
        }

        private static DateTime ReadDate(BsonDocument doc, string key, DateTime fallback)
        {
            if (doc == null || !doc.Contains(key) || !doc[key].IsValidDateTime) return fallback;
            return doc[key].ToUniversalTime();
        }

        private static DateTime? ReadNullableDate(BsonDocument doc, string key)
        {
            if (doc == null || !doc.Contains(key) || !doc[key].IsValidDateTime) return null;
            return doc[key].ToUniversalTime();
        }

        private static int? ReadNullableInt(BsonDocument doc, string key)
        {
            if (doc == null || !doc.Contains(key) || !doc[key].IsNumeric) return null;
            return doc[key].ToInt32();
        }

        private static bool Bool(BsonDocument doc, string key)
        {
            return doc != null && doc.Contains(key) && doc[key].IsBoolean && doc[key].AsBoolean;
        }

        private static IList<string> ReadStringArray(BsonDocument doc, string key)
        {
            if (doc == null || !doc.Contains(key) || !doc[key].IsBsonArray) return new List<string>();
            return doc[key].AsBsonArray.Where(x => !x.IsBsonNull).Select(x => x.ToString()).ToList();
        }
    }
}
