DACHE
===========


Distributed caching for .NET applications 

Fast, scalable distributed caching with meaningful performance metrics for your managers and a simple API for your development team

**WEB:**   http://www.dache.io

**EMAIL:** [info@dache.io](mailto:info@dache.io)

**NUGET:** [Dache.Client](http://www.nuget.org/packages/Dache.Client) and [Dache.CacheHost](http://www.nuget.org/packages/Dache.CacheHost)

**DOWNLOAD:** http://www.dache.io/download

**WIKI:** http://www.github.com/ironyx/dache/wiki


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
- **IMPORTANT:** releases will no longer be offered in GitHub. Instead, they will be found at our download page: http://www.dache.io/download - this is a much simpler and less confusing way to get the Dache binaries.


INSTALLATION INSTRUCTIONS
============================================


Getting started quickly involves standing up the Dache Client and a Dache Host for the client to communicate with.

## Client

The Dache Client is a single DLL which you include in any application which you would like to use Dache with. Include it via [NuGet](http://www.nuget.org/packages/Dache.Client). Your `web.config` or `app.config` will be automatically modified to install the default Dache client configuration:

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

**NOTE:** if you prefer to derive your settings from code rather than configuration, use the `new CacheClient(CacheClientConfigurationSection configuration)` constructor, passing in a `CacheClientConfigurationSection` which you've configured in code.

**IMPORTANT:** all clients should be configured with the same list of servers. The list of servers does not have to be in the same order, but each client's list should contain the same servers.

## Host

The host is the actual process that does the caching work. You have 3 options for hosting Dache:

- Run the **quick and easy console host** provided in the [latest release download](http://www.dache.io/download)
- Install the **Windows service** provided in the [latest release download](http://www.dache.io/download)
- Host Dache **in your own process** by including the [Dache.CacheHost NuGet package](http://www.nuget.org/packages/Dache.CacheHost)

### Quick And Easy Console Host

To use the console host, first download the [latest release](http://www.dache.io/download) and then run (or double click) `CacheHost/Dache.CacheHost.exe`. A console will open that verifies the Dache settings and then gives you information about Dache as it is used.

### Windows Service

To install and use the provided Windows service, first download the binaries from http://www.dache.io/download and then run `CacheHost/install.bat`. You will be offered custom installation settings, including the ability to rename the service if you want to install multiple Dache hosts on a single server under unique names.

After successful installation, you can run the service from Windows Services.

To uninstall Dache, run `CacheHost/uninstall.bat`.

### Host In Your Own Process

To host it in your own process, Include it via [NuGet](http://www.nuget.org/packages/Dache.CacheHost). Your `web.config` or `app.config` will be automatically modified to install the default Dache host configuration:

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

Next, instantiate the `CacheHostEngine`:

```csharp
// Using the settings from app.config or web.config
var cacheHost = new Dache.CacheHost.CacheHostEngine(CacheHostConfigurationSection.Settings);
```

or

```csharp
// Using programmatically created settings
var settings = new CacheHostConfigurationSettings { ... };
var cacheHost = new Dache.CacheHost.CacheHostEngine(settings);
```

## Next Steps

To learn more about using Dache, check out the [wiki](https://github.com/ironyx/dache/wiki).

## Board

Not yet completed. Feel free to contribute! :)

## LICENSE INFORMATION

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

## IMPORTANT NOTE TO SOURCE CODE CONTRIBUTORS

In order to clarify the intellectual property license granted with Contributions from any person or entity, Imperative Bytes, LLC. 
("Imperative Bytes") must have a Contributor License Agreement ("CLA") on file that has been signed by each Contributor, indicating 
agreement to the license terms of the **Dache Individual Contributor License Agreement** (located in `INDIVIDUAL.txt`). This license 
is for your protection as a Contributor as well as the protection of Imperative Bytes; it does not change your rights to use your own 
Contributions for any other purpose. If you have not already done so, please complete, scan, and e-mail an original signed Agreement 
to [info@dache.io](mailto:info@dache.io).
