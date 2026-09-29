# BankAccountManagementSystem — Technical Viva & Interview Preparation Notes

> **Quick Revision Guide:** Core Architecture, Data Flows, Design Decisions & 30 Technical Viva Questions and Answers for the Bank Account Management System (BAMS).  
> **Backend:** .NET 8 / ASP.NET Core Web API / EF Core 8 / SQL Server / ASP.NET Core Identity / JWT / Serilog  
> **Frontend:** React 18 / Vite / Axios / React Router v6 / Context API  

---

## 1. Quick Architectural Snapshot

```
[React 18 SPA]  <---(JSON over HTTPS)--->  [ASP.NET Core 8 Web API]  <---(EF Core 8 / TDS)--->  [SQL Server]
  - Vite 5                                   - Kestrel Web Server                                 - BankAccountDb
  - Axios + Interceptors                     - 3 Custom Middlewares                               - Identity Tables
  - AuthContext                              - Scoped Domain Services                             - Accounts (RowVersion)
  - Protected Routes                         - JWT Bearer Authentication                          - Transactions (Ledger)
```

### Architectural Highlights & Decision Summary

1. **Why Controller-Service-DbContext (Layered Architecture)?**  
   Keeps controllers thin (HTTP translation only), encapsulates domain validation in scoped services, and isolates data access inside EF Core.
2. **Why Scoped Lifetime for Services & DbContext?**  
   `DbContext` is not thread-safe and must be instantiated once per HTTP request. Registering services as `Scoped` ensures they share the request-bound `DbContext` instance and release resources when the request completes.
3. **Why `decimal(18,2)` instead of `double` or `float`?**  
   Floating-point types use binary base-2 representations, introducing inexact fractional rounding errors. `decimal` uses base-10 with 128 bits, guaranteeing precise financial arithmetic.
4. **Why `DeleteBehavior.Restrict` instead of `Cascade`?**  
   Financial ledgers require an immutable audit trail. We implement soft deletion (`IsActive = false`). If someone attempts to hard-delete an account or user at the database level, SQL Server rejects it, protecting historical transactions.
5. **Why Optimistic Concurrency (`RowVersion`) instead of Pessimistic Locking?**  
   Pessimistic locking holds database row locks across HTTP requests, destroying API scalability. Optimistic concurrency allows non-blocking reads and checks a SQL Server `rowversion` column during writes. If a conflict occurs, it returns HTTP `409 Conflict`.

---

## 2. Key Transaction Flows & Sequence Walkthroughs

### Flow A: Authentication & JWT Lifecycle
```
1. Client POST /api/v1/auth/login { username, password }
2. RateLimiter checks client IP (max 10 requests / min).
3. AuthService calls UserManager.FindByNameAsync() and CheckPasswordSignInAsync().
4. AuthService creates claims (NameIdentifier, Email, Role, Jti).
5. Token generated using HMAC-SHA256 with JwtSettings.SecretKey.
6. Returns 200 OK { token, user: { id, employeeCode, roles } }.
7. React client stores token in localStorage ('bams_token') and AuthContext.
8. Axios request interceptor attaches 'Authorization: Bearer <token>' to all subsequent calls.
```

### Flow B: Cash / Cheque Deposit
```
1. Client POST /api/v1/transactions/deposit { accountId, amount, mode }
2. Controller extracts CurrentUserId and UserRole from User.Claims.
3. TransactionService fetches Account:
   - Validates existence (if missing -> KeyNotFoundException -> 404).
   - Validates ownership (if not owner and not Admin -> UnauthorizedAccessException -> 401).
   - Validates account.IsActive (if false -> AccountInactiveException -> 422).
   - Checks Savings account rule: If account.AccountType == Savings && mode != Cash -> InvalidAccountTypeException -> 422.
4. account.Balance += amount;
5. Inserts Transaction entity (TransactionType=Deposit, Status=Completed, unique ReferenceNumber).
6. await _context.SaveChangesAsync() -> SQL Server updates balance and increments RowVersion.
7. Returns 200 OK with TransactionResponse.
```

### Flow C: Inter-Account Transfer (Checking to Checking)
```
1. Client POST /api/v1/transactions/transfer { sourceAccountId, destinationAccountId, amount, mode }
2. TransactionService checks source != destination (else InvalidTransactionException -> 422).
3. Fetches sourceAccount and destinationAccount.
4. Validates:
   - Both accounts must be Active.
   - Caller must own sourceAccount (or be Admin).
   - Both accounts must be Checking (Savings does not allow transfers).
   - sourceAccount.Balance >= amount (else InsufficientBalanceException -> 422).
5. Begins EF Core transaction: using var dbTx = await _context.Database.BeginTransactionAsync().
6. sourceAccount.Balance -= amount;
7. destinationAccount.Balance += amount;
8. Creates Outgoing Transaction record (Debit, RelatedAccountId = destination).
9. Creates Incoming Transaction record (Credit, RelatedAccountId = source).
10. await _context.SaveChangesAsync()
11. await dbTx.CommitAsync()
12. If any step fails or concurrency conflict arises, dbTx.RollbackAsync() executes.
```

### Flow D: Soft Account Deactivation
```
1. Client DELETE /api/v1/accounts/{id}
2. AccountService fetches account and verifies caller ownership or Admin role.
3. account.IsActive = false.
4. await _context.SaveChangesAsync().
5. Database record remains intact. Foreign keys on historical transactions are preserved.
6. Subsequent deposits, withdrawals, or transfers on this account throw AccountInactiveException (HTTP 422).
```

### Flow E: System Backup & Rehydration (Admin Only)
```
[SAVE / EXPORT]
1. Admin calls GET /api/v1/system/save
2. [Authorize(Roles = "Admin")] validates role claim.
3. SystemBackupService queries Accounts and Transactions using .AsNoTracking().
4. Packages entities into SystemBackupDto (includes export timestamp, counts, account/transaction lists).
5. Returns JSON file download.

[RESTORE / REHYDRATE]
1. Admin calls POST /api/v1/system/restore with JSON payload.
2. Begins database transaction: BeginTransactionAsync().
3. Deletes existing transactions and accounts.
4. Re-inserts accounts and transactions preserving primary and foreign keys.
5. Saves changes and commits transaction atomically.
```

---

## 3. 30 Technical Viva Questions and Detailed Answers

#### Q1: What version of .NET is used in this project, and what are its key advantages?
**Answer:** The project uses .NET 8.0 LTS. Key advantages include C# 12 language features, the minimal hosting API (`WebApplicationBuilder`), performance optimizations in Kestrel and CoreCLR, native support for fixed-window rate limiting, and Entity Framework Core 8 optimizations.

#### Q2: Explain the role and exact order of middlewares in your application.
**Answer:** In `Program.cs`, the order is:
1. `CorrelationIdMiddleware`: Generates or reuses `X-Correlation-ID`.
2. `ExceptionMiddleware`: Central `try-catch` wrapper for global error handling.
3. `RequestLoggingMiddleware`: Measures and logs execution time in ms.
4. `UseRateLimiter`: Enforces rate limit policies.
5. `UseAuthentication`: Parses JWT and populates `HttpContext.User`.
6. `UseAuthorization`: Evaluates role and policy claims.
7. `MapControllers` / `MapHealthChecks`: Directs requests to actions.
*Order matters because a middleware can only observe or handle actions executed by components placed downstream from it.*

#### Q3: How is global exception handling implemented without try-catch in every controller?
**Answer:** `ExceptionMiddleware` intercepts every incoming request using `await _next(context)`. If any exception is thrown downstream, the catch block captures it, retrieves the correlation ID from `HttpContext.Items["CorrelationId"]`, logs the exception via Serilog, and maps the exception type to an appropriate HTTP status code via a C# switch pattern before returning a sanitized JSON response.

#### Q4: How does your application handle race conditions on account balances?
**Answer:** We use optimistic concurrency. The `Account` entity contains a `byte[] RowVersion` property configured with `.IsRowVersion()` in EF Core. SQL Server maps this to a `rowversion` column, incrementing it on every update. EF Core includes the initial version in its SQL `UPDATE` statement. If another request updated the balance in the interim, zero rows match, causing EF Core to throw `DbUpdateConcurrencyException`, which our middleware maps to HTTP `409 Conflict`.

#### Q5: What is the difference between `AsNoTracking()` and standard EF Core queries?
**Answer:** Standard EF Core queries track retrieved entities in the `ChangeTracker`, monitoring property modifications for subsequent `SaveChangesAsync()` calls. `AsNoTracking()` disables this tracking snapshot, significantly reducing memory allocation and improving query execution speed. We use it on all read-only queries (e.g. transaction history and backup export).

#### Q6: How is JWT authentication configured in `Program.cs`?
**Answer:** We call `builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(...)`. We supply `TokenValidationParameters` to validate the `Issuer`, `Audience`, `Lifetime`, and `IssuerSigningKey` (symmetric key from `JwtSettings`). We also set `ClockSkew = TimeSpan.Zero` to enforce exact token expiration without the default 5-minute leeway.

#### Q7: How are user roles represented in the JWT token?
**Answer:** When `AuthService.GenerateJwtToken` executes, it calls `UserManager.GetRolesAsync(user)`. For every role, it adds a `new Claim(ClaimTypes.Role, role)` to the token claims list. When an incoming request arrives, the JWT middleware automatically populates `HttpContext.User` with these role claims, allowing `[Authorize(Roles = "Admin")]` attributes to evaluate permissions.

#### Q8: How does the application enforce account ownership?
**Answer:** In `AccountsController` and `TransactionsController`, the controller extracts the current user ID using `User.FindFirstValue(ClaimTypes.NameIdentifier)` and checks whether `User.IsInRole("Admin")`. Services compare `account.UserId == currentUserId`. If the user is neither the owner nor an Admin, the service throws an `UnauthorizedAccessException`, translated to HTTP `401/403`.

#### Q9: How is brute-force protection implemented on the login endpoint?
**Answer:** In `Program.cs`, we configure `AddRateLimiter` with a fixed-window limiter policy named `"login"`. It partitions requests by client remote IP address, allowing a maximum of 10 permits per 1-minute window with a queue limit of 0. The `AuthController.Login` action is decorated with `[EnableRateLimiting("login")]`. Excess requests are rejected with HTTP `429 Too Many Requests`.

#### Q10: What does the `/health` endpoint do and how is it configured?
**Answer:** Configured via `builder.Services.AddHealthChecks().AddDbContextCheck<ApplicationDbContext>("database")` and mapped using `app.MapHealthChecks("/health")`. It issues a probe against SQL Server. If the database connection succeeds, it returns `200 OK` (`Healthy`); if the database is unreachable, it returns `503 Service Unavailable` (`Unhealthy`).

#### Q11: Why is `AddIdentity` used instead of `AddDefaultIdentity`?
**Answer:** `AddDefaultIdentity` is a stripped-down configuration intended for Razor Pages UI and does not register role management services. `AddIdentity<ApplicationUser, IdentityRole>()` registers `RoleManager<IdentityRole>`, role validators, and role claims handling required for enterprise API role-based authorization.

#### Q12: Explain the database relationship between `Account` and `Transaction`.
**Answer:** There are two foreign keys:
1. `AccountId` (Required): Primary FK representing the owning account (`Account (1) <---> (*) Transaction`). Configured with `DeleteBehavior.Restrict`.
2. `RelatedAccountId` (Nullable): Optional FK used during transfers to link the counterparty account (`Account (1) <---> (0..1) Transaction`). Also configured with `DeleteBehavior.Restrict`.

#### Q13: What happens during an inter-account transfer if the destination account update fails?
**Answer:** The entire operation is enclosed in an EF Core transaction: `await _context.Database.BeginTransactionAsync()`. If an exception occurs during the credit leg or transaction row insertion, the `catch` block calls `await dbTransaction.RollbackAsync()`, restoring the source account balance and leaving no orphan transaction records.

#### Q14: How does the system restrict Savings accounts to cash transactions?
**Answer:** In `TransactionService.cs`:
- On Deposit and Withdrawal: `if (account.AccountType == AccountType.Savings && request.Mode != TransactionMode.Cash)` $\rightarrow$ throws `InvalidAccountTypeException`.
- On Transfer: `if (source.AccountType == AccountType.Savings || destination.AccountType == AccountType.Savings)` $\rightarrow$ throws `InvalidTransactionException`.
Both exceptions inherit from `InvalidOperationException` and return HTTP `422 Unprocessable Entity`.

#### Q15: Why does `ExceptionMiddleware` return HTTP 422 instead of HTTP 400 for insufficient balance?
**Answer:** HTTP 400 (Bad Request) denotes syntactic errors (malformed JSON, invalid data types, missing fields). An insufficient balance request is syntactically valid JSON and passes data annotation checks, but violates semantic business domain rules. RFC 4918 specifies HTTP `422 Unprocessable Entity` for semantic domain violations.

#### Q16: What is the purpose of `CorrelationIdMiddleware`?
**Answer:** It implements distributed tracing. For every incoming request, it checks for an existing `X-Correlation-ID` header; if absent, it generates a new GUID. It attaches this ID to `HttpContext.Items`, logs it in Serilog, includes it in `RequestLoggingMiddleware`, and appends it to response headers and error JSON payloads.

#### Q17: Where are application logs stored and how are they managed?
**Answer:** Logs are configured via Serilog in `Program.cs`. In development, logs write to the Console. In all environments, logs write to rolling daily files under `Logs/bams-.log` with a 30-day retention policy (`retainedFileCountLimit: 30`).

#### Q18: What is the difference between `IEnumerable<T>`, `IQueryable<T>`, and `List<T>` in EF Core?
**Answer:** `IQueryable<T>` represents a LINQ expression tree that has not yet been executed; filters (`Where`), projections (`Select`), and pagination (`Skip`, `Take`) appended to `IQueryable` translate into SQL queries executed on the database server. `List<T>` and `IEnumerable<T>` represent in-memory collections; calling `.ToList()` or `.ToListAsync()` executes the SQL and loads the records into application memory.

#### Q19: How is soft deletion implemented in the system?
**Answer:** The `Account` entity contains a boolean property `IsActive`. Calling `DELETE /api/v1/accounts/{id}` sets `IsActive = false` rather than issuing a SQL `DELETE`. The database record remains intact to satisfy foreign keys. All financial transaction operations verify `account.IsActive == true`; if false, an `AccountInactiveException` (HTTP 422) is thrown.

#### Q20: What is the DTO pattern and why is it used?
**Answer:** Data Transfer Objects (DTOs) are plain C# objects used to pass data between the client and API controllers. They prevent over-posting vulnerabilities (clients sending values for internal fields like `Balance` or `RowVersion`), hide sensitive internal database fields (like `PasswordHash`), and decouple external API contracts from internal database schema changes.

#### Q21: How are passwords secured in ASP.NET Core Identity?
**Answer:** Passwords are never stored in plaintext. ASP.NET Core Identity hashes passwords using PBKDF2 (Password-Based Key Derivation Function 2) with HMAC-SHA256, generating a cryptographically secure 128-bit salt and applying thousands of derivation iterations.

#### Q22: How does the React client preserve user authentication across page refreshes?
**Answer:** Upon successful login, the React frontend stores the JWT token and serialized user object in browser `localStorage` (`bams_token` and `bams_user`). When `AuthContext` initializes, it reads these values from `localStorage`. The Axios request interceptor pulls `bams_token` from storage and attaches it to the `Authorization` header on every request.

#### Q23: How does the React application handle expired tokens?
**Answer:** An Axios response interceptor in `src/api.js` listens for HTTP `401 Unauthorized` responses. When a 401 is received, the interceptor clears `bams_token` and `bams_user` from `localStorage` and triggers a redirect to `/login`.

#### Q24: What is the role of `[ApiController]` on controller classes?
**Answer:** `[ApiController]` enables several automatic API-specific behaviors:
1. Automatic HTTP 400 responses for invalid `ModelState`.
2. Automatic source binding inference (e.g. `[FromBody]` for complex types, `[FromRoute]` for route parameters).
3. Problem Details error status formatting.

#### Q25: Why is `IOptions<JwtSettings>` used instead of reading `IConfiguration` directly in services?
**Answer:** Direct use of `IConfiguration["JwtSettings:SecretKey"]` introduces magic strings, lacks compile-time validation, and couples business services to configuration file formats. The Options pattern binds configuration sections to strongly typed C# classes (`JwtSettings`), supporting dependency injection and unit testing.

#### Q26: Explain the difference between Authentication and Authorization.
**Answer:** Authentication is the process of identifying who the user is (verifying credentials and validating the signed JWT). Authorization is determining whether the authenticated user has permission to perform an action (evaluating roles such as `Admin` or ownership of an account ID).

#### Q27: How is pagination implemented in transaction history queries?
**Answer:** The `TransactionHistoryQuery` DTO accepts `Page` (default 1) and `PageSize` (default 10). In `TransactionService.cs`, LINQ executes `_context.Transactions.Where(...).OrderByDescending(t => t.TransactionDate).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync()`. SQL Server translates this into `OFFSET ... ROWS FETCH NEXT ... ROWS ONLY`.

#### Q28: What is the purpose of `DbSeeder.cs`?
**Answer:** `DbSeeder.SeedAsync` runs during application startup within a dedicated service scope. It verifies whether the `Admin` and `User` roles exist in `AspNetRoles`, creates them if missing, and provisions a default administrative user account (`admin@bank.com`) with the `Admin` role if one does not already exist.

#### Q29: What indexes exist in the database and why were they added?
**Answer:**
1. `IX_Accounts_AccountNumber` (Unique): Fast lookup by account number and enforcement of uniqueness.
2. `IX_Transactions_ReferenceNumber` (Unique): Fast transaction retrieval and reference uniqueness.
3. `IX_Transactions_TransactionDate`: Speeds up date-range sorting and queries.
4. `IX_Transactions_AccountId_TransactionDate`: Composite index optimizing account-filtered, chronologically sorted transaction history queries.

#### Q30: How does the System Restore endpoint prevent partial database corruption if a restore fails midway?
**Answer:** The entire restore routine in `SystemBackupService.RestoreSystemAsync` is wrapped in an `IDbContextTransaction`. If an error occurs during account deletion, transaction clearing, or rehydration, `await transaction.RollbackAsync()` is invoked. The database immediately reverts to its pre-restore state, ensuring zero corruption.

---
*Reference sheet designed for technical interviews, code defenses, and viva voce examinations.*
