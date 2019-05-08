using System;
namespace Xamarin.CodeAnalysis.Addin
{
    public class AddinInitializer
    {
        public AddinInitializer()
        {
            // HACK: REQUIRED TO UPDATE MEF COMPONENTS WITH THE CLASSES FROM THIS DLL
            if (MonoDevelop.Ide.IdeApp.IsInitialized) { }
        }
    }
}
