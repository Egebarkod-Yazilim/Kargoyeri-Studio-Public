-- ============================================================
-- !!! DEPRECATED — P5-#2 sonrasi !!!
-- Bu dosya EF Migrations sistemi devreye girmeden once manuel
-- kurulum icin kullaniliyordu. Yeni kurulumlarda KULLANMAYIN.
-- Uygulama acilisinda Program.cs `db.Database.MigrateAsync()`
-- otomatik olarak InitialCreate migration'ini uygular.
-- Bkz: docs/MIGRATIONS.md
-- Bir sonraki major release'de silinecek.
-- ============================================================
-- Kargoyeri Studio — Veritabani Kurulum Betiği
-- EF Core 8.0.8 / KargoyeriDbContext uyumlu
-- Güvenli: Mevcut tablolara dokunmaz (IF NOT EXISTS kontrolü)
-- ============================================================
-- 1. Önce veritabanını oluşturun (yoksa):
--    CREATE DATABASE KargoyeriStudio;
-- 2. Bu betiği KargoyeriStudio veritabanında çalıştırın.
-- ============================================================

-- ──────────────────────────────────────────────────────────
-- EF Core migration geçmişi
-- ──────────────────────────────────────────────────────────
IF OBJECT_ID(N'[__EFMigrationsHistory]', N'U') IS NULL
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId]    nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32)  NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
GO

-- ──────────────────────────────────────────────────────────
-- 1. Customers
-- ──────────────────────────────────────────────────────────
IF OBJECT_ID(N'[Customers]', N'U') IS NULL
    CREATE TABLE [Customers] (
        [TenantKey]               nvarchar(64)   NOT NULL,
        [Name]                    nvarchar(256)  NOT NULL,
        [IsActive]                bit            NOT NULL CONSTRAINT [DF_Customers_IsActive] DEFAULT 1,
        [ApiKeyHash]              nvarchar(512)  NULL,
        [AllowedProvidersJson]    nvarchar(max)  NOT NULL CONSTRAINT [DF_Customers_AllowedProviders]    DEFAULT N'[]',
        [NotificationTargetsJson] nvarchar(max)  NOT NULL CONSTRAINT [DF_Customers_NotificationTargets] DEFAULT N'[]',
        [MetadataJson]            nvarchar(max)  NOT NULL CONSTRAINT [DF_Customers_Metadata]            DEFAULT N'{}',
        [CreatedAtUtc]            datetimeoffset NOT NULL,
        [UpdatedAtUtc]            datetimeoffset NOT NULL,
        CONSTRAINT [PK_Customers] PRIMARY KEY ([TenantKey])
    );
GO

-- ──────────────────────────────────────────────────────────
-- 2. Shipments
-- ──────────────────────────────────────────────────────────
IF OBJECT_ID(N'[Shipments]', N'U') IS NULL
    CREATE TABLE [Shipments] (
        [Id]                      uniqueidentifier NOT NULL CONSTRAINT [DF_Shipments_Id] DEFAULT NEWSEQUENTIALID(),
        [ShipmentReference]       nvarchar(64)     NOT NULL,
        [TenantKey]               nvarchar(64)     NOT NULL,
        [OrderReference]          nvarchar(128)    NOT NULL,
        [ClientShipmentReference] nvarchar(128)    NULL,
        [IdempotencyKey]          nvarchar(256)    NULL,
        [Provider]                nvarchar(32)     NOT NULL,
        [Source]                  nvarchar(32)     NOT NULL,
        [Status]                  nvarchar(32)     NOT NULL,
        [CollectionAmount]        decimal(18,4)    NULL,
        [CurrencyCode]            nvarchar(8)      NOT NULL CONSTRAINT [DF_Shipments_CurrencyCode] DEFAULT N'TRY',
        -- Gonderen
        [SenderName]              nvarchar(256)    NOT NULL CONSTRAINT [DF_Shipments_SenderName]         DEFAULT N'',
        [SenderCompanyName]       nvarchar(256)    NULL,
        [SenderPhone]             nvarchar(32)     NULL,
        [SenderEmail]             nvarchar(256)    NULL,
        [SenderCountryCode]       nvarchar(8)      NOT NULL CONSTRAINT [DF_Shipments_SenderCountryCode]  DEFAULT N'TR',
        [SenderCity]              nvarchar(128)    NOT NULL CONSTRAINT [DF_Shipments_SenderCity]         DEFAULT N'',
        [SenderDistrict]          nvarchar(128)    NULL,
        [SenderPostalCode]        nvarchar(16)     NULL,
        [SenderAddressLine1]      nvarchar(512)    NOT NULL CONSTRAINT [DF_Shipments_SenderAddressLine1] DEFAULT N'',
        [SenderAddressLine2]      nvarchar(512)    NULL,
        -- Alici
        [RecipientName]           nvarchar(256)    NOT NULL CONSTRAINT [DF_Shipments_RecipientName]         DEFAULT N'',
        [RecipientCompanyName]    nvarchar(256)    NULL,
        [RecipientPhone]          nvarchar(32)     NULL,
        [RecipientEmail]          nvarchar(256)    NULL,
        [RecipientCountryCode]    nvarchar(8)      NOT NULL CONSTRAINT [DF_Shipments_RecipientCountryCode]  DEFAULT N'TR',
        [RecipientCity]           nvarchar(128)    NOT NULL CONSTRAINT [DF_Shipments_RecipientCity]         DEFAULT N'',
        [RecipientDistrict]       nvarchar(128)    NULL,
        [RecipientPostalCode]     nvarchar(16)     NULL,
        [RecipientAddressLine1]   nvarchar(512)    NOT NULL CONSTRAINT [DF_Shipments_RecipientAddressLine1] DEFAULT N'',
        [RecipientAddressLine2]   nvarchar(512)    NULL,
        -- JSON kolonlar
        [PackagesJson]            nvarchar(max)    NOT NULL CONSTRAINT [DF_Shipments_PackagesJson]  DEFAULT N'[]',
        [MetadataJson]            nvarchar(max)    NOT NULL CONSTRAINT [DF_Shipments_MetadataJson]  DEFAULT N'{}',
        -- Sonuc alanlari
        [TrackingNumber]          nvarchar(128)    NULL,
        [LabelUrl]                nvarchar(2048)   NULL,
        [LabelContentBase64]      nvarchar(max)    NULL,
        [ProviderMessage]         nvarchar(1024)   NULL,
        [ErrorMessage]            nvarchar(2048)   NULL,
        [RetryCount]              int              NOT NULL CONSTRAINT [DF_Shipments_RetryCount] DEFAULT 0,
        [CreatedAtUtc]            datetimeoffset   NOT NULL,
        [UpdatedAtUtc]            datetimeoffset   NOT NULL,
        [LastStatusCheckAtUtc]    datetimeoffset   NULL,
        CONSTRAINT [PK_Shipments] PRIMARY KEY ([Id])
    );
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Shipments_ShipmentReference' AND object_id = OBJECT_ID(N'[Shipments]'))
    CREATE UNIQUE INDEX [IX_Shipments_ShipmentReference] ON [Shipments] ([ShipmentReference]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Shipments_TenantKey' AND object_id = OBJECT_ID(N'[Shipments]'))
    CREATE INDEX [IX_Shipments_TenantKey] ON [Shipments] ([TenantKey]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Shipments_TenantKey_IdempotencyKey' AND object_id = OBJECT_ID(N'[Shipments]'))
    CREATE INDEX [IX_Shipments_TenantKey_IdempotencyKey] ON [Shipments] ([TenantKey], [IdempotencyKey])
    WHERE [IdempotencyKey] IS NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Shipments_UpdatedAtUtc' AND object_id = OBJECT_ID(N'[Shipments]'))
    CREATE INDEX [IX_Shipments_UpdatedAtUtc] ON [Shipments] ([UpdatedAtUtc]);
GO

-- ──────────────────────────────────────────────────────────
-- 3. Notifications
-- ──────────────────────────────────────────────────────────
IF OBJECT_ID(N'[Notifications]', N'U') IS NULL
    CREATE TABLE [Notifications] (
        [Id]                uniqueidentifier NOT NULL CONSTRAINT [DF_Notifications_Id] DEFAULT NEWSEQUENTIALID(),
        [TenantKey]         nvarchar(64)     NOT NULL,
        [ShipmentReference] nvarchar(64)     NOT NULL,
        [EventType]         nvarchar(32)     NOT NULL,
        [Channel]           nvarchar(32)     NOT NULL,
        [Status]            nvarchar(32)     NOT NULL,
        [Address]           nvarchar(512)    NOT NULL,
        [Subject]           nvarchar(512)    NOT NULL,
        [Body]              nvarchar(max)    NOT NULL,
        [ErrorMessage]      nvarchar(2048)   NULL,
        [CreatedAtUtc]      datetimeoffset   NOT NULL,
        [DeliveredAtUtc]    datetimeoffset   NULL,
        CONSTRAINT [PK_Notifications] PRIMARY KEY ([Id])
    );
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Notifications_TenantKey' AND object_id = OBJECT_ID(N'[Notifications]'))
    CREATE INDEX [IX_Notifications_TenantKey] ON [Notifications] ([TenantKey]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Notifications_ShipmentReference' AND object_id = OBJECT_ID(N'[Notifications]'))
    CREATE INDEX [IX_Notifications_ShipmentReference] ON [Notifications] ([ShipmentReference]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Notifications_CreatedAtUtc' AND object_id = OBJECT_ID(N'[Notifications]'))
    CREATE INDEX [IX_Notifications_CreatedAtUtc] ON [Notifications] ([CreatedAtUtc]);
GO

-- ──────────────────────────────────────────────────────────
-- 4. OperationLogs
-- ──────────────────────────────────────────────────────────
IF OBJECT_ID(N'[OperationLogs]', N'U') IS NULL
    CREATE TABLE [OperationLogs] (
        [Id]                uniqueidentifier NOT NULL CONSTRAINT [DF_OperationLogs_Id] DEFAULT NEWSEQUENTIALID(),
        [TenantKey]         nvarchar(64)     NOT NULL,
        [ShipmentReference] nvarchar(64)     NOT NULL,
        [Operation]         nvarchar(64)     NOT NULL,
        [Severity]          nvarchar(16)     NOT NULL,
        [Message]           nvarchar(2048)   NOT NULL,
        [ProviderPayload]   nvarchar(max)    NULL,
        [OccurredAtUtc]     datetimeoffset   NOT NULL,
        CONSTRAINT [PK_OperationLogs] PRIMARY KEY ([Id])
    );
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OperationLogs_TenantKey' AND object_id = OBJECT_ID(N'[OperationLogs]'))
    CREATE INDEX [IX_OperationLogs_TenantKey] ON [OperationLogs] ([TenantKey]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OperationLogs_ShipmentReference' AND object_id = OBJECT_ID(N'[OperationLogs]'))
    CREATE INDEX [IX_OperationLogs_ShipmentReference] ON [OperationLogs] ([ShipmentReference]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OperationLogs_OccurredAtUtc' AND object_id = OBJECT_ID(N'[OperationLogs]'))
    CREATE INDEX [IX_OperationLogs_OccurredAtUtc] ON [OperationLogs] ([OccurredAtUtc]);
GO

-- ──────────────────────────────────────────────────────────
-- 5. ProviderCredentials
-- ──────────────────────────────────────────────────────────
IF OBJECT_ID(N'[ProviderCredentials]', N'U') IS NULL
    CREATE TABLE [ProviderCredentials] (
        [TenantKey]              nvarchar(64)   NOT NULL,
        [Provider]               nvarchar(32)   NOT NULL,
        [IsEnabled]              bit            NOT NULL CONSTRAINT [DF_ProviderCredentials_IsEnabled] DEFAULT 0,
        [ClientCode]             nvarchar(256)  NULL,
        [Username]               nvarchar(256)  NULL,
        [Password]               nvarchar(512)  NULL,
        [ApiKey]                 nvarchar(1024) NULL,
        [EndpointBase]           nvarchar(512)  NULL,
        [AdditionalSettingsJson] nvarchar(max)  NOT NULL CONSTRAINT [DF_ProviderCredentials_AdditionalSettings] DEFAULT N'{}',
        [UpdatedAtUtc]           datetimeoffset NOT NULL,
        CONSTRAINT [PK_ProviderCredentials] PRIMARY KEY ([TenantKey], [Provider])
    );
GO

-- ──────────────────────────────────────────────────────────
-- Migration kaydı
-- ──────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20240101000000_InitialCreate')
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20240101000000_InitialCreate', N'8.0.8');
GO

PRINT '========================================';
PRINT 'Kargoyeri Studio kurulumu tamamlandi.';
PRINT '========================================';
