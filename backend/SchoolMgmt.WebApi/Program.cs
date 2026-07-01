using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SchoolMgmt.Application;
using SchoolMgmt.Application.Auth;
using SchoolMgmt.Infrastructure;
using SchoolMgmt.Infrastructure.Persistence;
using SchoolMgmt.WebApi.Filters;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddApplication().AddInfrastructure(builder.Configuration);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

// Bound lazily via IOptions<JwtOptions> (resolved from DI when JwtBearerOptions
// is actually materialized) rather than reading builder.Configuration directly
// here — eagerly reading config at this point in Program.cs can race with
// WebApplicationFactory's WithWebHostBuilder config overrides in tests,
// causing the validation key to differ from the one JwtTokenGenerator signs
// with (a real bug caught by the integration tests — see git history).
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearerOptions, jwtOptionsAccessor) =>
    {
        var jwtOptions = jwtOptionsAccessor.Value;

        // Default inbound claim mapping rewrites short claim names (sub, email,
        // name) to legacy XML schema URIs — disabled so claims survive exactly
        // as issued by JwtTokenGenerator (read by AuthController and by
        // HttpContextTenantProvider's "school_id" claim lookup).
        bearerOptions.MapInboundClaims = false;
        bearerOptions.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
        };
        bearerOptions.Events = new JwtBearerEvents
        {
            // The access token is delivered as an httpOnly cookie, never an
            // Authorization header — see .claude/context/architecture.md § Authentication.
            OnMessageReceived = context =>
            {
                if (context.Request.Cookies.TryGetValue("access_token", out var token))
                    context.Token = token;
                return Task.CompletedTask;
            },
        };
    });
    
builder.Services.AddAuthorization();

builder.Services.AddControllers(options =>
{
    options.Filters.Add<DomainExceptionFilter>();
    options.Filters.Add<ValidationFilter>();
})
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// /health checks both app liveness and DB connectivity (AddDbContextCheck
// runs AppDbContext.Database.CanConnectAsync()) — not docker-compose-wired
// (no compose-level healthcheck on api), but available for external
// monitoring/orchestration to probe.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>();

var app = builder.Build();

// Demo Admin user — IsDevelopment()-gated inside, never runs in Production.
await app.Services.SeedDemoDataAsync(app.Environment);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();

// Exposed for WebApplicationFactory<Program> in integration tests.
public partial class Program;
