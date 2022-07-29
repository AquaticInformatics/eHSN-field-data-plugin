using System;
using Version = EhsnPlugin.DataModel.Version;

namespace EhsnPlugin.Validators
{
    public class VersionValidator
    {
        private Config Config { get; }
        private Version MinVersion { get; } = DefaultVersion;
        private Version MaxVersion { get; } = DefaultVersion;

        private static readonly Version DefaultVersion = Version.Create("v2.3.2");

        public VersionValidator(Config config)
        {
            Config = config;

            var minVersion = Config.MinVersion;

            if (!string.IsNullOrWhiteSpace(minVersion))
                MinVersion = Version.Create(minVersion.Trim());

            var maxVersion = Config.MaxVersion;

            if (!string.IsNullOrWhiteSpace(maxVersion))
                MaxVersion = Version.Create(maxVersion.Trim());

            if (MaxVersion.IsLessThan(MinVersion))
                throw new Exception($"Invalid configuration. MaxVersion='{MaxVersion}' should be not be less than MinVersion='{MinVersion}'");
        }

        public void ThrowIfUnsupportedVersion(string versionText)
        {
            var version = Version.Create(versionText);

            if (version.IsLessThan(MinVersion))
                throw new Exception($"Unsupported eHSN version '{version}' is less than the minimum version of '{MinVersion}'.");

            if (MaxVersion.IsLessThan(version))
                throw new Exception($"Unsupported eHSN version '{version}' is greater than the maximum version of '{MaxVersion}'.");
        }
    }
}
