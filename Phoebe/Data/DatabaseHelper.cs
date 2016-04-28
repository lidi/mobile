using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using SQLite.Net;
using SQLite.Net.Interop;
using Toggl.Phoebe.Logging;
using XPlatUtils;

namespace Toggl.Phoebe.Data
{
    static class DatabaseHelper
    {
        public static bool FileExists(string path)
        {
            return File.Exists(path) && new FileInfo(path).Length > 0;
        }

        public static string GetDatabasePath(string dbDir, int dbVersion)
        {
            return Path.Combine(dbDir, dbVersion == 0 ? "toggl.db" : $"toggl.{dbVersion}.db");
        }

        public static string GetDatabaseDirectory()
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.Personal);
        }


        public static int GetVersion(SQLiteConnection connection)
        {
            var tableInfo = connection.GetTableInfo(nameof(SyncSqliteDataStore.MetaData));
            if (tableInfo == null || tableInfo.Count == 0)
                return 0;
            var data = connection.Table<SyncSqliteDataStore.MetaData>()
                       .Where(x => x.Id == nameof(SyncSqliteDataStore.DB_VERSION))
                       .FirstOrDefault();
            return data?.Convert<int>() ?? 0;
        }

        public static int CheckOldDb(string dbDir)
        {
            for (var i = 0; i < SyncSqliteDataStore.DB_VERSION; i++)
            {
                var dbPath = GetDatabasePath(dbDir, i);
                if (FileExists(dbPath))
                    return i;
            }

            return -1;
        }

        private static void resolveMigrateException(
            MigrationException ex, SQLiteConnection newDB, string dbDir)
        {
            var logger = ServiceContainer.Resolve<ILogger>();
            logger.Error(nameof(DatabaseMigrator), ex, ex.Message);

            // Close newDB to prevent conflicts when deleting the file
            if (newDB != null) { newDB.Close(); }

            var dbPath = GetDatabasePath(dbDir, SyncSqliteDataStore.DB_VERSION);
            if (FileExists(dbPath)) { File.Delete(dbPath); }
        }

        public static bool Migrate(ISQLitePlatform platformInfo, string dbDir,
                                   int fromVersion, int desiredVersion,
                                   Action<float> progressReporter)
        {
            SQLiteConnection migrateFromDB = null, newDB = null;

            // Close current connection to prevent conflicts
            var dataStore = ServiceContainer.Resolve<ISyncDataStore>();
            dataStore.Dispose();

            try
            {
                migrateFromDB = new SQLiteConnection(platformInfo, GetDatabasePath(dbDir, fromVersion));

                var desiredDBPath = GetDatabasePath(dbDir, desiredVersion);

                while (true)
                {
                    var migrator = DatabaseMigrator.ForVersion(fromVersion);

                    var expectedNewVersion = migrator.NewVersion;

                    validateMigrator(fromVersion, migrator, desiredVersion);

                    // Make sure the desiredDBPath doesn't exist to prevent corruption of data
                    if (expectedNewVersion == desiredVersion && FileExists(desiredDBPath))
                        File.Delete(desiredDBPath);

                    newDB = new SQLiteConnection(platformInfo, expectedNewVersion == desiredVersion
                                                 ? desiredDBPath
                                                 : "Data Source=:memory:");

                    migrator.Migrate(migrateFromDB, newDB, progressReporter);
                    migrateFromDB.Close();

                    var newVersion = GetVersion(newDB);

                    validateMigratedVersion(fromVersion, expectedNewVersion, newVersion, desiredVersion);

                    if (newVersion == desiredVersion)
                    {
                        // Migration has been successful, delete old db
                        var oldDbPath = GetDatabasePath(dbDir, fromVersion);
                        if (FileExists(oldDbPath)) { File.Delete(oldDbPath); }
                        return true;
                    }

                    migrateFromDB = newDB;
                }
            }
            catch (MigrationException ex)
            {
                resolveMigrateException(ex, newDB, dbDir);
            }
            catch (Exception ex)
            {
                var ex2 = new MigrationException("Unknown exception during migration", ex);
                resolveMigrateException(ex2, newDB, dbDir);
            }
            finally
            {
                if (newDB != null) { newDB.Close(); }
                if (migrateFromDB != null) { migrateFromDB.Close(); }

                // Register ISyncDataStore again
                var dbPath = GetDatabasePath(dbDir, SyncSqliteDataStore.DB_VERSION);
                ServiceContainer.Register<ISyncDataStore>(new SyncSqliteDataStore(dbPath, platformInfo));
            }
            return false;
        }

        private static void validateMigratedVersion(int oldVersion, int expectedNewVersion, int newVersion, int desiredVersion)
        {
            if (newVersion != expectedNewVersion)
                throw new MigrationException($"Expected new database version {expectedNewVersion}, but was {newVersion}");

            if (newVersion <= oldVersion || newVersion > desiredVersion)
                throw new MigrationException($"Database migrator upgraded from {oldVersion} to {newVersion} (app's version is {desiredVersion}).");
        }

        static void validateMigrator(int oldVersion, DatabaseMigrator migrator, int desiredVersion)
        {
            if (migrator == null)
                throw new MigrationException($"Do not know how to migrate database version {oldVersion} (app's version is {desiredVersion})");

            if (migrator.OldVersion != oldVersion)
                throw new MigrationException($"received wrong database migrator");
        }
    }

}