using System;
using System.Collections.Generic;
using System.Linq;
using EhsnPlugin.DataModel;
using EhsnPlugin.Helpers;
using EhsnPlugin.SystemCode;
using FieldDataPluginFramework.Context;
using FieldDataPluginFramework.DataModel;
using FieldDataPluginFramework.DataModel.ChannelMeasurements;
using FieldDataPluginFramework.DataModel.DischargeActivities;
using FieldDataPluginFramework.DataModel.Meters;
using FieldDataPluginFramework.DataModel.PickLists;
using FieldDataPluginFramework.DataModel.Verticals;

namespace EhsnPlugin.Mappers
{
    public class DischargeActivityMapper
    {
        private Config Config { get; }
        private LocationInfo LocationInfo { get; }
        private DateTime VisitDate { get; }
        private readonly EHSN _ehsn;

        public DischargeActivityMapper(Config config, LocationInfo locationInfo, DateTime visitDate, EHSN eHsn)
        {
            Config = config;
            LocationInfo = locationInfo;
            VisitDate = visitDate;
            _ehsn = eHsn;
        }

        public DischargeActivity Map()
        {
            if (_ehsn.DisMeas == null) return null;

            var dischargeMeasurementType = GetDischargeMeasurementType();

            if (dischargeMeasurementType == InstrumentDeploymentType.None) return null;

            var discharge = _ehsn.DisMeas.discharge.ToNullableDouble();

            if (!discharge.HasValue)
                throw new ArgumentException($"No discharge value found for {dischargeMeasurementType} measurement");

            var dischargeActivity = CreateDischargeActivityWithSummary(discharge.Value);

            SetDischargeSection(dischargeActivity, discharge.Value, dischargeMeasurementType);

            return dischargeActivity;
        }

        private enum InstrumentDeploymentType
        {
            None,
            MidSection,
            MovingBoat,
        }

        private InstrumentDeploymentType GetDischargeMeasurementType()
        {
            var methodType = _ehsn.InstrumentDeployment?.GeneralInfo?.methodType ?? string.Empty;

            if (!DeploymentTypes.TryGetValue(methodType, out var deploymentType))
                throw new ArgumentException($"'{methodType}' is not a supported InstrumentDeployment/GeneralInfo/methodType value");

            return deploymentType;
        }

        private static readonly Dictionary<string, InstrumentDeploymentType> DeploymentTypes =
            new Dictionary<string, InstrumentDeploymentType>(StringComparer.InvariantCultureIgnoreCase)
            {
                {"None", InstrumentDeploymentType.None},
                {"Mid-section", InstrumentDeploymentType.MidSection},
                {"ADCP by Moving Boat", InstrumentDeploymentType.MovingBoat},
            };

        private DischargeActivity CreateDischargeActivityWithSummary(double discharge)
        {
            var factory = new DischargeActivityFactory(Units.MetricUnitSystem);

            var measurementPeriod = GetMeasurementPeriod();
            var dischargeActivity = factory.CreateDischargeActivity(measurementPeriod, discharge);

            dischargeActivity.Comments = _ehsn.DisMeas.dischargeRemark;
            dischargeActivity.Party = _ehsn.PartyInfo?.party;

            AddMeanGageHeight(dischargeActivity);
            
            return dischargeActivity;
        }

        private DateTimeInterval GetMeasurementPeriod()
        {
            return new DateTimeInterval(
                TimeHelper.ParseTimeOrMinValue(_ehsn.DisMeas.startTime, VisitDate, LocationInfo.UtcOffset),
                TimeHelper.ParseTimeOrMinValue(_ehsn.DisMeas.endTime, VisitDate, LocationInfo.UtcOffset));
        }

        private void AddMeanGageHeight(DischargeActivity dischargeActivity)
        {
            if (_ehsn.StageMeas == null) return;

            var meanGageHeightSelector = MeanGageHeightSelectorMapper.Map(_ehsn.DisMeas.mghCmbo);

            if (!meanGageHeightSelector.HasValue) return;

            var publishedMeanGageHeight = _ehsn.DisMeas.mgh.ToNullableDouble();

            if (!publishedMeanGageHeight.HasValue)
                throw new ArgumentException($"'{_ehsn.DisMeas.mgh}' is not a valid mean gage height value");

            var stageMeasurementSummary = GetStageMeasurementSummary(meanGageHeightSelector.Value);

            var isAverage = "Average".Equals(_ehsn.StageMeas.MghMethod, StringComparison.InvariantCultureIgnoreCase);

            var gageHeightMeasurements = GetGageHeightMeasurements(meanGageHeightSelector.Value)
                .ToList();

            if (isAverage)
            {
                var meanGageHeight = gageHeightMeasurements.Average(ghm => ghm.GageHeight.Value);

                isAverage = publishedMeanGageHeight.Value.ToString("F3").Equals(meanGageHeight.ToString("F3"));
            }

            if (isAverage)
            {
                foreach (var gageHeightMeasurement in gageHeightMeasurements)
                {
                    dischargeActivity.GageHeightMeasurements.Add(gageHeightMeasurement);
                }
            }
            else
            {
                dischargeActivity.ManuallyCalculatedMeanGageHeight = new Measurement(publishedMeanGageHeight.Value, Units.DistanceUnitId);
            }
        }

        private class StageMeasurementSummary
        {
            public double MeanGageHeight { get; set; }
            public double? SensorResetCorrection { get; set; }
            public double? GageCorrection { get; set; }
            public double CorrectedMeanGageHeight { get; set; }
        }

        private StageMeasurementSummary GetStageMeasurementSummary(MeanGageHeightSelector selector)
        {
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
                MeanGageHeight = meanGageHeight.Value,
                SensorResetCorrection = sensorResetCorrection,
                GageCorrection = gageCorrection,
                CorrectedMeanGageHeight = correctedMeanGageHeight.Value
            };
        }

        private IEnumerable<GageHeightMeasurement> GetGageHeightMeasurements(MeanGageHeightSelector selector)
        {
            var stageMeasRows = _ehsn.StageMeas.StageMeasTable ?? new EHSNStageMeasStageMeasRow[0];

            foreach (var row in stageMeasRows)
            {
                var include = bool.TryParse(row.MghCkbox, out var isEnabled) && isEnabled;

                var time = TimeHelper.ParseTimeOrMinValue(row.time, VisitDate, LocationInfo.UtcOffset);

                if (time == DateTimeOffset.MinValue)
                    throw new ArgumentException($"Invalid time '{row.time}' in {selector} StageMeasRow={row.row}");

                var values = new Dictionary<MeanGageHeightSelector, double?>
                {
                    {MeanGageHeightSelector.HG1, row.HG1.ToNullableDouble()},
                    {MeanGageHeightSelector.HG2, row.HG2.ToNullableDouble()},
                    {MeanGageHeightSelector.WLR1, row.WL1.ToNullableDouble()},
                    {MeanGageHeightSelector.WLR2, row.WL1.ToNullableDouble()},
                };

                var value = values[selector];

                if (!value.HasValue) continue;

                yield return new GageHeightMeasurement(new Measurement(value.Value, Units.DistanceUnitId), time, include);
            }
        }

        private void SetDischargeSection(DischargeActivity dischargeActivity, double discharge, InstrumentDeploymentType dischargeMeasurementType)
        {
            var dischargeSection = CreateDischargeSectionWithDescription(dischargeActivity, discharge, dischargeMeasurementType);

            dischargeSection.Party = dischargeActivity.Party;

            dischargeActivity.ChannelMeasurements.Add(dischargeSection);
        }

        private ChannelMeasurementBase CreateDischargeSectionWithDescription(DischargeActivity dischargeActivity, double discharge, InstrumentDeploymentType dischargeMeasurementType)
        {
            switch (dischargeMeasurementType)
            {
                case InstrumentDeploymentType.MidSection:
                    return CreateMidSectionMeasurement(dischargeActivity, discharge);

                case InstrumentDeploymentType.MovingBoat:
                    return CreateAdcpMeasurement(dischargeActivity, discharge);
            }

            throw new ArgumentException($"Can't create discharge section for measurement type = '{dischargeMeasurementType}'");
        }

        private ManualGaugingDischargeSection CreateMidSectionMeasurement(DischargeActivity dischargeActivity, double discharge)
        {
            var factory = new ManualGaugingDischargeSectionFactory(Units.MetricUnitSystem)
            {
                DefaultChannelName = Config.DefaultChannelName
            };
            var dischargeSection = factory.CreateManualGaugingDischargeSection(dischargeActivity.MeasurementPeriod, discharge);

            dischargeSection.DischargeMethod = DischargeMethodType.MidSection;

            dischargeSection.AreaUnitId = Units.AreaUnitId;
            dischargeSection.AreaValue = _ehsn.DisMeas.area.ToNullableDouble();
            dischargeSection.WidthValue = _ehsn.DisMeas.width.ToNullableDouble();
            dischargeSection.VelocityAverageValue = _ehsn.DisMeas.meanVel.ToNullableDouble();
            dischargeSection.VelocityUnitId = Units.VelocityUnitId;
            dischargeSection.DeploymentMethod = GetMappedEnum(_ehsn.InstrumentDeployment?.GeneralInfo?.deployment, KnownMidSectionDeploymentTypes);

            AddPanelMeasurements(dischargeSection);

            return dischargeSection;
        }

        private void AddPanelMeasurements(ManualGaugingDischargeSection dischargeSection)
        {
            var channels = _ehsn.MidsecMeas?.DischargeMeasurement?.Channels ?? new EHSNMidsecMeasDischargeMeasurementChannel[0];

            if (!channels.Any()) return;

            if (channels.Length != 1)
                throw new ArgumentException($"Only single-channel measurements are supported, but {channels.Length} were found.");

            var channel = channels.First();
            var edges = channel.Edges ?? new EHSNMidsecMeasDischargeMeasurementChannelEdge[0];
            var panels = channel.Panels ?? new EHSNMidsecMeasDischargeMeasurementChannelPanel[0];

            var startingEdge = edges
                .FirstOrDefault(e => bool.TryParse(e.StartingEdge, out var value) && value);

            if (startingEdge == null)
                throw new ArgumentException($"No starting edge found.");

            if (edges.Length != 2)
                throw new ArgumentException($"Only 2 edges expected but {edges.Length} were found.");

            var endingEdge = edges
                .First(e => !e.StartingEdge.Equals(startingEdge.StartingEdge));

            dischargeSection.StartPoint = GetMappedEnum(startingEdge.LeftOrRight, KnownStartPointTypes);

            var meters = (_ehsn.MidsecMeas?.DischargeMeasurement?.MmtInitAndSummary?.MetersUsed ?? new EHSNMidsecMeasDischargeMeasurementMmtInitAndSummaryMeter[0])
                .Select(CreateMeterCalibration)
                .ToList();

            var edgeMeter = FindMeter(meters);

            dischargeSection.Verticals.Add(CreateEdge(startingEdge, VerticalType.StartEdgeNoWaterBefore, edgeMeter));

            foreach (var panel in panels)
            {
                dischargeSection.Verticals.Add(CreatePanel(panel, FindMeter(meters, panel.MeterNumber)));
            }

            dischargeSection.Verticals.Add(CreateEdge(endingEdge, VerticalType.EndEdgeNoWaterAfter, edgeMeter));

            dischargeSection.VelocityObservationMethod = FindMostCommonVelocityMethod(dischargeSection.Verticals);
        }

        private MeterCalibration FindMeter(List<MeterCalibration> meters, string serialNumber = null)
        {
            var meter = meters.FirstOrDefault(m => m.SerialNumber == serialNumber);

            if (meter != null)
                return meter;

            return new MeterCalibration
            {
                MeterType = MeterType.Unspecified,
                Equations = {new MeterCalibrationEquation {InterceptUnitId = Units.VelocityUnitId}}
            };
        }

        private MeterCalibration CreateMeterCalibration(EHSNMidsecMeasDischargeMeasurementMmtInitAndSummaryMeter meter)
        {
            var meterCalibration = new MeterCalibration
            {
                FirmwareVersion = _ehsn.InstrumentDeployment?.GeneralInfo?.firmware,
                Manufacturer = _ehsn.InstrumentDeployment?.GeneralInfo?.manufacturer,
                Model = _ehsn.InstrumentDeployment?.GeneralInfo?.model,
                SerialNumber = meter.Number,
                Configuration = meter.MeterCalibDate,
                MeterType = GetMappedEnum(_ehsn.InstrumentDeployment?.GeneralInfo?.model, KnownMeterTypes),
            };

            foreach (var equation in meter.Equation ?? new EHSNMidsecMeasDischargeMeasurementMmtInitAndSummaryMeterEquation[0])
            {
                var slope = equation.Slope.ToNullableDouble();
                var intercept = equation.Intercept.ToNullableDouble();

                if (!slope.HasValue || !intercept.HasValue) continue;

                meterCalibration.Equations.Add(new MeterCalibrationEquation
                {
                    Slope = slope.Value,
                    Intercept = intercept.Value,
                    InterceptUnitId = Units.VelocityUnitId
                });
            }

            return meterCalibration;
        }

        private Vertical CreateEdge(EHSNMidsecMeasDischargeMeasurementChannelEdge edge, VerticalType verticalType, MeterCalibration edgeMeter)
        {
            var taglinePosition = edge.Tagmark.ToNullableDouble() ?? 0;
            var depth = edge.Depth.ToNullableDouble() ?? 0;
            var area = edge.Area.ToNullableDouble() ?? 0;
            var velocity = edge.Velocity.ToNullableDouble() ?? 0;
            var discharge = edge.Discharge.ToNullableDouble() ?? 0;
            var width = edge.Width.ToNullableDouble() ?? 0;
            var percentFlow = edge.Flow.ToNullableDouble() ?? 0;

            var velocityObservation = new VelocityObservation
            {
                VelocityObservationMethod = PointVelocityObservationType.Surface,
                MeanVelocity = velocity,
                DeploymentMethod = DeploymentMethodType.Unspecified,
                MeterCalibration = edgeMeter,
                Observations = {
                    new VelocityDepthObservation
                    {
                        Depth = depth,
                        Velocity = velocity,
                        ObservationInterval = 0,
                        RevolutionCount = 0
                    },
                },
            };

            return new Vertical
            {
                VerticalType = verticalType,
                MeasurementTime = new DateTimeOffset(edge.Date, LocationInfo.UtcOffset),
                SequenceNumber = edge.panelId,
                TaglinePosition = taglinePosition,
                SoundedDepth = depth,
                EffectiveDepth = depth,
                MeasurementConditionData = new OpenWaterData(),
                FlowDirection = FlowDirectionType.Normal,
                VelocityObservation = velocityObservation,
                Segment = new Segment
                {
                    Area = area,
                    Discharge = discharge,
                    Width = width,
                    Velocity = velocity,
                    TotalDischargePortion = percentFlow,
                }
            };
        }

        private Vertical CreatePanel(EHSNMidsecMeasDischargeMeasurementChannelPanel panel, MeterCalibration meter)
        {
            var taglinePosition = panel.Tagmark.ToNullableDouble() ?? 0;
            var soundedDepth = panel.DepthReading.ToNullableDouble() ?? 0;
            var effectiveDepth = panel.DepthWithOffset.ToNullableDouble() ?? soundedDepth;
            var velocity = panel.AverageVelocity.ToNullableDouble() ?? 0;
            var discharge = panel.Discharge.ToNullableDouble() ?? 0;
            var width = panel.Width.ToNullableDouble() ?? 0;
            var percentFlow = panel.Flow.ToNullableDouble() ?? 0;

            var measurementCondition = panel.IceCovered != null
                ? (MeasurementConditionData) new IceCoveredData
                {
                    IceAssemblyType = panel.IceCovered.IceAssembly,
                    IceThickness = panel.IceCovered.IceThickness.ToNullableDouble(),
                    AboveFooting = panel.IceCovered.MeterAboveFooting.ToNullableDouble(),
                    BelowFooting = panel.IceCovered.MeterBelowFooting.ToNullableDouble(),
                    WaterSurfaceToBottomOfIce = panel.IceCovered.WSToBottomOfIceAdjusted.ToNullableDouble() ?? 0,
                    WaterSurfaceToBottomOfSlush = panel.IceCovered.WaterSurfaceToBottomOfSlush.ToNullableDouble() ?? 0,
                }
                : new OpenWaterData
                {
                    DistanceToMeter = panel.Open?.DistanceAboveWeight.ToNullableDouble(),
                    DryLineAngle = panel.DryAngle.ToNullableDouble() ?? 0,
                    DryLineCorrection = panel.DryCorrection.ToNullableDouble(),
                    WetLineCorrection = panel.WetCorrection.ToNullableDouble(),
                    SuspensionWeight = panel.Open?.AmountOfWeight
                };

            effectiveDepth = panel.IceCovered?.EffectiveDepth.ToNullableDouble() ?? effectiveDepth;

            var points = panel.PointMeasurements ?? new EHSNMidsecMeasDischargeMeasurementChannelPanelPointMeasurement[0];

            var fractionalDepths = string.Join("/", points.Select(p=>p.SamplingDepthCoefficient));

            if (!PointVelocityTypes.TryGetValue(fractionalDepths, out var pointVelocityObservationType))
            {
                if (!points.Any())
                {
                    pointVelocityObservationType = PointVelocityObservationType.Surface;
                }
                else
                {
                    throw new ArgumentException($"'{fractionalDepths}' is not a supported point velocity observation type");
                }
            }

            var velocityObservation = new VelocityObservation
            {
                VelocityObservationMethod = pointVelocityObservationType,
                MeanVelocity = velocity,
                DeploymentMethod = DeploymentMethodType.Unspecified,
                MeterCalibration = meter
            };

            if (!points.Any())
            {
                velocityObservation.Observations.Add(new VelocityDepthObservation
                {
                    Depth = soundedDepth,
                    Velocity = velocity,
                    ObservationInterval = 0,
                    RevolutionCount = 0
                });
            }
            else
            {
                foreach (var point in points)
                {
                    velocityObservation.Observations.Add(new VelocityDepthObservation
                    {
                        Depth = point.MeasurementDepth.ToNullableDouble() ?? 0,
                        Velocity = point.Velocity.ToNullableDouble() ?? 0,
                        ObservationInterval = point.ElapsedTime.ToNullableDouble(),
                        RevolutionCount = point.Revolutions
                    });
                }
            }

            var vertical = new Vertical
            {
                VerticalType = VerticalType.MidRiver,
                MeasurementTime = new DateTimeOffset(panel.Date, LocationInfo.UtcOffset),
                SequenceNumber = panel.panelId,
                TaglinePosition = taglinePosition,
                SoundedDepth = soundedDepth,
                EffectiveDepth = effectiveDepth,
                MeasurementConditionData = measurementCondition,
                FlowDirection = bool.TryParse(panel.ReverseFlow, out var value) && value
                    ? FlowDirectionType.Reversed
                    : FlowDirectionType.Normal,
                VelocityObservation = velocityObservation,
                Segment = new Segment
                {
                    Area = soundedDepth * width, // We need to infer the area
                    Discharge = discharge,
                    Width = width,
                    Velocity = velocity,
                    TotalDischargePortion = percentFlow,
                }
            };

            return vertical;
        }

        private static readonly Dictionary<string, PointVelocityObservationType> PointVelocityTypes = new Dictionary<string, PointVelocityObservationType>
        {
            {"0.5", PointVelocityObservationType.OneAtPointFive },
            {"0.6", PointVelocityObservationType.OneAtPointSix },
            {"0.2/0.8", PointVelocityObservationType.OneAtPointTwoAndPointEight },
            {"0.2/0.6/0.8", PointVelocityObservationType.OneAtPointTwoPointSixAndPointEight },
        };

        private PointVelocityObservationType FindMostCommonVelocityMethod(IEnumerable<Vertical> verticals)
        {
            var velocityMethodCounts = new Dictionary<PointVelocityObservationType, int>();

            var pointTypes = verticals
                .Select(v => v.VelocityObservation.VelocityObservationMethod ?? PointVelocityObservationType.Unknown);

            foreach (var pointType in pointTypes)
            {
                if (velocityMethodCounts.ContainsKey(pointType))
                {
                    velocityMethodCounts[pointType] += 1;
                }
                else
                {
                    velocityMethodCounts[pointType] = 1;
                }
            }

            return velocityMethodCounts
                .First(kvp => kvp.Value == velocityMethodCounts.Max(kvp2 => kvp2.Value))
                .Key;
        }

        private AdcpDischargeSection CreateAdcpMeasurement(DischargeActivity dischargeActivity, double discharge)
        {
            return new AdcpDischargeSection(
                dischargeActivity.MeasurementPeriod,
                Config.DefaultChannelName,
                new Measurement(discharge, Units.DistanceUnitId),
                _ehsn.InstrumentDeployment?.GeneralInfo?.instrument ?? "ADCP",
                Units.DistanceUnitId,
                Units.AreaUnitId,
                Units.VelocityUnitId)
            {
                AreaValue = _ehsn.DisMeas.area.ToNullableDouble(),
                WidthValue = _ehsn.DisMeas.width.ToNullableDouble(),
                VelocityAverageValue = _ehsn.DisMeas.meanVel.ToNullableDouble(),
                FirmwareVersion = _ehsn.InstrumentDeployment?.GeneralInfo?.firmware,
                SoftwareVersion = _ehsn.InstrumentDeployment?.GeneralInfo?.software,
                MeasurementDevice = new MeasurementDevice(
                    _ehsn.InstrumentDeployment?.GeneralInfo?.manufacturer,
                    _ehsn.InstrumentDeployment?.GeneralInfo?.model,
                    _ehsn.InstrumentDeployment?.GeneralInfo?.serialNum),
                MagneticVariation = _ehsn.InstrumentDeployment?.ADCPInfo?.magDecl.ToNullableDouble(),
                TransducerDepth = _ehsn.InstrumentDeployment?.ADCPInfo?.depth.ToNullableDouble(),
                DeploymentMethod = GetMappedEnum(_ehsn.InstrumentDeployment?.GeneralInfo?.deployment, KnownAdcpDeploymentTypes),
                DepthReference = GetMappedEnum(_ehsn.MovingBoatMeas?.depthRefCmbo, KnownDepthReferenceTypes),
                Comments = _ehsn.MovingBoatMeas?.ADCPMeasResults?.comments,
                BottomEstimateExponent = _ehsn.MovingBoatMeas?.velocityExponentCtrl.ToNullableDouble(),
                TopEstimateMethod = GetPicklistItem(_ehsn.MovingBoatMeas?.velocityTopCombo, Config.KnownTopEstimateMethods, s => new TopEstimateMethodPickList(s)),
                BottomEstimateMethod = GetPicklistItem(_ehsn.MovingBoatMeas?.velocityTopCombo, Config.KnownBottomEstimateMethods, s => new BottomEstimateMethodPickList(s)),
                NumberOfTransects = (_ehsn.MovingBoatMeas?.ADCPMeasTable ?? new EHSNMovingBoatMeasADCPMeasRow[0])
                    .Count(row => bool.TryParse(row.checkbox, out var enabled) && enabled),
            };
        }

        private static TEnum GetMappedEnum<TEnum>(string text, Dictionary<string, TEnum> knownValues) where TEnum : struct
        {
            if (string.IsNullOrWhiteSpace(text)) return default;

            return knownValues.TryGetValue(text.Trim(), out var value)
                ? value
                : default;
        }

        private TPicklist GetPicklistItem<TPicklist>(string text, Dictionary<string, string> knownValues, Func<string,TPicklist> creatorFunc) where TPicklist : class
        {
            if (string.IsNullOrWhiteSpace(text)) return null;

            text = text.Trim();

            return knownValues.TryGetValue(text, out var value)
                ? creatorFunc(value)
                : null;
        }

        private static readonly Dictionary<string, DeploymentMethodType> KnownMidSectionDeploymentTypes =
            new Dictionary<string, DeploymentMethodType>(StringComparer.InvariantCultureIgnoreCase)
            {
                {"Wading", DeploymentMethodType.Wading},
                {"Bridge Upstream", DeploymentMethodType.BridgeUpstreamSide},
                {"Bridge Downstream", DeploymentMethodType.BridgeDownstreamSide},
                {"Tethered Bridge Upstream", DeploymentMethodType.BridgeUpstreamSide},
                {"Tethered Bridge Downstream", DeploymentMethodType.BridgeDownstreamSide},
                {"Tethered Cableway", DeploymentMethodType.Cableway},
                {"Cableway", DeploymentMethodType.Cableway},
                {"Manned Boat", DeploymentMethodType.MannedMovingBoat},
                {"Ice Cover", DeploymentMethodType.Ice},
            };

        private static readonly Dictionary<string, StartPointType> KnownStartPointTypes =
            new Dictionary<string, StartPointType>(StringComparer.InvariantCultureIgnoreCase)
            {
                {"Left Bank", StartPointType.LeftEdgeOfWater},
                {"Right Bank", StartPointType.RightEdgeOfWater},
            };

        private static readonly Dictionary<string, MeterType> KnownMeterTypes =
            new Dictionary<string, MeterType>(StringComparer.InvariantCultureIgnoreCase)
            {
                {"Price AA", MeterType.PriceAa},
                {"Pygmy", MeterType.Pygmy},
                {"FlowTracker", MeterType.Adv},
            };

        private static readonly Dictionary<string, AdcpDeploymentMethodType> KnownAdcpDeploymentTypes =
            new Dictionary<string, AdcpDeploymentMethodType>(StringComparer.InvariantCultureIgnoreCase)
            {
                {"Tethered Cableway", AdcpDeploymentMethodType.Cableway},
                {"Tethered Bridge Upstream", AdcpDeploymentMethodType.BridgeUpstreamSide},
                {"Tethered Bridge Downstream", AdcpDeploymentMethodType.BridgeDownstreamSide},
                {"Manned Boat", AdcpDeploymentMethodType.MannedMovingBoat},
                {"Remote Control", AdcpDeploymentMethodType.RemoteControlledBoat},
            };

        private static readonly Dictionary<string, DepthReferenceType> KnownDepthReferenceTypes =
            new Dictionary<string, DepthReferenceType>(StringComparer.InvariantCultureIgnoreCase)
            {
                {"Bottom Track", DepthReferenceType.BottomTrack},
                {"Vertical Beam", DepthReferenceType.VerticalBeam},
                {"Composite (BT)", DepthReferenceType.Composite},
                {"Composite (VB)", DepthReferenceType.Composite},
                {"Depth Sounder", DepthReferenceType.DepthSounder},
            };
    }
}
