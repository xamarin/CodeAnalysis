using Microsoft.CodeAnalysis;
using Xamarin.CodeAnalysis.Properties;

namespace Xamarin.CodeAnalysis
{
    static internal class LocalizableString
    {
        public static LocalizableResourceString Localizable(string resourceName)
            => new LocalizableResourceString(resourceName, Resources.ResourceManager, typeof(Resources));
    }
}
