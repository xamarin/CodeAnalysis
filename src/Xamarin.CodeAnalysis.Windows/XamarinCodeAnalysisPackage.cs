using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

[assembly: ComVisible(false)]
[assembly: Guid("271716A6-CCF6-47F0-A358-70869B8B24BD")]
[assembly: ProvideCodeBase(AssemblyName = "Xamarin.CodeAnalysis")]
[assembly: ProvideCodeBase(AssemblyName = "Xamarin.CodeAnalysis.Completion")]
[assembly: ProvideCodeBase(AssemblyName = "Xamarin.CodeAnalysis.Windows")]

namespace Xamarin.CodeAnalysis
{
    [InstalledProductRegistration(
        productName: "#100",
        productDetails: "#101",
        productId: ThisAssembly.Metadata.InformationalVersion,
        IconResourceID = 400)]
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid("FBE35200-65BD-42B1-AC6D-2F4EB5719209")]
    [ProvideBindingPath]
    public class XamarinCodeAnalysisPackage : AsyncPackage
    {
    }
}
