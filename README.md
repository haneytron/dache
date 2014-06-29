DACHE
===========


distributed caching for .net applications 

fast, scalable distributed caching with meaningful performance metrics for your managers and a simple api for your development team

**WEB:**   http://www.getdache.net

**EMAIL:** [info@getdache.net](mailto:info@getdache.net)

**NUGET:** [Dache.Client](http://www.nuget.org/packages/Dache.Client) and [Dache.CacheHost](http://www.nuget.org/packages/Dache.CacheHost)


LICENSE INFORMATION
============================================


Dache software is dual licensed. You must choose which license you 
would like to use Dache under from the following 2 options:

The GNU General Public License Version 3 available for review 
at <http://www.gnu.org/copyleft/gpl.html> and also included below.

-or-

The Commercial Dache License, which must be purchased directly 
from Imperative Bytes, LLC - the Limited Liability Company which 
owns the Dache source code. You may purchase the Commercial Dache 
License by contacting us at <mailto:info@getdache.net>.

Please see `LICENSE.txt` for more information.


IMPORTANT NOTE TO SOURCE CODE CONTRIBUTORS
============================================


In order to clarify the intellectual property license granted with Contributions from any person or entity, Imperative Bytes, LLC. 
("Imperative Bytes") must have a Contributor License Agreement ("CLA") on file that has been signed by each Contributor, indicating 
agreement to the license terms of the **Dache Individual Contributor License Agreement** (located in `INDIVIDUAL.txt`). This license 
is for your protection as a Contributor as well as the protection of Imperative Bytes; it does not change your rights to use your own 
Contributions for any other purpose. If you have not already done so, please complete, scan, and e-mail an original signed Agreement 
to [info@getdache.net](mailto:info@getdache.net).


VERSION INFORMATION
============================================


1.3.2
------------------

- IMPORTANT LICENSING CHANGE: Dache software is now dual licensed. Please see the LICENSE.txt file for more information.

- POSSIBLE BREAKING CHANGE: Due to the separation of the cache host logic into an independent DLL, the cache host service has been renamed from Dache.CacheHost.exe to Dache.CacheHostService.exe

- Separated cache host into independent DLL which can be used in your own custom processes and code. This enables things like using Dache in custom Azure Worker Roles or console applications!

- Created NuGet package for the independent Dache Cache Host DLL.

- Cleaned up Performance Counter code and (hopefully) fixed bug related to performance counter memory leak. If the issue remains after this build I'll be removing the performance counters entirely.

- Removed unused Cache Evictions and Expirations Per Second performance counter.

- Fixed 100% cache miss on GetTaggedLocal

- Fixed possible infinite loops in cache client when no data was returned from the cache host

- Fixed bug with assigning/resolving cache host IP address in CommunicationClient

- Fixed very, VERY stupid error with absolute expiration of cached item date time format. I should have written unit tests!

- Various code cleanup housekeeping tasks

- Stubbed data persistence framework to allow Dache to persist cached data between restarts (recover from crashes and failures)! This feature is not yet complete.


INSTALLATION INSTRUCTIONS
============================================


Client
--------


The Dache Client is a single DLL which you include in any application which you wish to be able 
to talk to Dache from. Add it and its dependencies as a reference and begin coding ([NuGet](http://www.nuget.org/packages/Dache.Client)). There is an included 
XML file so that Intellisense will show you method and type information. An example configuration file named 
`Client.Example.config` is also included to show you how to configure your application.

**NOTE: all clients should be configured with the same list of servers. The list of servers does 
not have to be in the same order, but each client's list should contain the same servers.**

**Supported built-in custom Loggers and Serializers:**

`Dache.Client.Serialization.BinarySerializer, Dache.Client`

`Dache.Client.Serialization.GZipSerializer, Dache.Client`

`Dache.Core.Logging.EventViewerLogger, Dache.Core`

`Dache.Core.Logging.FileLogger, Dache.Core`


Host
--------


The host is the actual process that does the caching work. You have 2 options for hosting Dache. 
You can either use the Windows service provided with the code, or you can include `Dache.CacheHost.dll` 
in your code (([NuGet](http://www.nuget.org/packages/Dache.CacheHost)) and host it in your own process 
(such as an Azure worker role). The choice is yours!

To host it yourself, instantiate a `Dache.CacheHost.CacheHostEngine` with your desired mem cache 
implementation, logger, and settings.

To install and use the provided Windows service, run `install.bat` or
install it manually via .NET 4.0's `installutil` from a command prompt:

`C:\Windows\Microsoft.NET\Framework\v4.0.30319>installutil "C:\Path\To\Dache.CacheHostService.exe"`

You will be offered custom installation settings, including the ability to rename the 
service if you want to install multiple Dache hosts on a single server under unique names.

After installation, open the `Dache.CacheHostService.exe.config` file and configure the appropriate 
settings. The configuration file is fully XML commented.

To uninstall, run `uninstall.bat` or uninstall it manually via .NET 4.0's `installutil` from a command prompt:

`C:\Windows\Microsoft.NET\Framework\v4.0.30319>installutil /u "C:\Path\To\Dache.CacheHostService.exe"`

**Supported built-in custom MemCaches and Loggers:**

`Dache.CacheHost.Storage.MemCache, Dache.CacheHost`

`Dache.CacheHost.Storage.GZipMemCache, Dache.CacheHost`

`Dache.Core.Logging.EventViewerLogger, Dache.Core`

`Dache.Core.Logging.FileLogger, Dache.Core`


Board
--------


Not yet completed. Feel free to contribute! :)