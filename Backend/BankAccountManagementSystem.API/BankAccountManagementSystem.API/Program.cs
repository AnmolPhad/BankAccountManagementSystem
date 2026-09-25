using System.Text;
using BankAccountManagementSystem.API.Configuration;
using BankAccountManagementSystem.API.Data;
using BankAccountManagementSystem.API.Middleware;
using BankAccountManagementSystem.API.Models;
using BankAccountManagementSystem.API.Services.Implementations;
using BankAccountManagementSystem.API.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ════════════════════════════════════════════════════════════════════════════
// 1. DATABASE — Entity Framework Core + SQL Server
// ════════════════════════════════════════════════════════════════════════════
// We read the connection string from appsettings.json → "ConnectionStrings:DefaultConnection"
// Never hard-code the connection string here.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ════════════════════════════════════════════════════════════════════════════
// 2. ASP.NET CORE IDENTITY
// ════════════════════════════════════════════════════════════════════════════
// Identity manages: users, roles, password hashing, lockout, claims.
// AddIdentity registers: UserManager<T>, RoleManager<T>, SignInManager<T>
// We use AddIdentity (not AddDefaultIdentity) because we need Roles support.
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // ─── Password rules ───────────────────────────────────────────────────
    options.Password.RequireDigit = true;           // Must contain a number
    options.Password.RequireLowercase = true;        // Must have a-z
    options.Password.RequireUppercase = true;        // Must have A-Z
    options.Password.RequireNonAlphanumeric = true;  // Must have !@#$ etc.
    options.Password.RequiredLength = 8;             // Min 8 characters

    // ─── User rules ───────────────────────────────────────────────────────
    options.User.RequireUniqueEmail = true;          // No duplicate emails

    // ─── Lockout rules ────────────────────────────────────────────────────
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()  // Use our DB context for storage
.AddDefaultTokenProviders();                        // Enable password reset tokens etc.

// ════════════════════════════════════════════════════════════════════════════
// 3. JWT SETTINGS — Bind from appsettings.json
// ════════════════════════════════════════════════════════════════════════════
// This binds the "JwtSettings" section to our JwtSettings class.
// Services can now inject IOptions<JwtSettings> to access these values.
builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection("JwtSettings"));

// ════════════════════════════════════════════════════════════════════════════
// 4. JWT BEARER AUTHENTICATION
// ════════════════════════════════════════════════════════════════════════════
// This middleware validates JWT tokens on every incoming request.
// When a request hits [Authorize], ASP.NET Core checks the Authorization header,
// extracts the Bearer token, and validates it using these settings.
var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>()!;
var keyBytes = Encoding.UTF8.GetBytes(jwtSettings.SecretKey);

builder.Services.AddAuthentication(options =>
{
    // Set JWT Bearer as the default scheme for both authentication and challenge.
    // "Challenge" is what happens when an unauthenticated user hits [Authorize].
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        // Validate that the token was issued by our API (Issuer check)
        ValidateIssuer = true,
        ValidIssuer = jwtSettings.Issuer,

        // Validate that the token is intended for our client (Audience check)
        ValidateAudience = true,
        ValidAudience = jwtSettings.Audience,

        // Validate the token's expiry time
        ValidateLifetime = true,

        // Validate the signature using our secret key
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(keyBytes),

        // Remove the default 5-minute clock skew tolerance
        // (optional — keeps token expiry exact)
        ClockSkew = TimeSpan.Zero
    };
});

// ════════════════════════════════════════════════════════════════════════════
// 5. DEPENDENCY INJECTION — Register application services
// ════════════════════════════════════════════════════════════════════════════
// We register interfaces → implementations using Scoped lifetime.
// Scoped = one instance per HTTP request (correct for most web services).
// This way, controllers depend on abstractions (IAuthService), not concrete classes.
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<ITransactionService, TransactionService>();
builder.Services.AddScoped<ISystemBackupService, SystemBackupService>();

// ════════════════════════════════════════════════════════════════════════════
// 6. MVC + API CONTROLLERS
// ════════════════════════════════════════════════════════════════════════════
// AddControllersWithViews → supports both MVC (Razor Views) and API controllers.
// This preserves the existing HomeController + Razor Views.
builder.Services.AddControllersWithViews();

// ════════════════════════════════════════════════════════════════════════════
// 7. SWAGGER / OPENAPI
// ════════════════════════════════════════════════════════════════════════════
// Swagger generates an interactive API documentation page.
// We configure it to support JWT Bearer tokens so you can test protected endpoints.
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Bank Account Management System API",
        Version = "v1",
        Description = "Phase 1: User Authentication & Management"
    });

    // ─── Add JWT Bearer security definition ───────────────────────────────
    // This adds the "Authorize" button in Swagger UI.
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token.\n\nExample: eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
    });

    // ─── Apply security globally to all API endpoints ─────────────────────
    // This tells Swagger to send the Bearer token on every request.
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ════════════════════════════════════════════════════════════════════════════
// BUILD
// ════════════════════════════════════════════════════════════════════════════
var app = builder.Build();

// ════════════════════════════════════════════════════════════════════════════
// 8. SEED DATABASE (roles + admin user)
// ════════════════════════════════════════════════════════════════════════════
// We call the seeder after the app is built so DI services are available.
// CreateScope() ensures the seeder gets its own scoped service lifetime.
using (var scope = app.Services.CreateScope())
{
    await DbSeeder.SeedAsync(scope.ServiceProvider);
}

// ════════════════════════════════════════════════════════════════════════════
// 9. MIDDLEWARE PIPELINE
// ════════════════════════════════════════════════════════════════════════════
// ORDER MATTERS in ASP.NET Core middleware pipeline.
// Each middleware calls next() to pass the request down the chain.

// Global exception handler — must be FIRST so it wraps everything
app.UseMiddleware<ExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    // Swagger is only available in Development for security.
    // In production, you would disable or password-protect this.
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "BAMS API v1");
        // Optional: make Swagger the root page during development
        // options.RoutePrefix = string.Empty;
    });
}
else
{
    // Production error handling via the MVC Error action
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Authentication must come BEFORE Authorization in the pipeline.
// UseAuthentication → reads the token and populates HttpContext.User
// UseAuthorization  → checks [Authorize] attributes using HttpContext.User
app.UseAuthentication();
app.UseAuthorization();

// ─── MVC route for Razor Views (keeps existing HomeController working) ────
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// ─── API controllers are discovered automatically via [Route] attributes ──
app.MapControllers();

app.Run();

