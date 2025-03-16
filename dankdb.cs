using System.Collections.Concurrent;
using System.Text.Json;

namespace DankDB
{
    public class Manager
    {
        private static LruCache<string, CacheItem> cache = new LruCache<string, CacheItem>(100);
        private static ConcurrentDictionary<string, SemaphoreSlim> fileLocks = new();

        public Manager(int cache_size = 100)
        {
            cache = new LruCache<string, CacheItem>(cache_size);
        }

        private class CacheItem
        {
            public Dictionary<string, JsonElement> Data { get; set; } = new();
            public DateTime LastModified { get; set; }
        }

        public static void CreateDatabase(string path)
        {
            if (!File.Exists(path))
                File.WriteAllText(path, "{}");
        }

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

        public static void DeleteKey(string path, string key)
        {
            var semaphore = fileLocks.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
            semaphore.Wait();
            try
            {
                var cacheItem = LoadCacheItem(path);
                if (cacheItem.Data.Remove(key))
                    SaveCacheItem(path, cacheItem);
            }
            finally
            {
                semaphore.Release();
            }
        }

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
                return cachedItem;
            }

            Dictionary<string, JsonElement> data;
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
            cacheItem.LastModified = new FileInfo(path).LastWriteTimeUtc;
            cache.AddOrUpdate(path, cacheItem);
        }
    }

    public class AsyncManager
    {
        private static LruCache<string, CacheItem> cache = new LruCache<string, CacheItem>(100);
        private static ConcurrentDictionary<string, SemaphoreSlim> file_locks = new();

        public AsyncManager(int cache_size = 100)
        {
            cache = new LruCache<string, CacheItem>(cache_size);
        }

        private class CacheItem
        {
            public Dictionary<string, JsonElement> data { get; set; } = new();
            public DateTime last_modified { get; set; }
        }

        public static async Task CreateDatabaseAsync(string path)
        {
            if (!File.Exists(path))
                await File.WriteAllTextAsync(path, "{}");
        }

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

        public static async Task DeleteKeyAsync(string path, string key)
        {
            var semaphore = file_locks.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync();
            try
            {
                var cacheItem = await LoadCacheItemAsync(path);
                if (cacheItem.data.Remove(key))
                    await SaveCacheItemAsync(path, cacheItem);
            }
            finally
            {
                semaphore.Release();
            }
        }

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
                return cachedItem;
            }

            Dictionary<string, JsonElement> data;
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
            cacheItem.last_modified = new FileInfo(path).LastWriteTimeUtc;
            cache.AddOrUpdate(path, cacheItem);
        }
    }

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
