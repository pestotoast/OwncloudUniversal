using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OwncloudUniversal.Synchronization.Model
{
    enum SyncResult
    {
        Sent,
        Received,
        Deleted,
        Moved,
        Renamed,
        Ignored,
        Failed
    }
}
