# ==========================================================================
# Kargoyeri.Studio — Production Dockerfile
# --------------------------------------------------------------------------
# DIKKAT: Build context'i repo'nun BIR UST dizini olmalidir, cunku
# Kargoyeri.Studio.Core projesi `../../../Projeler/KargoEntegre.../Kargoyeri/src/...`
# yolundaki referanslara baglidir.
#
# Build komutu (Desktop dizininden):
#   docker build -f Kargoyeri.Studio/Dockerfile -t kargoyeri-studio:latest .
#
# Veya docker-compose.yml zaten dogru context ile yapilandirilmistir:
#   docker compose up -d --build
# ==========================================================================

# ---- Build asamasi ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Katman cache'lemek icin once SADECE csproj dosyalarini kopyala (restore)
COPY ["Kargoyeri.Studio/src/Kargoyeri.Studio.Web/Kargoyeri.Studio.Web.csproj",            "Kargoyeri.Studio/src/Kargoyeri.Studio.Web/"]
COPY ["Kargoyeri.Studio/src/Kargoyeri.Studio.Core/Kargoyeri.Studio.Core.csproj",          "Kargoyeri.Studio/src/Kargoyeri.Studio.Core/"]
COPY ["Kargoyeri.Studio/src/Kargoyeri.Studio.Embedded/Kargoyeri.Studio.Embedded.csproj",  "Kargoyeri.Studio/src/Kargoyeri.Studio.Embedded/"]

# KargoEntegre referansli projelerin csproj'lari
COPY ["Projeler/KargoEntegre--Full-Otomatik-master/Kargoyeri/src/Kargoyeri.Application/Kargoyeri.Application.csproj",   "Projeler/KargoEntegre--Full-Otomatik-master/Kargoyeri/src/Kargoyeri.Application/"]
COPY ["Projeler/KargoEntegre--Full-Otomatik-master/Kargoyeri/src/Kargoyeri.Contracts/Kargoyeri.Contracts.csproj",       "Projeler/KargoEntegre--Full-Otomatik-master/Kargoyeri/src/Kargoyeri.Contracts/"]
COPY ["Projeler/KargoEntegre--Full-Otomatik-master/Kargoyeri/src/Kargoyeri.Domain/Kargoyeri.Domain.csproj",             "Projeler/KargoEntegre--Full-Otomatik-master/Kargoyeri/src/Kargoyeri.Domain/"]
COPY ["Projeler/KargoEntegre--Full-Otomatik-master/Kargoyeri/src/Kargoyeri.Infrastructure/Kargoyeri.Infrastructure.csproj", "Projeler/KargoEntegre--Full-Otomatik-master/Kargoyeri/src/Kargoyeri.Infrastructure/"]
COPY ["Kargoyeri.Studio/NuGet.Config", "Kargoyeri.Studio/"]

RUN dotnet restore "Kargoyeri.Studio/src/Kargoyeri.Studio.Web/Kargoyeri.Studio.Web.csproj"

# Tum kaynak kodu
COPY Kargoyeri.Studio/  Kargoyeri.Studio/
COPY Projeler/          Projeler/

WORKDIR /src/Kargoyeri.Studio/src/Kargoyeri.Studio.Web
RUN dotnet publish "Kargoyeri.Studio.Web.csproj" -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---- Runtime asamasi ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Healthcheck icin curl + zaman dilimi (Turkiye)
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl tzdata ca-certificates \
 && ln -snf /usr/share/zoneinfo/Europe/Istanbul /etc/localtime \
 && echo "Europe/Istanbul" > /etc/timezone \
 && rm -rf /var/lib/apt/lists/*

# Guvenlik: root olmayan kullanici
RUN groupadd -r app && useradd -r -g app -d /app -s /sbin/nologin app

COPY --from=build /app/publish .

# Veri ve key dizinlerini olustur, izinleri ayarla
RUN mkdir -p /app/data /app/data-protection-keys /app/logs \
 && chown -R app:app /app

USER app

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV Storage__BasePath=/app/data
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV DOTNET_PRINT_TELEMETRY_MESSAGE=false
ENV TZ=Europe/Istanbul

EXPOSE 8080

# Liveness check — readiness icin compose'da /health/ready kullanin
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
  CMD curl -fsS http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "Kargoyeri.Studio.Web.dll"]
