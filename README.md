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

The **GNU General Public License Version 3** available for review 
at http://www.gnu.org/copyleft/gpl.html

-or-

The **Commercial Dache License**, which must be purchased directly 
from Imperative Bytes, LLC - the Limited Liability Company which 
owns the Dache source code. You may purchase the Commercial Dache 
License by contacting us at [info@getdache.net](mailto:info@getdache.net).

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


1.4.1 - THE FASTER, BETTER, STRONGER BUILD!
------------------

- HIGH AVAILABILITY! You can now configure Dache to use > 1 cache host per cache fragment/bucket. This allows for redundancy in the case of cache server failure! To utilize this, adjust the `hostRedundancyLayers` setting in the dache client `.config` file.

- FASTER BETTER COMMUNICATION! SimplSockets rewritten for much more efficient communication. Also streamlined the Dache low level TCP syntax.

- MUCH SIMPLER CLIENT METHOD CALLS! Reduced most operations to 1 or 2 methods with named parameters instead of having lke 17 Add____ methods.

- CACHED ITEM REMOVAL CALLBACKS! Now you can be notified when a cache key is removed from cache. NOTE: updating a cache key triggers a removal, so don't spin yourself into a loop with this!

- Performance counter overhaul; no longer using performance counters for self-hosting (fixes errors related to permissions for self-hosting)

- Catching and swallowing regex pattern exceptions. If you use a bad regex pattern, you'll get no results.

- Fixed cache memory limit reporting (`MemoryCache` MSDN docs LIED to me!)

- White space checks on cache keys and tag names (will throw exception at client)

- Added new performance test for removed item callbacks. This lets you see how subscribed to removed cache keys works.

- Updated `TODO.txt`

- Updated example client config file and documentation

- Updated various XML code comments

- Minor formatting of `README` file

- Various minor edge case bug fixes

- Fixed breaking change in tag adds introduced in 1.4.0

- Fixed breaking change in cache key misses introduced in 1.4.0


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
in your code ([NuGet](http://www.nuget.org/packages/Dache.CacheHost)) and host it in your own process 
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