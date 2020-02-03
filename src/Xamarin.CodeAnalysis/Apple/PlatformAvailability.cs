using System;
using System.Linq;
using System.Text;
using System.Collections;
using System.Collections.Generic;

namespace Xamarin.CodeAnalysis.Apple
{
    [Flags]
    public enum PlatformArchitecture : byte
    {
        None = 0x00,
        Arch32 = 0x01,
        Arch64 = 0x02,
        All = 0xff
    }

    public enum PlatformName : byte
    {
        None,
        MacOSX,
        iOS,
        WatchOS,
        TvOS
    }

    public enum AvailabilityKind
    {
        Introduced,
        Deprecated,
        Obsoleted,
        Unavailable
    }

    public class Platform : IComparable<Platform>
    {
        internal uint value;

        public PlatformName Name { get; set; }

        public PlatformArchitecture Architecture
        {
            get
            {
                var arch = (byte)(value >> 24);
                if (arch == 0)
                    return PlatformArchitecture.All;
                return (PlatformArchitecture)arch;
            }

            set { this.value = (this.value & 0x00ffffff) | (uint)(byte)value << 24; }
        }

        public byte Major
        {
            get { return (byte)(value >> 16); }
            set { this.value = (this.value & 0xff00ffff) | (uint)(byte)value << 16; }
        }

        public byte Minor
        {
            get { return (byte)(value >> 8); }
            set { this.value = (this.value & 0xffff00ff) | (uint)(byte)value << 8; }
        }

        public byte Subminor
        {
            get { return (byte)value; }
            set { this.value = (this.value & 0xffffff00) | (uint)(byte)value; }
        }

        static string ToNiceName(PlatformName name)
        {
            switch (name)
            {
                case PlatformName.None:
                    return "None";
                case PlatformName.MacOSX:
                    return "macOS";
                case PlatformName.iOS:
                    return "iOS";
                case PlatformName.WatchOS:
                    return "watchOS";
                case PlatformName.TvOS:
                    return "tvOS";
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public string FullName
        {
            get
            {
                var name = ToNiceName(Name);

                // when a version is specified, use that; all bits set mean
                // the attribute applies to all versions, so ignore that
                if (Major > 0 && Major != 0xff)
                    return String.Format("{0} {1}.{2}", name, Major, Minor);

                // otherwise note the architecture
                if (Architecture == PlatformArchitecture.Arch32)
                    return String.Format("{0} 32-bit", name);

                if (Architecture == PlatformArchitecture.Arch64)
                    return String.Format("{0} 64-bit", name);

                // unless it applies to a combination or lack
                // of architectures, then just use the name
                return name;
            }
        }

        public bool IsSpecified => value != 0;

        internal Platform(PlatformName name, uint value)
        {
            Name = name;
            this.value = value;
        }

        public Platform(PlatformName name,
            PlatformArchitecture architecture = PlatformArchitecture.All,
            byte major = 0, byte minor = 0, byte subminor = 0)
        {
            Name = name;
            Architecture = architecture;
            Major = major;
            Minor = minor;
            Subminor = subminor;
        }

        public int CompareTo(Platform other)
        {
            if (other == null)
                return 1;

            if (Major > other.Major)
                return 1;
            if (Major < other.Major)
                return -1;

            if (Minor > other.Minor)
                return 1;
            if (Minor < other.Minor)
                return -1;

            if (Subminor > other.Subminor)
                return 1;
            if (Subminor < other.Subminor)
                return -1;

            return 0;
        }

        public override string ToString()
        {
            if (!IsSpecified)
                return String.Empty;

            var name = ToNiceName(Name);

            StringBuilder sb = new StringBuilder();

            if (Major > 0)
            {
                sb.AppendFormat("Platform.{0}_{1}_{2}", name, Major, Minor);
                if (Subminor > 0)
                    sb.AppendFormat("_{0}", Subminor);
            }

            if (Architecture != PlatformArchitecture.All)
            {
                if (sb.Length != 0)
                    sb.Append(" | ");
                sb.Append(Architecture.ToString().Replace("Any", "Platform." + name + "_Arch"));
            }

            return sb.ToString();
        }
    }

    public class PlatformSet : IEnumerable<Platform>
    {
        // Analysis disable once InconsistentNaming
        public Platform iOS { get; private set; }
        public Platform MacOSX { get; private set; }
        public Platform WatchOS { get; private set; }
        public Platform TvOS { get; private set; }

        public bool IsSpecified => this.Any(p => p.IsSpecified);

        public PlatformSet()
        {
            iOS = new Platform(PlatformName.iOS, PlatformArchitecture.None);
            MacOSX = new Platform(PlatformName.MacOSX, PlatformArchitecture.None);
            WatchOS = new Platform(PlatformName.WatchOS, PlatformArchitecture.None);
            TvOS = new Platform(PlatformName.TvOS, PlatformArchitecture.None);
        }

        /// <summary>
        /// Initialize a <see cref="MonoDevelop.MacDev.PlatformVersion"/> struct with
        /// version information encoded as a value of <see cref="ObjCRuntime.Platform"/>
        /// enum. For example (ulong)(Platform.iOS_8_0 | Platform.Mac_10_10).
        /// </summary>
        /// <param name="platformEncoding">Should have the bit format AAJJNNSSAAJJNNSS, where
        /// AA are the supported architecture flags, JJ is the maJor version, NN
        /// is the miNor version, and SS is the Subminor version. The high AAJJNNSS
        /// bytes indicate Mac version information and the low AAJJNNSS bytes
        /// indicate iOS version information. Only Major and Minor version components
        /// are parsed from the version.</param>
        public PlatformSet(ulong platformEncoding)
        {
            //This constructor is only useful in XAMCORE_2 or older, since it only supports iOS and OSX, keep it for backward compatibility
            iOS = new Platform(PlatformName.iOS, (uint)platformEncoding);
            MacOSX = new Platform(PlatformName.MacOSX, (uint)(platformEncoding >> 32));
            WatchOS = new Platform(PlatformName.WatchOS, PlatformArchitecture.None);
            TvOS = new Platform(PlatformName.TvOS, PlatformArchitecture.None);
        }

        public static PlatformSet operator |(PlatformSet a, PlatformSet b)
        {
            if (a == null && b == null)
                return null;

            var result = new PlatformSet();

            if (a == null)
            {
                result.MacOSX.value = b.MacOSX.value;
                result.iOS.value = b.iOS.value;
                result.WatchOS.value = b.WatchOS.value;
                result.TvOS.value = b.TvOS.value;
            }
            else if (b == null)
            {
                result.MacOSX.value = a.MacOSX.value;
                result.iOS.value = a.iOS.value;
                result.WatchOS.value = a.WatchOS.value;
                result.TvOS.value = a.TvOS.value;
            }
            else
            {
                result.MacOSX.value = a.MacOSX.value | b.MacOSX.value;
                result.iOS.value = a.iOS.value | b.iOS.value;
                result.WatchOS.value = a.WatchOS.value | b.WatchOS.value;
                result.TvOS.value = a.TvOS.value | b.TvOS.value;
            }

            return result;
        }

        public IEnumerator<Platform> GetEnumerator()
        {
            yield return iOS;
            yield return MacOSX;
            yield return WatchOS;
            yield return TvOS;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            foreach (var platform in this)
            {
                var platformString = platform.ToString();
                if (!String.IsNullOrEmpty(platformString))
                {
                    if (sb.Length != 0)
                        sb.Append(" | ");
                    sb.Append(platformString);
                }
            }

            return sb.ToString();
        }

        public Platform GetPlatform(PlatformName platformName)
        {
            switch (platformName)
            {
                case (PlatformName.MacOSX):
                    return this.MacOSX;
                case (PlatformName.iOS):
                    return this.iOS;
                case (PlatformName.WatchOS):
                    return this.WatchOS;
                case (PlatformName.TvOS):
                    return this.TvOS;
            }

            return null;
        }

        public bool IsSpecifiedFor(PlatformName platformName) => GetPlatform(platformName)?.IsSpecified ?? false;

        public string GetFullName(PlatformName platformName) => GetPlatform(platformName)?.FullName ?? String.Empty;
    }

    public class PlatformAvailability
    {
        public PlatformSet Introduced { get; set; }
        public PlatformSet Deprecated { get; set; }
        public PlatformSet Obsoleted { get; set; }
        public PlatformSet Unavailable { get; set; }
        public string Message { get; set; }

        public bool IsSpecified
        {
            get
            {
                return
                    (Introduced != null && Introduced.IsSpecified) ||
                    (Deprecated != null && Deprecated.IsSpecified) ||
                    (Obsoleted != null && Obsoleted.IsSpecified) ||
                    (Unavailable != null && Unavailable.IsSpecified) ||
                    Message != null;
            }
        }

        public Platform GetIntroducedPlatformFor(Platform platform) => Introduced?.GetPlatform(platform.Name);

        public Platform GetDeprecatedPlatformFor(Platform platform) => Deprecated?.GetPlatform(platform.Name);

        public Platform GetObsoletedPlatformFor(Platform platform) => Obsoleted?.GetPlatform(platform.Name);

        public Platform GetUnavailablePlatformFor(Platform platform) => Unavailable?.GetPlatform(platform.Name);

        public override string ToString()
        {
            var s = new StringBuilder();

            if (Introduced != null && Introduced.IsSpecified)
                s.AppendFormat("Introduced = {0}, ", Introduced);

            if (Deprecated != null && Deprecated.IsSpecified)
                s.AppendFormat("Deprecated = {0}, ", Deprecated);

            if (Obsoleted != null && Obsoleted.IsSpecified)
                s.AppendFormat("Obsoleted = {0}, ", Obsoleted);

            if (Unavailable != null && Unavailable.IsSpecified)
                s.AppendFormat("Unavailable = {0}, ", Unavailable);

            if (!String.IsNullOrEmpty(Message))
                s.AppendFormat("Message = \"{0}\", ", Message.Replace("\"", "\\\""));

            if (s.Length < 2)
                return "[Availability]";

            s.Length -= 2;

            return String.Format("[Availability ({0})]", s);
        }
    }
}
