// Program.cs
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using GameRealmAPI.Data;
using GameRealmAPI.Helpers;
using GameRealmAPI.Services;

var builder = WebApplication.CreateBuilder(args);

var cs = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string missing");

// ── DATABASE — AddDbContextPool only (not both) ───────────────────
builder.Services.AddDbContextPool<AppDbContext>(options =>
    options.UseMySql(cs, ServerVersion.AutoDetect(cs), mysql =>
    {
        mysql.CommandTimeout(30);
        mysql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);
        mysql.UseQuerySplittingBehavior(QuerySplittingBehavior.SingleQuery);
    })
    .EnableSensitiveDataLogging(builder.Environment.IsDevelopment())
    .EnableDetailedErrors(builder.Environment.IsDevelopment()),
    poolSize: 128
);

// ── JWT ───────────────────────────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("JWT Key not configured");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });
builder.Services.AddAuthorization();

// ── RATE LIMITING ─────────────────────────────────────────────────
builder.Services.AddRateLimiter(o =>
{
    o.AddFixedWindowLimiter("login", x => { x.PermitLimit = 5; x.Window = TimeSpan.FromSeconds(30); x.QueueLimit = 0; });
    o.AddFixedWindowLimiter("register", x => { x.PermitLimit = 3; x.Window = TimeSpan.FromSeconds(10); x.QueueLimit = 0; });
    o.AddFixedWindowLimiter("api", x => { x.PermitLimit = 120; x.Window = TimeSpan.FromSeconds(60); x.QueueLimit = 2; });
    o.RejectionStatusCode = 429;
    o.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.ContentType = "application/json";
        await ctx.HttpContext.Response.WriteAsync(
            "{\"success\":false,\"message\":\"Too many requests. Please slow down.\"}", ct);
    };
});

// ── HTTP CLIENT ───────────────────────────────────────────────────
builder.Services.AddHttpClient("paypal", c => c.Timeout = TimeSpan.FromSeconds(30));

// ── OUTPUT CACHE ──────────────────────────────────────────────────
builder.Services.AddOutputCache(o =>
{
    o.AddBasePolicy(b => b.Cache());
    o.AddPolicy("wiki", b => b.Expire(TimeSpan.FromMinutes(5)));
    o.AddPolicy("market", b => b.Expire(TimeSpan.FromSeconds(30)));
    o.AddPolicy("news", b => b.Expire(TimeSpan.FromMinutes(2)));
    o.AddPolicy("stats", b => b.Expire(TimeSpan.FromMinutes(3)));
});

// ── RESPONSE COMPRESSION ──────────────────────────────────────────
builder.Services.AddResponseCompression(o => o.EnableForHttps = true);

// ── CORS ──────────────────────────────────────────────────────────
builder.Services.AddCors(o => o.AddPolicy("VueFrontend", p =>
    p.WithOrigins(
        "http://localhost:5173",
        "http://localhost:3000",
        "https://yourgame.com"
    ).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

// ── SERVICES ──────────────────────────────────────────────────────
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<GameService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<StoreService>();
builder.Services.AddScoped<PayPalService>();
builder.Services.AddScoped<MarketService>();
builder.Services.AddScoped<NewsService>();
builder.Services.AddScoped<WikiService>();
builder.Services.AddSingleton<JwtHelper>();

builder.Services.AddControllers();

// ── SWAGGER ───────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "GameRealm API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Enter: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement {{
        new OpenApiSecurityScheme {
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
        }, Array.Empty<string>()
    }});
});

// ── BUILD ─────────────────────────────────────────────────────────
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseResponseCompression();
app.UseCors("VueFrontend");
app.UseRateLimiter();
app.UseOutputCache();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ── MIGRATE + SEED ────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    await db.Database.MigrateAsync();
    logger.LogInformation("Migrations applied");

    var store = scope.ServiceProvider.GetRequiredService<StoreService>();
    var market = scope.ServiceProvider.GetRequiredService<MarketService>();
    var news = scope.ServiceProvider.GetRequiredService<NewsService>();
    var wiki = scope.ServiceProvider.GetRequiredService<WikiService>();

    await store.SeedDefaultDataAsync();
    await market.SeedMarketDataAsync();
    await news.SeedNewsAsync();
    await wiki.SeedWikiAsync();
}

app.Run();