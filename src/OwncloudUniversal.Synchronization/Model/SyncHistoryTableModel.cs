using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SQLitePCL;

namespace OwncloudUniversal.Synchronization.Model
{
    class SyncHistoryTableModel : AbstractTableModelBase<SyncHistoryEntry, long>
    {
        protected override SyncHistoryEntry CreateInstance(ISQLiteStatement query)
        {
            var entry = new SyncHistoryEntry();
            entry.Id = (long) query["Id"];
            DateTime date;
            if (!DateTime.TryParse((string)query["CreateDate"], out date))
                date = DateTime.MinValue;
            entry.CreateDate = date;
            entry.Message = (string) query["Message"];
            SyncResult result;
            Enum.TryParse((string) query["Result"], out result);
            entry.Result = result;
            entry.SourceItemId = (long) query["SourceItemId"];
            entry.TargetItemId = (long)query["TargetItemId"];
            return entry;
        }

        protected override string GetSelectItemQuery()
        {
            return
                "SELECT Id, CreateDate, Message, Result, SourceItemId, TargetItemId From SyncHistory where Id = @Id";
        }

        protected override void BindSelectItemQuery(ISQLiteStatement query, long key)
        {
            query.Bind("Id", key);
        }

        protected override void BindSelectItemQuery(ISQLiteStatement query, string itemId)
        {
            throw new NotImplementedException();
        }

        protected override string GetInsertItemQuery()
        {
            return
                "INSERT INTO SyncHistory (CreateDate, Message, Result, SourceItemId, TargetItemId) VALUES (@CreateDate, @Message, @Result, @SourceItemId, @TargetItemId)";
        }

        protected override void BindInsertItemQuery(ISQLiteStatement query, SyncHistoryEntry item)
        {
            query.Bind(nameof(item.CreateDate), item.CreateDate);
            query.Bind(nameof(item.Message), item.Message);
            query.Bind(nameof(item.Result), item.Result);
            query.Bind(nameof(item.SourceItemId), item.SourceItemId);
            query.Bind(nameof(item.TargetItemId), item.TargetItemId);

        }

        protected override string GetUpdateItemQuery()
        {
            return
                "UPDATE SyncHistory SET CreateDate = @CreateDate, Message = @Message, Result = @Result, SourceItemId = @SourceItemId, TargetItemId = @TargetItemId where Id = @Id";
        }

        protected override void BindUpdateItemQuery(ISQLiteStatement query, SyncHistoryEntry item, long key)
        {
            query.Bind(nameof(item.Id), key);
            query.Bind(nameof(item.CreateDate), item.CreateDate);
            query.Bind(nameof(item.Message), item.Message);
            query.Bind(nameof(item.Result), item.Result);
            query.Bind(nameof(item.SourceItemId), item.SourceItemId);
            query.Bind(nameof(item.TargetItemId), item.TargetItemId);
        }

        protected override string GetDeleteItemQuery()
        {
            return "DELTE FROM SyncHistory Where Id = @Id";
        }

        protected override void BindDeleteItemQuery(ISQLiteStatement query, long key)
        {
            query.Bind("Id", key);
        }

        protected override string GetSelectAllQuery()
        {
            return
                "SELECT Id, CreateDate, Message, Result, SourceItemId, TargetItemId From SyncHistory";
        }

        protected override void BindSelectAllQuery(ISQLiteStatement query)
        {
            //nothing to do
        }

        protected override string GetLastInsertRowIdQuery()
        {
            return "SELECT last_insert_rowid() FROM SyncHistory";
        }

        protected override string GetSelectByEntityIdQuery()
        {
            throw new NotImplementedException();
        }

        protected override void BindSelectByPathQuery(ISQLiteStatement query, string path, long folderId)
        {
            throw new NotImplementedException();
        }
    }
}
