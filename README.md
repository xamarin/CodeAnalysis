# Xamarin CodeAnalysis

Analyzers, code fixers and custom completion for Xamarin projects

## Installing

In order to dogfood the enhanced code analysis, follow these steps:

### Visual Studio

1. Configure a new feed in the Extensions settings pointing to `https://xamci.azurewebsites.net/feed/xamarin/CodeAnalysis`:

![Tools | Options, Environment > Extensions](https://dl.internalx.com/vsts-devdiv/CodeAnalysis/public/docs/vs-feed.png)

2. Once configured, click on `Extensions > Manage Extensions` 
   and go to the `Online` tab. The new extension will be available under the Xamarin Code Analysis node:

![Xamarin Code Analysis extension](https://dl.internalx.com/vsts-devdiv/CodeAnalysis/public/docs/vs-online.png)

3. After installation, the extension will appear under the `Installed` node:

![Xamarin Code Analysis installed](https://dl.internalx.com/vsts-devdiv/CodeAnalysis/public/docs/vs-installed.png)

> Note the *Version* field on the details pane.

4. You can check for updates from the `Online` tab. Whenever a version newer than the one you have 
   currently installed is available, you can just click `Download` and after restarting the IDE, it
   will be applied:

![Xamarin Code Analysis installed](https://dl.internalx.com/vsts-devdiv/CodeAnalysis/public/docs/vs-update.png)

## Building

First build:

* Open an administrator developer command prompt and run `msbuild Xamarin.CodeAnalysis.sln /restore /t:configure`

> This is necessary because the extension provides MSBuild targets that need to be symlinked from the 
> `Exp` hive location to the `VsInstallDir\MSBuild` location, which requires elevation.

Subsequent/incremental builds:

* Just open Xamarin.CodeAnalysis.sln and build/run (using `Xamarin.CodeAnalysis.Windows` as the startup 
  project on Windows)

