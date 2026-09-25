using BankAccountManagementSystem.API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BankAccountManagementSystem.API.Data
{
    /// <summary>
    /// The central database context for the application.
    ///
    /// We inherit from IdentityDbContext&lt;ApplicationUser&gt; instead of DbContext.
    ///
    /// IdentityDbContext automatically creates and manages the following tables:
    ///   - AspNetUsers          → stores ApplicationUser rows
    ///   - AspNetRoles          → stores role definitions (Admin, User)
    ///   - AspNetUserRoles      → many-to-many: which user has which role
    ///   - AspNetUserClaims     → optional claims attached to users
    ///   - AspNetRoleClaims     → optional claims attached to roles
    ///   - AspNetUserLogins     → external login providers (Google, Facebook, etc.)
    ///   - AspNetUserTokens     → tokens (e.g., password reset, 2FA)
    ///
    /// Phase 2: Added Accounts table.
    /// Phase 3: Added Transactions table and RowVersion concurrency token on Account.
    /// </summary>
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        /// <summary>
        /// Constructor receives DbContextOptions injected by ASP.NET Core DI.
        /// This is how EF Core knows the connection string and provider (SQL Server).
        /// </summary>
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        /// <summary>
        /// Bank accounts managed by the system.
        /// </summary>
        public DbSet<Account> Accounts => Set<Account>();

        /// <summary>
        /// Financial transactions recorded against accounts.
        /// Each Deposit, Withdrawal, or Transfer leg is its own row.
        /// </summary>
        public DbSet<Transaction> Transactions => Set<Transaction>();

        /// <summary>
        /// Override OnModelCreating to apply custom configurations.
        /// Always call base.OnModelCreating(builder) first so Identity
        /// can configure its own tables before we add our customizations.
        /// </summary>
        protected override void OnModelCreating(ModelBuilder builder)
        {
            // IMPORTANT: Must call base first.
            // This lets Identity configure its tables, indexes, and FK constraints.
            base.OnModelCreating(builder);

            // ─── ApplicationUser custom column constraints ──────────────────
            builder.Entity<ApplicationUser>(entity =>
            {
                // EmployeeCode must be unique across all users.
                entity.HasIndex(u => u.EmployeeCode)
                      .IsUnique()
                      .HasDatabaseName("IX_AspNetUsers_EmployeeCode");

                entity.Property(u => u.EmployeeCode)
                      .HasMaxLength(50)
                      .IsRequired();

                entity.Property(u => u.FirstName)
                      .HasMaxLength(100)
                      .IsRequired();

                entity.Property(u => u.LastName)
                      .HasMaxLength(100)
                      .IsRequired();
            });

            // ─── Account configuration ──────────────────────────────────────
            builder.Entity<Account>(entity =>
            {
                entity.HasKey(a => a.AccountId);

                entity.Property(a => a.AccountNumber)
                      .HasMaxLength(20)
                      .IsRequired();

                entity.HasIndex(a => a.AccountNumber)
                      .IsUnique()
                      .HasDatabaseName("IX_Accounts_AccountNumber");

                // Balance: decimal(18,2) — never float or double for money
                entity.Property(a => a.Balance)
                      .HasColumnType("decimal(18,2)")
                      .HasDefaultValue(0.00m)
                      .IsRequired();

                entity.Property(a => a.AccountType)
                      .IsRequired();

                entity.Property(a => a.IsActive)
                      .HasDefaultValue(true)
                      .IsRequired();

                entity.Property(a => a.CreatedAt)
                      .IsRequired();

                // RowVersion: SQL Server rowversion column used as an EF Core concurrency token.
                // IsRowVersion() maps this to the SQL Server "rowversion" (timestamp) data type.
                // SQL Server automatically increments it on every UPDATE to the row.
                // EF Core includes it in: UPDATE Accounts SET ... WHERE AccountId = @id AND RowVersion = @original
                // If another request updated the row first (changed RowVersion), EF gets 0 rows affected
                // and throws DbUpdateConcurrencyException → ExceptionMiddleware returns 409 Conflict.
                // This is OPTIMISTIC concurrency: no lock is held during read; conflict is detected at write.
                entity.Property(a => a.RowVersion)
                      .IsRowVersion();

                // Relationship: ApplicationUser (1) <-> (*) Account
                // Restrict: no cascade delete — accounts survive user soft-deletes.
                entity.HasOne(a => a.User)
                      .WithMany(u => u.Accounts)
                      .HasForeignKey(a => a.UserId)
                      .OnDelete(DeleteBehavior.Restrict)
                      .IsRequired();
            });

            // ─── Transaction configuration ──────────────────────────────────
            builder.Entity<Transaction>(entity =>
            {
                entity.HasKey(t => t.TransactionId);

                // Amount: always decimal(18,2)
                entity.Property(t => t.Amount)
                      .HasColumnType("decimal(18,2)")
                      .IsRequired();

                entity.Property(t => t.TransactionType)
                      .IsRequired();

                entity.Property(t => t.TransactionMode)
                      .IsRequired();

                entity.Property(t => t.Status)
                      .IsRequired();

                // ReferenceNumber: server-generated unique reference
                // Format: TXN-YYYYMMDD-XXXXXXXX (max ~26 chars; 30 provides a safe margin)
                entity.Property(t => t.ReferenceNumber)
                      .HasMaxLength(30)
                      .IsRequired();

                entity.HasIndex(t => t.ReferenceNumber)
                      .IsUnique()
                      .HasDatabaseName("IX_Transactions_ReferenceNumber");

                entity.Property(t => t.TransactionDate)
                      .IsRequired();

                // ─── Indexes for Transaction History Queries ────────────────────────
                // Index on TransactionDate for global ordering and cross-account queries
                entity.HasIndex(t => t.TransactionDate)
                      .HasDatabaseName("IX_Transactions_TransactionDate");

                // Composite index on (AccountId, TransactionDate) for account-specific history queries
                entity.HasIndex(t => new { t.AccountId, t.TransactionDate })
                      .HasDatabaseName("IX_Transactions_AccountId_TransactionDate");

                entity.Property(t => t.Description)
                      .HasMaxLength(500);

                // ─── Primary FK: Transaction → Account (the owning account) ─────────
                // Every transaction row belongs to exactly one account (Deposit → that account,
                // Withdrawal → that account, Transfer source → source account,
                // Transfer destination → destination account).
                //
                // DeleteBehavior.Restrict:
                //   We never physically delete accounts (soft delete only), so this FK is safe.
                //   Restrict is defence-in-depth: if anyone ever tried to hard-delete an account,
                //   the database engine would reject it rather than cascade-delete the audit trail.
                entity.HasOne(t => t.Account)
                      .WithMany(a => a.Transactions)
                      .HasForeignKey(t => t.AccountId)
                      .OnDelete(DeleteBehavior.Restrict)
                      .IsRequired();

                // ─── Secondary FK: Transaction → RelatedAccount (Transfer only) ────
                // RelatedAccountId is nullable — null for Deposit and Withdrawal.
                // For a Transfer, the source row has RelatedAccountId = destination,
                // and the destination row has RelatedAccountId = source.
                //
                // We CANNOT configure .WithMany(a => a.Transactions) here because that
                // inverse navigation is already taken by the primary FK above.
                // Instead we use a separate unidirectional FK (no inverse navigation property).
                //
                // DeleteBehavior.Restrict: same safety rationale as the primary FK.
                entity.HasOne(t => t.RelatedAccount)
                      .WithMany()
                      .HasForeignKey(t => t.RelatedAccountId)
                      .OnDelete(DeleteBehavior.Restrict)
                      .IsRequired(false);
            });
        }
    }
}
