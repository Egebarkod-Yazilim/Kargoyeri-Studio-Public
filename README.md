# Kargoyeri Studio

This repository is a private source snapshot of the Kargoyeri Studio project.

## Contents

- `src/` and `Kargoyeri.Studio.sln`: the current seven-project source solution, including Studio Web, Core, Embedded, Contracts, Domain, Application, and Infrastructure.
- `Kargoyeri.Unified/`: an older recovery attempt kept for reference. Its recovered Application and Domain methods include stubs; do not deploy this subfolder as the product.
- `docs/`, `scripts/`, `tools/`, `postman/`: supporting project material.

## Local setup

Install the .NET 8 SDK, then configure a SQL Server connection and an administrator account locally. The committed `appsettings.json` files intentionally contain no credentials. Use environment variables or .NET user secrets, for example:

```powershell
$env:ConnectionStrings__Kargoyeri = "Server=.;Database=KargoyeriStudio;Trusted_Connection=True;TrustServerCertificate=True"
$env:StudioAccess__Admins__0__Email = "admin@example.com"
$env:StudioAccess__Admins__0__Password = "<set-a-strong-local-password>"
$env:StudioAccess__Admins__0__IsActive = "true"
dotnet restore .\Kargoyeri.Studio.sln --configfile .\NuGet.Config
dotnet build .\Kargoyeri.Studio.sln --no-restore
```

The `Kargoyeri.Unified/Kargoyeri.Studio.sln` solution can be built separately, but the root solution is the product entry point. A successful build is not a functional or deployment test.

Runtime logs, customer data, Data Protection keys, local databases, and old build archives are intentionally excluded from this source snapshot. Preserve those locally and back them up separately through an encrypted channel when needed.
