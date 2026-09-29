# BankAccountManagementSystem — Technical Concept Analysis & Syllabus Mapping

> **Project Name:** Bank Account Management System (BAMS)  
> **Backend:** ASP.NET Core (.NET 8.0), Entity Framework Core 8.0.10, SQL Server (`PTPL-264\SQLEXPRESS`), ASP.NET Core Identity, JWT Bearer Authentication, Serilog, Fixed-Window Rate Limiting, Health Checks.  
> **Frontend:** React 18, Vite 5, Axios (with Request/Response Interceptors), React Router v6, React Context API (`AuthContext`), CSS Modules.  
> **Analysis Date:** September 2026  
> **Target Framework:** .NET 8.0 LTS / Node.js 18+  

---

## Executive Summary & Classification Standard

This document maps the real, existing implementation of the **Bank Account Management System** to the 17 core modules of the enterprise ASP.NET Core curriculum. Every topic in the curriculum is classified strictly according to its verified presence in the codebase:

- **`IMPLEMENTED`**: Fully designed, coded, and operational in the repository.
- **`PARTIALLY IMPLEMENTED`**: Core aspects are implemented, but specific optional or secondary syllabus subtopics are absent or adapted.
- **`NOT IMPLEMENTED`**: Topic is absent from the repository; explicitly marked as `"Not found in the current implementation."`
- **`NOT APPLICABLE / NOT USED IN THIS PROJECT`**: Topic represents an architectural alternative not selected for this decoupled Web API + React SPA architecture.

---

## 1. Project Overview & High-Level Architecture

### Architectural Overview
The Bank Account Management System is built as a **Decoupled Single Page Application (SPA) + RESTful Web API** architecture. 

- **Frontend Client:** React 18 SPA bundled with Vite, interacting with the backend exclusively over HTTP/HTTPS via JSON payloads. Client-side authentication state is preserved in browser `localStorage` and injected across API calls via an Axios request interceptor.
- **Backend API:** ASP.NET Core Web API running on .NET 8 LTS. It uses controller-based endpoints, a custom three-tiered middleware pipeline, scoped service layers for business rules, Entity Framework Core 8 as the Object-Relational Mapper (ORM), and Microsoft SQL Server as the relational store.
- **Identity & Security:** ASP.NET Core Identity tables manage user credentials and roles (`Admin` and `User`). State-less authentication is achieved via digitally signed JSON Web Tokens (JWT with HMAC-SHA256).

### System Component Diagram

```
+----------------------------------------------------------------------------------------------------+
|                                    REACT 18 SINGLE PAGE APPLICATION                                 |
|                                                                                                    |
|  Pages:                                                                                            |
|  - LoginPage / RegisterPage             - DashboardPage / ProfilePage                              |
|  - AccountsPage / AddAccountPage        - AccountDetailsPage                                       |
|  - DepositPage / WithdrawPage           - TransferPage                                             |
|  - TransactionsPage / TxnHistoryPage    - BackupRestorePage (Admin)                                |
|                                                                                                    |
|  State Management & HTTP:                                                                          |
|  - AuthContext (user, token, role, login, logout)                                                  |
|  - Axios Instance (BaseURL: /api/v1, Bearer Token Interceptor, 401 Redirect Interceptor)            |
+-------------------------------------------------+--------------------------------------------------+
                                                  | JSON over HTTP/HTTPS
                                                  v
+-------------------------------------------------+--------------------------------------------------+
|                              ASP.NET CORE 8 REST API (Kestrel Server)                              |
|                                                                                                    |
|  Middleware Pipeline (Strict Sequential Order):                                                    |
|  1. CorrelationIdMiddleware   --> Generates/extracts X-Correlation-ID; adds to HttpContext.Items   |
|  2. ExceptionMiddleware       --> Global catch: maps Domain/EF exceptions to 400/401/404/409/422   |
|  3. RequestLoggingMiddleware   --> Captures HTTP Method, Path, Status Code, Elapsed Milliseconds   |
|  4. UseRateLimiter            --> Fixed-window partition: 10 requests/min per IP on /auth/login    |
|  5. UseAuthentication         --> Validates JWT Bearer signature, issuer, audience, and lifetime   |
|  6. UseAuthorization          --> Enforces [Authorize] and [Authorize(Roles = "Admin")] claims     |
|                                                                                                    |
|  Controller Layer:                                                                                 |
|  - AuthController          (/api/v1/auth)         - UsersController      (/api/v1/users)           |
|  - AccountsController      (/api/v1/accounts)     - TransactionsController (/api/v1/transactions)  |
|  - SystemController        (/api/v1/system)       - HomeController       (/ & /Home/Index)         |
|                                                                                                    |
|  Scoped Service Layer:                                                                             |
|  - AuthService             - UserService          - AccountService                                 |
|  - TransactionService (ACID Transfers, Soft-delete validation, Savings cash-only rule)             |
|  - SystemBackupService (Atomic JSON export / rehydration within IDbContextTransaction)             |
|                                                                                                    |
|  Data Access Layer:                                                                                |
|  - ApplicationDbContext : IdentityDbContext<ApplicationUser>                                      |
|  - Tables: AspNetUsers, AspNetRoles, AspNetUserRoles, Accounts, Transactions                       |
|  - Concurrency: RowVersion byte[] on Accounts (SQL Server rowversion column)                       |
+-------------------------------------------------+--------------------------------------------------+
                                                  | ADO.NET / TDS Protocol
                                                  v
+-------------------------------------------------+--------------------------------------------------+
|                                    MICROSOFT SQL SERVER                                            |
|                               (Database: BankAccountDb)                                            |
+----------------------------------------------------------------------------------------------------+
```

---

## 2. Original Banking Requirements Mapping

| # | Requirement | Status | Backend Files & Handlers | Frontend Pages & Services | End-to-End Implementation Flow |
|---|-------------|--------|--------------------------|---------------------------|--------------------------------|
| **1** | **Savings & Checking Account Types** | `IMPLEMENTED` | `Models/AccountType.cs`<br>`Models/Account.cs`<br>`Services/Implementations/AccountService.cs` (`CreateAccountAsync`) | `pages/AddAccountPage.jsx`<br>`pages/AccountsPage.jsx`<br>`services/accountService.js` | User selects `AccountType` (0 = Savings, 1 = Checking). Account is created with unique `AccountNumber` (`ACC-YYYYMMDD-XXXX`), initial balance, and tied to authenticated user ID via foreign key. |
| **2** | **Savings Account Cash-Only Restriction** | `IMPLEMENTED` | `Services/Implementations/TransactionService.cs`<br>`Exceptions/BankingExceptions.cs` (`InvalidTransactionException`, `InvalidAccountTypeException`) | `pages/DepositPage.jsx`<br>`pages/WithdrawPage.jsx`<br>`pages/TransferPage.jsx` | In `TransactionService.cs`: Withdrawals and Deposits inspect `TransactionMode`. If `account.AccountType == AccountType.Savings && request.Mode != TransactionMode.Cash`, an `InvalidAccountTypeException` is thrown. On Transfer, if source or destination is `Savings`, transfer is rejected immediately. Frontend dynamically disables Cheque mode for Savings accounts. |
| **3** | **Checking Account Cash & Cheque Support** | `IMPLEMENTED` | `Services/Implementations/TransactionService.cs`<br>`Models/TransactionMode.cs` | `pages/DepositPage.jsx`<br>`pages/WithdrawPage.jsx`<br>`pages/TransferPage.jsx` | Checking accounts allow `TransactionMode.Cash` (0) and `TransactionMode.Cheque` (1). Cheque withdrawals, deposits, and inter-account transfers are processed without constraint violations. |
| **4** | **Financial Transaction Processing (Deposit, Withdraw, Transfer)** | `IMPLEMENTED` | `Controllers/TransactionsController.cs`<br>`Services/Implementations/TransactionService.cs`<br>`Data/ApplicationDbContext.cs` | `pages/DepositPage.jsx`<br>`pages/WithdrawPage.jsx`<br>`pages/TransferPage.jsx`<br>`services/transactionService.js` | **Deposit:** Increments balance, writes `Transaction` record (`Type=Deposit`).<br>**Withdraw:** Validates active state and `Balance >= Amount`; decrements balance, writes `Transaction` record (`Type=Withdrawal`).<br>**Transfer:** Initiated within an explicit EF Core database transaction (`BeginTransactionAsync`); decrements sender balance, increments recipient balance, creates two linked transaction rows (Source withdrawal + Destination deposit) sharing the same `ReferenceNumber` pattern and linking `RelatedAccountId`. |
| **5** | **Soft Deactivation of Accounts** | `IMPLEMENTED` | `Controllers/AccountsController.cs`<br>`Services/Implementations/AccountService.cs` (`DeactivateAccountAsync`) | `pages/AccountsPage.jsx`<br>`pages/AccountDetailsPage.jsx` | When `DELETE /api/v1/accounts/{id}` is called, `IsActive` is set to `false`. Database record is preserved so transaction history FKs remain valid (`OnDelete(DeleteBehavior.Restrict)`). Any subsequent transaction attempt throws `AccountInactiveException` (HTTP 422). |
| **6** | **Save & Restore / System Backup & Rehydration** | `IMPLEMENTED` | `Controllers/SystemController.cs`<br>`Services/Implementations/SystemBackupService.cs`<br>`DTOs/System/*` | `pages/BackupRestorePage.jsx`<br>`services/systemService.js` | **Export (`GET /api/v1/system/save`):** Reads all accounts and transactions with `AsNoTracking()`, outputs structured JSON payload with schema version and timestamp. Restricted to Admin.<br>**Restore (`POST /api/v1/system/restore`):** Wrapped in `BeginTransactionAsync()`. Validates payload format, clears dependent transactions and accounts via SQL truncation/deletion, maps and rehydrates accounts with identity preservation, commits transaction atomically. |

---

## 3. Session-by-Session Syllabus Mapping

### Session 1: .NET Architecture & C# Fundamentals

#### Topic 1.1: Common Language Runtime (CLR), CLI, CTS, CLS & JIT Compilation
- **Classification:** `IMPLEMENTED`
- **Location:** Project configuration & runtime target: `Backend/BankAccountManagementSystem.API/BankAccountManagementSystem.API/BankAccountManagementSystem.API.csproj`
- **Explanation:** The project targets `<TargetFramework>net8.0</TargetFramework>`. When compiled, C# compiles to Common Intermediate Language (CIL) complying with Common Type System (CTS) and Common Language Specification (CLS). At runtime, the CoreCLR JIT compiler generates host-native x64 machine instructions.

#### Topic 1.2: Memory Management & Garbage Collection
- **Classification:** `IMPLEMENTED`
- **Location:** Scoped service scopes and DbContext lifecycle in `Program.cs` (lines 137–142, 234–237) and `SystemBackupService.cs`.
- **Explanation:** Memory allocation relies on .NET 8 Generational Garbage Collector (Gen 0, 1, 2, LOH). Short-lived DTOs generated per HTTP request are collected in Gen 0. `DbContext` instances are scoped to requests to avoid memory retention. Explicit cleanup is demonstrated during seed execution via `using (var scope = app.Services.CreateScope())`.

#### Topic 1.3: Asynchronous Programming (`async`, `await`, `Task`, `Task<T>`)
- **Classification:** `IMPLEMENTED`
- **Location:** 
  - `Controllers/TransactionsController.cs` (lines 40–120)
  - `Services/Implementations/TransactionService.cs` (lines 25–230)
- **Explanation:** Every database and I/O call is fully asynchronous, preventing thread-pool starvation. Methods utilize `async Task<ActionResult<T>>` and `await _context.SaveChangesAsync()`, releasing Kestrel worker threads back to the thread pool while awaiting I/O completion.

#### Topic 1.4: Language Integrated Query (LINQ)
- **Classification:** `IMPLEMENTED`
- **Location:**
  - `Services/Implementations/TransactionService.cs` (lines 180–225)
  - `Services/Implementations/AccountService.cs` (lines 45–70)
- **Explanation:** Uses LINQ method syntax and comprehension expressions:
  - Filtering: `.Where(t => t.AccountId == query.AccountId.Value)`
  - Projection: `.Select(a => new AccountDto { ... })`
  - Ordering: `.OrderByDescending(t => t.TransactionDate)`
  - Aggregation/Pagination: `.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync()`

#### Topic 1.5: Modern C# Features (Records, Pattern Matching, Nullable Reference Types)
- **Classification:** `IMPLEMENTED`
- **Location:**
  - Project configuration: `<Nullable>enable</Nullable>` in `.csproj`
  - Pattern matching switch expressions in `Middleware/ExceptionMiddleware.cs` (lines 73–99)
- **Explanation:** `ExceptionMiddleware` evaluates exceptions using C# pattern matching expressions:
  ```csharp
  var statusCode = exception switch
  {
      DbUpdateConcurrencyException => (int)HttpStatusCode.Conflict,
      UnauthorizedAccessException  => (int)HttpStatusCode.Unauthorized,
      KeyNotFoundException         => (int)HttpStatusCode.NotFound,
      ArgumentException            => (int)HttpStatusCode.BadRequest,
      InvalidOperationException    => 422,
      _                            => (int)HttpStatusCode.InternalServerError
  };
  ```

---

### Session 2: ASP.NET Core Architecture & Hosting

#### Topic 2.1: Hosting Architecture & Kestrel Web Server
- **Classification:** `IMPLEMENTED`
- **Location:** `Program.cs` (lines 20–301), `Properties/launchSettings.json`
- **Explanation:** The application executes as an in-process ASP.NET Core console application hosted by Kestrel. `launchSettings.json` defines application URLs (`https://localhost:7190;http://localhost:5247`). Kestrel handles HTTP requests and directs them into the ASP.NET Core middleware pipeline.

#### Topic 2.2: Application Startup Evolution & WebApplicationBuilder
- **Classification:** `IMPLEMENTED`
- **Location:** `Program.cs` (lines 26, 227)
- **Explanation:** Uses the modern .NET 6/7/8 minimal hosting model:
  - `var builder = WebApplication.CreateBuilder(args);` initializes host configuration, logging, and services.
  - `var app = builder.Build();` builds the middleware pipeline and host. No legacy `Startup.cs` file is required.

#### Topic 2.3: Configuration Sources & `IHostEnvironment`
- **Classification:** `IMPLEMENTED`
- **Location:** 
  - `Program.cs` (lines 28, 53, 95, 254–271)
  - `appsettings.json`, `appsettings.Development.json`
- **Explanation:** Configuration is aggregated hierarchically from `appsettings.json`, environment variables, and `IHostEnvironment`. `ExceptionMiddleware` injects `IHostEnvironment` to conditionally display detailed exception messages in Development while masking them in Production.

---

### Session 3: Middleware Pipeline

#### Topic 3.1: The Middleware Concept & Request Execution Flow
- **Classification:** `IMPLEMENTED`
- **Location:** `Program.cs` (lines 245–286)
- **Explanation:** ASP.NET Core handles HTTP requests using a bidirectional chain of components (`RequestDelegate`). The request pipeline in `Program.cs` executes in strict sequential order:
  1. `CorrelationIdMiddleware`
  2. `ExceptionMiddleware`
  3. `RequestLoggingMiddleware`
  4. `UseSwagger` & `UseSwaggerUI` (Development only)
  5. `UseHttpsRedirection`
  6. `UseStaticFiles`
  7. `UseRouting`
  8. `UseRateLimiter`
  9. `UseAuthentication`
  10. `UseAuthorization`
  11. `MapHealthChecks`, `MapControllerRoute`, `MapControllers`

#### Topic 3.2: Custom Middleware Implementation
- **Classification:** `IMPLEMENTED`
- **Location:**
  - `Middleware/CorrelationIdMiddleware.cs`
  - `Middleware/ExceptionMiddleware.cs`
  - `Middleware/RequestLoggingMiddleware.cs`
- **Explanation:**
  - `CorrelationIdMiddleware`: Inspects incoming header `X-Correlation-ID`. Reuses it or generates `Guid.NewGuid()`. Binds it to `HttpContext.Items["CorrelationId"]` and injects it into response headers via `context.Response.OnStarting()`.
  - `ExceptionMiddleware`: Wraps downstream execution in `try / catch (Exception ex)`. Logs uncaught exceptions with correlation metadata and serializes a standard JSON error response.
  - `RequestLoggingMiddleware`: Uses `System.Diagnostics.Stopwatch` to track request duration and logs `{Method} {Path} responded {StatusCode} in {ElapsedMs}ms`.

#### Topic 3.3: Middleware Short-Circuiting
- **Classification:** `IMPLEMENTED`
- **Location:**
  - `Program.cs` (line 200: Rate Limiter)
  - `Middleware/ExceptionMiddleware.cs` (lines 48–64)
- **Explanation:** If a client exceeds 10 login requests per minute, `UseRateLimiter` short-circuits the pipeline and immediately returns `429 Too Many Requests`. Similarly, `UseAuthorization` short-circuits with `401 Unauthorized` or `403 Forbidden` without invoking downstream controllers.

---

### Session 4: Dependency Injection (DI)

#### Topic 4.1: Built-in IoC Container & Service Lifetimes
- **Classification:** `IMPLEMENTED`
- **Location:** `Program.cs` (lines 52–54, 137–142, 234–237)
- **Explanation:** The built-in container registers services with explicit lifetimes:
  - **Scoped:** `IAuthService` $\rightarrow$ `AuthService`, `IUserService` $\rightarrow$ `UserService`, `IAccountService` $\rightarrow$ `AccountService`, `ITransactionService` $\rightarrow$ `TransactionService`, `ISystemBackupService` $\rightarrow$ `SystemBackupService`, and `ApplicationDbContext`. An instance is created once per HTTP request.
  - **Singleton:** Serilog logger instance (`Log.Logger`).
  - **Transient:** Not found in the current implementation (all domain services require request-scoped EF DbContext).

#### Topic 4.2: Constructor Injection
- **Classification:** `IMPLEMENTED`
- **Location:**
  - `Controllers/TransactionsController.cs` (lines 18–26)
  - `Services/Implementations/TransactionService.cs` (lines 20–28)
- **Explanation:** Classes declare dependencies via public constructors:
  ```csharp
  public class TransactionsController : ControllerBase
  {
      private readonly ITransactionService _transactionService;
      private readonly ILogger<TransactionsController> _logger;

      public TransactionsController(ITransactionService transactionService, ILogger<TransactionsController> logger)
      {
          _transactionService = transactionService;
          _logger = logger;
      }
  }
  ```

#### Topic 4.3: Service Locator Anti-Pattern vs Manual Scope Resolution
- **Classification:** `PARTIALLY IMPLEMENTED`
- **Location:** `Program.cs` (lines 234–237)
- **Explanation:** The service locator anti-pattern is avoided in controllers and business services. However, during startup before an HTTP request exists, manual service scope creation is used to resolve scoped identity managers for database seeding:
  ```csharp
  using (var scope = app.Services.CreateScope())
  {
      await DbSeeder.SeedAsync(scope.ServiceProvider);
  }
  ```

---

### Session 5: Configuration & Logging

#### Topic 5.1: `IConfiguration` & The Options Pattern (`IOptions<T>`)
- **Classification:** `IMPLEMENTED`
- **Location:**
  - `Configuration/JwtSettings.cs`
  - `Program.cs` (lines 86–88, 95)
  - `Services/Implementations/AuthService.cs` (lines 26–32)
- **Explanation:** The strongly typed options pattern binds configuration settings:
  ```csharp
  builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
  ```
  `AuthService` injects `IOptions<JwtSettings> jwtOptions` to access `SecretKey`, `Issuer`, `Audience`, and `ExpiryInMinutes`.

#### Topic 5.2: Structured Logging with Serilog
- **Classification:** `IMPLEMENTED`
- **Location:**
  - `Program.cs` (lines 20–45, 307–313)
  - `appsettings.json`
  - Output directory: `Backend/BankAccountManagementSystem.API/BankAccountManagementSystem.API/Logs/`
- **Explanation:** Serilog is configured as the primary logging engine with two sinks:
  - Console Sink (development output).
  - File Sink: writes daily rolling files to `Logs/bams-.log` with 30-day retention and structured message templates.
  - Sensitive credentials (passwords, JWTs, hashes) are strictly excluded from logging statements.

---

### Session 6: Routing & Controllers

#### Topic 6.1: Attribute Routing vs Conventional Routing
- **Classification:** `IMPLEMENTED`
- **Location:**
  - `Controllers/AccountsController.cs` (line 12)
  - `Controllers/TransactionsController.cs` (line 14)
  - `Program.cs` (lines 292–298)
- **Explanation:**
  - **Attribute Routing:** Used for all REST API endpoints:
    `[Route("api/v1/[controller]")]` evaluates to `/api/v1/accounts`, `/api/v1/transactions`, etc.
  - **Conventional Routing:** Configured in `Program.cs` for legacy MVC controllers:
    `app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");`

#### Topic 6.2: Action Return Types (`IActionResult`, `ActionResult<T>`)
- **Classification:** `IMPLEMENTED`
- **Location:** All controllers under `Controllers/`
- **Explanation:** Endpoints use strongly typed `ActionResult<T>` to provide compile-time type safety and OpenAPI/Swagger schema generation, while allowing standard HTTP helper methods (`Ok()`, `CreatedAtAction()`, `NotFound()`, `BadRequest()`).

#### Topic 6.3: Content Negotiation & API Versioning
- **Classification:** `PARTIALLY IMPLEMENTED`
- **Location:** `Controllers/` routes, `Program.cs`
- **Explanation:** URL-based versioning is implemented conventionally by prefixing routes with `/api/v1/`. However, the formal NuGet package `Microsoft.AspNetCore.Mvc.Versioning` is *not found in the current implementation*. Content negotiation defaults to `application/json`.

---

### Session 7: Model Binding & Validation

#### Topic 7.1: Model Binding Sources (`[FromBody]`, `[FromRoute]`, `[FromQuery]`)
- **Classification:** `IMPLEMENTED`
- **Location:**
  - `Controllers/TransactionsController.cs` (lines 35, 78, 92)
  - `Controllers/AccountsController.cs` (lines 35, 52)
- **Explanation:** Parameters are mapped explicitly using binding attributes:
  - `[FromBody] DepositRequest request` binds JSON from the HTTP request body.
  - `[FromRoute] int id` extracts route parameters.
  - `[FromQuery] TransactionHistoryQuery query` binds query string parameters for filtering and pagination.

#### Topic 7.2: Data Annotations & Validation Attributes
- **Classification:** `IMPLEMENTED`
- **Location:** `DTOs/Transactions/DepositRequest.cs`, `DTOs/Auth/RegisterRequest.cs`, `DTOs/Accounts/CreateAccountRequest.cs`
- **Explanation:** Request DTOs declare declarative validation constraints:
  ```csharp
  public class DepositRequest
  {
      [Required(ErrorMessage = "Account ID is required.")]
      public int AccountId { get; set; }

      [Range(0.01, 10000000.00, ErrorMessage = "Amount must be between 0.01 and 10,000,000.00.")]
      public decimal Amount { get; set; }

      [Required(ErrorMessage = "Transaction mode is required.")]
      public TransactionMode Mode { get; set; }
  }
  ```

#### Topic 7.3: Automatic Model Validation via `[ApiController]`
- **Classification:** `IMPLEMENTED`
- **Location:** Decorating all API controllers (e.g., `[ApiController]` on `TransactionsController`).
- **Explanation:** The `[ApiController]` attribute automatically triggers a `400 Bad Request` response containing a validation problem details object if `ModelState.IsValid == false`, without requiring manual checks inside action methods.

---

### Session 8: Entity Framework Core Fundamentals

#### Topic 8.1: ORM Concepts, `DbContext`, and `DbSet<T>`
- **Classification:** `IMPLEMENTED`
- **Location:** `Data/ApplicationDbContext.cs` (lines 25–45)
- **Explanation:** `ApplicationDbContext` inherits from `IdentityDbContext<ApplicationUser>`, exposing `DbSet<Account> Accounts` and `DbSet<Transaction> Transactions`.

#### Topic 8.2: Code-First Workflow & Migrations
- **Classification:** `IMPLEMENTED`
- **Location:** Directory `Backend/BankAccountManagementSystem.API/BankAccountManagementSystem.API/Migrations/`
- **Explanation:** Database schema evolution is handled through EF Core Code-First migrations:
  1. `20260925051402_InitialIdentity.cs`: Configures ASP.NET Core Identity tables.
  2. `20260925061111_AddBankAccount.cs`: Introduces the `Accounts` table with foreign keys.
  3. `20260925085600_AddTransactions.cs`: Introduces the `Transactions` table and `RowVersion` concurrency column on `Accounts`.
  4. `20260925092310_AddTransactionHistoryIndexes.cs`: Adds database indexes for transaction querying.

#### Topic 8.3: Fluent API Model Configuration
- **Classification:** `IMPLEMENTED`
- **Location:** `Data/ApplicationDbContext.cs` (`OnModelCreating`, lines 52–202)
- **Explanation:** Schema rules are configured fluently:
  - Column precision: `.HasColumnType("decimal(18,2)")` on `Account.Balance` and `Transaction.Amount`.
  - Unique indexes: `.HasIndex(a => a.AccountNumber).IsUnique()`
  - Concurrency token: `entity.Property(a => a.RowVersion).IsRowVersion()`

---

### Session 9: EF Core Relationships & Queries

#### Topic 9.1: Entity Relationships & Foreign Key Constraints
- **Classification:** `IMPLEMENTED`
- **Location:** `Data/ApplicationDbContext.cs` (lines 118–125, 171–201)
- **Explanation:**
  - **One-to-Many:** `ApplicationUser (1) <---> (*) Account` with `.OnDelete(DeleteBehavior.Restrict)`.
  - **One-to-Many:** `Account (1) <---> (*) Transaction` (`AccountId` foreign key).
  - **Self-referencing Optional Foreign Key:** `Account (1) <---> (0..1) Transaction` (`RelatedAccountId` on `Transactions` to track destination/source in transfers).

#### Topic 9.2: Query Execution, Eager Loading & `AsNoTracking()`
- **Classification:** `IMPLEMENTED`
- **Location:**
  - `Services/Implementations/TransactionService.cs` (lines 180–220)
  - `Services/Implementations/SystemBackupService.cs` (lines 35–45)
- **Explanation:**
  - `AsNoTracking()` is used on read-only queries (system export, balance checks, history queries) to bypass EF change tracking overhead.
  - Eager Loading: `.Include(t => t.Account).Include(t => t.RelatedAccount)` loads related entities in a single SQL query.
  - Lazy loading is *not found in the current implementation* (proxies are not enabled, preventing N+1 query issues).

---

### Session 10: EF Core Advanced Topics

#### Topic 10.1: Database Transactions (`IDbContextTransaction`)
- **Classification:** `IMPLEMENTED`
- **Location:**
  - `Services/Implementations/TransactionService.cs` (`TransferAsync`, lines 110–175)
  - `Services/Implementations/SystemBackupService.cs` (`RestoreSystemAsync`, lines 70–140)
- **Explanation:** ACID operations are wrapped in explicit database transactions:
  ```csharp
  using var dbTransaction = await _context.Database.BeginTransactionAsync();
  try
  {
      // 1. Debit Source Account & 2. Credit Destination Account
      // 3. Insert Outgoing & Incoming Transaction Records
      await _context.SaveChangesAsync();
      await dbTransaction.CommitAsync();
  }
  catch
  {
      await dbTransaction.RollbackAsync();
      throw;
  }
  ```

#### Topic 10.2: Optimistic Concurrency Control
- **Classification:** `IMPLEMENTED`
- **Location:**
  - `Models/Account.cs` (line 28: `byte[] RowVersion`)
  - `Data/ApplicationDbContext.cs` (lines 115–116)
  - `Middleware/ExceptionMiddleware.cs` (lines 75–78)
- **Explanation:** `Account.RowVersion` is mapped via `.IsRowVersion()` to a SQL Server `rowversion` column. When two simultaneous requests attempt to update the same account balance, EF Core appends `WHERE RowVersion = @original` to the UPDATE query. The second update impacts 0 rows, triggering `DbUpdateConcurrencyException`, which `ExceptionMiddleware` catches and translates to `409 Conflict`.

---

### Session 11: ASP.NET Core Identity

#### Topic 11.1: Identity Architecture & Architecture Tables
- **Classification:** `IMPLEMENTED`
- **Location:**
  - `Models/ApplicationUser.cs`
  - `Data/ApplicationDbContext.cs`
  - `Program.cs` (lines 61–79)
- **Explanation:** `ApplicationUser` extends `IdentityUser`, adding `FirstName`, `LastName`, and `EmployeeCode`. `AddIdentity<ApplicationUser, IdentityRole>()` manages the standard Identity tables: `AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`, `AspNetUserClaims`, etc.

#### Topic 11.2: Password Hashing & Lockout Policies
- **Classification:** `IMPLEMENTED`
- **Location:** `Program.cs` (lines 63–77)
- **Explanation:** Identity options enforce strong security policies:
  - Minimum 8 characters with digit, lowercase, uppercase, and non-alphanumeric character.
  - Password hashing uses PBKDF2 with HMAC-SHA256 and salt.
  - Account lockout triggers for 5 minutes after 5 consecutive failed access attempts.

---

### Session 12: Authentication & Authorization

#### Topic 12.1: JWT Bearer Token Authentication
- **Classification:** `IMPLEMENTED`
- **Location:**
  - `Program.cs` (lines 98–129)
  - `Services/Implementations/AuthService.cs` (`GenerateJwtToken`, lines 80–120)
- **Explanation:** `AuthService` issues signed JWT tokens containing claims:
  - `ClaimTypes.NameIdentifier` (User ID)
  - `ClaimTypes.Email`
  - `ClaimTypes.Role` (e.g. `Admin`, `User`)
  - `JwtRegisteredClaimNames.Jti` (Unique token GUID)
  The token is signed using `SymmetricSecurityKey` with `SecurityAlgorithms.HmacSha256`. In `Program.cs`, `AddJwtBearer` validates `Issuer`, `Audience`, `Lifetime`, and `IssuerSigningKey`.

#### Topic 12.2: Role-Based Authorization
- **Classification:** `IMPLEMENTED`
- **Location:**
  - `Controllers/UsersController.cs` (`[Authorize(Roles = "Admin")]`)
  - `Controllers/SystemController.cs` (`[Authorize(Roles = "Admin")]`)
- **Explanation:** Endpoints are protected by roles:
  - Only users with the `Admin` role claim can access user management or execute backup/restore.
  - Endpoints with general `[Authorize]` attributes (e.g. `AccountsController`, `TransactionsController`) allow authenticated users to perform operations scoped to their own accounts via user ID claim validation.

---

### Session 13: Web API Design & Best Practices

#### Topic 13.1: RESTful Principles & HTTP Status Code Mapping
- **Classification:** `IMPLEMENTED`
- **Location:** Across all controllers and `ExceptionMiddleware.cs`
- **Explanation:** Standard REST verbs and status codes are strictly followed:
  - `GET`: `200 OK` (retrieval)
  - `POST`: `201 Created` with `Location` header or `200 OK`
  - `DELETE`: `200 OK` or `204 No Content`
  - `400 Bad Request`: Model validation failure
  - `401 Unauthorized`: Missing or invalid JWT
  - `403 Forbidden`: Authenticated user lacks role permissions
  - `404 Not Found`: Target entity ID does not exist
  - `409 Conflict`: Concurrency conflict on balance update
  - `422 Unprocessable Entity`: Domain business rule violation (e.g. insufficient balance, deactivated account)
  - `429 Too Many Requests`: Rate limit exceeded
  - `500 Internal Server Error`: Unhandled server exception

#### Topic 13.2: Data Transfer Object (DTO) Pattern
- **Classification:** `IMPLEMENTED`
- **Location:** Directory `Backend/BankAccountManagementSystem.API/BankAccountManagementSystem.API/DTOs/`
- **Explanation:** Internal EF Core domain entities (`Account`, `Transaction`, `ApplicationUser`) are never returned directly to the client. Dedicated request and response DTOs prevent over-posting and hide internal fields such as password hashes and concurrency tokens.

---

### Session 14: Error Handling & Middleware Filters

#### Topic 14.1: Centralized Exception Handling Middleware
- **Classification:** `IMPLEMENTED`
- **Location:** `Middleware/ExceptionMiddleware.cs`
- **Explanation:** Global exception interception centralizes error handling, logging, and status code translation. It guarantees that database errors and stack traces are never exposed to clients in production environments.

#### Topic 14.2: Domain-Specific Exception Classes
- **Classification:** `IMPLEMENTED`
- **Location:** `Exceptions/BankingExceptions.cs`
- **Explanation:** Custom domain exceptions express domain failures cleanly:
  - `InsufficientBalanceException`
  - `AccountInactiveException`
  - `InvalidTransactionException`
  - `InvalidAccountTypeException`

---

### Session 15: Security & Performance

#### Topic 15.1: Rate Limiting
- **Classification:** `IMPLEMENTED`
- **Location:**
  - `Program.cs` (lines 200–214)
  - `Controllers/AuthController.cs` (`[EnableRateLimiting("login")]`)
- **Explanation:** ASP.NET Core rate limiting protects the login endpoint with a fixed-window partition: maximum 10 requests per 1-minute window per IP address. Exceeding requests receive `429 Too Many Requests`.

#### Topic 15.2: Health Checks
- **Classification:** `IMPLEMENTED`
- **Location:** `Program.cs` (lines 221–222, 290)
- **Explanation:** Configured via `builder.Services.AddHealthChecks().AddDbContextCheck<ApplicationDbContext>("database");` and mapped to `GET /health`. Returns `200 OK` (Healthy) or `503 Service Unavailable` if database connectivity fails.

#### Topic 15.3: SQL Injection & XSS Defenses
- **Classification:** `IMPLEMENTED`
- **Location:** `Data/ApplicationDbContext.cs`, all LINQ queries
- **Explanation:** SQL Injection is prevented by design because all database queries are compiled through EF Core parameterized expressions. XSS is mitigated on the client via React's automatic JSX output escaping.

---

### Session 16: State Management & Caching

#### Topic 16.1: Server-Side State Management (Session State, Cookies)
- **Classification:** `NOT APPLICABLE / NOT USED IN THIS PROJECT`
- **Location:** N/A (Stateless REST API design)
- **Explanation:** *Not found in the current implementation.* The backend API is designed to be completely stateless. Client identity is communicated via the HTTP `Authorization: Bearer <token>` header on every request.

#### Topic 16.2: In-Memory Caching (`IMemoryCache`) & Distributed Caching (Redis)
- **Classification:** `NOT IMPLEMENTED`
- **Location:** N/A
- **Explanation:** *Not found in the current implementation.* All balance checks, history queries, and user lookups execute directly against SQL Server to ensure real-time consistency.

---

### Session 17: Advanced Topics & Deployment

#### Topic 17.1: Real-Time Communication (SignalR)
- **Classification:** `NOT IMPLEMENTED`
- **Location:** N/A
- **Explanation:** *Not found in the current implementation.* Real-time WebSockets/SignalR hubs are not implemented.

#### Topic 17.2: Automated Unit Testing (xUnit / NUnit / Moq)
- **Classification:** `NOT IMPLEMENTED`
- **Location:** N/A
- **Explanation:** *Not found in the current implementation.* Formal xUnit/NUnit test projects do not exist in the solution; verification is conducted via automated end-to-end HTTP integration test scripts.

#### Topic 17.3: Containerization (Docker) & Microservices
- **Classification:** `NOT IMPLEMENTED`
- **Location:** N/A
- **Explanation:** *Not found in the current implementation.* No `Dockerfile` or `docker-compose.yml` is present.

---

## 4. Frontend Concept Mapping (React 18 SPA)

### Component Hierarchy & Route Architecture
The frontend is built with React 18 and React Router v6 in `Frontend/bank-management-frontend/src`:

```
App.jsx (AuthProvider, BrowserRouter)
 ├── /login                    --> LoginPage.jsx
 ├── /register                 --> RegisterPage.jsx
 └── ProtectedRoute (Role/Token Guard)
      └── DashboardLayout.jsx (Header, Sidebar Navigation, User Info)
           ├── /dashboard            --> DashboardPage.jsx
           ├── /accounts             --> AccountsPage.jsx
           ├── /accounts/new         --> AddAccountPage.jsx
           ├── /accounts/:id         --> AccountDetailsPage.jsx
           ├── /transactions         --> TransactionsPage.jsx
           ├── /transactions/deposit --> DepositPage.jsx
           ├── /transactions/withdraw--> WithdrawPage.jsx
           ├── /transactions/transfer--> TransferPage.jsx
           ├── /transactions/history --> TransactionHistoryPage.jsx
           ├── /profile              --> ProfilePage.jsx
           └── /system/backup-restore--> BackupRestorePage.jsx (Admin Only)
```

### Axios HTTP Client & Interceptors
File: `Frontend/bank-management-frontend/src/api.js`
- **Base URL:** Points to the backend API (`http://localhost:5247/api/v1` or via proxy).
- **Request Interceptor:** Reads `bams_token` from `localStorage`. If present, injects `Authorization: Bearer ${token}` on every outgoing request.
- **Response Interceptor:** Intercepts `401 Unauthorized` responses, clears stored tokens, and redirects the user to `/login`.

### State Management (`AuthContext`)
File: `Frontend/bank-management-frontend/src/context/AuthContext.jsx`
- Exposes `user`, `token`, `role`, `login(token, user)`, and `logout()` to the component tree.
- Persists session tokens in browser `localStorage` under `bams_token` and `bams_user`.

---

## 5. Complete API Inventory Table

| HTTP Method | Route / Endpoint | Controller | Purpose | Auth Required | Role Required | Request Body / Query DTO | Response DTO |
|:-----------:|:-----------------|:-----------|:--------|:-------------:|:-------------:|:-------------------------|:-------------|
| `POST` | `/api/v1/auth/register` | `AuthController` | Register a new banking user | No | Anonymous | `RegisterRequest` | `RegisterResponse` |
| `POST` | `/api/v1/auth/login` | `AuthController` | Authenticate & obtain JWT | No | Anonymous (Rate Limited) | `LoginRequest` | `LoginResponse` |
| `GET` | `/api/v1/auth/me` | `AuthController` | Retrieve profile of authenticated user | Yes | Any | None | User profile object |
| `GET` | `/api/v1/users` | `UsersController` | Retrieve all users | Yes | `Admin` | None | `IEnumerable<UserResponse>` |
| `GET` | `/api/v1/users/{id}` | `UsersController` | Get user details by ID | Yes | `Admin` | None | `UserResponse` |
| `POST` | `/api/v1/users` | `UsersController` | Admin create new user | Yes | `Admin` | `CreateUserRequest` | `UserResponse` |
| `PUT` | `/api/v1/users/{id}` | `UsersController` | Update user details | Yes | `Admin` | `UpdateUserRequest` | `UserResponse` |
| `DELETE` | `/api/v1/users/{id}` | `UsersController` | Delete user | Yes | `Admin` | None | `204 No Content` |
| `POST` | `/api/v1/users/{id}/roles` | `UsersController` | Assign role to user | Yes | `Admin` | `AssignRoleRequest` | `200 OK` |
| `POST` | `/api/v1/accounts` | `AccountsController` | Open new bank account | Yes | Any (Creates for self/admin) | `CreateAccountRequest` | `AccountResponse` |
| `GET` | `/api/v1/accounts` | `AccountsController` | List accounts (User: own, Admin: all) | Yes | Any | None | `IEnumerable<AccountResponse>` |
| `GET` | `/api/v1/accounts/{id}` | `AccountsController` | Get single account details | Yes | Any (Owner / Admin) | None | `AccountResponse` |
| `GET` | `/api/v1/accounts/{id}/balance` | `AccountsController` | Get current account balance | Yes | Any (Owner / Admin) | None | Balance object |
| `DELETE` | `/api/v1/accounts/{id}` | `AccountsController` | Soft-deactivate an account | Yes | Any (Owner / Admin) | None | `200 OK` |
| `POST` | `/api/v1/transactions/deposit` | `TransactionsController` | Deposit funds (Cash/Cheque) | Yes | Any (Owner / Admin) | `DepositRequest` | `TransactionResponse` |
| `POST` | `/api/v1/transactions/withdraw` | `TransactionsController` | Withdraw funds (Cash/Cheque) | Yes | Any (Owner / Admin) | `WithdrawalRequest` | `TransactionResponse` |
| `POST` | `/api/v1/transactions/transfer` | `TransactionsController` | Transfer between Checking accts | Yes | Any (Owner / Admin) | `TransferRequest` | `TransactionResponse` |
| `GET` | `/api/v1/transactions` | `TransactionsController` | Global transaction history query | Yes | `Admin` | `TransactionHistoryQuery` | `PagedResult<TransactionResponse>` |
| `GET` | `/api/v1/transactions/{id}` | `TransactionsController` | Get transaction by ID | Yes | Any (Owner / Admin) | None | `TransactionResponse` |
| `GET` | `/api/v1/transactions/account/{accountId}` | `TransactionsController` | Account transaction history | Yes | Any (Owner / Admin) | `TransactionHistoryQuery` | `PagedResult<TransactionResponse>` |
| `GET` | `/api/v1/system/save` | `SystemController` | Export system backup JSON | Yes | `Admin` | None | `SystemBackupDto` |
| `POST` | `/api/v1/system/restore` | `SystemController` | Rehydrate system from JSON | Yes | `Admin` | `SystemBackupDto` | `RestoreResponseDto` |
| `GET` | `/health` | Health Check Engine | Probe API and DB connectivity | No | Anonymous | None | Health status object |

---

## 6. File-to-Concept Mapping Table

| File Path | Primary Class / Component | Core Syllabus Concepts Demonstrated |
|:----------|:--------------------------|:-------------------------------------|
| `Program.cs` | Top-level Statements | WebApplicationBuilder, Kestrel, Serilog bootstrap, Scoped DI registrations, JWT Bearer configuration, Identity setup, RateLimiter, HealthChecks, Middleware order, Route mapping. |
| `Data/ApplicationDbContext.cs` | `ApplicationDbContext` | EF Core, IdentityDbContext, Fluent API, decimal(18,2) precision, Unique indexes, Concurrency token (RowVersion), Foreign key restrict behaviors. |
| `Middleware/CorrelationIdMiddleware.cs` | `CorrelationIdMiddleware` | Custom middleware, RequestDelegate, HttpContext.Items, OnStarting header injection, distributed tracing foundation. |
| `Middleware/ExceptionMiddleware.cs` | `ExceptionMiddleware` | Centralized exception handling, C# pattern matching switch expressions, HTTP status translation (400, 401, 404, 409, 422, 500), JSON serialization. |
| `Middleware/RequestLoggingMiddleware.cs` | `RequestLoggingMiddleware` | Custom middleware, System.Diagnostics.Stopwatch, HTTP request telemetry, structured logging. |
| `Exceptions/BankingExceptions.cs` | Custom Exception Classes | C# Object-Oriented inheritance (`InvalidOperationException`), custom domain exception modeling. |
| `Configuration/JwtSettings.cs` | `JwtSettings` | Strongly typed Options pattern, appsettings.json section binding. |
| `Services/Implementations/TransactionService.cs` | `TransactionService` | Scoped business service, EF Core transactions (`BeginTransactionAsync`), optimistic concurrency checks, LINQ queries, pagination (`Skip`/`Take`). |
| `Services/Implementations/SystemBackupService.cs` | `SystemBackupService` | AsNoTracking queries, database backup/restore, transactional database truncation/rehydration. |
| `Controllers/TransactionsController.cs` | `TransactionsController` | `[ApiController]`, `[Route]`, `[Authorize]`, `[FromBody]`, `[FromQuery]`, `ActionResult<T>`, REST API design. |
| `Frontend/.../api.js` | Axios Client Instance | HTTP client configuration, Request Interceptors (Bearer token injection), Response Interceptors (401 redirection). |
| `Frontend/.../AuthContext.jsx` | `AuthContext`, `AuthProvider` | React Context API, state persistence via localStorage, React custom hooks (`useAuth`). |
| `Frontend/.../ProtectedRoute.jsx` | `ProtectedRoute` | Client-side authorization guard, role-based route access, navigation redirects. |

---

## 7. Database Concept Mapping & Entity-Relationship Schema

### Database Schema Architecture
The database consists of **Identity Management Tables** (scaffolded automatically by `IdentityDbContext`) and **Domain Banking Tables** (`Accounts`, `Transactions`).

### ASCII Entity-Relationship Diagram

```
+------------------------------------+
|            AspNetUsers             |
+------------------------------------+
| PK  Id               nvarchar(450) |<--------+
|     EmployeeCode     nvarchar(50)  |         |
|     FirstName        nvarchar(100) |         |
|     LastName         nvarchar(100) |         |
|     Email            nvarchar(256) |         |
|     PasswordHash     nvarchar(max) |         |
|     SecurityStamp    nvarchar(max) |         |
+------------------------------------+         |
                   | 1                         | 1 (Restricted)
                   |                           |
                   | N                         |
+------------------------------------+         |
|         AspNetUserRoles            |         |
+------------------------------------+         |
| PK,FK UserId         nvarchar(450) |         |
| PK,FK RoleId         nvarchar(450) |         |
+------------------------------------+         |
                                               |
+------------------------------------+         |
|              Accounts              |         |
+------------------------------------+         |
| PK  AccountId        int (Identity)|         |
| FK  UserId           nvarchar(450) |---------+
|     AccountNumber    nvarchar(20)  | (Unique Index: IX_Accounts_AccountNumber)
|     Balance          decimal(18,2) |
|     AccountType      int           | (0 = Savings, 1 = Checking)
|     IsActive         bit           | (Soft delete flag)
|     CreatedAt        datetime2     |
|     RowVersion       rowversion    | (Concurrency Token)
+------------------------------------+
       | 1                     | 1
       |                       |
       | N (Owning Account)    | 0..1 (Related Account in Transfers)
+----------------------------------------------------+
|                    Transactions                    |
+----------------------------------------------------+
| PK  TransactionId     int (Identity)               |
| FK  AccountId         int                          | (DeleteBehavior.Restrict)
| FK  RelatedAccountId  int (Nullable)               | (DeleteBehavior.Restrict)
|     Amount            decimal(18,2)                |
|     TransactionType   int (0=Deposit, 1=Withdrawal, 2=Transfer)
|     TransactionMode   int (0=Cash, 1=Cheque)       |
|     Status            int (0=Pending, 1=Completed, 2=Failed)
|     ReferenceNumber   nvarchar(30)                 | (Unique Index: IX_Transactions_ReferenceNumber)
|     TransactionDate   datetime2                    | (Index: IX_Transactions_TransactionDate)
|     Description       nvarchar(500)                |
+----------------------------------------------------+
 Composite Index: IX_Transactions_AccountId_TransactionDate (AccountId, TransactionDate)
```

---

## 8. Design Patterns & Architecture

1. **Layered (N-Tier) Architecture:** Clean separation of concerns across Controller $\rightarrow$ Service $\rightarrow$ Data Access $\rightarrow$ Database. Controllers remain thin, handling only HTTP parsing and status mapping.
2. **Data Transfer Object (DTO) Pattern:** Decouples internal database persistence entities from external API contracts, eliminating over-posting risks.
3. **Repository & Unit of Work (via EF Core):** EF Core natively implements these patterns: `DbSet<T>` serves as the Repository and `DbContext` manages the Unit of Work via `SaveChangesAsync()`.
4. **Middleware (Chain of Responsibility) Pattern:** Requests pass sequentially through correlation, error handling, logging, rate limiting, and authentication handlers.
5. **Dependency Injection & Inversion of Control (IoC):** High-level modules depend on abstractions (`ITransactionService`, `IAccountService`), registered via `IServiceCollection` in `Program.cs`.

---

## 9. Security Analysis

- **Authentication:** Stateless authentication using JWT Bearer tokens signed with a 256-bit symmetric key (`HMAC-SHA256`).
- **Authorization:** Granular enforcement:
  - Role-based restrictions (`[Authorize(Roles = "Admin")]`) for administrative endpoints.
  - Ownership validation: users can only inspect and transact on accounts linked to their own `ClaimTypes.NameIdentifier`, unless holding the `Admin` role.
- **Brute-Force Attack Mitigation:** Fixed-window rate limiter on `/api/v1/auth/login` restricts requests to 10 per minute per IP address.
- **Information Leakage Prevention:** `ExceptionMiddleware` sanitizes error responses in production, replacing internal details with `"Something went wrong. Please try again later."`

---

## 10. Error Handling & HTTP Status Code Mapping

| Status Code | Scenario | Exception Source | Handled By | Sample Error Response |
|:-----------:|:---------|:-----------------|:-----------|:----------------------|
| **`400`** | Missing required fields, negative deposit amount | Model validation failure / `ArgumentException` | `[ApiController]` / `ExceptionMiddleware` | `{"errors": {"Amount": ["Amount must be between 0.01 and 10,000,000.00."]}}` |
| **`401`** | Missing or expired JWT Bearer token | JWT Authentication Handler | ASP.NET Core Security | `401 Unauthorized` |
| **`403`** | Non-admin attempting to access `/api/v1/system/save` | Authorization Filter | ASP.NET Core Security | `403 Forbidden` |
| **`404`** | Account ID or Transaction ID does not exist | `KeyNotFoundException` | `ExceptionMiddleware` | `{"success": false, "message": "Account with ID 999 not found."}` |
| **`409`** | Simultaneous balance update conflict | `DbUpdateConcurrencyException` | `ExceptionMiddleware` | `{"success": false, "message": "The record was modified by another user. Please retry."}` |
| **`422`** | Withdrawal exceeds balance / Savings cheque attempt | `InsufficientBalanceException` / `InvalidAccountTypeException` | `ExceptionMiddleware` | `{"success": false, "message": "Insufficient balance. Available: 1000.00, Requested: 3000.00."}` |
| **`429`** | Exceeded 10 login requests per minute from one IP | `RateLimiterMiddleware` | Built-in Rate Limiter | `429 Too Many Requests` |
| **`500`** | Database unreachable / Unexpected server crash | `Exception` | `ExceptionMiddleware` | `{"success": false, "message": "Something went wrong. Please try again later."}` |

---

## 11. Logging Architecture & Audit Trail

- **Engine:** Serilog (`Serilog.AspNetCore`).
- **Log Files:** Saved to `Logs/bams-.log` (daily rolling interval, 30-day retention).
- **Console Sink:** Enabled for local development debugging.
- **Correlation ID Tracking:** `CorrelationIdMiddleware` extracts or generates `X-Correlation-ID`. It is attached to `HttpContext.Items`, logged across every request by `RequestLoggingMiddleware`, and included in error responses.
- **Sensitive Data Scrubbing:** Passwords, JWT tokens, and connection strings are strictly omitted from log templates.

---

## 12. Business Logic Mapping

### Savings vs Checking Account Rules
- **Savings Account:** Strictly permits `TransactionMode.Cash`. Attempting a transfer or selecting `TransactionMode.Cheque` throws `InvalidAccountTypeException` (HTTP 422).
- **Checking Account:** Supports both `TransactionMode.Cash` and `TransactionMode.Cheque`. Inter-account transfers are supported exclusively between checking accounts.

### Transaction Atomicity & Concurrency Handling
- Transfers are wrapped in an `IDbContextTransaction`. Debit and credit legs occur atomically; any runtime failure triggers an immediate database rollback.
- Account updates leverage SQL Server `rowversion` concurrency tokens. Race conditions result in `DbUpdateConcurrencyException`, translated to `409 Conflict`.

### Soft Deletion Strategy
- Calling `DELETE /api/v1/accounts/{id}` sets `IsActive = false`. 
- Rows are never physically deleted (`DeleteBehavior.Restrict`), preserving foreign key integrity with historical audit logs. 
- Inactive accounts reject all financial operations with `AccountInactiveException`.

---

## 13. Save & Restore / System Backup & Rehydration

- **Endpoint Contracts:**
  - `GET /api/v1/system/save` (`[Authorize(Roles = "Admin")]`)
  - `POST /api/v1/system/restore` (`[Authorize(Roles = "Admin")]`)
- **Export Process:** Queries `Accounts` and `Transactions` using `.AsNoTracking()`. Formats data into a structured `SystemBackupDto` containing export timestamps, record counts, and payload collections.
- **Restore Process:**
  1. Validates the structural integrity of `SystemBackupDto`.
  2. Initiates `await _context.Database.BeginTransactionAsync()`.
  3. Clears existing `Transactions` and `Accounts` records.
  4. Rehydrates accounts and transactions while preserving relational keys.
  5. Calls `await _context.SaveChangesAsync()` and commits the transaction. If any record fails, the entire restore rolls back.

---

## 14. What This Project Demonstrates

1. **Enterprise C# & .NET 8 Proficiency:** Clean separation of concerns, modern minimal hosting, async/await pipelines, and LINQ queries.
2. **Production-Grade Data Modeling:** EF Core Code-First with migrations, fluent composite indexes, explicit foreign key deletion behaviors, and optimistic concurrency tokens.
3. **Comprehensive Security Implementation:** ASP.NET Core Identity, PBKDF2 password hashing, lockout policies, signed JWT Bearer authentication, and rate limiting.
4. **Reliable Error & Logging Architectures:** Global middleware exception handling, structured JSON error responses, correlation ID tracing, and rolling Serilog file sinks.
5. **Decoupled Modern Frontend:** React 18 SPA with Axios interceptors, JWT lifecycle management, dynamic UI permissions, and responsive CSS modules.

---

## 15. Curriculum Topics Not Implemented in This Project

| Topic | Curriculum Reference | Status | Architectural Reason for Absence |
|:------|:---------------------|:-------|:---------------------------------|
| **SignalR / WebSockets** | Session 17 | `NOT IMPLEMENTED` | *Not found in the current implementation.* Real-time notifications were not required for this banking specification. |
| **Distributed Caching (Redis)** | Session 16 | `NOT IMPLEMENTED` | *Not found in the current implementation.* All queries hit SQL Server directly to ensure real-time financial ledger accuracy. |
| **In-Memory Caching (`IMemoryCache`)** | Session 16 | `NOT IMPLEMENTED` | *Not found in the current implementation.* Financial balances require immediate consistency rather than cached reads. |
| **Server-Side Session & Cookies** | Session 16 | `NOT APPLICABLE` | Stateless REST APIs use JWT Bearer authorization headers instead of server-side sessions or cookies. |
| **Razor Pages** | Session 6 | `NOT APPLICABLE` | The frontend is implemented as a decoupled React 18 SPA. |
| **Automated Unit Testing Projects** | Session 17 | `NOT IMPLEMENTED` | *Not found in the current implementation.* Solution does not contain xUnit/NUnit test projects (tested via automated HTTP scripts). |
| **Docker Containerization** | Session 17 | `NOT IMPLEMENTED` | *Not found in the current implementation.* No `Dockerfile` or `docker-compose.yml` present in repository. |
| **API Versioning NuGet Package** | Session 13 | `PARTIALLY IMPLEMENTED` | URL versioning (`/api/v1/`) is implemented conventionally; `Microsoft.AspNetCore.Mvc.Versioning` package is not used. |

---

## 16. Technical Viva & Interview Questions (With Project Answers)

#### Q1: What is the execution order of your middleware pipeline and why does it matter?
**Answer:** The pipeline executes in strict sequence: `CorrelationIdMiddleware` $\rightarrow$ `ExceptionMiddleware` $\rightarrow$ `RequestLoggingMiddleware` $\rightarrow$ `UseRateLimiter` $\rightarrow$ `UseAuthentication` $\rightarrow$ `UseAuthorization` $\rightarrow$ `MapControllers`. `CorrelationIdMiddleware` must run first so downstream loggers and exception handlers can attach the ID. `ExceptionMiddleware` must wrap all downstream components to catch unhandled errors. `UseAuthentication` must precede `UseAuthorization` so that `HttpContext.User` is populated before permissions are evaluated.

#### Q2: How does your application prevent concurrent balance modification issues?
**Answer:** We implement optimistic concurrency using a `byte[] RowVersion` column on the `Account` entity, mapped via `.IsRowVersion()` to a SQL Server `rowversion` type. SQL Server automatically increments this value on every update. EF Core includes the original `RowVersion` in the `UPDATE ... WHERE AccountId = @id AND RowVersion = @original` statement. If another thread has modified the row, zero rows are affected and EF Core throws a `DbUpdateConcurrencyException`, which `ExceptionMiddleware` translates into an HTTP `409 Conflict`.

#### Q3: Why is `DeleteBehavior.Restrict` configured on Account and Transaction relationships?
**Answer:** In banking systems, financial ledgers must maintain an immutable audit trail. We implement soft deletion (`IsActive = false`). `DeleteBehavior.Restrict` prevents accidental cascade deletion of related transactions or accounts at the database engine level if a delete query is issued.

#### Q4: How is atomicity guaranteed during an inter-account fund transfer?
**Answer:** Fund transfers require two distinct balance adjustments and two transaction rows. In `TransactionService.TransferAsync`, the operation is wrapped in an explicit EF Core transaction via `using var dbTransaction = await _context.Database.BeginTransactionAsync()`. If any step fails (e.g. concurrency violation, constraint failure), the transaction rolls back, leaving balances untouched.

#### Q5: What is the difference between `AddIdentity` and `AddDefaultIdentity` in `Program.cs`?
**Answer:** `AddDefaultIdentity` is a convenience helper tailored for Razor Pages with default UI and does not configure role management infrastructure. We use `builder.Services.AddIdentity<ApplicationUser, IdentityRole>()` because our system requires full role-based authorization (`Admin` vs `User`) across Web API controllers.

#### Q6: How does the application enforce the "Savings accounts are cash-only" business rule?
**Answer:** In `TransactionService.cs`, when processing deposits or withdrawals, the account type is inspected. If `AccountType == Savings` and `TransactionMode != Cash`, an `InvalidAccountTypeException` is thrown. In transfer requests, if either the source or destination account is `Savings`, the operation is rejected.

#### Q7: Why are financial values stored as `decimal(18,2)` instead of `double` or `float`?
**Answer:** Floating-point data types (`float`, `double`) use base-2 binary approximations, causing rounding errors in arithmetic operations. `decimal` uses a base-10 representation with 128-bit precision, guaranteeing exact representation of monetary amounts without rounding discrepancies.

#### Q8: How does your React frontend ensure users cannot access protected views?
**Answer:** The React application wraps protected views inside a `<ProtectedRoute>` component. This component reads the user's authentication token and role from `AuthContext`. If the token is missing, the user is redirected to `/login`. If an endpoint requires an `Admin` role and the user lacks it, access is blocked.

#### Q9: What happens when a user attempts to withdraw more money than their current balance?
**Answer:** `TransactionService.WithdrawAsync` compares the requested amount against `account.Balance`. If requested exceeds available, it throws `InsufficientBalanceException(account.Balance, request.Amount)`. This exception is caught by `ExceptionMiddleware` and returned as an HTTP `422 Unprocessable Entity` with a descriptive error message.

#### Q10: How does the system handle system-wide backup and restore?
**Answer:** `SystemController` exposes `GET /api/v1/system/save` and `POST /api/v1/system/restore`, accessible only to `Admin` users. The export extracts accounts and transactions using `AsNoTracking()` into a structured JSON format. The restore endpoint executes inside an `IDbContextTransaction`, validating the backup payload, clearing existing records, and rehydrating the database atomically.

---

## 17. Project Demonstration Flow

The following 16-step flow demonstrates the end-to-end functionality of the system:

```
Step 1: Application Boot & Database Seeding (Program.cs, DbSeeder.cs)
        --> Serilog bootstraps, EF Core connects, Admin role and default admin user seeded.

Step 2: API & Database Health Verification (GET /health)
        --> Health check probe queries SQL Server; returns HTTP 200 OK.

Step 3: User Registration (POST /api/v1/auth/register)
        --> Client submits RegisterRequest; UserManager hashes password and persists user.

Step 4: User Authentication & JWT Generation (POST /api/v1/auth/login)
        --> Rate limiter validates IP permit; AuthService issues signed JWT Bearer token.

Step 5: Frontend Authorization Handshake (AuthContext.jsx, api.js)
        --> React client receives token, stores it in localStorage, and configures Axios headers.

Step 6: User Profile Retrieval (GET /api/v1/auth/me)
        --> Request passes through UseAuthentication & UseAuthorization; returns user profile.

Step 7: Create Savings Account (POST /api/v1/accounts)
        --> AccountService generates unique account number ACC-YYYYMMDD-XXXX with AccountType = Savings.

Step 8: Create Checking Account (POST /api/v1/accounts)
        --> Creates a second account with AccountType = Checking.

Step 9: Cash Deposit into Savings (POST /api/v1/transactions/deposit)
        --> Validates Cash mode; balance increments and completed Transaction record is inserted.

Step 10: Invalid Transaction Rejection (POST /api/v1/transactions/deposit with Cheque mode on Savings)
         --> Service throws InvalidAccountTypeException; ExceptionMiddleware returns HTTP 422.

Step 11: Cheque Deposit into Checking (POST /api/v1/transactions/deposit)
         --> Validates Checking account; accepts Cheque mode and updates balance.

Step 12: Inter-Account Transfer (POST /api/v1/transactions/transfer)
         --> Checks source and destination checking accounts; executes atomic transfer under IDbContextTransaction.

Step 13: Query Filtered Transaction History (GET /api/v1/transactions/account/{id}?page=1&pageSize=10)
         --> LINQ query applies pagination (Skip/Take) and returns PagedResult<TransactionResponse>.

Step 14: Soft Deactivation of Account (DELETE /api/v1/accounts/{id})
         --> Sets IsActive = false; database record preserved to maintain audit trail.

Step 15: Deactivated Account Protection (POST /api/v1/transactions/withdraw on deactivated account)
         --> Throws AccountInactiveException; ExceptionMiddleware returns HTTP 422.

Step 16: System Backup Export & Atomic Restore (GET /api/v1/system/save & POST /api/v1/system/restore)
         --> Admin exports complete database state to JSON and rehydrates it inside a transaction.
```

---

## 18. Final Concept Scorecard

| Module / Area | Verified Status | Key Code References |
|:--------------|:---------------:|:--------------------|
| .NET 8 & C# Fundamentals | `IMPLEMENTED` | Async/await, LINQ, records, pattern matching in `ExceptionMiddleware.cs`. |
| ASP.NET Core Minimal Hosting | `IMPLEMENTED` | `WebApplicationBuilder`, `Program.cs`, Kestrel, `launchSettings.json`. |
| Middleware Architecture | `IMPLEMENTED` | Custom `CorrelationIdMiddleware`, `ExceptionMiddleware`, `RequestLoggingMiddleware`. |
| Dependency Injection | `IMPLEMENTED` | Scoped service registrations, constructor injection throughout controllers and services. |
| Configuration & Logging | `IMPLEMENTED` | `IOptions<JwtSettings>`, Serilog console and rolling file sinks under `Logs/`. |
| Routing & Controllers | `IMPLEMENTED` | `[Route("api/v1/[controller]")]`, `ActionResult<T>`, `[ApiController]`. |
| Model Binding & Validation | `IMPLEMENTED` | `[FromBody]`, `[FromQuery]`, Data Annotations (`[Required]`, `[Range]`). |
| EF Core 8 & Migrations | `IMPLEMENTED` | Code-First migrations, `ApplicationDbContext`, Fluent API mappings. |
| Entity Relationships & LINQ | `IMPLEMENTED` | 1-to-many, self-referencing foreign keys, `Include()`, `AsNoTracking()`. |
| Transactions & Concurrency | `IMPLEMENTED` | `IDbContextTransaction`, `RowVersion` concurrency tokens, `DbUpdateConcurrencyException`. |
| ASP.NET Core Identity | `IMPLEMENTED` | `IdentityDbContext<ApplicationUser>`, password hashing, lockout policies. |
| Authentication & Authorization | `IMPLEMENTED` | JWT Bearer authentication, role-based `[Authorize(Roles = "Admin")]`. |
| Web API REST Standards | `IMPLEMENTED` | HTTP verbs, DTO pattern, standard status codes (200, 201, 400, 401, 403, 404, 409, 422, 500). |
| Performance & Security | `IMPLEMENTED` | Fixed-window rate limiting on login, `/health` checks, SQL injection prevention. |
| React 18 Single Page App | `IMPLEMENTED` | React Router v6, Axios interceptors, `AuthContext`, CSS Modules. |
| Caching (Redis / In-Memory) | `NOT IMPLEMENTED` | *Not found in current implementation.* Real-time SQL consistency required. |
| Real-time Communication (SignalR) | `NOT IMPLEMENTED` | *Not found in current implementation.* Real-time hubs omitted from specification. |
| Unit Testing & Containerization | `NOT IMPLEMENTED` | *Not found in current implementation.* No xUnit project or Dockerfile present. |

---
*Report compiled automatically through source code inspection of the BankAccountManagementSystem repository.*
