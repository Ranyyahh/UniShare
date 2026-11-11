using System;
using System.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;
using UniShare.Models;

namespace UniShare.Services
{
    public static class MongoDatabase
    {
        private static readonly Lazy<IMongoDatabase> Database = new Lazy<IMongoDatabase>(() =>
        {
            var uri = Environment.GetEnvironmentVariable("MONGODB_URI") ?? ConfigurationManager.AppSettings["MongoDbUri"] ?? "mongodb://127.0.0.1:27017";
            var client = new MongoClient(uri);
            var dbName = Environment.GetEnvironmentVariable("MONGODB_DATABASE") ?? ConfigurationManager.AppSettings["MongoDbName"] ?? "UniShare";
            return client.GetDatabase(dbName);
        });

        public static IMongoCollection<UserDocument> Users { get { return Database.Value.GetCollection<UserDocument>("users"); } }
        public static IMongoCollection<VerificationApplicationDocument> Verifications { get { return Database.Value.GetCollection<VerificationApplicationDocument>("verificationApplications"); } }
        public static IMongoCollection<AuditLogDocument> AuditLogs { get { return Database.Value.GetCollection<AuditLogDocument>("auditLogs"); } }
        public static IMongoCollection<ItemDocument> Items { get { return Database.Value.GetCollection<ItemDocument>("items"); } }
        public static IMongoCollection<BsonDocument> Bookings { get { return Database.Value.GetCollection<BsonDocument>("bookings"); } }
        public static IMongoCollection<BsonDocument> BookingLocks { get { return Database.Value.GetCollection<BsonDocument>("bookingLocks"); } }
        public static IMongoCollection<BsonDocument> Rentals { get { return Database.Value.GetCollection<BsonDocument>("rentals"); } }
        public static IMongoCollection<BsonDocument> Ratings { get { return Database.Value.GetCollection<BsonDocument>("ratings"); } }
    }
}
