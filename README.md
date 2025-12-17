# TaskMansagement - Setup & Run

This README explains how to set up and run the `TaskMansagement` ASP.NET Core Web API locally (uses LocalDB and JWT authentication).

## Requirements
- .NET 8 SDK
- SQL Server LocalDB (installed with Visual Studio)
- Optional: `dotnet-ef` tool for migrations: `dotnet tool install --global dotnet-ef`
## Packages Install
Microsoft.EntityFrameworkCore.SqlServer
Microsoft.EntityFrameworkCore.Tools
xunit
xunit.runner.visualstudio
Moq
Microsoft.EntityFrameworkCore.InMemory
FluentAssertions


## Setup
1. Open a terminal and change to the solution folder (where `.sln` or project folders are located):

   `cd TaskMansagement`

2. Restore and build:

   `dotnet restore`

   `dotnet build`

3. (Optional) Run EF migrations manually if you prefer explicit migration management:

   - Create a migration: `dotnet ef migrations add <Name> -p TaskMansagement/TaskMansagement.csproj`
   - Apply migrations: `dotnet ef database update -p TaskMansagement/TaskMansagement.csproj`

   The application also attempts to apply migrations and seed data at startup.

## Configuration
- Connection string is in `TaskMansagement/appsettings.json` under `ConnectionStrings:DefaultConnection`. By default it uses LocalDB:

  `Server=(localdb)\\mssqllocaldb;Database=TaskManagementDb;Trusted_Connection=True;MultipleActiveResultSets=true`

- JWT settings are in `TaskMansagement/appsettings.json` under the `Jwt` section (Key, Issuer, Audience, ExpiresMinutes).

## Run the API
Start the API from the `TaskMansagement` project folder:

`dotnet run --project TaskMansagement/TaskMansagement.csproj`

By default the API runs on the bound Kestrel port (check the console). Swagger UI is available at `/swagger` in Development.

## Seeded accounts (local development)
The app attempts to seed three users at startup (email / password):

- Admin: `admin@demo.com` / `Admin123!`
- Manager: `manager@demo.com` / `Manager123!`
- Employee: `employee@demo.com` / `Employee123!`

If users are not present, the startup seeding will add them. If you change the DB manually, ensure those users exist and have a password hash.

## Authentication (JWT)
1. Obtain a token by POSTing to:

   `POST /api/auth/login`

   Body (JSON):

   { "email": "admin@demo.com", "password": "Admin123!" }

   Response: `{ "token": "<jwt>" }`

2. Use the token in requests to protected endpoints by adding the header:

   `Authorization: Bearer <token>`

Example curl to login and call a protected endpoint (adjust host/port):

```bash
# Login
TOKEN=$(curl -s -X POST https://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@demo.com","password":"Admin123!"}' | jq -r .token)

# Call protected endpoint
curl -H "Authorization: Bearer $TOKEN" https://localhost:5001/api/users
```

## Swagger
- Open `https://localhost:<port>/swagger`.
- Use `POST /api/auth/login` to get a token, then click `Authorize` and paste `Bearer <token>` (include the `Bearer ` prefix).
- After authorizing, call protected endpoints from the UI.

## Running Unit Tests
From the solution root run:

`dotnet test`

This will run tests in `TaskManagementAPITestProject` which use an in-memory EF provider.

## Troubleshooting
- 401 Unauthorized when calling protected endpoints:
  - Ensure you obtained a token from `POST /api/auth/login` and include it as `Authorization: Bearer <token>`.
  - Restart the app so startup seeding and schema patching run if you recently changed code.
  - Verify `Jwt` settings in `appsettings.json` match the values used to generate tokens.

- Database errors about missing columns:
  - Run `dotnet ef database update` or restart the app so the startup migration/patch code runs.

## Security note
This project uses a simple SHA-256-based password hasher and direct JWT handling for demo purposes only. For production use, integrate ASP.NET Core Identity and a proper password hasher (PBKDF2/BCrypt/Argon2), use secure key management, and follow best practices for authentication and authorization.

## Further improvements
- Replace the simple hasher with ASP.NET Core Identity.
- Add refresh tokens and stronger password policies.
- Add API documentation for each endpoint and DTOs.

-- End
