# MongoDB setup

The app connects to the `UniShare` database through the official MongoDB .NET driver and reads marketplace records from `items`. For a local MongoDB Community server, `Web.config` uses `mongodb://127.0.0.1:27017`. For Atlas or another server, set `MONGODB_URI` in the environment used by IIS Express or IIS; it takes priority over the local setting. Keep credentials out of source control.

PowerShell for the current terminal:

```powershell
$env:MONGODB_URI = 'mongodb+srv://<user>:<password>@<cluster>/UniShare?retryWrites=true&w=majority'
```

Restart Visual Studio/IIS Express after setting a user or machine environment variable. The application uses `MongoDbName` from `Web.config` (`UniShare` by default). It writes users and verification applications to the `users` and `verificationApplications` collections created by the supplied seed script.

New accounts store a PBKDF2 password hash in `users.passwordHash`; no plain text password is stored. The `database` seed uses fixed demo-only credentials so its accounts can sign in locally: `owner@qcu.edu.ph` / `OwnerDemo!2026`, `borrower@qcu.edu.ph` / `BorrowerDemo!2026`, `student@qcu.edu.ph` / `PendingDemo!2026`, and `admin@qcu.edu.ph` / `AdminDemo!2026`. Do not use these demo credentials outside a disposable development database or put real passwords in a seed script.

The marketplace reads approved, available documents from `items`, and listing changes are scoped to the signed-in owner's ObjectId. Item photos are saved under `Uploads`; item documents store only generated photo filenames. The Student Verification page saves the school ID and current registration form under `App_Data/Verification`, which is outside the public web root. Keep this folder on persistent storage and restrict server access. Verification application documents store only generated filenames. Admin document links require an authenticated `ADMIN` role.

Rental requests and their status transitions use `bookings`. Payment choices and owner receipt confirmations are stored in `payments`; late-fee estimates and owner/admin finalization are stored separately in `lateFees`. Confirmed handovers, return requests, and condition evidence are recorded in `handovers`, `returns`, and `conditionRecords`. Booking locks expire automatically through a TTL index. The seed script initializes these collections and indexes.

To provision the first administrator, have a trusted database operator register that person's account through the app, then change that account's role directly in MongoDB, for example `db.users.updateOne({ schoolEmail: "admin@unishare.qcu.edu.ph" }, { $set: { role: "ADMIN" } })`. Sign out and back in so the new role is placed in the auth cookie. Public registration creates `STUDENT` accounts; admin privileges cannot be self-assigned. The seeded admin account is only for local demo use.
