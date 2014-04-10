DACHE 1.3.0
===========


distributed caching for .net applications 

fast, scalable distributed caching with meaningful performance metrics for your managers and a simple api for your development team

**WEB:**   http://www.getdache.net

**EMAIL:** info@getdache.net


VERSION INFORMATION
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

Supported built-in custom Loggers and Serializers:

`Dache.Client.Serialization.BinarySerializer, Dache.Client`
`Dache.Client.Serialization.GZipSerializer, Dache.Client`

`Dache.Core.Logging.EventViewerLogger, Dache.Core`
`Dache.Core.Logging.FileLogger, Dache.Core`


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

Supported built-in custom MemCaches and Serializers:

`Dache.CacheHost.Storage.MemCache, Dache.CacheHost`
`Dache.CacheHost.Storage.GZipMemCache, Dache.CacheHost`

`Dache.Core.Logging.EventViewerLogger, Dache.Core`
`Dache.Core.Logging.FileLogger, Dache.Core`


Board
--------


Not yet completed. Feel free to contribute! :)
