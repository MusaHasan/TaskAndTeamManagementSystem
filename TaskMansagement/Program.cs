using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TaskMansagement.Data;
using TaskMansagement.Models;
using Microsoft.AspNetCore.Authorization;
using System.Data.Common;
using Microsoft.OpenApi.Models;
using TaskMansagement.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
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
            new string[] { }
        }
    });
});

// Configure DbContext
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrEmpty(connectionString))
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlServer(connectionString));
}

// Configure JWT
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection.GetValue<string>("Key");
var jwtIssuer = jwtSection.GetValue<string>("Issuer");
var jwtAudience = jwtSection.GetValue<string>("Audience");

var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = key,
        ValidateLifetime = true
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole(Role.Admin.ToString()));
    options.AddPolicy("ManagerOrAdmin", policy => policy.RequireRole(Role.Manage.ToString(), Role.Admin.ToString()));
});

var app = builder.Build();

// Use exception middleware early
app.UseMiddleware<ExceptionMiddleware>();

// Seed default users
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // Apply migrations (if any)
    try
    {
        db.Database.Migrate();
    }
    catch
    {
        // ignore failures here; we'll try to patch schema as needed
    }

    // Ensure PasswordHash column exists in Users table (for existing DBs)
    try
    {
        var conn = db.Database.GetDbConnection();
        conn.Open();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Users' AND COLUMN_NAME='PasswordHash'";
            var exists = Convert.ToInt32(cmd.ExecuteScalar() ?? 0) > 0;
            if (!exists)
            {
                using var addCmd = conn.CreateCommand();
                addCmd.CommandText = "ALTER TABLE [Users] ADD [PasswordHash] NVARCHAR(MAX) NULL";
                addCmd.ExecuteNonQuery();
            }
        }
        conn.Close();
    }
    catch
    {
        // ignore any errors altering schema in dev scenarios
    }

    var passwordHasher = new SimplePasswordHasher();

    // Ensure specific seeded users exist and have password hashes
    var seeds = new[] {
        (Email: "admin@demo.com", Name: "Admin", Role: Role.Admin, Password: "Admin123!"),
        (Email: "manager@demo.com", Name: "Manager", Role: Role.Manage, Password: "Manager123!"),
        (Email: "employee@demo.com", Name: "Employee", Role: Role.Employee, Password: "Employee123!")
    };

    foreach (var s in seeds)
    {
        var existing = db.Users.FirstOrDefault(u => u.Email.ToLower() == s.Email.ToLower());
        if (existing == null)
        {
            db.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                FullName = s.Name,
                Email = s.Email,
                Role = s.Role,
                PasswordHash = passwordHasher.Hash(s.Password)
            });
        }
        else
        {
            var changed = false;
            if (string.IsNullOrWhiteSpace(existing.PasswordHash))
            {
                existing.PasswordHash = passwordHasher.Hash(s.Password);
                changed = true;
            }
            if (existing.Role != s.Role)
            {
                existing.Role = s.Role;
                changed = true;
            }
            if (changed)
            {
                db.Users.Update(existing);
            }
        }
    }

    db.SaveChanges();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Simple password hasher for demo purposes
public class SimplePasswordHasher
{
    public string Hash(string password)
    {
        // DO NOT use this in production. Use a proper password hasher.
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(password);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    public bool Verify(string hash, string password)
    {
        return Hash(password) == hash;
    }
}
