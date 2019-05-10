using System;
namespace Xamarin.CodeAnalysis
{
    public static class MyResources
    {
        public static string XAA1002_Title = "Resource id must exist in a resource file";
        public static string XAA1002_MessageFormat = "No resource found with identifier '{0}'.";
        public static string XAA1002_Description = "Resource identifier must exist in an Android resource file.";

        public static string XAA1001_ActionTitle = "Move string to resource";
        public static string XAA1001_Description = "By placing literal strings in a resource file, it can be easily localized.";
        public static string XAA1001_MessageFormat = "Attribute value '{0}' should be placed in a resource file.";
        public static string XAA1001_Title = "Attribute value should be placed in a resource file.";
    }
}
