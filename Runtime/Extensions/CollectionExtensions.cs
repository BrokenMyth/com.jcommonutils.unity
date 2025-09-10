using System.Collections.Generic;

namespace Extensions
{
    public static class CollectionExtensions
    {
        public static bool IsNullOrEmpty<T>(this ICollection<T> self)
        {
            return self == null || self.Count <= 0;
        }
    }
}