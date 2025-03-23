# DankDB Documentation (v13)

A lightweight C# database library with LRU caching and thread-safe operations.  
**License**: GNU  
**Author**: ItzKITb

## Namespaces
### `DankDB`
Main namespace containing all database classes.

## Classes Overview

### `Worker`
Internal utility class handling cache, locks, and debug settings.
```csharp
public class Worker
{
    public static LruCache<string, CacheItem> cache = new(1000);
    internal static ConcurrentDictionary<string, SemaphoreSlim> file_locks = new();
    public static bool debug = false; // Enable debug logging
    internal static DateTime start = DateTime.UtcNow;
}
```

### `Statistics`
Tracks database operations globally.
```csharp
public class Statistics
{
    public static BigInteger reads { get; }      // Total file reads
    public static BigInteger writes { get; }     // Total file writes
    public static BigInteger cache_reads { get; } // Total cache hits
    public static BigInteger cache_writes { get; } // Total cache updates
    public static BigInteger removes { get; }    // Total key deletions
    public static BigInteger renames { get; }    // Total key renames
    public static BigInteger checks { get; }     // Total file existence checks
}
```

---

## Core API

### `Manager` (Synchronous)
Thread-safe synchronous database operations.

#### Methods:
```csharp
public static void CreateDatabase(string path)
```
- Creates an empty JSON database file if it doesn't exist.

---

```csharp
public static void Save(string path, string key, object data)
```
- Saves/updates serialized `data` under `key` in the specified database file.

---

```csharp
public static T Get<T>(string path, string key)
```
- Retrieves and deserializes data from `key` in the database.  
- Returns `default(T)` if key doesn't exist.

---

```csharp
public static void DeleteKey(string path, string key)
```
- Removes a key from the database.

---

```csharp
public static bool IsContainsKey(string path, string key)
```
- Checks if a key exists in the database.

---

```csharp
public static void RenameKey(string path, string oldKey, string newKey)
```
- Renames a database key.

---

### `AsyncManager` (Asynchronous)
Asynchronous counterpart of `Manager` with `Task`-based methods.

#### Methods mirror `Manager` with async/await support:
- `CreateDatabase`
- `Save`
- `Get<T>`
- `DeleteKey`
- `IsContainsKey`
- `RenameKey`

---

## Cache System

### `LruCache<TKey, TValue>`
LRU (Least Recently Used) cache implementation.

#### Key Methods:
```csharp
public TValue GetOrAdd(TKey key, Func<TKey, TValue> valueFactory)
public async Task<TValue> GetOrAddAsync(...)
public bool TryGet(TKey key, out TValue value)
public void AddOrUpdate(TKey key, TValue value)
public void Invalidate(TKey key)
```

#### Properties:
- `capacity`: Maximum cached items
- `count`: Current cached items

---

## Advanced Components

### `CacheItem`
```csharp
public class CacheItem
{
    public Dictionary<string, JsonElement> data { get; set; }
    public DateTime last_modified { get; set; }
}
```
- Internal cache item format storing JSON data and modification timestamp.

---

### Debugging
Enable debug output via `Worker.debug = true;`.  
Messages follow format:  
`[DankDB debug] [<microseconds since start>]: <message>`

---

## Example Usage

```csharp
// Initialize database
Manager.CreateDatabase("data.db");

// Sync write/read
Manager.Save("data.db", "user1", new { Name = "Alice", Age = 30 });
var user = Manager.Get<User>("data.db", "user1");

// Async write/read
await AsyncManager.SaveAsync("data.db", "user2", new { Name = "Bob", Age = 25 });
var user2 = await AsyncManager.GetAsync<User>("data.db", "user2");

// Check statistics
Console.WriteLine($"Total writes: {Statistics.writes}");
```

---

## Performance Notes
- **Thread Safety**: Uses `SemaphoreSlim` per file path for concurrency control.
- **Caching**: LRU cache (default: 1000 items (To change, use Worker.cache = new(*{Number of items the cache can store}*);)) with file timestamp validation.
- **Serialization**: Uses `System.Text.Json` for JSON operations.
