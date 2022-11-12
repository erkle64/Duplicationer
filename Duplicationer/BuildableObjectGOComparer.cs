using System.Collections.Generic;

namespace Duplicationer
{
    public class BuildableObjectGOComparer : IEqualityComparer<BuildableObjectGO>
    {
        public bool Equals(BuildableObjectGO x, BuildableObjectGO y)
        {
            return x.GetInstanceID() == y.GetInstanceID();
        }

        public int GetHashCode(BuildableObjectGO obj)
        {
            return obj.GetInstanceID();
        }
    }
}