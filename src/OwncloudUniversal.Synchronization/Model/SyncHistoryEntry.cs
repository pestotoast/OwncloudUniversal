using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OwncloudUniversal.Synchronization.Model
{
    class SyncHistoryEntry
    {
        public long Id { get; set; }
        public long TargetItemId { get; set; }
        public long SourceItemId { get; set; }
        public DateTime CreateDate { get; set; }
        public SyncResult Result { get; set; }
        public string Message { get; set; }
        public string OldItemDisplayName { get; set; }//the name to display if an item was deleted
    }
}
