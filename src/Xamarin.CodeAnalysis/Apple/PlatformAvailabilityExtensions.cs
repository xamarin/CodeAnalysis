using Microsoft.CodeAnalysis;
using System;

namespace Xamarin.CodeAnalysis.Apple
{
    public static class PlatformAvailabilityExtensions
    {
        public static PlatformAvailability Merge(this PlatformAvailability availability, AttributeData attr)
        {
            if (attr.AttributeClass.ContainingNamespace.Name != "ObjCRuntime")
                return availability;


            switch (attr.AttributeClass.Name)
            {
                case "iOSAttribute":
                    availability = MergeShorthandAttribute(availability, attr, false);
                    break;
                case "MacAttribute":
                    availability = MergeShorthandAttribute(availability, attr, true);
                    break;
                case "AvailabilityAttribute": // all of the following attributes subclass this
                    availability = MergeAvailabilityAttribute(availability, attr);
                    break;
                case "IntroducedAttribute":
                    availability = MergeNewStyleAvailabilityAttribute(availability, attr, AvailabilityKind.Introduced);
                    break;
                case "DeprecatedAttribute":
                    availability = MergeNewStyleAvailabilityAttribute(availability, attr, AvailabilityKind.Deprecated);
                    break;
                case "ObsoletedAttribute":
                    availability = MergeNewStyleAvailabilityAttribute(availability, attr, AvailabilityKind.Obsoleted);
                    break;
                case "UnavailableAttribute":
                    availability = MergeNewStyleAvailabilityAttribute(availability, attr, AvailabilityKind.Unavailable);
                    break;
            }

            return availability;
        }

        static PlatformAvailability MergeNewStyleAvailabilityAttribute(PlatformAvailability availability, AttributeData attr, AvailabilityKind availabilityKind)
        {
            var platformName = (PlatformName)(byte)attr.ConstructorArguments[0].Value;
            var platformSet = new PlatformSet();
            Platform platform = platformSet.GetPlatform(platformName);
            if (platform == null)
                throw new ArgumentOutOfRangeException($"Platform name: {platformName}.");

            if (availability == null)
                availability = new PlatformAvailability();

            switch (attr.ConstructorArguments.Length)
            {
                case 3: //PlatformName platform, PlatformArchitecture architecture, string message
                    platform.Architecture = (PlatformArchitecture)(byte)attr.ConstructorArguments[1].Value;
                    break;
                case 5: //PlatformName platform, int majorVersion, int minorVersion, PlatformArchitecture architecture, string message
                    platform.Major = (byte)(int)attr.ConstructorArguments[1].Value;
                    platform.Minor = (byte)(int)attr.ConstructorArguments[2].Value;
                    platform.Architecture = (PlatformArchitecture)(byte)attr.ConstructorArguments[3].Value;
                    availability.Message = (string)attr.ConstructorArguments[4].Value;
                    break;
                case 6: //PlatformName platform, int majorVersion, int minorVersion, int subminorVersion, PlatformArchitecture architecture, string message
                    platform.Major = (byte)(int)attr.ConstructorArguments[1].Value;
                    platform.Minor = (byte)(int)attr.ConstructorArguments[2].Value;
                    platform.Minor = (byte)(int)attr.ConstructorArguments[3].Value;
                    platform.Architecture = (PlatformArchitecture)(byte)attr.ConstructorArguments[4].Value;
                    availability.Message = (string)attr.ConstructorArguments[5].Value;
                    break;
            }

            switch (availabilityKind)
            {
                case AvailabilityKind.Introduced:
                    availability.Introduced |= platformSet;
                    break;
                case AvailabilityKind.Deprecated:
                    availability.Deprecated |= platformSet;
                    break;
                case AvailabilityKind.Obsoleted:
                    availability.Obsoleted |= platformSet;
                    break;
                case AvailabilityKind.Unavailable:
                    availability.Unavailable |= platformSet;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return availability;
        }

        static PlatformAvailability MergeShorthandAttribute(PlatformAvailability availability,
                                         AttributeData attr, bool isMac, byte legacyNamedMacAttrMinorVersion = 0)
        {
            if (availability == null)
                availability = new PlatformAvailability();

            if (availability.Introduced == null)
                availability.Introduced = new PlatformSet();

            var platform = isMac
                ? availability.Introduced.MacOSX
                : availability.Introduced.iOS;

            var onlyOn64ArgIndex = 2;

            if (legacyNamedMacAttrMinorVersion > 0)
            {
                onlyOn64ArgIndex = 0;
                platform.Major = 10;
                platform.Minor = legacyNamedMacAttrMinorVersion;
            }
            else
            {
                platform.Major = (byte)attr.ConstructorArguments[0].Value;
                platform.Minor = (byte)attr.ConstructorArguments[1].Value;
            }

            if (attr.ConstructorArguments.Length == onlyOn64ArgIndex + 1 &&
                (bool)attr.ConstructorArguments[onlyOn64ArgIndex].Value)
                platform.Architecture = PlatformArchitecture.Arch64;

            return availability;
        }

        static PlatformAvailability MergeAvailabilityAttribute(PlatformAvailability availability, AttributeData attr)
        {
            if (availability == null)
                availability = new PlatformAvailability();

            // first read positional arguments as they come first syntatically
            int n = Math.Min(attr.ConstructorArguments.Length, 4);

            for (int i = 0; i < n; i++)
            {
                var value = (ulong)attr.ConstructorArguments[i].Value;
                if (value == 0)
                    continue;

                var platform = new PlatformSet(value);

                switch (i)
                {
                    case 0:
                        availability.Introduced |= platform;
                        break;
                    case 1:
                        availability.Deprecated |= platform;
                        break;
                    case 2:
                        availability.Obsoleted |= platform;
                        break;
                    case 3:
                        availability.Unavailable |= platform;
                        break;
                }
            }

            // (e.g. [Availability (Platform.Mac_10_9, Introduced = Platform.Mac_10_10)]
            // makes no sense, but could technically happen, in which case Platform.Mac_10_10
            // would be the winner).
            foreach (var named in attr.NamedArguments)
            {
                switch (named.Key)
                {
                    case "Introduced":
                        availability.Introduced |= new PlatformSet((ulong)named.Value.Value);
                        break;
                    case "Deprecated":
                        availability.Deprecated |= new PlatformSet((ulong)named.Value.Value);
                        break;
                    case "Obsoleted":
                        availability.Obsoleted |= new PlatformSet((ulong)named.Value.Value);
                        break;
                    case "Unavailable":
                        availability.Unavailable |= new PlatformSet((ulong)named.Value.Value);
                        break;
                    case "Message":
                        string message = named.Value.Value as string;

                        if (message != null) // maybe throw an exception here instead of silently skipping this?
                            availability.Message += (availability.Message == null) ? message : string.Join("; ", message);

                        break;
                }
            }

            return availability;
        }

        public static string GetMinimumRequiredPlatformMessage(this PlatformAvailability availability, Version minOSVersion, PlatformName platformName)
        {
            if (availability == null || !availability.IsSpecified || platformName == PlatformName.None)
                return null;

            var targetPlatform = GetDeploymentTargetPlatform(minOSVersion, platformName); // used to get Project project passed in
            if (targetPlatform == null)
                return null;

            var introduced = availability.GetIntroducedPlatformFor(targetPlatform);
            if (introduced == null || !introduced.IsSpecified || introduced.CompareTo(targetPlatform) <= 0)
                return null;

            if (introduced.Architecture != PlatformArchitecture.All &&
                introduced.Architecture.HasFlag(PlatformArchitecture.Arch64))
                return $"is only available on {introduced.FullName} or newer and requires a 64-bit architecture";
            else
                return $"is only available on {introduced.FullName} or newer";
        }

        static Boolean IsAttributeOfCurrentPlatform(PlatformSet platformSet, PlatformName platformName)
            => (platformSet == null) ? false : platformSet.IsSpecifiedFor(platformName);

        static string GetFullName(PlatformSet platformSet, PlatformName platformName) =>
            (platformSet != null) ? platformSet.GetFullName(platformName) : throw new ArgumentNullException();

        public static string GetDeprecatedMessage(this PlatformAvailability availability, PlatformName platformName)
        {
            if (availability == null || !availability.IsSpecified || platformName == PlatformName.None)
                return null;

            var unavailable = IsAttributeOfCurrentPlatform(availability.Unavailable, platformName);
            var obsoleted = IsAttributeOfCurrentPlatform(availability.Obsoleted, platformName);
            var deprecated = IsAttributeOfCurrentPlatform(availability.Deprecated, platformName);

            if (unavailable)
                return $"was made unavailable in {GetFullName(availability.Unavailable, platformName)}";

            if (obsoleted && deprecated)
                return $"was deprecated in {GetFullName(availability.Deprecated, platformName)} and obsoleted (might be removed) in {GetFullName(availability.Obsoleted, platformName)}";

            if (obsoleted)
                return $"was obsoleted (might be removed) in {GetFullName(availability.Obsoleted, platformName)}";

            if (deprecated)
                return $"was deprecated in {GetFullName(availability.Deprecated, platformName)}";

            return null;
        }

        static Platform GetDeploymentTargetPlatform(Version minOSVersion, PlatformName platformName)
            => new Platform(
                platformName,
                major: (byte)minOSVersion.Major,
                minor: (byte)minOSVersion.Minor
            );
    }
}
