using UnityEngine;

namespace HoyoToon.Runtime.Core
{
    public sealed class PropertyLabelAttribute : PropertyAttribute
    {
        public PropertyLabelAttribute(string displayName)
        {
            DisplayName = displayName;
        }

        public PropertyLabelAttribute(string displayName, string xName, string yName, string zName = null, string wName = null)
        {
            DisplayName = displayName;
            XName = xName;
            YName = yName;
            ZName = zName;
            WName = wName;
        }

        public PropertyLabelAttribute(
            string displayName,
            string xName,
            string yName,
            string zName,
            string wName,
            bool useRange,
            float rangeMin,
            float rangeMax)
        {
            DisplayName = displayName;
            XName = xName;
            YName = yName;
            ZName = zName;
            WName = wName;
            UseRange = useRange;
            RangeMin = rangeMin;
            RangeMax = rangeMax;
        }

        public PropertyLabelAttribute(
            string displayName,
            string xName,
            string yName,
            float rangeMin,
            float rangeMax,
            string zName = null,
            string wName = null)
        {
            DisplayName = displayName;
            XName = xName;
            YName = yName;
            ZName = zName;
            WName = wName;
            UseRange = true;
            RangeMin = rangeMin;
            RangeMax = rangeMax;
        }

        public string DisplayName { get; }
        public string XName { get; }
        public string YName { get; }
        public string ZName { get; }
        public string WName { get; }
        public bool UseRange { get; }
        public float RangeMin { get; }
        public float RangeMax { get; }

        public bool HasCustomAxisLabels =>
            !string.IsNullOrWhiteSpace(XName) ||
            !string.IsNullOrWhiteSpace(YName) ||
            !string.IsNullOrWhiteSpace(ZName) ||
            !string.IsNullOrWhiteSpace(WName);
    }
}
