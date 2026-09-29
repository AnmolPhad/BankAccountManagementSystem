# BankAccountManagementSystem — Technical File & Code Symbol Reference

> **Quick Reference Guide:** Concept-to-File-to-Symbol mapping across the Bank Account Management System codebase.  
> **Use Case:** Instant lookup during code reviews, debugging, viva examinations, and technical discussions.

---

## 1. Backend Startup, Hosting & Middleware

| Technical Concept | Source File | Class / Struct / Interface | Method / Symbol | Detailed Technical Explanation |
|:------------------|:------------|:---------------------------|:----------------|:--------------------------------|
| **Kestrel & Startup** | `Program.cs` | Top-level Statements | `WebApplication.CreateBuilder` | Configures host, Kestrel web server, and registers service dependencies. |
| **Serilog Bootstrap** | `Program.cs` | `Serilog.Log` | `CreateBootstrapLogger()` | Early-stage logger to capture errors occurring before DI is ready. |
| **Serilog File Sink** | `Program.cs` | `builder.Host.UseSerilog` | `.WriteTo.File()` | Configures daily rolling file logging to `Logs/bams-.log` with 30-day retention. |
| **Scoped Service DI** | `Program.cs` | `builder.Services` | `.AddScoped<TInterface, TImpl>()` | Registers domain services with scoped lifetime (one instance per HTTP request). |
| **Identity Registration**| `Program.cs` | `builder.Services` | `.AddIdentity<ApplicationUser, IdentityRole>()` | Registers ASP.NET Core Identity user, role, and password managers with DB stores. |
| **JWT Bearer Options** | `Program.cs` | `builder.Services` | `.AddJwtBearer()` | Configures `TokenValidationParameters` (issuer, audience, symmetric signing key, zero clock skew). |
| **Rate Limiter Policy** | `Program.cs` | `builder.Services` | `.AddRateLimiter()` | Configures fixed-window limiter `"login"` (10 requests/min per remote IP). |
| **Health Checks** | `Program.cs` | `builder.Services` | `.AddHealthChecks().AddDbContextCheck()` | Registers `/health` endpoint to monitor API and SQL Server connectivity. |
| **Correlation ID Tracking** | `Middleware/CorrelationIdMiddleware.cs` | `CorrelationIdMiddleware` | `InvokeAsync(HttpContext context)` | Extracts or generates `X-Correlation-ID`, attaches to `HttpContext.Items` and response headers. |
| **Global Error Handling** | `Middleware/ExceptionMiddleware.cs` | `ExceptionMiddleware` | `InvokeAsync`, `HandleExceptionAsync` | Global try-catch middleware; maps domain/EF exceptions to HTTP 400/401/404/409/422/500 JSON responses. |
| **HTTP Request Telemetry** | `Middleware/RequestLoggingMiddleware.cs` | `RequestLoggingMiddleware` | `InvokeAsync(HttpContext context)` | Measures HTTP request duration using `Stopwatch` and logs status code and duration. |
| **Database Seeding** | `Data/DbInitializer.cs` | `DbSeeder` | `SeedAsync(IServiceProvider serviceProvider)` | Seeds `Admin` and `User` roles and provisions default administrator account. |

---

## 2. Data Access Layer & Entity Models

| Technical Concept | Source File | Class / Struct / Interface | Method / Symbol | Detailed Technical Explanation |
|:------------------|:------------|:---------------------------|:----------------|:--------------------------------|
| **Database Context** | `Data/ApplicationDbContext.cs` | `ApplicationDbContext` | `base(options)` | Inherits from `IdentityDbContext<ApplicationUser>`, binds DbSets to SQL Server. |
| **Fluent API Mapping** | `Data/ApplicationDbContext.cs` | `ApplicationDbContext` | `OnModelCreating(ModelBuilder builder)` | Configures column types (`decimal(18,2)`), unique indexes, and foreign key restrict behaviors. |
| **Concurrency Token** | `Data/ApplicationDbContext.cs` | `ApplicationDbContext` | `entity.Property(a => a.RowVersion).IsRowVersion()` | Maps `RowVersion` property to SQL Server `rowversion` column for optimistic locking. |
| **User Entity** | `Models/ApplicationUser.cs` | `ApplicationUser` | `FirstName`, `LastName`, `EmployeeCode` | Extends `IdentityUser`; adds employee code and navigation property to `Accounts`. |
| **Account Entity** | `Models/Account.cs` | `Account` | `AccountId`, `AccountNumber`, `Balance`, `RowVersion` | Core domain entity representing customer accounts with optimistic concurrency token. |
| **Account Type Enum** | `Models/AccountType.cs` | `AccountType` | `Savings = 0`, `Checking = 1` | Enumeration distinguishing Savings and Checking accounts. |
| **Transaction Entity** | `Models/Transaction.cs` | `Transaction` | `TransactionId`, `AccountId`, `RelatedAccountId`, `Amount` | Financial ledger entity tracking every deposit, withdrawal, and transfer. |
| **Transaction Type Enum** | `Models/TransactionType.cs` | `TransactionType` | `Deposit = 0`, `Withdrawal = 1`, `Transfer = 2` | Enumeration indicating transaction nature. |
| **Transaction Mode Enum** | `Models/TransactionMode.cs` | `TransactionMode` | `Cash = 0`, `Cheque = 1` | Enumeration distinguishing cash and cheque payment instruments. |
| **Transaction Status Enum** | `Models/TransactionStatus.cs` | `TransactionStatus` | `Pending = 0`, `Completed = 1`, `Failed = 2` | Financial transaction lifecycle states. |

---

## 3. Business Exceptions & Error Handling

| Technical Concept | Source File | Class / Struct / Interface | Method / Symbol | Detailed Technical Explanation |
|:------------------|:------------|:---------------------------|:----------------|:--------------------------------|
| **Insufficient Funds** | `Exceptions/BankingExceptions.cs` | `InsufficientBalanceException` | `AvailableBalance`, `RequestedAmount` | Thrown when withdrawal or transfer exceeds available balance; maps to HTTP 422. |
| **Inactive Account** | `Exceptions/BankingExceptions.cs` | `AccountInactiveException` | `AccountNumber` | Thrown when operations are attempted on a deactivated account; maps to HTTP 422. |
| **Invalid Operation** | `Exceptions/BankingExceptions.cs` | `InvalidTransactionException` | Constructor with message | Thrown for domain violations (e.g. transfer to same account); maps to HTTP 422. |
| **Invalid Account Type**| `Exceptions/BankingExceptions.cs` | `InvalidAccountTypeException` | Constructor with message | Thrown when cheque or transfer is attempted on a Savings account; maps to HTTP 422. |

---

## 4. Authentication, Users & Security

| Technical Concept | Source File | Class / Struct / Interface | Method / Symbol | Detailed Technical Explanation |
|:------------------|:------------|:---------------------------|:----------------|:--------------------------------|
| **Auth Controller** | `Controllers/AuthController.cs` | `AuthController` | `Register`, `Login`, `GetCurrentUser` | Exposes endpoints for registration, rate-limited login, and user profile retrieval. |
| **Auth Service Contract** | `Services/Interfaces/IAuthService.cs` | `IAuthService` | `RegisterAsync`, `LoginAsync`, `GenerateJwtToken` | Abstraction for authentication workflows. |
| **Auth Service Logic** | `Services/Implementations/AuthService.cs` | `AuthService` | `LoginAsync`, `GenerateJwtToken` | Validates credentials via `UserManager` and issues HMAC-SHA256 signed JWT tokens. |
| **User Controller (Admin)**| `Controllers/UsersController.cs` | `UsersController` | `GetUsers`, `CreateUser`, `AssignRole` | Administrative endpoints decorated with `[Authorize(Roles = "Admin")]`. |
| **User Service** | `Services/Implementations/UserService.cs` | `UserService` | `GetUsersAsync`, `AssignRoleAsync` | CRUD operations for managing user records and role claims. |
| **JWT Options Class** | `Configuration/JwtSettings.cs` | `JwtSettings` | `SecretKey`, `Issuer`, `Audience`, `ExpiryInMinutes` | POCO bound to `appsettings.json` section for strong typing. |

---

## 5. Accounts Domain

| Technical Concept | Source File | Class / Struct / Interface | Method / Symbol | Detailed Technical Explanation |
|:------------------|:------------|:---------------------------|:----------------|:--------------------------------|
| **Accounts Controller** | `Controllers/AccountsController.cs` | `AccountsController` | `CreateAccount`, `GetAccounts`, `DeactivateAccount` | Exposes endpoints for opening, retrieving, and soft-deactivating accounts. |
| **Account Service Contract** | `Services/Interfaces/IAccountService.cs` | `IAccountService` | `CreateAccountAsync`, `GetAccountsAsync` | Interface declaring banking account lifecycle operations. |
| **Account Business Logic** | `Services/Implementations/AccountService.cs` | `AccountService` | `CreateAccountAsync` | Formats account number (`ACC-YYYYMMDD-XXXX`), validates ownership, saves entity. |
| **Soft Deactivation** | `Services/Implementations/AccountService.cs` | `AccountService` | `DeactivateAccountAsync` | Sets `account.IsActive = false`, preserving transaction history foreign keys. |
| **Account Request DTO** | `DTOs/Accounts/CreateAccountRequest.cs` | `CreateAccountRequest` | `AccountType`, `InitialDeposit` | Validated request payload for opening a new account. |

---

## 6. Transactions Domain

| Technical Concept | Source File | Class / Struct / Interface | Method / Symbol | Detailed Technical Explanation |
|:------------------|:------------|:---------------------------|:----------------|:--------------------------------|
| **Transactions Controller** | `Controllers/TransactionsController.cs` | `TransactionsController` | `Deposit`, `Withdraw`, `Transfer`, `GetHistory` | REST endpoints for financial transactions and transaction query filtering. |
| **Transaction Contract** | `Services/Interfaces/ITransactionService.cs` | `ITransactionService` | `DepositAsync`, `WithdrawAsync`, `TransferAsync` | Interface defining financial ledger operations and query filters. |
| **Cash/Cheque Deposit** | `Services/Implementations/TransactionService.cs` | `TransactionService` | `DepositAsync` | Validates Savings cash-only rule, credits balance, creates transaction log. |
| **Cash/Cheque Withdrawal**| `Services/Implementations/TransactionService.cs` | `TransactionService` | `WithdrawAsync` | Validates active status and balance; debits funds and logs transaction. |
| **Atomic Transfer** | `Services/Implementations/TransactionService.cs` | `TransactionService` | `TransferAsync` | Enclosed in `BeginTransactionAsync`; validates Checking accounts, updates balances, logs dual legs. |
| **History & Pagination** | `Services/Implementations/TransactionService.cs` | `TransactionService` | `GetAccountTransactionsAsync` | Uses LINQ `.Skip()` and `.Take()` to provide paginated `PagedResult<TransactionResponse>`. |
| **Deposit Request DTO** | `DTOs/Transactions/DepositRequest.cs` | `DepositRequest` | `AccountId`, `Amount`, `Mode` | Data annotation validated request object for deposits. |
| **Withdrawal Request DTO**| `DTOs/Transactions/WithdrawalRequest.cs` | `WithdrawalRequest` | `AccountId`, `Amount`, `Mode` | Validated request object for withdrawals. |
| **Transfer Request DTO** | `DTOs/Transactions/TransferRequest.cs` | `TransferRequest` | `SourceAccountId`, `DestinationAccountId`, `Amount` | Validated request object for inter-account transfers. |
| **Query Parameters DTO** | `DTOs/Transactions/TransactionHistoryQuery.cs`| `TransactionHistoryQuery` | `Page`, `PageSize`, `StartDate`, `EndDate` | Query parameters for transaction history filtering. |

---

## 7. System Backup & Rehydration Domain

| Technical Concept | Source File | Class / Struct / Interface | Method / Symbol | Detailed Technical Explanation |
|:------------------|:------------|:---------------------------|:----------------|:--------------------------------|
| **System Controller** | `Controllers/SystemController.cs` | `SystemController` | `SaveSystem`, `RestoreSystem` | Administrative endpoints (`[Authorize(Roles = "Admin")]`) for backup/restore. |
| **Backup Contract** | `Services/Interfaces/ISystemBackupService.cs` | `ISystemBackupService` | `ExportSystemBackupAsync`, `RestoreSystemAsync` | Abstraction for system save and restore operations. |
| **System Export Logic** | `Services/Implementations/SystemBackupService.cs`| `SystemBackupService` | `ExportSystemBackupAsync` | Reads all accounts and transactions with `AsNoTracking()` into `SystemBackupDto`. |
| **Atomic Rehydration** | `Services/Implementations/SystemBackupService.cs`| `SystemBackupService` | `RestoreSystemAsync` | Begins EF Core transaction, truncates existing data, rehydrates backup atomically. |
| **System Backup DTO** | `DTOs/System/SystemBackupDto.cs` | `SystemBackupDto` | `ExportedAt`, `Version`, `Accounts`, `Transactions` | Complete system export payload structure. |
| **Restore Response DTO**| `DTOs/System/RestoreResponseDto.cs` | `RestoreResponseDto` | `Success`, `AccountsRestored`, `TransactionsRestored` | Confirmation report after system rehydration. |

---

## 8. Frontend Client Architecture (React 18 SPA)

| Technical Concept | Source File | Component / Hook / Function | Detailed Technical Explanation |
|:------------------|:------------|:----------------------------|:--------------------------------|
| **Axios Instance & Interceptor** | `Frontend/src/api.js` | `api` (Axios instance) | Injects Bearer token into headers; catches 401 errors and redirects to `/login`. |
| **Auth State Provider** | `Frontend/src/context/AuthContext.jsx` | `AuthContext`, `AuthProvider` | Manages `user`, `token`, `role`, and persists session in `localStorage`. |
| **Custom Auth Hook** | `Frontend/src/context/AuthContext.jsx` | `useAuth()` | Custom React hook providing access to authentication state. |
| **Route Authorization Guard** | `Frontend/src/components/ProtectedRoute.jsx`| `ProtectedRoute` | Evaluates token and role; redirects unauthorized users to `/login`. |
| **Dashboard Layout** | `Frontend/src/layouts/DashboardLayout.jsx` | `DashboardLayout` | Renders shared sidebar navigation, header, and outlet for nested page routes. |
| **Navigation Component** | `Frontend/src/components/BankingNavigation.jsx`| `BankingNavigation` | Renders role-aware links (Dashboard, Accounts, Transactions, Backup & Restore). |

---

## 9. Frontend Pages & Views

| Page / Feature | Source File | Key Functions / State Handlers | Description |
|:---------------|:------------|:-------------------------------|:------------|
| **Login View** | `pages/LoginPage.jsx` | `handleSubmit`, `loading`, `error` | Form handling login submission; invokes `login()` from `AuthContext`. |
| **Registration View** | `pages/RegisterPage.jsx` | `handleSubmit`, `formData` | Registers new user and redirects to login upon success. |
| **Dashboard View** | `pages/DashboardPage.jsx` | `useEffect`, `accounts`, `stats` | Displays summary of accounts, quick balance totals, and recent activity. |
| **Accounts List** | `pages/AccountsPage.jsx` | `fetchAccounts`, `handleDeactivate` | Lists user accounts with soft-deactivation buttons and status badges. |
| **Open Account** | `pages/AddAccountPage.jsx` | `handleSubmit`, `accountType` | Form to create a Savings or Checking account. |
| **Account Details** | `pages/AccountDetailsPage.jsx` | `fetchDetails`, `balance` | Displays single account information and linked transactions. |
| **Deposit View** | `pages/DepositPage.jsx` | `handleSubmit`, `mode` (Cash/Cheque) | Form for deposits; disables Cheque option if account is Savings. |
| **Withdrawal View** | `pages/WithdrawPage.jsx` | `handleSubmit`, `amount` | Form for cash or cheque withdrawals. |
| **Transfer View** | `pages/TransferPage.jsx` | `sourceId`, `destId`, `amount` | Inter-account transfer interface between checking accounts. |
| **Transaction History** | `pages/TransactionHistoryPage.jsx` | `page`, `pageSize`, `filter` | Paginated transaction ledger with date and type filtering. |
| **Backup & Restore** | `pages/BackupRestorePage.jsx` | `handleExport`, `handleRestore` | Admin interface for exporting and uploading system backup JSON files. |
| **User Profile** | `pages/ProfilePage.jsx` | `user`, `employeeCode` | Displays authenticated user details and assigned roles. |

---
*Reference index verifying complete codebase architecture and symbol mapping.*
