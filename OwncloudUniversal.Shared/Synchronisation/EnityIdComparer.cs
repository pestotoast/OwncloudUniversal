using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OwncloudUniversal.Shared.Model;

namespace OwncloudUniversal.Shared.Synchronisation
{
    class EnityIdComparer : IEqualityComparer<BaseItem>
    {
        public bool Equals(BaseItem x, BaseItem y)
        {
            return x?.EntityId == y?.EntityId;
        }

        public int GetHashCode(BaseItem obj)
        {
            return obj.EntityId.GetHashCode();
        }
    }
}
