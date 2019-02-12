using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace EhsnPlugin.DataModel
{
    public class Version
    {
        public static Version Create(string version)
        {
            return new Version(version);
        }

        private readonly string _version;
        private readonly int[] _versionComponents;
        private readonly string _suffix;

        private Version(string version)
        {
            var match = VersionRegex.Match(version);

            if (!match.Success)
                throw new ArgumentException($"'{version}' is not a valid eHSN version identifier.");

           _versionComponents = match.Groups["components"].Value
                .Split('.')
                .Select(int.Parse)
                .ToArray();

           _suffix = match.Groups["suffix"].Value;
           _version = version;
        }

        private static readonly Regex VersionRegex = new Regex(@"^v?(?<components>(\d+)(\.\d+)*)(?<suffix>.*)$");

        public override string ToString()
        {
            return _version;
        }

        public bool IsLessThan(Version other)
        {
            if (IsNumericalVersionLessThan(other))
                return true;

            if (other.IsNumericalVersionLessThan(this))
                return false;

            // When otherwise equal, compare by version suffix
            return IsSuffixLessThan(other);
        }

        private bool IsNumericalVersionLessThan(Version other)
        {
            for (var i = 0; i < _versionComponents.Length; ++i)
            {
                if (i >= other._versionComponents.Length)
                    return false;

                if (_versionComponents[i] < other._versionComponents[i])
                    return true;

                if (_versionComponents[i] > other._versionComponents[i])
                    return false;
            }

            return _versionComponents.Length < other._versionComponents.Length;
        }

        private bool IsSuffixLessThan(Version other)
        {
            // The presence of a suffix always makes a version less than one without
            if (string.IsNullOrEmpty(_suffix))
                return false;

            if (string.IsNullOrEmpty(other._suffix))
                return true;

            return string.Compare(_suffix, other._suffix, StringComparison.InvariantCultureIgnoreCase) < 0;
        }
    }
}
