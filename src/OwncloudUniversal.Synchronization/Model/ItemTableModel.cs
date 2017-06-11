using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OwncloudUniversal.Synchronization.Model;
using SQLitePCL;

namespace OwncloudUniversal.Synchronization.Model
{
    public class ItemTableModel : AbstractTableModelBase<BaseItem, long>
    {
        private ItemTableModel() { }

        private static ItemTableModel instance;
        public static ItemTableModel GetDefault()
        {
            lock (typeof(ItemTableModel))
            {
                if (instance == null)
                    instance = new ItemTableModel();
                return new ItemTableModel();
        }
    }
        protected override void BindDeleteItemQuery(ISQLiteStatement query, long key)
        {
            query.Bind(1, key);
        }

        protected override void BindSelectAllQuery(ISQLiteStatement query)
        {
            //nix
        }

        protected override void BindSelectByPathQuery(ISQLiteStatement query, string path, long folderId)
        {
            throw new NotImplementedException();
        }

        protected override void BindSelectItemQuery(ISQLiteStatement query, long key)
        {
            query.Bind(1, key);
        }

        protected override string GetDeleteItemQuery()
        {
            return "DELETE from Item where Id = ?";
        }
             
        protected override string GetInsertItemQuery()
        {
            return "INSERT INTO Item (AssociationId, EntityId, IsCollection, ChangeKey, ChangeNumber, SyncPostponed, AdapterType, LastModified) VALUES(@AssociationId, @EntityId, @iscollection, @changekey, @changenumber, @syncpostponed, @adaptertype, @lastmodified)";
        }

        protected override string GetLastInsertRowIdQuery()
        {
            return "SELECT last_insert_rowid() FROM Item";
        }

        protected override string GetSelectAllQuery()
        {
            return "SELECT Id, AssociationId, EntityId, IsCollection, ChangeKey, ChangeNumber, SyncPostponed, AdapterType, LastModified FROM Item ORDER BY EntityId ASC";
        }

        protected override string GetSelectByEntityIdQuery()
        {
            return
                "SELECT Id, AssociationId, EntityId, IsCollection, ChangeKey, ChangeNumber, SyncPostponed, AdapterType, LastModified FROM Item WHERE EntityId = ? COLLATE NOCASE";//some chars might be escaped like this %2c or this %2C
        }

        protected override string GetSelectItemQuery()
        {
            return "SELECT Id, AssociationId, EntityId, IsCollection, ChangeKey, ChangeNumber, SyncPostponed, AdapterType, LastModified FROM Item WHERE Id = ?";
        }

        protected override string GetUpdateItemQuery()
        {
            return "UPDATE Item SET EntityId = ?, IsCollection = ?, ChangeKey = ?, ChangeNumber = ?, SyncPostponed = ?, AdapterType=?, LastModified=? WHERE Id = ?";
        }

        protected override void BindSelectItemQuery(ISQLiteStatement query, string itemId)
        {
            query.Bind(1, itemId);
        }

        protected override void BindInsertItemQuery(ISQLiteStatement query, BaseItem item)
        {
            //"INSERT INTO Item (AssociationId, ItemId, IsCollection, ChangeKey, ChangeNumber) VALUES(@AssociationId, @itemid, @iscollection, @changekey, @changenumber)";
            query.Bind(1, item.Association.Id);
            query.Bind(2, item.EntityId);
            query.Bind(3, item.IsCollection ? 1 : 0);
            query.Bind(4, item.ChangeKey);
            query.Bind(5, item.ChangeNumber);
            query.Bind(6, item.SyncPostponed ? 1 : 0);
            query.Bind(7, item.AdapterType.AssemblyQualifiedName);
            query.Bind(8, SQLite.DateTimeHelper.DateTimeSQLite(item.LastModified));
        }

        protected override void BindUpdateItemQuery(ISQLiteStatement query, BaseItem item, long key)
        {
            //"UPDATE Item SET EntityId = ?, IsCollection = ?, ChangeKey = ?, ChangeNumber = ? WHERE Id = ?";
            query.Bind(1, item.EntityId);
            query.Bind(2, item.IsCollection ? 1 : 0);
            query.Bind(3, item.ChangeKey);
            query.Bind(4, item.ChangeNumber);
            query.Bind(5, item.SyncPostponed ? 1: 0);
            query.Bind(6, item.AdapterType.AssemblyQualifiedName);
            query.Bind(7, SQLite.DateTimeHelper.DateTimeSQLite(item.LastModified));
            query.Bind(8, key);
        }

        protected override BaseItem CreateInstance(ISQLiteStatement query)
        {
            //SELECT Id, AssociationId, EntityId, IsCollection, ChangeKey, ChangeNumber FROM Item WHERE Id = ?
            var item = new BaseItem();
            item.Id = (long)query["Id"];
            var associationModel = FolderAssociationTableModel.GetDefault();
            item.Association = associationModel.GetItem((long)query["AssociationId"]);
            item.EntityId = (string)(query["EntityId"] == null ? default(string) : query["EntityId"]);
            item.IsCollection = (long)query["IsCollection"] == 1;
            item.ChangeKey = (string)query["ChangeKey"];
            item.ChangeNumber = (long)query["ChangeNumber"];
            item.SyncPostponed = (long) query["SyncPostponed"] == 1;
            item.AdapterType = Type.GetType((string)query["AdapterType"]);
            DateTime date;
            if (DateTime.TryParse((string)query["LastModified"], out date))
                item.LastModified = date;
            return item;
        }

        public BaseItem GetItem(BaseItem item)
        {
            using (var query = Connection.Prepare(GetSelectByEntityIdQuery()))
            {
                query.Bind(1, item.EntityId);
                if (query.Step() == SQLiteResult.ROW)
                {
                    item = CreateInstance(query);
                    return item;
                }
            }
            return null;
        }

        public BaseItem GetItemFromEntityId(string entityId)
        {
            var item = new BaseItem();
            using (var query = Connection.Prepare(GetSelectByEntityIdQuery()))
            {
                query.Bind(1, entityId);
                if (query.Step() == SQLiteResult.ROW)
                {
                    item = CreateInstance(query);
                    return item;
                }
            }
            return null;
        }

        public ObservableCollection<BaseItem> GetPostponedItems()
        {
            var items = new ObservableCollection<BaseItem>();
            using (var query = Connection.Prepare("select i.Id, i.AssociationId, i.EntityId, i.IsCollection, i.ChangeKey, i.ChangeNumber, i.SyncPostponed, AdapterType, LastModified from Item i " +
                                                  "where i.SyncPostponed = 1"))
            {
                while (query.Step() == SQLiteResult.ROW)
                {
                    var item = CreateInstance(query);
                    items.Add(item);
                }
            }
            return items;
        }

        public ObservableCollection<BaseItem> GetFilesForFolder(FolderAssociation association, Type adapterType)
        {
            var items = new ObservableCollection<BaseItem>();
            using (var query = Connection.Prepare("select i.Id, i.AssociationId, i.EntityId, i.IsCollection, i.ChangeKey, i.ChangeNumber, i.SyncPostponed, AdapterType, LastModified from Item i " +
                                                  $"where i.AssociationId = '{association.Id}' AND i.AdapterType = '{adapterType.AssemblyQualifiedName}' ORDER BY EntityId ASC"))
            {
                while (query.Step() == SQLiteResult.ROW)
                {
                    var item = CreateInstance(query);
                    items.Add(item);
                }
            }
            return items;
        }

        public ObservableCollection<BaseItem> GetFilesForFolder(string folderPath)
        {
            var items = new ObservableCollection<BaseItem>();
            using (var query = Connection.Prepare("select i.Id, i.AssociationId, i.EntityId, i.IsCollection, i.ChangeKey, i.ChangeNumber, i.SyncPostponed, AdapterType, LastModified from Item i " +
                                                  $"where i.EntityId like '{folderPath}%'  ORDER BY EntityId " +
                                                  $"COLLATE NOCASE"))
            {
                while (query.Step() == SQLiteResult.ROW)
                {
                    var item = CreateInstance(query);
                    items.Add(item);
                }
            }
            return items;
        }

        public void DeleteItemsFromAssociation(FolderAssociation association)
        {
            using (var query = Connection.Prepare($"delete from Item where AssociationId='{association.Id}'"))
            {
                query.Step();
            }
        }
    }
}
