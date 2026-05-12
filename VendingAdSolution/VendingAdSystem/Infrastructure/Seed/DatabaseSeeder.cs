using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using VendingAdSystem.Domain.Entities;
using VendingAdSystem.Infrastructure.Persistence;

namespace VendingAdSystem.Infrastructure.Seed;

public static class DatabaseSeeder
{
    public static void Seed(AppDbContext db)
    {
        db.Database.EnsureCreated();

        if (db.Database.IsSqlite())
        {
            EnsureColumn(db, "Users", "Username", "TEXT NOT NULL DEFAULT ''");
            EnsureColumn(db, "Devices", "UserId", "INTEGER NULL");
            EnsureColumn(db, "Devices", "ClaimCode", "TEXT NULL");
            EnsureColumn(db, "Devices", "ClaimedAt", "TEXT NULL");
            EnsureColumn(db, "Medias", "UserId", "INTEGER NULL");
            EnsurePlaybackScheduleColumns(db);
            MigratePlaylistsSchema(db);
            EnsurePlaybackScheduleTables(db);

            db.Database.ExecuteSqlRaw("UPDATE Users SET Username = Email WHERE Username = ''");
            db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_Users_Username ON Users (Username)");
            db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_Devices_ClaimCode ON Devices (ClaimCode) WHERE ClaimCode IS NOT NULL");
        }

        if (!db.Admins.Any())
        {
            db.Admins.Add(new Admin
            {
                Email = "admin@admin",
                PasswordHash = HashPassword("admin@admin"),
                FullName = "System Administrator",
                Role = "Admin",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });
            db.SaveChanges();
        }

        if (!db.Users.Any())
        {
            db.Users.AddRange(new User { Username = "test", Email = "test@test", PasswordHash = HashPassword("test@test"), FullName = "Demo User", IsActive = true });
            db.SaveChanges();
        }

        var users = db.Users.OrderBy(u => u.Id).ToList();
        if (users.Any() && db.Database.IsSqlite())
        {
            db.Database.ExecuteSqlRaw("UPDATE Medias SET UserId = {0} WHERE UserId IS NULL", users[0].Id);
        }

        if (users.Any() && !db.Devices.Any())
        {
            db.Devices.AddRange(
                new Device { DeviceCode = "TAB-01", UserId = users[0].Id, Location = "Vincom Center", LastSeen = DateTime.UtcNow.AddMinutes(-2), IsActive = true },
                new Device { DeviceCode = "TAB-02", UserId = users[0].Id, Location = "Ben Thanh Market", LastSeen = DateTime.UtcNow.AddMinutes(-5), IsActive = true }
            );
            db.SaveChanges();
        }
    }

    private static string HashPassword(string password)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashedBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(hashedBytes);
    }

    private static void EnsureColumn(AppDbContext db, string tableName, string columnName, string columnDefinition)
    {
        if (HasColumn(db, tableName, columnName))
            return;

        try
        {
            ExecuteSchemaCommand(db, $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition}");
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 1 && ex.Message.Contains("duplicate column name", StringComparison.OrdinalIgnoreCase))
        {
            // EnsureCreated may already create new columns before the repair step runs.
        }
    }

    private static void ExecuteSchemaCommand(AppDbContext db, string commandText)
    {
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;

        if (shouldClose)
            connection.Open();

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = commandText;
            command.ExecuteNonQuery();
        }
        finally
        {
            if (shouldClose)
                connection.Close();
        }
    }

    private static bool HasColumn(AppDbContext db, string tableName, string columnName)
    {
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;

        if (shouldClose)
            connection.Open();

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info({tableName})";
            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                var name = reader[1]?.ToString();
                if (string.Equals(name, columnName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
        finally
        {
            if (shouldClose)
                connection.Close();
        }
    }

    private static void EnsurePlaybackScheduleTables(AppDbContext db)
    {
        ExecuteSchemaCommand(db, """
            CREATE TABLE IF NOT EXISTS PlaybackSchedules (
                Id INTEGER NOT NULL CONSTRAINT PK_PlaybackSchedules PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                UserId INTEGER NOT NULL,
                StartDate TEXT NOT NULL,
                EndDate TEXT NOT NULL,
                StartTime TEXT NOT NULL,
                EndTime TEXT NOT NULL,
                IsActive INTEGER NOT NULL,
                IsImmediate INTEGER NOT NULL DEFAULT 0,
                ImmediateStartedAt TEXT NULL,
                CreatedAt TEXT NOT NULL,
                CONSTRAINT FK_PlaybackSchedules_Users_UserId FOREIGN KEY (UserId) REFERENCES Users (Id) ON DELETE CASCADE
            )
            """);

        ExecuteSchemaCommand(db, """
            CREATE TABLE IF NOT EXISTS PlaybackScheduleDevices (
                Id INTEGER NOT NULL CONSTRAINT PK_PlaybackScheduleDevices PRIMARY KEY AUTOINCREMENT,
                PlaybackScheduleId INTEGER NOT NULL,
                DeviceId INTEGER NOT NULL,
                CONSTRAINT FK_PlaybackScheduleDevices_PlaybackSchedules_PlaybackScheduleId FOREIGN KEY (PlaybackScheduleId) REFERENCES PlaybackSchedules (Id) ON DELETE CASCADE,
                CONSTRAINT FK_PlaybackScheduleDevices_Devices_DeviceId FOREIGN KEY (DeviceId) REFERENCES Devices (Id) ON DELETE CASCADE
            )
            """);

        ExecuteSchemaCommand(db, """
            CREATE TABLE IF NOT EXISTS PlaybackScheduleItems (
                Id INTEGER NOT NULL CONSTRAINT PK_PlaybackScheduleItems PRIMARY KEY AUTOINCREMENT,
                PlaybackScheduleId INTEGER NOT NULL,
                MediaId INTEGER NOT NULL,
                OrderIndex INTEGER NOT NULL,
                CONSTRAINT FK_PlaybackScheduleItems_PlaybackSchedules_PlaybackScheduleId FOREIGN KEY (PlaybackScheduleId) REFERENCES PlaybackSchedules (Id) ON DELETE CASCADE,
                CONSTRAINT FK_PlaybackScheduleItems_Medias_MediaId FOREIGN KEY (MediaId) REFERENCES Medias (Id) ON DELETE CASCADE
            )
            """);

        ExecuteSchemaCommand(db, "CREATE INDEX IF NOT EXISTS IX_PlaybackSchedules_UserId ON PlaybackSchedules (UserId)");
        ExecuteSchemaCommand(db, "CREATE INDEX IF NOT EXISTS IX_PlaybackScheduleDevices_PlaybackScheduleId ON PlaybackScheduleDevices (PlaybackScheduleId)");
        ExecuteSchemaCommand(db, "CREATE INDEX IF NOT EXISTS IX_PlaybackScheduleDevices_DeviceId ON PlaybackScheduleDevices (DeviceId)");
        ExecuteSchemaCommand(db, "CREATE INDEX IF NOT EXISTS IX_PlaybackScheduleItems_PlaybackScheduleId ON PlaybackScheduleItems (PlaybackScheduleId)");
        ExecuteSchemaCommand(db, "CREATE INDEX IF NOT EXISTS IX_PlaybackScheduleItems_MediaId ON PlaybackScheduleItems (MediaId)");
    }

    private static void EnsurePlaybackScheduleColumns(AppDbContext db)
    {
        EnsureColumn(db, "PlaybackSchedules", "IsImmediate", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(db, "PlaybackSchedules", "ImmediateStartedAt", "TEXT NULL");
    }

    private static void MigratePlaylistsSchema(AppDbContext db)
    {
        if (!HasColumn(db, "Playlists", "StartDate") && !HasColumn(db, "Playlists", "EndDate"))
            return;

        ExecuteSchemaCommand(db, "PRAGMA foreign_keys=OFF");
        try
        {
            ExecuteSchemaCommand(db, "DROP TABLE IF EXISTS Playlists_New");
            ExecuteSchemaCommand(db, """
                CREATE TABLE Playlists_New (
                    Id INTEGER NOT NULL CONSTRAINT PK_Playlists PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    UserId INTEGER NOT NULL,
                    IsActive INTEGER NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    CONSTRAINT FK_Playlists_Users_UserId FOREIGN KEY (UserId) REFERENCES Users (Id) ON DELETE CASCADE
                )
                """);

            ExecuteSchemaCommand(db, """
                INSERT INTO Playlists_New (Id, Name, UserId, IsActive, CreatedAt)
                SELECT Id, Name, UserId, IsActive, CreatedAt
                FROM Playlists
                """);

            ExecuteSchemaCommand(db, "DROP TABLE Playlists");
            ExecuteSchemaCommand(db, "ALTER TABLE Playlists_New RENAME TO Playlists");
            ExecuteSchemaCommand(db, "CREATE INDEX IF NOT EXISTS IX_Playlists_UserId ON Playlists (UserId)");
        }
        finally
        {
            ExecuteSchemaCommand(db, "PRAGMA foreign_keys=ON");
        }
    }
}
