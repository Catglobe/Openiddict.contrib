namespace Openiddict.Contrib.Server;

internal static class AsyncEnumerableExtensions
{
   public static Task<List<T>> ToListAsync<T>(this IAsyncEnumerable<T> source)
   {
      if (source == null) { throw new ArgumentNullException(nameof(source)); }

      return ExecuteAsync();

      async Task<List<T>> ExecuteAsync()
      {
         var list = new List<T>();

         await foreach (var element in source) { list.Add(element); }

         return list;
      }
   }

   public static ValueTask<TSource?> FirstOrDefaultAsync<TSource>(this IAsyncEnumerable<TSource> source)
   {
      if (source == null) { throw new ArgumentNullException(nameof(source)); }

      return Core(source);

      static async ValueTask<TSource?> Core(IAsyncEnumerable<TSource> source)
      {
         await using var e = source.GetAsyncEnumerator();
         return await e.MoveNextAsync() ? e.Current : default;
      }
   }
}