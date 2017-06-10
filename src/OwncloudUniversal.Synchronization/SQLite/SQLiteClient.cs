using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SQLitePCL;

namespace OwncloudUniversal.Synchronization.SQLite
{
    public static class SQLiteClient
    {
        private static SQLiteConnection _connection;
        public static SQLiteConnection Connection => _connection ?? (_connection = new SQLiteConnection("webdav-sync.db"));
        public static void Init()
        {
            string query = "";
            query = @"CREATE TABLE IF NOT EXISTS [Item] (
                        [Id] INTEGER  NOT NULL PRIMARY KEY AUTOINCREMENT,
                        [AssociationId] INTEGER NULL,
                        [IsCollection] BOOLEAN  NULL,
                        [ChangeNumber] INTEGER NULL,
                        [ChangeKey] NVARCHAR(510)  NULL,
                        [EntityId] NVARCHAR(510) NOT NULL UNIQUE,
                        [SyncPostponed] BOOLEAN NULL,
                        [AdapterType] NVARCHAR(128) NULL,
                        [LastModified] NVARCHAR(32) NULL
                    );";
            using (var statement = Connection.Prepare(query))
            {
                statement.Step();
            }

            query = @"CREATE TABLE IF NOT EXISTS [LinkStatus] (
                        [Id] INTEGER  NOT NULL PRIMARY KEY AUTOINCREMENT,
                        [TargetItemId] INTEGER NULL,
                        [SourceItemId] INTEGER  NULL,
                        [ChangeNumber] INTEGER  NULL,
                        [AssociationId] INTEGER NULL
                    );";
            using (var statement = Connection.Prepare(query))
            {
                statement.Step();
            }

            query = @"CREATE TABLE IF NOT EXISTS [Association] (
                        [Id] INTEGER  PRIMARY KEY AUTOINCREMENT NOT NULL,
                        [LocalItemId] INTEGER  NULL,
                        [RemoteItemId] INTEGER  NULL,
                        [IsActive] BOOLEAN  NULL,
                        [SyncDirection] NVARCHAR(32),
                        [LastSync] NVARCHAR(32)
                    );";
            using (var statement = Connection.Prepare(query))
            {
                statement.Step();
            }

            query = @"CREATE TABLE IF NOT EXISTS [SyncHistory] (
                        [Id] INTEGER  PRIMARY KEY AUTOINCREMENT NOT NULL,
                        [TargetItemId] INTEGER  NULL,
                        [SourceItemId] INTEGER  NULL,
                        [CreateDate] NVARCHAR(32),
                        [Result] NVARCHAR(32),
                        [Message] TEXT,
                        [OldItemDisplayName] TEXT
                    );";
            using (var statement = Connection.Prepare(query))
            {
                statement.Step();
            }

            //activate foreign keys?
            query = "PRAGMA foreign_keys = ON";
            using (var statement = Connection.Prepare(query))
            {
                statement.Step();
            }

        }

        public static void Reset()
        {
            string query = "";

            query = @"DROP TABLE LinkStatus;";
            using (var statement = Connection.Prepare(query))
            {
                statement.Step();
            }

            query = @"DROP TABLE Item;";
            using (var statement = Connection.Prepare(query))
            {
                statement.Step();
            }

            query = @"DROP TABLE Association;";
            using (var statement = Connection.Prepare(query))
            {
                statement.Step();
            }
            Init();
        }
    }
}
