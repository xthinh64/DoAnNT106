# MySQL authentication

`new MySqlAuthService(connectionString, allowLegacyMigration: false)` implements `OnlineChessGame.Network.IAuthService`. Supported actions are exact lowercase `signin` and `signup`. Connection strings belong in server environment configuration, never source control. The service does not log credentials or database exceptions.

- Usernames are trimmed, 1–50 UTF-16 code units (raw input capped at 256); display names are trimmed, at most 100; omitted/blank display names use the username.
- Passwords are **never trimmed**: signup accepts 8–128 code units, signin 1–128 to accommodate older accounts.
- Hash format: `pbkdf2-sha256$v1$600000$<base64 16-byte salt>$<base64 32-byte key>`. SHA-256, random salt, fixed-time key comparison. Unknown users perform dummy PBKDF2 verification.
- Signup inserts the user and both default-Elo stats in one transaction. Unique constraint error 1062 becomes `username_taken`; other database errors become `service_unavailable`. Cancellation propagates.
- Signin reads persisted identity and CHESS/CARO Elo. Missing legacy stats fall back to 1200 without modifying the database.
- Wrong passwords and missing users both return `invalid_credentials`. Correct plaintext legacy passwords return `reset_required`, never success, by default. Password reset itself is outside this component.
- Explicit one-time migration is available via the second constructor argument. A host may map an explicitly configured `AUTH_ALLOW_LEGACY_MIGRATION=true` environment variable to that argument; this library does **not** read that variable implicitly. Migration verifies the exact plaintext, then replaces it with a hash using a conditional update. Disable the flag after a controlled migration window. Malformed/unsupported `pbkdf2-` values never fall back to plaintext.
- Rate limiting belongs at the parent network boundary (especially IP/global concurrency limits); this service does not implement an account lockout that attackers could use for denial of service.

Response codes: `ok`, `invalid_request`, `invalid_credentials`, `reset_required`, `username_taken`, `service_unavailable`.

## Tests

`dotnet test Tests/AuthTests/AuthTests.csproj`

Unit tests run normally; integration tests explicitly skip unless `AUTH_TEST_MYSQL` is supplied. Use only a **disposable loopback** MySQL/MariaDB instance, never an existing/shared database. The supplied test account must be able to create/drop databases and create triggers. Each test creates a unique `auth_test_<guid>` database and drops it in async disposal. They do not use an ambient connection string or existing application data.

Set `AUTH_TEST_MYSQL` in your shell to the disposable server connection string, then run the same command. Integration coverage includes persisted salted hashes, username/password whitespace behavior, real profile/Elo reads, concurrent unique-key races, rollback when stats insertion fails, generic signin failures, reset-required defaults, and opt-in legacy migration.

Verified locally against a fresh scratch MariaDB 11.4.5 ZIP instance bound only to 127.0.0.1 on a dedicated port. No live application database was accessed. SQLite/mocks are not substituted for these tests.
