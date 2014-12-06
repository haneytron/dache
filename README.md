DACHE
===========


Distributed caching for .NET applications 

Fast, scalable distributed caching with meaningful performance metrics for your managers and a simple API for your development team

**WEB:**   http://www.dache.io

**EMAIL:** [info@dache.io](mailto:info@dache.io)

**NUGET:** [Dache.Client](http://www.nuget.org/packages/Dache.Client) and [Dache.CacheHost](http://www.nuget.org/packages/Dache.CacheHost)

**DOWNLOAD:** http://www.dache.io/download


VERSION INFORMATION
============================================


1.4.5
------------------

- Greatly simplified Dache host and client configuration. Most settings are now optional.
- Created new Dache Host executable that can be run as a quick-start without installing the service. This file hosts Dache with default settings and provides information in a console window.
- Upgraded SimplSockets. The latest version supports a maximum message size limit and also returns the `Connect` method of the client to synchronous.
- Fixed a broken unit test.
- Massively improved Dache Host install.bat and uninstall.bat process. Now provides much more feedback and does checks for permissions prior to install and uninstall.
- Significantly improved NuGet packages. The host is simplified to take a `CacheHostConfigurationSection` instead of multiple variables. The client is installed with default settings injected into `Web.config` or `App.config` and now provides `CacheProvider.cs` which is a simple, working implementation of the Dache client which can be used to experiment.
- Created MSBUILD tasks to create a nice execution folder structure for Dache files after each build. This new folder is in root of solution and is called `dache-<version>`.
- NOTE: releases will not longer be offered in GitHub but instead at our download page: http://www.dache.io/download - this is a much simpler and less confusing way to get Dache.


INSTALLATION INSTRUCTIONS
============================================


Client
--------


The Dache Client is a single DLL which you include in any application which you would like to use Dache with. Include it via ([NuGet](http://www.nuget.org/packages/Dache.Client)). Your `web.config` or `app.config` will be automatically modified to install the default Dache client configuration:

```xml
<configuration>
  <configSections>
    <section name="cacheClientSettings"
             type="Dache.Client.Configuration.CacheClientConfigurationSection, Dache.Client"/>
  </configSections>
  <cacheClientSettings>
    <cacheHosts>
      <add address="localhost" port="33333" />
    </cacheHosts>
  </cacheClientSettings>
</configuration>
```

A file called `CacheProvider.cs` will also be installed at the root of your project. It is a working example of using the Dache client and is intended for experimentation and getting a quick-start with Dache. You can build on top of this implementation or discard it completely. The purpose of it is to show you how to use the Dache client in your code.

**NOTE: all clients should be configured with the same list of servers. The list of servers does not have to be in the same order, but each client's list should contain the same servers.**

[**See Quick-Start for more information**](https://github.com/ironyx/dache/wiki/Quick-Start)

Host
--------


The host is the actual process that does the caching work. You have 2 options for hosting Dache. You can either use the Windows service provided in the download (see: http://www.dache.io/download), or you can include the Dache host ([NuGet](http://www.nuget.org/packages/Dache.CacheHost)) package and host it in your own process (such as an Azure worker role). The choice is yours!

To host it in your own process, Include it via ([NuGet](http://www.nuget.org/packages/Dache.Client)). Your `web.config` or `app.config` will be automatically modified to install the default Dache host configuration:

```xml
<configuration>
  <configSections>
    <section name="cacheHostSettings"
             type="Dache.CacheHost.Configuration.CacheHostConfigurationSection, Dache.CacheHost"
             allowExeDefinition="MachineToApplication" />
  </configSections>
  <cacheHostSettings port="33333" />
</configuration>
```

Next, instantiate a `new Dache.CacheHost.CacheHostEngine(CacheHostConfigurationSection configuration)`, passing in either `CacheHostConfigurationSection.Settings` to use the settings from your `app.config` or `web.config`, or a `new CacheHostConfigurationSection()` which you have manually configured to supply the settings via code.

To install and use the provided Windows service, first download the binaries from http://www.dache.io/download and then run `install.bat` which is located in the `CacheHost` folder. You will be offered custom installation settings, including the ability to rename the service if you want to install multiple Dache hosts on a single server under unique names.

After successful installation, you can run the service from Windows Services.

To uninstall Dache, run `uninstall.bat` which is located in the `CacheHost` folder.

[**See Quick-Start for more information**](https://github.com/ironyx/dache/wiki/Quick-Start)


Board
--------


Not yet completed. Feel free to contribute! :)


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
License by contacting us at [info@dache.io](mailto:info@dache.io).

Please see `LICENSE.txt` for more information.


IMPORTANT NOTE TO SOURCE CODE CONTRIBUTORS
============================================


In order to clarify the intellectual property license granted with Contributions from any person or entity, Imperative Bytes, LLC. 
("Imperative Bytes") must have a Contributor License Agreement ("CLA") on file that has been signed by each Contributor, indicating 
agreement to the license terms of the **Dache Individual Contributor License Agreement** (located in `INDIVIDUAL.txt`). This license 
is for your protection as a Contributor as well as the protection of Imperative Bytes; it does not change your rights to use your own 
Contributions for any other purpose. If you have not already done so, please complete, scan, and e-mail an original signed Agreement 
to [info@dache.io](mailto:info@dache.io).
