DACHE 1.0.0

http://www.getdache.net
info@getdache.net

============================================
VERSION HISTORY
============================================

------------------
1.0.0
------------------

Initial release of Dache

-Includes cache manager, cache host, and client.
-Some Dache Board work completed: needs more work in future.
-Custom performance counters.


============================================
INSTALLATION INSTRUCTIONS
============================================

--------
Client
--------

The Dache Client is a single DLL which you include in any application which you wish to be able 
to talk to Dache from. Add it as a reference and begin coding. There is an included XML file so 
that Intellisense will show you method and type information. An example configuration file is 
also included to show you how to configure your application.

--------
Host
--------

The host is the actual process that does the caching work. Install it via .NET 4.0's installutil 
from a command prompt:

C:\Windows\Microsoft.NET\Framework\v4.0.30319>installutil "C:\Path\To\Dache.Service.CacheHost.exe"

You will be offered custom installation settings at this time, including the ability to rename the 
service if you want to install multiple Dache hosts on a single server under unique names.

After installation, open the Dache.Service.CacheHost.exe.config file and configure the appropriate 
settings.

--------
Manager
--------

The manager exists to help cache hosts register with each other and communicate with each other. 
It is a very low overhead, low network traffic service that is not processor or memory intensive. 
We recommend that you install this alongside a host on any particular server, though you can dedicate 
a server to it if you wish. Install it via .NET 4.0's installutil from a command prompt:

C:\Windows\Microsoft.NET\Framework\v4.0.30319>installutil "C:\Path\To\Dache.Service.CacheManager.exe"

You will be offered custom installation settings at this time.

After installation, open the Dache.Service.CacheManager.exe.config file and configure the appropriate 
settings.
