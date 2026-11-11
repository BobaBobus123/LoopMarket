using Microsoft.EntityFrameworkCore;

namespace TradeStorm.Api.Data;

/// <summary>
/// Ensures auxiliary tables (added after EF migrations) exist and backfills legacy NULL columns
/// so EF Core materialization works consistently across SQLite and PostgreSQL.
/// </summary>
public static class DatabaseSchemaInitializer
{
    public static void EnsureMissingTablesAndColumns(AppDbContext db, bool isPostgres)
    {
        if (isPostgres)
        {
            EnsurePostgresSchema(db);
        }
        else
        {
            EnsureSqliteSchema(db);
        }
    }

    private static void EnsureSqliteSchema(AppDbContext db)
    {
        TryExecute(db, @"CREATE TABLE IF NOT EXISTS SiteAnnouncements (
            Id INTEGER NOT NULL CONSTRAINT PK_SiteAnnouncements PRIMARY KEY AUTOINCREMENT,
            Title TEXT NOT NULL DEFAULT '',
            Message TEXT NOT NULL DEFAULT '',
            Type TEXT NOT NULL DEFAULT 'warning',
            TargetPage TEXT NOT NULL DEFAULT 'all',
            Placement TEXT NOT NULL DEFAULT 'top',
            ExpiresAt TEXT NULL,
            ActionText TEXT NULL,
            ActionUrl TEXT NULL,
            ImageUrl TEXT NULL,
            IsActive INTEGER NOT NULL DEFAULT 1,
            CreatedAt TEXT NOT NULL DEFAULT (CURRENT_TIMESTAMP)
        );");

        TryExecute(db, @"CREATE TABLE IF NOT EXISTS AdminAuditLogs (
            Id INTEGER NOT NULL CONSTRAINT PK_AdminAuditLogs PRIMARY KEY AUTOINCREMENT,
            AdminId INTEGER NOT NULL DEFAULT 0,
            AdminEmail TEXT NOT NULL DEFAULT '',
            Action TEXT NOT NULL DEFAULT '',
            TargetType TEXT NULL,
            TargetId INTEGER NULL,
            Details TEXT NULL,
            CreatedAt TEXT NOT NULL DEFAULT (CURRENT_TIMESTAMP)
        );");

        TryExecute(db, @"CREATE TABLE IF NOT EXISTS StoredFiles (
            Id INTEGER NOT NULL CONSTRAINT PK_StoredFiles PRIMARY KEY AUTOINCREMENT,
            FileName TEXT NOT NULL DEFAULT '',
            ContentType TEXT NOT NULL DEFAULT 'image/jpeg',
            Data BLOB NOT NULL,
            CreatedAt TEXT NOT NULL DEFAULT (CURRENT_TIMESTAMP)
        );");

        TryExecute(db, @"CREATE TABLE IF NOT EXISTS Complaints (
            Id INTEGER NOT NULL CONSTRAINT PK_Complaints PRIMARY KEY AUTOINCREMENT,
            ReporterId INTEGER NOT NULL,
            TargetUserId INTEGER NOT NULL,
            ListingId INTEGER NULL,
            Reason TEXT NOT NULL DEFAULT '',
            Details TEXT NULL,
            Status TEXT NOT NULL DEFAULT 'Pending',
            CreatedAt TEXT NOT NULL DEFAULT (CURRENT_TIMESTAMP),
            ResolutionNotes TEXT NULL
        );");

        TryExecute(db, @"CREATE TABLE IF NOT EXISTS PasswordResetSessions (
            Id INTEGER NOT NULL CONSTRAINT PK_PasswordResetSessions PRIMARY KEY AUTOINCREMENT,
            UserId INTEGER NOT NULL,
            SessionToken TEXT NOT NULL,
            OtpHash TEXT NOT NULL,
            ProofTokenHash TEXT NULL,
            FailedAttempts INTEGER NOT NULL DEFAULT 0,
            RequiresPhoneCheck INTEGER NOT NULL DEFAULT 0,
            RequiresTelegramApproval INTEGER NOT NULL DEFAULT 0,
            IsEmailVerified INTEGER NOT NULL DEFAULT 0,
            IsPhoneVerified INTEGER NOT NULL DEFAULT 0,
            IsTelegramApproved INTEGER NOT NULL DEFAULT 0,
            IsRevoked INTEGER NOT NULL DEFAULT 0,
            IsCompleted INTEGER NOT NULL DEFAULT 0,
            RequestIp TEXT NULL,
            UserAgent TEXT NULL,
            CreatedAt TEXT NOT NULL DEFAULT (CURRENT_TIMESTAMP),
            LastSentAt TEXT NOT NULL DEFAULT (CURRENT_TIMESTAMP),
            ExpiresAt TEXT NOT NULL,
            CompletedAt TEXT NULL
        );");
        TryExecute(db, @"CREATE UNIQUE INDEX IF NOT EXISTS IX_PasswordResetSessions_SessionToken ON PasswordResetSessions(SessionToken);");
        TryExecute(db, @"CREATE INDEX IF NOT EXISTS IX_PasswordResetSessions_UserId_CreatedAt ON PasswordResetSessions(UserId, CreatedAt);");

        TryExecute(db, @"ALTER TABLE SiteAnnouncements ADD COLUMN Title TEXT NOT NULL DEFAULT '';");
        TryExecute(db, @"ALTER TABLE SiteAnnouncements ADD COLUMN TargetPage TEXT NOT NULL DEFAULT 'all';");
        TryExecute(db, @"ALTER TABLE SiteAnnouncements ADD COLUMN Placement TEXT NOT NULL DEFAULT 'top';");
        TryExecute(db, @"ALTER TABLE SiteAnnouncements ADD COLUMN ExpiresAt TEXT;");
        TryExecute(db, @"ALTER TABLE SiteAnnouncements ADD COLUMN ActionText TEXT;");
        TryExecute(db, @"ALTER TABLE SiteAnnouncements ADD COLUMN ActionUrl TEXT;");
        TryExecute(db, @"ALTER TABLE SiteAnnouncements ADD COLUMN ImageUrl TEXT;");

        TryExecute(db, @"UPDATE SiteAnnouncements SET
            Title = COALESCE(Title, ''),
            Message = COALESCE(Message, ''),
            Type = COALESCE(Type, 'warning'),
            TargetPage = COALESCE(TargetPage, 'all'),
            Placement = COALESCE(Placement, 'top')
            WHERE Title IS NULL OR Message IS NULL OR Type IS NULL OR TargetPage IS NULL OR Placement IS NULL;");

        // Індекси для стабільної роботи під високим навантаженням
        TryExecute(db, @"CREATE INDEX IF NOT EXISTS IX_Users_Email ON Users(Email);");
        TryExecute(db, @"CREATE INDEX IF NOT EXISTS IX_Users_PhoneNumber ON Users(PhoneNumber);");
        TryExecute(db, @"CREATE INDEX IF NOT EXISTS IX_Users_TelegramChatId ON Users(TelegramChatId);");
        TryExecute(db, @"CREATE INDEX IF NOT EXISTS IX_Listings_SellerId_Status ON Listings(SellerId, Status);");
        TryExecute(db, @"CREATE INDEX IF NOT EXISTS IX_Listings_Status_CreatedAt ON Listings(Status, CreatedAt);");
        TryExecute(db, @"CREATE INDEX IF NOT EXISTS IX_StoredFiles_FileName ON StoredFiles(FileName);");
        TryExecute(db, @"CREATE INDEX IF NOT EXISTS IX_Notifications_UserId_IsRead_CreatedAt ON Notifications(UserId, IsRead, CreatedAt);");
        TryExecute(db, @"CREATE INDEX IF NOT EXISTS IX_Reviews_TargetUserId ON Reviews(TargetUserId);");
        TryExecute(db, @"CREATE INDEX IF NOT EXISTS IX_SiteAnnouncements_Active ON SiteAnnouncements(IsActive, TargetPage, Placement, CreatedAt);");
    }

    private static void EnsurePostgresSchema(AppDbContext db)
    {
        TryExecute(db, @"CREATE TABLE IF NOT EXISTS ""SiteAnnouncements"" (
            ""Id"" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
            ""Title"" text NOT NULL DEFAULT '',
            ""Message"" text NOT NULL DEFAULT '',
            ""Type"" text NOT NULL DEFAULT 'warning',
            ""TargetPage"" text NOT NULL DEFAULT 'all',
            ""Placement"" text NOT NULL DEFAULT 'top',
            ""ExpiresAt"" timestamp with time zone NULL,
            ""ActionText"" text NULL,
            ""ActionUrl"" text NULL,
            ""ImageUrl"" text NULL,
            ""IsActive"" boolean NOT NULL DEFAULT TRUE,
            ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT NOW()
        );");

        TryExecute(db, @"CREATE TABLE IF NOT EXISTS ""AdminAuditLogs"" (
            ""Id"" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
            ""AdminId"" integer NOT NULL DEFAULT 0,
            ""AdminEmail"" text NOT NULL DEFAULT '',
            ""Action"" text NOT NULL DEFAULT '',
            ""TargetType"" text NULL,
            ""TargetId"" integer NULL,
            ""Details"" text NULL,
            ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT NOW()
        );");

        TryExecute(db, @"CREATE TABLE IF NOT EXISTS ""StoredFiles"" (
            ""Id"" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
            ""FileName"" text NOT NULL DEFAULT '',
            ""ContentType"" text NOT NULL DEFAULT 'image/jpeg',
            ""Data"" bytea NOT NULL,
            ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT NOW()
        );");

        TryExecute(db, @"CREATE TABLE IF NOT EXISTS ""Complaints"" (
            ""Id"" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
            ""ReporterId"" integer NOT NULL,
            ""TargetUserId"" integer NOT NULL,
            ""ListingId"" integer NULL,
            ""Reason"" text NOT NULL DEFAULT '',
            ""Details"" text NULL,
            ""Status"" text NOT NULL DEFAULT 'Pending',
            ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT NOW(),
            ""ResolutionNotes"" text NULL
        );");

        TryExecute(db, @"CREATE TABLE IF NOT EXISTS ""PasswordResetSessions"" (
            ""Id"" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
            ""UserId"" integer NOT NULL,
            ""SessionToken"" varchar(128) NOT NULL,
            ""OtpHash"" varchar(128) NOT NULL,
            ""ProofTokenHash"" varchar(128) NULL,
            ""FailedAttempts"" integer NOT NULL DEFAULT 0,
            ""RequiresPhoneCheck"" boolean NOT NULL DEFAULT FALSE,
            ""RequiresTelegramApproval"" boolean NOT NULL DEFAULT FALSE,
            ""IsEmailVerified"" boolean NOT NULL DEFAULT FALSE,
            ""IsPhoneVerified"" boolean NOT NULL DEFAULT FALSE,
            ""IsTelegramApproved"" boolean NOT NULL DEFAULT FALSE,
            ""IsRevoked"" boolean NOT NULL DEFAULT FALSE,
            ""IsCompleted"" boolean NOT NULL DEFAULT FALSE,
            ""RequestIp"" varchar(64) NULL,
            ""UserAgent"" varchar(256) NULL,
            ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT NOW(),
            ""LastSentAt"" timestamp with time zone NOT NULL DEFAULT NOW(),
            ""ExpiresAt"" timestamp with time zone NOT NULL,
            ""CompletedAt"" timestamp with time zone NULL
        );");
        TryExecute(db, @"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_PasswordResetSessions_SessionToken"" ON ""PasswordResetSessions""(""SessionToken"");");
        TryExecute(db, @"CREATE INDEX IF NOT EXISTS ""IX_PasswordResetSessions_UserId_CreatedAt"" ON ""PasswordResetSessions""(""UserId"", ""CreatedAt"");");

        TryExecute(db, @"ALTER TABLE ""SiteAnnouncements"" ADD COLUMN IF NOT EXISTS ""Title"" TEXT;");
        TryExecute(db, @"ALTER TABLE ""SiteAnnouncements"" ADD COLUMN IF NOT EXISTS ""TargetPage"" VARCHAR(50);");
        TryExecute(db, @"ALTER TABLE ""SiteAnnouncements"" ADD COLUMN IF NOT EXISTS ""Placement"" VARCHAR(50);");
        TryExecute(db, @"ALTER TABLE ""SiteAnnouncements"" ADD COLUMN IF NOT EXISTS ""ExpiresAt"" timestamp with time zone;");
        TryExecute(db, @"ALTER TABLE ""SiteAnnouncements"" ADD COLUMN IF NOT EXISTS ""ActionText"" VARCHAR(100);");
        TryExecute(db, @"ALTER TABLE ""SiteAnnouncements"" ADD COLUMN IF NOT EXISTS ""ActionUrl"" VARCHAR(500);");
        TryExecute(db, @"ALTER TABLE ""SiteAnnouncements"" ADD COLUMN IF NOT EXISTS ""ImageUrl"" TEXT;");

        TryExecute(db, @"UPDATE ""SiteAnnouncements"" SET
            ""Title"" = COALESCE(""Title"", ''),
            ""Message"" = COALESCE(""Message"", ''),
            ""Type"" = COALESCE(""Type"", 'warning'),
            ""TargetPage"" = COALESCE(""TargetPage"", 'all'),
            ""Placement"" = COALESCE(""Placement"", 'top')
            WHERE ""Title"" IS NULL OR ""Message"" IS NULL OR ""Type"" IS NULL OR ""TargetPage"" IS NULL OR ""Placement"" IS NULL;");

        // Індекси PostgreSQL для високого навантаження
        TryExecute(db, @"CREATE INDEX IF NOT EXISTS ""IX_Users_Email"" ON ""Users""(""Email"");");
        TryExecute(db, @"CREATE INDEX IF NOT EXISTS ""IX_Users_PhoneNumber"" ON ""Users""(""PhoneNumber"");");
        TryExecute(db, @"CREATE INDEX IF NOT EXISTS ""IX_Users_TelegramChatId"" ON ""Users""(""TelegramChatId"");");
        TryExecute(db, @"CREATE INDEX IF NOT EXISTS ""IX_Listings_SellerId_Status"" ON ""Listings""(""SellerId"", ""Status"");");
        TryExecute(db, @"CREATE INDEX IF NOT EXISTS ""IX_Listings_Status_CreatedAt"" ON ""Listings""(""Status"", ""CreatedAt"");");
        TryExecute(db, @"CREATE INDEX IF NOT EXISTS ""IX_StoredFiles_FileName"" ON ""StoredFiles""(""FileName"");");
        TryExecute(db, @"CREATE INDEX IF NOT EXISTS ""IX_Notifications_UserId_IsRead_CreatedAt"" ON ""Notifications""(""UserId"", ""IsRead"", ""CreatedAt"");");
        TryExecute(db, @"CREATE INDEX IF NOT EXISTS ""IX_Reviews_TargetUserId"" ON ""Reviews""(""TargetUserId"");");
        TryExecute(db, @"CREATE INDEX IF NOT EXISTS ""IX_SiteAnnouncements_Active"" ON ""SiteAnnouncements""(""IsActive"", ""TargetPage"", ""Placement"", ""CreatedAt"");");
    }

    private static void TryExecute(AppDbContext db, string sql)
    {
        try
        {
            db.Database.ExecuteSqlRaw(sql);
        }
        catch
        {
            // Column already exists or SQLite ALTER TABLE constraint
        }
    }
}
