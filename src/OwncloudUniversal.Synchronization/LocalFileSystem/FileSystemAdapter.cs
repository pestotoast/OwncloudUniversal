using OwncloudUniversal.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Provider;
using Windows.Storage.Search;
using OwncloudUniversal.Synchronization.Model;
using OwncloudUniversal.Synchronization.Synchronisation;

namespace OwncloudUniversal.Synchronization.LocalFileSystem
{
    public class FileSystemAdapter : AbstractAdapter
    {
        private async Task _GetChangesFromSearchIndex(StorageFolder folder, long associationId,
            List<BaseItem> result)
        {
            var association = FolderAssociationTableModel.GetDefault().GetItem(associationId);
            var files = new List<IStorageItem>();
            var options = new QueryOptions();
            options.FolderDepth = FolderDepth.Deep;
            options.IndexerOption = IndexerOption.UseIndexerWhenAvailable;
            //details about filesystem queries using the indexer
            //https://msdn.microsoft.com/en-us/magazine/mt620012.aspx
            string timeFilter = "System.Search.GatherTime:>=" + association.LastSync;
            options.ApplicationSearchFilter = timeFilter;
            var prefetchedProperties = new List<string> {"System.DateModified", "System.Size"};
            options.SetPropertyPrefetch(PropertyPrefetchOptions.BasicProperties, prefetchedProperties);
            if (!folder.AreQueryOptionsSupported(options))
                throw new Exception($"Windows Search Index has to be enabled for {folder.Path}");

            var queryResult = folder.CreateItemQueryWithOptions(options);
            queryResult.ApplyNewQueryOptions(options);
            files.AddRange(await queryResult.GetItemsAsync());

            foreach (var file in files)
            {
                try
                {
                    IDictionary<string, object> propertyResult = null;
                    if (file.IsOfType(StorageItemTypes.File))
                        propertyResult =
                            await ((StorageFile) file).Properties.RetrievePropertiesAsync(prefetchedProperties);
                    else if (file.IsOfType(StorageItemTypes.Folder))
                        propertyResult =
                            await ((StorageFolder) file).Properties.RetrievePropertiesAsync(prefetchedProperties);
                    var item = new LocalItem(new FolderAssociation {Id = associationId}, file, propertyResult);

                    var existingItem = ItemTableModel.GetDefault().GetItem(item);
                    if (existingItem != null)
                    {
                        if (!item.IsCollection)
                        {
                            //additional check if the file has changed:
                            //even though the size not the best way to make sure if a file has changed
                            //its very unlikely that after a change they have the exact same byte count
                            //so its the best option we have
                            if ((ulong)propertyResult["System.Size"] == existingItem.Size)
                                continue;
                        }
                    }
                    result.Add(item);
                }
                catch (Exception)
                {
                    Debug.WriteLine(file);
                    throw;
                }
            }

            if (!IsBackgroundSync)
            {
                var unsynced =
                    ItemTableModel.GetDefault()
                        .GetPostponedItems()
                        .Where(x => x.AdapterType == typeof(FileSystemAdapter));
                foreach (var abstractItem in unsynced)
                {
                    abstractItem.SyncPostponed = false;
                }
                result.AddRange(unsynced);
            }
        }

        public override async Task<BaseItem> AddItem(BaseItem item)
        {
            StorageFolder folder = null;
            try
            {
                folder = await _GetStorageFolder(item);
            }
            catch (ArgumentException)
            {
                ToastHelper.SendToast($"Path has invalid characters {Uri.EscapeUriString(item.EntityId)}");
                return item;
            }
            IStorageItem storageItem;
            var displayName = _BuildFilePath(item).TrimEnd('\\');
            displayName = displayName.Substring(displayName.LastIndexOf('\\') + 1);
            if (!PathIsValid(displayName))
            {
                ToastHelper.SendToast($"Path has invalid characters {Uri.EscapeUriString(item.EntityId)}");
                return item;
            }
            if (item.IsCollection)
            {
                storageItem = await folder.CreateFolderAsync(displayName, CreationCollisionOption.OpenIfExists);
            }
            else
            {
                storageItem = await folder.TryGetItemAsync(displayName) ?? await folder.CreateFileAsync(displayName, CreationCollisionOption.OpenIfExists);
                var adapter = (IBackgroundSyncAdapter)LinkedAdapter;
                await adapter.CreateDownload(item, storageItem);
            }
            BasicProperties bp = await storageItem.GetBasicPropertiesAsync();
            var targetItem = new LocalItem(item.Association, storageItem, bp);
            return targetItem;
        }

        public override async Task<BaseItem> UpdateItem(BaseItem item)
        {
            return await AddItem(item);
        }

        public override async Task DeleteItem(BaseItem item)
        {
            long fileId;
            var link = LinkStatusTableModel.GetDefault().GetItem(item);
            if (link == null)
            {
                await LogHelper.Write($"LinkStatus could not be found: EntityId: {item.EntityId} Id: {item.Id}");
                return;
            }
            fileId = item.Id == link.SourceItemId ? link.TargetItemId : link.SourceItemId;
            var fileItem = ItemTableModel.GetDefault().GetItem(fileId);
            if(fileItem == null)
                return;

            if (item.IsCollection)
            {
                if (new DirectoryInfo(fileItem.EntityId).Exists)
                {
                    var folder = await StorageFolder.GetFolderFromPathAsync(fileItem.EntityId);
                    await folder.DeleteAsync(StorageDeleteOption.PermanentDelete);
                }
            }
            else
            {
                if (new FileInfo(fileItem.EntityId).Exists)
                {
                    var file = await StorageFile.GetFileFromPathAsync(fileItem.EntityId);
                    await file.DeleteAsync(StorageDeleteOption.PermanentDelete);
                }
            }
        }

        public override async Task<List<BaseItem>> GetUpdatedItems(FolderAssociation association)
        {
            List<BaseItem> items = new List<BaseItem>();
            var item = GetAssociatedItem(association.LocalFolderId);
            StorageFolder folder = await StorageFolder.GetFolderFromPathAsync(item.EntityId);
            await _GetChangesFromSearchIndex(folder, association.Id, items);
            //await _CheckLocalFolderRecursive(folder, association.Id, items);
            return items;
        }

        private async Task<StorageFolder> _GetStorageFolder(BaseItem item)
        {
            string filePath = item.IsCollection ? _BuildFolderPath(item) : _BuildFilePath(item);
            if (!PathIsValid(filePath))
                throw new ArgumentException();
            string folderPath = Path.GetDirectoryName(filePath);

            var currentFolder = await StorageFolder.GetFolderFromPathAsync(item.Association.LocalFolderPath);
            string[] folders = folderPath.Replace(currentFolder.Path, "").TrimStart('\\').Split('\\');

            foreach (var folder in folders)
            {
                if(string.IsNullOrWhiteSpace(folder))
                    continue;
                IStorageItem tmp = null;
                tmp = await currentFolder.TryGetItemAsync(folder) ?? await currentFolder.CreateFolderAsync(folder);
                currentFolder = (StorageFolder)tmp;
            }
            return currentFolder;
        }

        private string _BuildFilePath(BaseItem item)
        {
            var folderUri = new Uri(GetAssociatedItem(item.Association.LocalFolderId).EntityId);
            var remoteFolder = GetAssociatedItem(item.Association.RemoteFolderId);
            var relativefileUri = item.EntityId.Replace(remoteFolder.EntityId, "");
            string path = Uri.UnescapeDataString(relativefileUri.ToString().Replace('/', '\\'));
            var result = folderUri.LocalPath + '\\' + path;
            return folderUri.LocalPath + '\\' + path;
        }

        private string _BuildFolderPath(BaseItem item)
        {
            var folderUri = new Uri(GetAssociatedItem(item.Association.LocalFolderId).EntityId);
            var remoteFolder = GetAssociatedItem(item.Association.RemoteFolderId);
            var relativefileUri = item.EntityId.Replace(remoteFolder.EntityId, "");
            if(relativefileUri.Contains('/'))
                relativefileUri = relativefileUri.Remove(relativefileUri.LastIndexOf("/"));
            string relativePath = Uri.UnescapeDataString(relativefileUri.ToString().Replace('/', '\\'));
            var absoltuePath = folderUri.LocalPath + '\\' + relativePath;
            //var result = absoltuePath.Remove(absoltuePath.LastIndexOf("\\"));
            return absoltuePath;
        }

        private BaseItem GetAssociatedItem(long id)
        {
            return ItemTableModel.GetDefault().GetItem(id);
        }

        public FileSystemAdapter(bool isBackgroundSync, AbstractAdapter linkedAdapter) : base(isBackgroundSync, linkedAdapter)
        {
        }

        public override async Task<List<BaseItem>> GetDeletedItemsAsync(FolderAssociation association)
        {
            List<BaseItem> result = new List<BaseItem>();
            //get the storagefolder
            var localFolderItem = GetAssociatedItem(association.LocalFolderId);
            StorageFolder sFolder = await StorageFolder.GetFolderFromPathAsync(localFolderItem.EntityId);

            var sItems = new List<IStorageItem>();
            var options = new QueryOptions();
            options.FolderDepth = FolderDepth.Deep;
            options.IndexerOption = IndexerOption.UseIndexerWhenAvailable;
            var itemQuery = sFolder.CreateItemQueryWithOptions(options);
            var queryTask = itemQuery.GetItemsAsync();

            //get all the files and folders from the db that were inside the folder at the last time
            var existingItems = ItemTableModel.GetDefault().GetFilesForFolder(association, this.GetType());
            sItems.AddRange(await queryTask);

            var storageItems = new List<BaseItem>();
            foreach (var storageItem in sItems)
            {
                storageItems.Add(new BaseItem{EntityId = storageItem.Path});
            }
            var missingItems = existingItems.Except(storageItems, new EnityIdComparer()).ToList();

            foreach (var missingItem in missingItems)
            {
                if (missingItem.EntityId == sFolder.Path) continue;
                //additional check if the file really does not exist
                //for some reason the query seems to return wrong results sometimes
                if (!missingItem.IsCollection)
                {
                    FileInfo fInfo = new FileInfo(missingItem.EntityId);
                    if (!fInfo.Exists)
                        result.Add(missingItem);
                }
                else
                {
                    DirectoryInfo dInfo = new DirectoryInfo(missingItem.EntityId);
                    if (!dInfo.Exists)
                        result.Add(missingItem);
                }
            }
            return result;
        }
        private bool PathIsValid(string path)
        {
            string chars = "?*<>|\"";
            int indexOf = path.IndexOfAny(chars.ToCharArray());
            if (indexOf == -1)
            {
                return true;
            }
            return false;
        }
        public override string BuildEntityId(BaseItem item)
        {
            var folderUri = new Uri(GetAssociatedItem(item.Association.LocalFolderId).EntityId);
            var remoteFolder = GetAssociatedItem(item.Association.RemoteFolderId);
            var relativefileUri = item.EntityId.Replace(remoteFolder.EntityId, "");
            string path = Uri.UnescapeDataString(relativefileUri.ToString().Replace('/', '\\'));
            var result = folderUri.LocalPath + '\\' + path;
            return result.TrimEnd('\\');
        }

    }
}
