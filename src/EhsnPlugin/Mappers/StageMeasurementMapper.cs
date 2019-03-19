using System;
using System.Collections.Generic;
using EhsnPlugin.DataModel;
using EhsnPlugin.Helpers;

namespace EhsnPlugin.Mappers
{
    public class StageMeasurementMapper
    {
        private readonly EHSN _ehsn;

        public StageMeasurementMapper(EHSN ehsn)
        {
            _ehsn = ehsn;
        }

        public StageMeasurementSummary Map()
        {
            var meanGageHeightSelector = MeanGageHeightSelectorMapper.Map(_ehsn.DisMeas.mghCmbo);

            if (!meanGageHeightSelector.HasValue) return null;

            var selector = meanGageHeightSelector.Value;

            var measurements = new Dictionary<MeanGageHeightSelector, (string MeanGageHeight, string SensorResetCorrection, string GageCorrection, string CorrectedMeanGageHeight)>
            {
                {MeanGageHeightSelector.HG1,  (_ehsn.StageMeas.MGHHG1, _ehsn.StageMeas.SRCHG1, _ehsn.StageMeas.GCHG1, _ehsn.StageMeas.CMGHHG1)},
                {MeanGageHeightSelector.HG2,  (_ehsn.StageMeas.MGHHG2, _ehsn.StageMeas.SRCHG2, _ehsn.StageMeas.GCHG2, _ehsn.StageMeas.CMGHHG2)},
                {MeanGageHeightSelector.WLR1, (_ehsn.StageMeas.MGHWL1, null, _ehsn.StageMeas.GCWL1, _ehsn.StageMeas.CMGHWL1)},
                {MeanGageHeightSelector.WLR2, (_ehsn.StageMeas.MGHWL2, null, _ehsn.StageMeas.GCWL2, _ehsn.StageMeas.CMGHWL2)},
            };

            var column = measurements[selector];

            var meanGageHeight = column.MeanGageHeight.ToNullableDouble();
            var sensorResetCorrection = column.SensorResetCorrection.ToNullableDouble();
            var gageCorrection = column.GageCorrection.ToNullableDouble();
            var correctedMeanGageHeight = column.CorrectedMeanGageHeight.ToNullableDouble();

            if (!meanGageHeight.HasValue)
                throw new ArgumentException($"The weighted mean gauge height value for {selector} is missing");

            if (!correctedMeanGageHeight.HasValue)
                throw new ArgumentException($"The corrected mean gauge height value for {selector} is missing");

            return new StageMeasurementSummary
            {
                Selector = selector,
                MeanGageHeight = meanGageHeight.Value,
                SensorResetCorrection = sensorResetCorrection,
                GageCorrection = gageCorrection,
                CorrectedMeanGageHeight = correctedMeanGageHeight.Value
            };
        }

    }
}
