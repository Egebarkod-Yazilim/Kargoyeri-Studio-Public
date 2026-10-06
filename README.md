# Kargoyeri Studio

This repository is a private source snapshot of the Kargoyeri Studio project.

## Contents

- `src/` and `Kargoyeri.Studio.sln`: the current seven-project source solution, including Studio Web, Core, Embedded, Contracts, Domain, Application, and Infrastructure.
- `Kargoyeri.Unified/`: an older recovery attempt kept for reference. Its recovered Application and Domain methods include stubs; do not deploy this subfolder as the product.
- `docs/`, `scripts/`, `tools/`, `postman/`: supporting project material.

## Local setup

Install the .NET 8 SDK, then run in the `Development` environment to use the local demo accounts below. `Admin` is limited to operations (dashboard, shipments, pickups, reports and notifications); `SuperAdmin` can access all pages and configuration. These demo credentials are defined only in `appsettings.Development.json`; production must use separate BCrypt password hashes and TOTP. The committed base `appsettings.json` intentionally contains no credentials. To configure your own accounts, use environment variables or .NET user secrets, for example:

| Role | Email | Password | Access |
| --- | --- | --- | --- |
| Super Admin | `superadmin@localhost` | `SuperAdmin123!` | All pages |
| Admin | `admin@localhost` | `Admin123!` | Operations only |

```powershell
$env:ConnectionStrings__Kargoyeri = "Server=.;Database=KargoyeriStudio;Trusted_Connection=True;TrustServerCertificate=True"
$env:StudioAccess__Admins__0__Email = "admin@example.com"
$env:StudioAccess__Admins__0__Password = "<set-a-strong-local-password>"
$env:StudioAccess__Admins__0__IsActive = "true"
$env:StudioAccess__Admins__0__Role = "SuperAdmin" # or Admin (operational pages only)
dotnet user-secrets set --project .\src\Kargoyeri.Studio.Web\Kargoyeri.Studio.Web.csproj "Storage:Mode" "InMemory" # optional, for a no-SQL local demo
dotnet restore .\Kargoyeri.Studio.sln --configfile .\NuGet.Config
dotnet build .\Kargoyeri.Studio.sln --no-restore
dotnet run --project .\src\Kargoyeri.Studio.Web\Kargoyeri.Studio.Web.csproj
```

For development, configure the account values with `dotnet user-secrets set --project .\src\Kargoyeri.Studio.Web\Kargoyeri.Studio.Web.csproj "StudioAccess:Admins:0:Email" "admin@example.com"` and matching `Password`, `IsActive`, and `Role` keys. For production, use a BCrypt `PasswordHash`, enable TOTP, and configure at least one active `SuperAdmin`.

The `Kargoyeri.Unified/Kargoyeri.Studio.sln` solution can be built separately, but the root solution is the product entry point. A successful build is not a functional or deployment test.

Runtime logs, customer data, Data Protection keys, local databases, and old build archives are intentionally excluded from this source snapshot. Preserve those locally and back them up separately through an encrypted channel when needed.
