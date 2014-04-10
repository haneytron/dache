DACHE 1.3.0
===========


distributed caching for .net applications 

fast, scalable distributed caching with meaningful performance metrics for your managers and a simple api for your development team

**WEB:**   http://www.getdache.net

**EMAIL:** info@getdache.net


VERSION HISTORY
============================================


1.3.0
------------------

- Upgraded projects to .NET 4.5

- Created SharpMemoryCache to fix bug with MemoryCache not trimming correctly at the polling interval. Cache will now closely respect the cache memory percentage limit at the polling interval.

- Cache Host now supports new Keys and Keys-Tag methods to search for keys and return lists of keys. NOTE: the Keys operations are EXPENSIVE and lock the cache for the duration, but are done in parallel to minimize blocking time. Use with caution/sparingly for large data sets.

- Cache Host now supports Clear method to clear the cache.

- Fixed bug with loading custom logger and serializer from configuration file. It now actually works!

- Set cache client to use file logger by default. EventViewerLogger can still be used but people who ran the process without admin rights would experience a crash/error related to event logging.

- New GZipSerializer built-in and supported. It's a bit slower but saves on memory, so the choice is yours!

- Added SimplSockets as a Nuget package. SharpMemoryCache is also added as a Nuget package.

- New MemCache type, GZipMemCache, created. This zips all incoming data on the server side regardless of client serialization mechanisms. A little more computationally expensive but saves on memory and centralizes zipping logic. Can be enabled in host config.

- Config file cleanup (removal of unused nodes).

- Code cleanup (removal of unused usings etc.).

- All projects now outputing XML comments.

- Communication protocol bug fixes and clean-up to simplify.

- Fixed all solution warnings!

- Improved efficiency of Remove method of MemCache. Should be notably faster now (not that it wasn't fast before!).

- Documented configuration files a little more.

- New unit tests (need way more)

***** SPECIAL THANKS TO THE MAJOR CONTRIBUTORS TO THIS BUILD: mmajcica and aweber1 - you rock! :)


1.2.3
------------------

- Changed SimplSockets so that it binds to IpAddress.Any to enable listening on all interfaces for a given port. This should resolve some communication issues when a server has multiple NICs or interfaces.

- Fixed a bug in SimplSockets regarding calculation of message length (thanks to Christoph Martens for identifying and reporting the bug)


1.2.2
------------------

- Fixed issue in how SimplSockets assigned listening IP for Cache Host

- Fixed issue with Customer Performance Counters that was causing a crash on startup for some servers and users


1.2.1
------------------

- Fixed issue with infinite hang on startup if not all listed cache hosts were online

- Upgraded SimplSockets to 1.1.0 which introduces many fixes to Dache socket communication.

- Client threads will no longer hang when a cache host is disconnected.

- Special thanks to Ruslan (https://github.com/ruzhovt) for discovering and documenting the source of these errors.

- Fixed bug in cache client host orders when disconnect/reconnect occurs. This ensures that all cache clients have the same host order, so that cached items are distributed properly.


1.2.0
------------------


- This release is MASSIVE and AWESOME! :)

- Created and implemented SimplSockets, a library designed for extremely efficient socket communication. As a result, WCF has been removed entirely. The client side uses multiplexing on a single connection. Throughput to Dache has nearly tripled: I could not max out a single cache server with 2 of my computers doing roughly 100,000 commands per second each! http://www.github.com/ironyx/simplsockets if you'd like to learn more.

- Dache now operates on a TCP syntax that is platform independent. This means that people can use Dache from non-.NET applications if they choose to write a native client that employs the commands. In the commands, all objects are passed as base-64 strings. The commands are:

> get cacheKey1 cacheKey2 cacheKey3 cacheKey4...

> get-tag tagName

> set cacheKey1 serializedObject1 cacheKey2 serializedObject2

> set absoluteExpiration cacheKey1 serializedObject1 cacheKey2 serializedObject2

> set slidingExpiration cacheKey1 serializedObject1 cacheKey2 serializedObject2

> set-intern cacheKey1 serializedObject1 cacheKey2 serializedObject2

> set-tag tagName cacheKey1 serializedObject1 cacheKey2 serializedObject2

> set-tag tagName absoluteExpiration cacheKey1 serializedObject1 cacheKey2 serializedObject2

> set-tag tagName slidingExpiration cacheKey1 serializedObject1 cacheKey2 serializedObject2

> set-tag-intern tagName cacheKey1 serializedObject1 cacheKey2 serializedObject2

> del cacheKey1 cacheKey2 cacheKey3 cacheKey4...

> del-tag tagName1 tagName2 tagName3 tagName4...

- Created local caching that allows for a "turbo" of sorts. The objects cached locally will not be kept in sync with the cache hosts, however this is a great option for "reference data" that never changes. By using local caching, there is no repeated trip over the wire for static data.

- Added runnable performance tests that can be executed to test Dache throughput locally or remotely.

- Improved efficiency of initial client connection to cache host.

- Removed static Container classes which were not very OO; they have been replaced with proper inversion of control.

- Created initial unit tests; will add to them as development continues in the future

- Cleaned up various classes and updated XML comments

- Renamed methods on client contract. Unfortunately this is a breaking change however the client commands are now overloaded and much more intuitive to use.


1.1.2
------------------


- Further testing proved that interning everything was quite troublesome, so interning has been changed. Interning methods are now exposed on the client-server contract and are opt-in only. Interned objects cannot expire or be evicted - they must be removed manually when appropriate.

- Updated client contract with IEnumerable<T> to be more flexible than the previous ICollection<T>

- Modified the comments of the client to be clearer

- Preparing for unit and performance testing, which is definitely needed


1.1.1
------------------


- Intelligent interning of all objects stored in cache. This results in a > 40% memory use reduction for repeatedly cached objects at a performance hit of roughly 0.2% - a good trade!

- Removed erroneous TODO.txt reference in Dache.CacheHost project

- Updated uninstall.bat output (it said it was installing when it was actually uninstalling)

- Updated TODO.txt with tasks that need to get done soon


1.1.0
------------------


- Removal of cache manager entirely as it was not necessary

- Simplification of solution as a whole; consolidation of assemblies

- More efficent tagging and bulk API operations

- Order of listed cache hosts in client config no longer matters

- Added install.bat and uninstall.bat for easy installation and uninstallation


1.0.0
------------------


- Initial release of Dache

- Includes cache manager, cache host, and client.

- Some Dache Board work completed: needs more work in future.

- Custom performance counters.


INSTALLATION INSTRUCTIONS
============================================


Client
--------


The Dache Client is a single DLL which you include in any application which you wish to be able 
to talk to Dache from. Add it as a reference and begin coding. There is an included XML file so 
that Intellisense will show you method and type information. An example configuration file named 
`Client.Example.config` is also included to show you how to configure your application.

NOTE: all clients should be configured with the same list of servers. The list of servers does 
not have to be in the same order, but each client's list should contain the same servers.


Host
--------


The host is the actual process that does the caching work. To install it, run `install.bat` or
install it manually via .NET 4.0's `installutil` from a command prompt:

    C:\Windows\Microsoft.NET\Framework\v4.0.30319>installutil "C:\Path\To\Dache.CacheHost.exe"

You will be offered custom installation settings at this time, including the ability to rename the 
service if you want to install multiple Dache hosts on a single server under unique names.

After installation, open the `Dache.CacheHost.exe.config` file and configure the appropriate 
settings. The configuration file is fully XML commented.

To uninstall, run `uninstall.bat` or uninstall it manually via .NET 4.0's `installutil` from a command prompt:

    C:\Windows\Microsoft.NET\Framework\v4.0.30319>installutil /u "C:\Path\To\Dache.CacheHost.exe"


Board
--------


Not yet completed. Feel free to contribute! :)
