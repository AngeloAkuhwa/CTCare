namespace CTCare.Shared.Utilities
{
    public static class EnumerableExtensions
    {
        /// <summary>
        /// Converts a sequence into a HashSet.
        /// Useful if you need .NET Framework compatibility or custom comparer.
        /// </summary>
        public static HashSet<T> ToHashSetCompat<T>(
            this IEnumerable<T> source,
            IEqualityComparer<T>? comparer = null)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return new HashSet<T>(source, comparer ?? EqualityComparer<T>.Default);
        }

        /// <summary>
        /// Adds a Guid to the set only if it is not null or Guid.Empty.
        /// </summary>
        public static void AddIfNotEmpty(this ISet<Guid> source, Guid? id)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (id.HasValue && id.Value != Guid.Empty)
            {
                source.Add(id.Value);
            }
        }

        /// <summary>
        /// Projects a sequence into a HashSet of results.
        /// </summary>
        public static HashSet<TResult> ToHashSetCompat<TSource, TResult>(
            this IEnumerable<TSource> source,
            Func<TSource, TResult> selector,
            IEqualityComparer<TResult>? comparer = null)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (selector == null)
            {
                throw new ArgumentNullException(nameof(selector));
            }

            return new HashSet<TResult>(source.Select(selector), comparer ?? EqualityComparer<TResult>.Default);
        }

        /// <summary>
        /// Returns true if the collection is null or contains no elements.
        /// </summary>
        public static bool IsNullOrEmpty<T>(this IEnumerable<T>? source)
            => source == null || !source.Any();

        /// <summary>
        /// Splits a collection into fixed-size batches (default 100).
        /// </summary>
        public static IEnumerable<IEnumerable<T>> GetBatches<T>(
            this IReadOnlyCollection<T> source,
            int batchSize = 100)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (batchSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(batchSize));
            }

            for (var index = 0; index < source.Count; index += batchSize)
            {
                yield return source.Skip(index).Take(batchSize);
            }
        }
    }
}
