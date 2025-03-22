using System.Collections.Concurrent;
using System.Numerics;
using System.Text.Json;

// Version 12
// Created by ItzKITb
// GNU license

namespace DankDB
{
    class Worker
    {
        public static LruCache<string, CacheItem> cache = new LruCache<string, CacheItem>(1000);
        public static ConcurrentDictionary<string, SemaphoreSlim> file_locks = new();
        public static bool debug = false;
        public static DateTime start = DateTime.UtcNow;
    }

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
        /// <summary>
        /// Total files checks
        /// </summary>
        public static BigInteger checks { internal set; get; } = 0;
    }

    /// <summary>
    /// Database sync manager
    /// </summary>
    public class Manager
    {
        /// <summary>
        /// Create a database and write empty data into it
        /// </summary>
        /// <param name="path"></param>
        public static void CreateDatabase(string path)
        {
            Statistics.checks++;
            if (!File.Exists(path))
            {
                File.WriteAllText(path, "{}");
                Debugger.Write($"[Sync] Database \"{path}\" created.");
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
            var semaphore = Worker.file_locks.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
            semaphore.Wait();
            try
            {
                var cacheItem = LoadCacheItem(path);
                cacheItem.data[key] = JsonSerializer.SerializeToElement(data);
                SaveCacheItem(path, cacheItem);
                Debugger.Write($"[Sync] Data \"{data}\" in key \"{key}\" is stored in \"{path}\".");
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
            var semaphore = Worker.file_locks.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
            semaphore.Wait();
            try
            {
                var cacheItem = LoadCacheItem(path);
                Debugger.Write($"[Sync] The key \"{key}\" from the file \"{path}\" was successfully retrieved.");
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
        public static void DeleteKey(string path, string key)
        {
            var semaphore = Worker.file_locks.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
            semaphore.Wait();
            try
            {
                var cacheItem = LoadCacheItem(path);
                if (cacheItem.data.Remove(key))
                {
                    Statistics.removes++;
                    SaveCacheItem(path, cacheItem);
                    Debugger.Write($"[Sync] Key \"{key}\" in file \"{path}\" removed.");
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
            var semaphore = Worker.file_locks.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
            semaphore.Wait();
            try
            {
                var cacheItem = LoadCacheItem(path);
                if (cacheItem.data.TryGetValue(oldKey, out var value))
                {
                    cacheItem.data.Remove(oldKey);
                    cacheItem.data[newKey] = value;
                    Statistics.renames++;
                    SaveCacheItem(path, cacheItem);
                    Debugger.Write($"[Sync] The key \"{oldKey}\" in the file \"{path}\" has been renamed to \"{newKey}\".");
                }
            }
            finally
            {
                semaphore.Release();
            }
        }

        private static CacheItem LoadCacheItem(string path)
        {
            Statistics.checks++;
            var fileInfo = new FileInfo(path);
            fileInfo.Refresh();
            var currentLastModified = fileInfo.Exists ? fileInfo.LastWriteTimeUtc : DateTime.MinValue;

            if (Worker.cache.TryGet(path, out var cachedItem) &&
                cachedItem.last_modified.ToString() == currentLastModified.ToString())
            {
                Statistics.cache_reads++;
                Debugger.Write($"[Sync] Data for file \"{path}\" loaded from cache.");
                return cachedItem;
            }

            Debugger.Write($"[Sync] Loading from file. ({(cachedItem == null ? "null" : cachedItem)}, {(cachedItem == null ? "null" : cachedItem.last_modified == currentLastModified)}, {(cachedItem == null ? "null" : cachedItem.last_modified)}, {currentLastModified})");

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

            var newCacheItem = new CacheItem { data = data, last_modified = currentLastModified };
            Worker.cache.AddOrUpdate(path, newCacheItem);
            Debugger.Write("[Sync] Loaded.");
            return newCacheItem;
        }

        private static void SaveCacheItem(string path, CacheItem cacheItem)
        {
            using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            JsonSerializer.Serialize(stream, cacheItem.data);
            Statistics.writes++;

            var fileInfo = new FileInfo(path);
            fileInfo.Refresh();
            cacheItem.last_modified = fileInfo.LastWriteTimeUtc;

            Worker.cache.AddOrUpdate(path, cacheItem);
            Statistics.cache_writes++;
            Debugger.Write($"[Sync] File \"{path}\" saved.");
        }
    }

    /// <summary>
    /// Database async
    /// </summary>
    public class AsyncManager
    {
        /// <summary>
        /// Create a database and write empty data into it
        /// </summary>
        /// <param name="path"></param>
        public static async Task CreateDatabaseAsync(string path)
        {
            Statistics.checks++;
            if (!File.Exists(path))
            {
                await File.WriteAllTextAsync(path, "{}");
                Debugger.Write($"[Async] Database \"{path}\" created.");
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
            var semaphore = Worker.file_locks.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync();
            try
            {
                var cacheItem = await LoadCacheItemAsync(path);
                cacheItem.data[key] = JsonSerializer.SerializeToElement(data);
                await SaveCacheItemAsync(path, cacheItem);
                Debugger.Write($"[Async] Data \"{data}\" in key \"{key}\" is stored in \"{path}\".");
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
            var semaphore = Worker.file_locks.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync();
            try
            {
                var cacheItem = await LoadCacheItemAsync(path);
                Debugger.Write($"[Async] The key \"{key}\" from the file \"{path}\" was successfully retrieved.");
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
            var semaphore = Worker.file_locks.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync();
            try
            {
                var cacheItem = await LoadCacheItemAsync(path);
                if (cacheItem.data.Remove(key))
                {
                    await SaveCacheItemAsync(path, cacheItem);
                    Statistics.removes++;
                    Debugger.Write($"[Async] Key \"{key}\" in file \"{path}\" removed.");
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
            var semaphore = Worker.file_locks.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
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
                    Debugger.Write($"[Async] The key \"{oldKey}\" in the file \"{path}\" has been renamed to \"{newKey}\".");
                }
            }
            finally
            {
                semaphore.Release();
            }
        }

        private static async Task<CacheItem> LoadCacheItemAsync(string path)
        {
            Statistics.checks++;
            var fileInfo = new FileInfo(path);
            fileInfo.Refresh();
            var currentLastModified = fileInfo.Exists ? fileInfo.LastWriteTimeUtc : DateTime.MinValue;

            if (Worker.cache.TryGet(path, out var cachedItem) &&
                cachedItem.last_modified.ToString() == currentLastModified.ToString())
            {
                Statistics.cache_reads++;
                Debugger.Write($"[Async] Data for file \"{path}\" loaded from cache.");
                return cachedItem;
            }

            Debugger.Write($"[Async] Loading data from file \"{path}\". ({(cachedItem == null ? "null" : cachedItem)}, {(cachedItem == null ? "null" : cachedItem.last_modified == currentLastModified)}, {(cachedItem == null ? "null" : cachedItem.last_modified)}, {currentLastModified})");

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
            Worker.cache.AddOrUpdate(path, newCacheItem);
            Debugger.Write("[Async] Loaded.");
            return newCacheItem;
        }

        private static async Task SaveCacheItemAsync(string path, CacheItem cacheItem)
        {
            await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
            await JsonSerializer.SerializeAsync(stream, cacheItem.data);
            Statistics.writes++;
            Statistics.checks++;
            var fileInfo = new FileInfo(path);
            fileInfo.Refresh();
            cacheItem.last_modified = fileInfo.LastWriteTimeUtc;

            Worker.cache.AddOrUpdate(path, cacheItem);
            Statistics.cache_writes++;
            Debugger.Write($"[Async] File \"{path}\" saved.");
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

    class CacheItem
    {
        public Dictionary<string, JsonElement> data { get; set; } = new();
        public DateTime last_modified { get; set; }
    }

    class Debugger
    {
        public static void Write(string text)
        {
            if (Worker.debug) Console.WriteLine($"[DankDB debug] [{(DateTime.UtcNow - Worker.start).TotalMicroseconds}]: {text}"); 
        }
    }
}
