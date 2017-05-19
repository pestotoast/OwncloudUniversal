using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OwncloudUniversal.Shared.Model;

namespace OwncloudUniversal.Shared.Model
{
    /// <summary>
    /// Base class for the adapters used for the synchronisation.
    /// Every Adapter needs a linked Adapter to sync with.
    /// Example: WebDavAdapter and LocalFileSystemAdapter belong together and sync files with each other 
    /// </summary>
    public abstract class AbstractAdapter
    {
        /// <summary>
        /// Initializes a new Adapter
        /// </summary>
        /// <param name="isBackgroundSync">Has to be true if the sync runs in a background task</param>
        /// <param name="linkedAdapter">The instance of the adapter to sync with</param>
        protected AbstractAdapter(bool isBackgroundSync, AbstractAdapter linkedAdapter)
        {
            IsBackgroundSync = isBackgroundSync;
            LinkedAdapter = linkedAdapter;
        }

        /// <summary>
        /// The Adapter to which this Adapter should be connected
        /// </summary>
        public AbstractAdapter LinkedAdapter { get; set; }

        /// <summary>
        /// Returns true if the adapter is used by a background-task
        /// </summary>
        protected bool IsBackgroundSync { get; }

        /// <summary>
        /// Adds an item to the target system
        /// </summary>
        /// <param name="item">The item of the <see cref="LinkedAdapter"/> a corresponding item will be created in the target</param>
        /// <returns>Return the corresponding item that has been created</returns>
        public abstract Task<BaseItem> AddItem(BaseItem item);

        /// <summary>
        /// Updates an item in the target system of the adapter
        /// </summary>
        /// <param name="item">The item of the <see cref="LinkedAdapter"/>. The corresponding item will be  updated in the target</param>
        /// <returns>The corresponding item that was updated with the new properties</returns>
        public abstract Task<BaseItem> UpdateItem(BaseItem item);//TODO only use entityid?

        /// <summary>
        /// Deletes an item in the target system of the adapter
        /// </summary>
        /// <param name="item">The item of the <see cref="LinkedAdapter"/>. The corresponding item will be  updated in the target</param>
        /// <returns></returns>
        public abstract Task DeleteItem(BaseItem item);

        /// <summary>
        /// Get the updated items which updated since <see cref="Configuration.LastSync"/>. If errors occured in the last sync <see cref="Configuration.LastSync"/> must not change.
        /// </summary>
        /// <param name="association">The pair of folders in which you want to search</param>
        /// <returns>A list of <see cref="BaseItem"/> which were changed since <see cref="Configuration.LastSync"/></returns>
        public abstract Task<List<BaseItem>> GetUpdatedItems(FolderAssociation association);

        /// <summary>
        /// Get the delted items which updated since <see cref="Configuration.LastSync"/>. If errors occured in the last sync <see cref="Configuration.LastSync"/> must not change.
        /// </summary>
        /// <param name="association">The pair of folders in which you want to search</param>
        /// <returns>A list of <see cref="BaseItem"/> which were deleted since <see cref="Configuration.LastSync"/></returns>
        public abstract Task<List<BaseItem>> GetDeletedItemsAsync(FolderAssociation association);

        /// <summary>
        /// Generates the corresponding EntityId for an item of the <see cref="LinkedAdapter"/>  This can be used for duplicate searching.
        /// Example: Get the corresponding URL for a file from a specific folder.
        /// </summary>
        /// <param name="item">The item you need an EntityId for</param>
        /// <returns>The correspongin EntityId</returns>
        public abstract string BuildEntityId(BaseItem item);
    }
}
