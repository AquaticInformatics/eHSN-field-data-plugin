namespace EhsnPlugin.DataModel
{
    public class StageMeasurementSummary
    {
        public MeanGageHeightSelector Selector { get; set; }
        public double MeanGageHeight { get; set; }
        public double? SensorResetCorrection { get; set; }
        public double? GageCorrection { get; set; }
        public double CorrectedMeanGageHeight { get; set; }
    }
}
