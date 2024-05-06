using System;
using System.Text.RegularExpressions;
using EhsnPlugin.DataModel;

namespace EhsnPlugin.Mappers
{
    public static class MeanGageHeightSelectorMapper
    {
        private static readonly Regex SelectedColumnRegex = new Regex(@"\(\s*(?<name>.+)\s*\)\s*$");

        public static MeanGageHeightSelector? Map(string comboBoxValue)
        {
            if (string.IsNullOrWhiteSpace(comboBoxValue)) return null;

            var match = SelectedColumnRegex.Match(comboBoxValue);

            if (!match.Success)
                throw new ArgumentException($"Can't determine selected Mean Gauge Height measurement from '{comboBoxValue}'");

            var selectorName = match.Groups["name"].Value;

            if (Enum.TryParse<MeanGageHeightSelector>(selectorName, true, out var value))
                return value;

            if ("hg".Equals(selectorName, StringComparison.InvariantCultureIgnoreCase))
                return MeanGageHeightSelector.HG1;

            throw new ArgumentException($"'{comboBoxValue}' is not a supported Mean Gauge Height selection.");
        }
    }
}
