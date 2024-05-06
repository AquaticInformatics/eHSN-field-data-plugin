using FieldDataPluginFramework.Units;

namespace EhsnPlugin.SystemCode
{
    public static class Units
    {
        public static string DistanceUnitId => "m";
        public static string AreaUnitId => "m^2";
        public static string VelocityUnitId => "m/s";
        public static string DischargeUnitId => "m^3/s";
        public static string TemperatureUnitId => "degC";

        public static UnitSystem MetricUnitSystem => new UnitSystem
        {
            DistanceUnitId = DistanceUnitId,
            AreaUnitId = AreaUnitId,
            VelocityUnitId = VelocityUnitId,
            DischargeUnitId = DischargeUnitId,
        };
    }
}
