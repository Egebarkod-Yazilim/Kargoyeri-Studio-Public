$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot
dotnet run --project .\src\Kargoyeri.Studio.Web\Kargoyeri.Studio.Web.csproj --configfile .\NuGet.Config
