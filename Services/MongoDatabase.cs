using System;
using System.Configuration;
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
            var dbName = ConfigurationManager.AppSettings["MongoDbName"] ?? "UniShare";
            return client.GetDatabase(dbName);
        });

        public static IMongoCollection<UserDocument> Users { get { return Database.Value.GetCollection<UserDocument>("users"); } }
        public static IMongoCollection<VerificationApplicationDocument> Verifications { get { return Database.Value.GetCollection<VerificationApplicationDocument>("verificationApplications"); } }
        public static IMongoCollection<AuditLogDocument> AuditLogs { get { return Database.Value.GetCollection<AuditLogDocument>("auditLogs"); } }
    }
}
