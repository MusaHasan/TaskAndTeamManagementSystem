using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using TaskMansagement.Controllers;
using TaskMansagement.Data;
using TaskMansagement.Models;
using Xunit;

namespace TaskManagementAPITestProject
{
    public class AuthTests
    {
        private AppDbContext CreateContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;

            return new AppDbContext(options);
        }

        private static string Sha256Base64(string input)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(input);
            var hash = sha.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

        [Fact]
        public async Task Login_WithValidCredentials_Returns_Token_With_Correct_Claims()
        {
            // Arrange
            var db = CreateContext("Auth_Valid");
            var password = "Admin123!";
            var user = new User
            {
                Id = Guid.NewGuid(),
                FullName = "Admin",
                Email = "admin@demo.com",
                Role = Role.Admin,
                PasswordHash = Sha256Base64(password)
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            var inMemorySettings = new Dictionary<string, string?> {
                {"Jwt:Key", "ThisIsASecretKeyForDemoPurposesOnlyChangeMe"},
                {"Jwt:Issuer", "TaskMansagementDemo"},
                {"Jwt:Audience", "TaskMansagementUsers"},
                {"Jwt:ExpiresMinutes", "60"}
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings!).Build();

            var controller = new AuthController(db, config);

            var loginRequest = new LoginRequest { Email = user.Email, Password = password };

            // Act
            var result = await controller.Login(loginRequest) as OkObjectResult;

            // Assert
            result.Should().NotBeNull();
            var tokenProp = result.Value?.GetType().GetProperty("token");
            tokenProp.Should().NotBeNull();
            var token = tokenProp!.GetValue(result.Value) as string;
            token.Should().NotBeNullOrWhiteSpace();

            // validate token signature and claims
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(inMemorySettings["Jwt:Key"]!);
            var validationParams = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = inMemorySettings["Jwt:Issuer"],
                ValidateAudience = true,
                ValidAudience = inMemorySettings["Jwt:Audience"],
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            tokenHandler.Invoking(th => th.ValidateToken(token, validationParams, out _))
                .Should().NotThrow();

            var jwt = tokenHandler.ReadJwtToken(token!);
            jwt.Claims.Should().Contain(c => c.Type == System.Security.Claims.ClaimTypes.Email && c.Value == user.Email);
            jwt.Claims.Should().Contain(c => c.Type == System.Security.Claims.ClaimTypes.Role && c.Value == user.Role.ToString());
        }

        [Fact]
        public async Task Login_WithInvalidPassword_Returns_Unauthorized()
        {
            // Arrange
            var db = CreateContext("Auth_Invalid");
            var password = "Admin123!";
            var user = new User
            {
                Id = Guid.NewGuid(),
                FullName = "Admin",
                Email = "admin@demo.com",
                Role = Role.Admin,
                PasswordHash = Sha256Base64(password)
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            var inMemorySettings = new Dictionary<string, string?> {
                {"Jwt:Key", "ThisIsASecretKeyForDemoPurposesOnlyChangeMe"},
                {"Jwt:Issuer", "TaskMansagementDemo"},
                {"Jwt:Audience", "TaskMansagementUsers"},
                {"Jwt:ExpiresMinutes", "60"}
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings!).Build();

            var controller = new AuthController(db, config);

            var loginRequest = new LoginRequest { Email = user.Email, Password = "WrongPassword" };

            // Act
            var result = await controller.Login(loginRequest);

            // Assert
            result.Should().BeOfType<UnauthorizedResult>();
        }
    }
}
