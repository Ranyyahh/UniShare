# MongoDB setup

The app connects to the `UniShare` database through the official MongoDB .NET driver. It defaults to local MongoDB at `mongodb://localhost:27017`. For Atlas or another server, set `MONGODB_URI` in the environment used by IIS Express or IIS. Keep the URI out of source control.

PowerShell for the current terminal:

```powershell
$env:MONGODB_URI = 'mongodb+srv://<user>:<password>@<cluster>/UniShare?retryWrites=true&w=majority'
```

Restart Visual Studio/IIS Express after setting a user or machine environment variable. The application uses `MongoDbName` from `Web.config` (`UniShare` by default). It writes users and verification applications to the `users` and `verificationApplications` collections created by the supplied seed script.

New accounts store a PBKDF2 password hash in `users.passwordHash`; no plain text password is stored. The seed script's demonstration accounts do not include a password hash and cannot sign in until one is set. Do not use a real password in a seed script.

The Student Verification page saves the school ID and current registration form under `App_Data/Verification`, which is outside the public web root. Keep this folder on persistent storage and restrict server access. Verification application documents store only generated filenames. Admin document links require an authenticated `ADMIN` role.

To provision the first administrator, have a trusted database operator register that person's account through the app, then change that account's role directly in MongoDB, for example `db.users.updateOne({ schoolEmail: "admin@unishare.qcu.edu.ph" }, { $set: { role: "ADMIN" } })`. Sign out and back in so the new role is placed in the auth cookie. Public registration only permits `OWNER` and `BORROWER`; admin privileges cannot be self-assigned. The seed script's sample `admin` account has no password hash, so it also cannot sign in until an operator provisions its credentials.
