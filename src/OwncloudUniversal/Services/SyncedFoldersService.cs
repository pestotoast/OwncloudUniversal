using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.AccessCache;
using OwncloudUniversal.Model;
using OwncloudUniversal.Synchronization.Model;
using OwncloudUniversal.OwnCloud;
using OwncloudUniversal.OwnCloud.Model;

namespace OwncloudUniversal.Services
{
    public class SyncedFoldersService
    {

        public async Task<FolderAssociation> AddFolderToSyncAsync (StorageFolder folder, DavItem remoteFolderItem)
        {
            StorageApplicationPermissions.FutureAccessList.Add(folder);
            var properties = await folder.Properties.RetrievePropertiesAsync(new List<string> { "System.DateModified" });

            FolderAssociation fa = new FolderAssociation
            {
                IsActive = true,
                LocalFolderId = 0,
                RemoteFolderId = 0,
                SyncDirection = SyncDirection.FullSync,
                LastSync = DateTime.MinValue
            };
            FolderAssociationTableModel.GetDefault().InsertItem(fa);
            fa = FolderAssociationTableModel.GetDefault().GetLastInsertItem();

            BaseItem li = new LocalItem
            {
                IsCollection = true,
                LastModified = ((DateTimeOffset)properties["System.DateModified"]).LocalDateTime,
                EntityId = folder.Path,
                Association = fa,
            };
            ItemTableModel.GetDefault().InsertItem(li);
            li = ItemTableModel.GetDefault().GetLastInsertItem();

            remoteFolderItem.Association = fa;
            ItemTableModel.GetDefault().InsertItem(remoteFolderItem);
            var ri = ItemTableModel.GetDefault().GetLastInsertItem();

            fa.RemoteFolderId = ri.Id;
            fa.LocalFolderId = li.Id;
            FolderAssociationTableModel.GetDefault().UpdateItem(fa, fa.Id);
            var link = new LinkStatus(li,ri);
            LinkStatusTableModel.GetDefault().InsertItem(link);
            return fa;
        }

        public FolderAssociation CreateCameraUploadAssociation(DavItem targetFolder)
        {
            foreach (var association in FolderAssociationTableModel.GetDefault().GetAllItems().ToList().Where(x => x.SupportsInstantUpload))
            {
                RemoveFromSyncedFolders(association);
            }
            var folder = KnownFolders.PicturesLibrary;
            FolderAssociation fa = new FolderAssociation
            {
                IsActive = true,
                LocalFolderId = 0,
                RemoteFolderId = 0,
                SyncDirection = SyncDirection.UploadOnly,
                SupportsInstantUpload = true,
                LastSync = DateTime.MinValue
            };
            FolderAssociationTableModel.GetDefault().InsertItem(fa);
            fa = FolderAssociationTableModel.GetDefault().GetLastInsertItem();

            BaseItem li = new LocalItem
            {
                IsCollection = true,
                LastModified = DateTime.Now,
                EntityId = folder.Path,
                Association = fa,
            };
            ItemTableModel.GetDefault().InsertItem(li);
            li = ItemTableModel.GetDefault().GetLastInsertItem();

            targetFolder.Association = fa;
            ItemTableModel.GetDefault().InsertItem(targetFolder);
            var ri = ItemTableModel.GetDefault().GetLastInsertItem();

            fa.RemoteFolderId = ri.Id;
            fa.LocalFolderId = li.Id;
            FolderAssociationTableModel.GetDefault().UpdateItem(fa, fa.Id);
            var link = new LinkStatus(li, ri);
            LinkStatusTableModel.GetDefault().InsertItem(link);
            return fa;
        }

        public List<FolderAssociation> GetAllSyncedFolders()
        {
            return FolderAssociationTableModel.GetDefault().GetAllItems().ToList().Where(x => x.SupportsInstantUpload == false).ToList();
        }

        public void RemoveFromSyncedFolders(FolderAssociation association)
        {
            ItemTableModel.GetDefault().DeleteItemsFromAssociation(association);
            LinkStatusTableModel.GetDefault().DeleteLinksFromAssociation(association);
            FolderAssociationTableModel.GetDefault().DeleteItem(association.Id);
        }
    }
}
