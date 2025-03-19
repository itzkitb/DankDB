using System.Collections.Concurrent;
using System.Numerics;
using System.Text.Json;

// Version 11
// Created by ItzKITb
// GNU license

namespace DankDB
{
    /// <summary>
    /// Database statistics
    /// </summary>
    public class Statistics
    {
        /// <summary>
        /// Total reads
        /// </summary>
        public static BigInteger reads { internal set; get; } = 0;
        /// <summary>
        /// Total writes
        /// </summary>
        public static BigInteger writes { internal set; get; } = 0;
        /// <summary>
        /// Total cache reads
        /// </summary>
        public static BigInteger cache_reads { internal set; get; } = 0;
        /// <summary>
        /// Total cache writes
        /// </summary>
        public static BigInteger cache_writes { internal set; get; } = 0;
        /// <summary>
        /// Total removes
        /// </summary>
        public static BigInteger removes { internal set; get; } = 0;
        /// <summary>
        /// Total renames
        /// </summary>
        public static BigInteger renames { internal set; get; } = 0;
    }

    /// <summary>
    /// Database sync manager
    /// </summary>
    public class Manager
    {
        private static LruCache<string, CacheItem> cache = new LruCache<string, CacheItem>(1000);
        private static ConcurrentDictionary<string, SemaphoreSlim> fileLocks = new();

        /// <summary>
        /// Initializing a class (Optional)
        /// </summary>
        /// /// <param name="cache_size"></param>
        public Manager(int cache_size = 1000)
        {
            cache = new LruCache<string, CacheItem>(cache_size);
        }

        private class CacheItem
        {
            public Dictionary<string, JsonElement> Data { get; set; } = new();
            public DateTime LastModified { get; set; }
        }

        /// <summary>
        /// Create a database and write empty data into it
        /// </summary>
        /// <param name="path"></param>
        public static void CreateDatabase(string path)
        {
            if (!File.Exists(path))
            {
                File.WriteAllText(path, "{}");
                Statistics.writes++;
            }
        }

        /// <summary>
        /// Add/Overwrite information in a specific database key
        /// </summary>
        /// <param name="path"></param>
        /// <param name="key"></param>
        /// <param name="data"></param>
        public static void Save(string path, string key, object data)
        {
            var semaphore = fileLocks.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
            semaphore.Wait();
            try
            {
                var cacheItem = LoadCacheItem(path);
                cacheItem.Data[key] = JsonSerializer.SerializeToElement(data);
                SaveCacheItem(path, cacheItem);
            }
            finally
            {
                semaphore.Release();
            }
        }
        /// <summary>
        /// Get data from database key
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="path"></param>
        /// <param name="key"></param>
        /// <returns>Data in the database key</returns>
        public static T Get<T>(string path, string key)
        {
            var semaphore = fileLocks.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
            semaphore.Wait();
            try
            {
                var cacheItem = LoadCacheItem(path);
                return cacheItem.Data.TryGetValue(key, out var value)
                    ? JsonSerializer.Deserialize<T>(value.GetRawText())
                    : default;
            }
            finally
            {
                semaphore.Release();
            }
        }

        /// <summary>
        /// Delete key from database
        /// </summary>
        /// <param name="path"></param>
        /// <param name="key"></param>
        public static void DeleteKey(string path, string key)
        {
            var semaphore = fileLocks.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
            semaphore.Wait();
            try
            {
                var cacheItem = LoadCacheItem(path);
                if (cacheItem.Data.Remove(key))
                {
                    Statistics.removes++;
                    SaveCacheItem(path, cacheItem);
                }
            }
            finally
            {
                semaphore.Release();
            }
        }

        /// <summary>
        /// Change the name of the key in the database
        /// </summary>
        /// <param name="path"></param>
        /// <param name="oldKey"></param>
        /// <param name="newKey"></param>
        public static void RenameKey(string path, string oldKey, string newKey)
        {
            var semaphore = fileLocks.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
            semaphore.Wait();
            try
            {
                var cacheItem = LoadCacheItem(path);
                if (cacheItem.Data.TryGetValue(oldKey, out var value))
                {
                    cacheItem.Data.Remove(oldKey);
                    cacheItem.Data[newKey] = value;
                    Statistics.renames++;
                    SaveCacheItem(path, cacheItem);
                }
            }
            finally
            {
                semaphore.Release();
            }
        }

        private static CacheItem LoadCacheItem(string path)
        {
            var fileInfo = new FileInfo(path);
            var currentLastModified = fileInfo.Exists ? fileInfo.LastWriteTimeUtc : DateTime.MinValue;

            if (cache.TryGet(path, out var cachedItem) &&
                cachedItem.LastModified == currentLastModified)
            {
                Statistics.cache_reads++;
                return cachedItem;
            }

            Dictionary<string, JsonElement> data;
            Statistics.reads++;
            if (File.Exists(path))
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(stream);
            }
            else
            {
                data = new Dictionary<string, JsonElement>();
            }

            var newCacheItem = new CacheItem { Data = data, LastModified = currentLastModified };
            cache.AddOrUpdate(path, newCacheItem);
            return newCacheItem;
        }

        private static void SaveCacheItem(string path, CacheItem cacheItem)
        {
            using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            JsonSerializer.Serialize(stream, cacheItem.Data);
            Statistics.writes++;
            cacheItem.LastModified = new FileInfo(path).LastWriteTimeUtc;
            cache.AddOrUpdate(path, cacheItem);
            Statistics.cache_writes++;
        }
    }

    /// <summary>
    /// Database async
    /// </summary>
    public class AsyncManager
    {
        private static LruCache<string, CacheItem> cache = new LruCache<string, CacheItem>(100);
        private static ConcurrentDictionary<string, SemaphoreSlim> file_locks = new();
        
        /// <summary>
        /// Initializing a class (Optional)
        /// </summary>
        /// <param name="cache_size"></param>
        public AsyncManager(int cache_size = 100)
        {
            cache = new LruCache<string, CacheItem>(cache_size);
        }

        private class CacheItem
        {
            public Dictionary<string, JsonElement> data { get; set; } = new();
            public DateTime last_modified { get; set; }
        }

        /// <summary>
        /// Create a database and write empty data into it
        /// </summary>
        /// <param name="path"></param>
        public static async Task CreateDatabaseAsync(string path)
        {
            if (!File.Exists(path))
            {
                await File.WriteAllTextAsync(path, "{}");
                Statistics.writes++;
            }
        }

        /// <summary>
        /// Add/Overwrite information in a specific database key
        /// </summary>
        /// <param name="path"></param>
        /// <param name="key"></param>
        /// <param name="data"></param>
        public static async Task SaveAsync(string path, string key, object data)
        {
            var semaphore = file_locks.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync();
            try
            {
                var cacheItem = await LoadCacheItemAsync(path);
                cacheItem.data[key] = JsonSerializer.SerializeToElement(data);
                await SaveCacheItemAsync(path, cacheItem);
            }
            finally
            {
                semaphore.Release();
            }
        }

        /// <summary>
        /// Get data from database key
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="path"></param>
        /// <param name="key"></param>
        /// <returns>Data in the database key</returns>
        public static async Task<T> GetAsync<T>(string path, string key)
        {
            var semaphore = file_locks.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync();
            try
            {
                var cacheItem = await LoadCacheItemAsync(path);
                return cacheItem.data.TryGetValue(key, out var value)
                    ? JsonSerializer.Deserialize<T>(value.GetRawText())
                    : default;
            }
            finally
            {
                semaphore.Release();
            }
        }

        /// <summary>
        /// Delete key from database
        /// </summary>
        /// <param name="path"></param>
        /// <param name="key"></param>
        public static async Task DeleteKeyAsync(string path, string key)
        {
            var semaphore = file_locks.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync();
            try
            {
                var cacheItem = await LoadCacheItemAsync(path);
                if (cacheItem.data.Remove(key))
                {
                    await SaveCacheItemAsync(path, cacheItem);
                    Statistics.removes++;
                }
            }
            finally
            {
                semaphore.Release();
            }
        }

        /// <summary>
        /// Change the name of the key in the database
        /// </summary>
        /// <param name="path"></param>
        /// <param name="oldKey"></param>
        /// <param name="newKey"></param>
        public static async Task RenameKeyAsync(string path, string oldKey, string newKey)
        {
            var semaphore = file_locks.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync();
            try
            {
                var cacheItem = await LoadCacheItemAsync(path);
                if (cacheItem.data.TryGetValue(oldKey, out var value))
                {
                    cacheItem.data.Remove(oldKey);
                    cacheItem.data[newKey] = value;
                    Statistics.renames++;
                    await SaveCacheItemAsync(path, cacheItem);
                }
            }
            finally
            {
                semaphore.Release();
            }
        }

        private static async Task<CacheItem> LoadCacheItemAsync(string path)
        {
            var fileInfo = new FileInfo(path);
            var currentLastModified = fileInfo.Exists ? fileInfo.LastWriteTimeUtc : DateTime.MinValue;

            if (cache.TryGet(path, out var cachedItem) &&
                cachedItem.last_modified == currentLastModified)
            {
                Statistics.cache_reads++;
                return cachedItem;
            }

            Dictionary<string, JsonElement> data;
            Statistics.reads++;
            if (File.Exists(path))
            {
                await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, true);
                data = await JsonSerializer.DeserializeAsync<Dictionary<string, JsonElement>>(stream);
            }
            else
            {
                data = new Dictionary<string, JsonElement>();
            }

            var newCacheItem = new CacheItem { data = data, last_modified = currentLastModified };
            cache.AddOrUpdate(path, newCacheItem);
            return newCacheItem;
        }

        private static async Task SaveCacheItemAsync(string path, CacheItem cacheItem)
        {
            await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
            await JsonSerializer.SerializeAsync(stream, cacheItem.data);
            Statistics.writes++;
            cacheItem.last_modified = new FileInfo(path).LastWriteTimeUtc;
            cache.AddOrUpdate(path, cacheItem);
            Statistics.cache_writes++;
        }
    }

    /// <summary>
    /// LRU (least recently used) cache class
    /// </summary>
    public sealed class LruCache<TKey, TValue> where TKey : notnull
    {
        private readonly ConcurrentDictionary<TKey, LinkedListNode<LruCacheItem>> cache = new();
        private readonly LinkedList<LruCacheItem> lru = new();
        public int capacity { get; set; }
        public int count => cache.Count;

        public LruCache(int capacity)
        {
            this.capacity = capacity;
        }

        public TValue GetOrAdd(TKey key, Func<TKey, TValue> valueFactory)
        {
            var node = cache.GetOrAdd(key, k =>
            {
                var item = new LruCacheItem(k, valueFactory(k));
                return new LinkedListNode<LruCacheItem>(item);
            });

            lock (lru)
            {
                if (node.List == lru)
                    lru.Remove(node);

                lru.AddFirst(node);
            }

            MaintainCapacity();
            return node.Value.Value;
        }

        public async Task<TValue> GetOrAddAsync(TKey key, Func<TKey, Task<TValue>> valueFactory)
        {
            if (cache.TryGetValue(key, out var existingNode))
            {
                lock (lru)
                {
                    lru.Remove(existingNode);
                    lru.AddFirst(existingNode);
                }
                return existingNode.Value.Value;
            }

            var value = await valueFactory(key);
            var newNode = new LinkedListNode<LruCacheItem>(new LruCacheItem(key, value));

            lock (lru)
            {
                if (cache.TryAdd(key, newNode))
                {
                    lru.AddFirst(newNode);
                }
                else
                {
                    lru.AddFirst(cache[key]);
                }
            }

            MaintainCapacity();
            return value;
        }

        public void AddOrUpdate(TKey key, TValue value)
        {
            var newNode = new LinkedListNode<LruCacheItem>(new LruCacheItem(key, value));

            lock (lru)
            {
                if (cache.TryGetValue(key, out var oldNode))
                {
                    lru.Remove(oldNode);
                }

                cache[key] = newNode;
                lru.AddFirst(newNode);
                MaintainCapacity();
            }
        }

        public bool TryGet(TKey key, out TValue value)
        {
            if (cache.TryGetValue(key, out var node))
            {
                value = node.Value.Value;
                lock (lru)
                {
                    lru.Remove(node);
                    lru.AddFirst(node);
                }
                return true;
            }
            value = default;
            return false;
        }

        public void Refresh(TKey key)
        {
            if (cache.TryGetValue(key, out var node))
            {
                lock (lru)
                {
                    lru.Remove(node);
                    lru.AddFirst(node);
                }
            }
        }

        public void Invalidate(TKey key)
        {
            lock (lru)
            {
                if (cache.TryRemove(key, out var node))
                    lru.Remove(node);
            }
        }

        public void Clear()
        {
            lock (lru)
            {
                cache.Clear();
                lru.Clear();
            }
        }

        private void MaintainCapacity()
        {
            while (cache.Count > capacity)
            {
                lock (lru)
                {
                    if (cache.Count <= capacity) break;

                    var lastNode = lru.Last;
                    cache.TryRemove(lastNode.Value.Key, out _);
                    lru.RemoveLast();
                }
            }
        }

        private sealed class LruCacheItem
        {
            public TKey Key { get; }
            public TValue Value { get; }

            public LruCacheItem(TKey key, TValue value)
            {
                Key = key;
                Value = value;
            }
        }
    }
}
