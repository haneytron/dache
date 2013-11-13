DACHE 1.1.2
===========


distributed caching for .net applications 

fast, scalable distributed caching with meaningful performance metrics for your managers and a simple api for your development team

http://www.getdache.net

info@getdache.net


VERSION HISTORY
============================================


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
that Intellisense will show you method and type information. An example configuration file is 
also included to show you how to configure your application.

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

To uninstall, run 'uninstall.bat' or uninstall it manually via .NET 4.0's `installutil` from a command prompt:

    C:\Windows\Microsoft.NET\Framework\v4.0.30319>installutil /u "C:\Path\To\Dache.CacheHost.exe"


Board
--------


Not yet completed. Feel free to contribute! :)
