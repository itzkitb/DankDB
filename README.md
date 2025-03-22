
# [DankDB logo](https://i.imgur.com/QlxO6Ob.png) DankDB
(*Dank database*)

**DankDB** - is a powerful c# library for working with any JSON databases with LRU caching for the least access to the hard disk

### 🗃️ Library Functions
- Getting data in the required format
- Saving any data
- Renaming keys in the database
- Deleting keys in the database
- (Coming soon) Key availability check

### ⚡️ Speed and performance
(*There will be results soon*)

A 1 megabyte database is used for testing

*HDD* - 2 TB Seagate BarraCuda (ST2000DM005) [SATA III, 6 gbit/с, 5400 rpm, cache memory - 256mb]

*SSD* - 120 GB 2.5" SATA Drive HP S700 (2DP97AA#ABB) [SATA, read - 550 MB/sec, Write - 480 MB/sec, 3D NAND 3 bit TLC, TBW - 70 TB]
#### 1️⃣ The first request (Downloading data from the database)
- Synchronous data acquisition
> - HDD: 0ms
> - SSD: 0ms
- Asynchronous data acquisition
> - HDD: 0ms
> - SSD: 0ms
- Synchronous data saving
> - HDD: 0ms
> - SSD: 0ms
- Asynchronous data saving
> - HDD: 0ms
> - SSD: 0ms
#### 2️⃣ The second request (Downloading data from the cache)
- Synchronous data acquisition
> - HDD: 0ms
> - SSD: 0ms
- Asynchronous data acquisition
> - HDD: 0ms
> - SSD: 0ms
- Synchronous data saving
> - HDD: 0ms
> - SSD: 0ms
- Asynchronous data saving
> - HDD: 0ms
> - SSD: 0ms
#### 3️⃣ Stress test
- 1 million asynchronous reads and writes of random data 
> - HDD: 0ms
> - SSD: 0ms
- 1 million synchronous reads and writes of random data 
> - HDD: 0ms
> - SSD: 0ms

### ℹ️ Library info
Current version: 12

Release date: 22.03.25



*FeelsDankDBMan*
