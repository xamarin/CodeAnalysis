using System;
using Mono.Addins;
using Mono.Addins.Description;

[assembly:Addin (
    "Addin", 
    Namespace = "Xamarin.CodeAnalysis",
    Version = "1.0"
)]

[assembly:AddinName ("Xamarin.CodeAnalysis")]
[assembly:AddinCategory ("Code Analysis")]
[assembly:AddinDescription ("Code Analysis for Xamarin Projects")]
[assembly:AddinAuthor ("xamarin")]

[assembly:AddinDependency ("::MonoDevelop.Core", MonoDevelop.BuildInfo.Version)]
[assembly:AddinDependency ("::MonoDevelop.Ide", MonoDevelop.BuildInfo.Version)]
