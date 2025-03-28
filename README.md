
<p align="center">
    <img src="https://i.imgur.com/QlxO6Ob.png" alt="DankDB logo" width="300px">
</p>

# DankDB
(*Dank database*)

**DankDB** - is a powerful c# library for working with any JSON databases with LRU caching for the least access to the hard disk

🔗 [Documentation](https://github.com/itzkitb/DankDB/blob/master/Documentation.md)

🔗 [Support me](https://www.donationalerts.com/r/itzkitb)

### 🗃️ Library Functions
- Getting data in the required format
- Saving any data
- Renaming keys in the database
- Deleting keys in the database
- (Coming soon) Key availability check

### ⚡️ Speed and performance
A 1 megabyte database is used for testing

*HDD* - 2 TB Seagate BarraCuda (ST2000DM005) [SATA III, 6 gbit/sec, 5400 rpm, cache memory - 256mb]

*SSD* - 120 GB 2.5" SATA Drive HP S700 (2DP97AA#ABB) [SATA, read - 550 MB/sec, Write - 480 MB/sec, 3D NAND 3 bit TLC, TBW - 70 TB]
#### ⏲ The first request (Downloading data from the database)
- Synchronous data acquisition
> - HDD: 2,55ms
> - SSD: 2,45ms
- Asynchronous data acquisition
> - HDD: 2,23ms
> - SSD: 2,33ms
- Synchronous data saving
> - HDD: 8,39ms
> - SSD: 8,30ms
- Asynchronous data saving
> - HDD: 8,73ms
> - SSD: 8,56ms

#### 🚀 The second request (Downloading data from the cache)
- Synchronous data acquisition
> - HDD: 1,14ms
> - SSD: 1,26ms
- Asynchronous data acquisition
> - HDD: 1,01ms
> - SSD: 1,15ms
- Synchronous data saving
> - HDD: 6,65ms
> - SSD: 6,51ms
- Asynchronous data saving
> - HDD: 6,80ms
> - SSD: 6,01ms

### ℹ️ Library info
Current version: 12

Release date: 22.03.25

*FeelsDankDBMan*
