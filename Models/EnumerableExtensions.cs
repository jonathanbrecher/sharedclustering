using System;
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
            if (count <= 1)
            {
                return items.Max();
            }

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

                    // Remove smallest item from set (unless it's the only entry in the set)
                    ++resultCount;
                    if (resultCount - first.Value >= count && set.Count() > 1)
                    {
                        set.Remove(first.Key);
                        resultCount -= first.Value;
                        first = set.First();
                    }
                }
            }
            return set.First().Key;
        }

        // Much faster than sorting the list, especially for large lists
        public static IEnumerable<T> LowestN<T,TKey>(this IEnumerable<T> items, Func<T, TKey> keyFunc, int count)
        {
            var comparer = Comparer<TKey>.Default;
            var set = new SortedDictionary<TKey, List<T>> { { default(TKey), new List<T>() } };
            var resultCount = 0;
            var last = set.Last();
            foreach (var item in items)
            {
                // If the key is already larger than the largest
                // item in the set, we can ignore this item
                var key = keyFunc(item);
                if (resultCount < count || comparer.Compare(key, last.Key) <= 0)
                {
                    // Add next item to set
                    if (!set.ContainsKey(key))
                    {
                        set[key] = new List<T> { item };
                    }
                    else
                    {
                        set[key].Add(item);
                    }

                    // Remove largest item from set
                    ++resultCount;
                    if (resultCount - last.Value.Count >= count)
                    {
                        set.Remove(last.Key);
                        resultCount -= last.Value.Count;
                        last = set.Last();
                    }
                }
            }
            return set.SelectMany(kvp => kvp.Value);
        }
    }
}
