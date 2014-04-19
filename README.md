DACHE 1.3.1
===========


distributed caching for .net applications 

fast, scalable distributed caching with meaningful performance metrics for your managers and a simple api for your development team

**WEB:**   http://www.getdache.net

**EMAIL:** info@getdache.net


VERSION INFORMATION
============================================


1.3.1
------------------

***** NOTE: it is STRONGLY recommended that you IMMEDIATELY upgrade to this version as it has important memory usage fixes (memory usage reduced by a factor of 90-95%) *****

- Fixed Garbage Collection issue with Cache Host: memory usage has dropped SUBSTANTIALLY as a result! Infinite add test now uses ~ 6 megabytes total (was > 250 megabytes)!

- Downgraded projects to .NET 4.0 because it allows more people to use Dache. Included Microsoft.Bcl.Async package to enable async/await on .NET 4.0

- Added configurable maximum connections to Cache Host (specified in the .config file)

- Upgraded SimplSockets to 1.1.2 which improves memory efficiency and usage

- Updated copyright information to my company: Imperative Bytes, LLC


INSTALLATION INSTRUCTIONS
============================================


Client
--------


The Dache Client is a single DLL which you include in any application which you wish to be able 
to talk to Dache from. Add it and its dependencies as a reference and begin coding. There is an included 
XML file so that Intellisense will show you method and type information. An example configuration file named 
`Client.Example.config` is also included to show you how to configure your application.

**NOTE: all clients should be configured with the same list of servers. The list of servers does 
not have to be in the same order, but each client's list should contain the same servers.**

Supported built-in custom Loggers and Serializers:

`Dache.Client.Serialization.BinarySerializer, Dache.Client`

`Dache.Client.Serialization.GZipSerializer, Dache.Client`

`Dache.Core.Logging.EventViewerLogger, Dache.Core`

`Dache.Core.Logging.FileLogger, Dache.Core`


Host
--------


The host is the actual process that does the caching work. To install it, run `install.bat` or
install it manually via .NET 4.0's `installutil` from a command prompt:

`C:\Windows\Microsoft.NET\Framework\v4.0.30319>installutil "C:\Path\To\Dache.CacheHost.exe"`

You will be offered custom installation settings at this time, including the ability to rename the 
service if you want to install multiple Dache hosts on a single server under unique names.

After installation, open the `Dache.CacheHost.exe.config` file and configure the appropriate 
settings. The configuration file is fully XML commented.

To uninstall, run `uninstall.bat` or uninstall it manually via .NET 4.0's `installutil` from a command prompt:

`C:\Windows\Microsoft.NET\Framework\v4.0.30319>installutil /u "C:\Path\To\Dache.CacheHost.exe"`

Supported built-in custom MemCaches and Serializers:

`Dache.CacheHost.Storage.MemCache, Dache.CacheHost`

`Dache.CacheHost.Storage.GZipMemCache, Dache.CacheHost`

`Dache.Core.Logging.EventViewerLogger, Dache.Core`

`Dache.Core.Logging.FileLogger, Dache.Core`


Board
--------


Not yet completed. Feel free to contribute! :)
