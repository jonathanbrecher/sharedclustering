using System.Collections.Generic;
using System.Linq;

namespace AncestryDnaClustering.Models
{
    public static class EnumerableExtensions
    {
        public static IEnumerable<IEnumerable<T>> Batch<T>(this IEnumerable<T> source, int size)
        {
            T[] bucket = null;
            var count = 0;

            foreach (var item in source)
            {
                if (bucket == null)
                {
                    bucket = new T[size];
                }

                bucket[count++] = item;

                if (count != size)
                {
                    continue;
                }

                yield return bucket.Select(x => x);

                bucket = null;
                count = 0;
            }

            // Return the last bucket with all remaining elements
            if (bucket != null && count > 0)
            {
                yield return bucket.Take(count);
            }
        }

        // Much faster than sorting the list, especially for large lists
        public static T NthLargest<T>(this IEnumerable<T> items, int count)
        {
            var comparer = Comparer<T>.Default;
            var set = new SortedDictionary<T, int> { { default(T), 0 } };
            var resultCount = 0;
            var first = set.First();
            foreach (var item in items)
            {
                // If the key is already smaller than the smallest
                // item in the set, we can ignore this item
                if (resultCount < count || comparer.Compare(item, first.Key) >= 0)
                {
                    // Add next item to set
                    if (!set.ContainsKey(item))
                    {
                        set[item] = 1;
                    }
                    else
                    {
                        ++set[item];
                    }

                    // Remove smallest item from set
                    ++resultCount;
                    if (resultCount - first.Value >= count)
                    {
                        set.Remove(first.Key);
                        resultCount -= first.Value;
                        first = set.First();
                    }
                }
            }
            return set.First().Key;
        }
    }
}
