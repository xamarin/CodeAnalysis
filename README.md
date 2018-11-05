# Xamarin CodeAnalyis

Analyzers, code fixers and custom completion for Xamarin projects

## Building

Just open Xamarin.CodeAnalysis.sln and build.

> NOTE: the *first* build ever needs to be run from an administrator command prompt, 
> because the extension provides MSBuild targets that need to be symlinked from the 
> `Exp` hive location to the `VsInstallDir\MSBuild` location, which requires elevation.

