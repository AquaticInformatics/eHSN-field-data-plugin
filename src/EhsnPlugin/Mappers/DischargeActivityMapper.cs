using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using Channel = EHSNMidsecMeasDischargeMeasurementChannel;
using Meter = EHSNMidsecMeasDischargeMeasurementMmtInitAndSummaryMeter;
using MeterEquation = EHSNMidsecMeasDischargeMeasurementMmtInitAndSummaryMeterEquation;
using Edge = EHSNMidsecMeasDischargeMeasurementChannelEdge;
using Panel = EHSNMidsecMeasDischargeMeasurementChannelPanel;
using PointMeasurement = EHSNMidsecMeasDischargeMeasurementChannelPanelPointMeasurement;

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

            var stageMeasurementSummary = new StageMeasurementMapper(_ehsn)
                .Map();

            if (stageMeasurementSummary == null) return;

            var sensorResetCorrectionComment = stageMeasurementSummary.SensorResetCorrection.HasValue
                ? $"Sensor Reset Correction of {stageMeasurementSummary.SensorResetCorrection:F3}"
                : string.Empty;
            var gageCorrectionComment = stageMeasurementSummary.GageCorrection.HasValue
                ? $"Gage Correction of {stageMeasurementSummary.GageCorrection:F3}"
                : string.Empty;

            var meanGaugeHeightComment = string.Empty;

            if (!DoubleHelper.AreEqual(stageMeasurementSummary.MeanGageHeight, stageMeasurementSummary.CorrectedMeanGageHeight))
            {
                meanGaugeHeightComment = $"Corrected M.G.H. includes {string.Join(" and ", new[]{sensorResetCorrectionComment, gageCorrectionComment}.Where(s => !string.IsNullOrWhiteSpace(s)))} applied to Weighted M.G.H of {stageMeasurementSummary.MeanGageHeight:F3}";

                if (!string.IsNullOrWhiteSpace(dischargeActivity.Comments))
                {
                    meanGaugeHeightComment = "\n" + meanGaugeHeightComment;
                }
            }

            var gageHeightMeasurements = GetGageHeightMeasurements(stageMeasurementSummary.Selector)
                .ToList();

            var isAverage = gageHeightMeasurements.Any()
                            && "Average".Equals(_ehsn.StageMeas.MghMethod, StringComparison.InvariantCultureIgnoreCase);

            if (isAverage)
            {
                var meanGageHeight = gageHeightMeasurements.Average(ghm => ghm.GageHeight.Value);

                isAverage = stageMeasurementSummary.CorrectedMeanGageHeight.ToString("F3").Equals(meanGageHeight.ToString("F3"));
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
                dischargeActivity.ManuallyCalculatedMeanGageHeight = new Measurement(stageMeasurementSummary.CorrectedMeanGageHeight, Units.DistanceUnitId);
            }

            dischargeActivity.Comments = string.Join("\n",
                new[] {dischargeActivity.Comments, meanGaugeHeightComment, _ehsn.StageMeas?.stageRemark}
                    .Where(s => !string.IsNullOrWhiteSpace(s)));
        }

        private IEnumerable<GageHeightMeasurement> GetGageHeightMeasurements(MeanGageHeightSelector selector)
        {
            var stageMeasRows = _ehsn.StageMeas.StageMeasTable ?? new EHSNStageMeasStageMeasRow[0];

            foreach (var row in stageMeasRows)
            {
                var include = row.MghCkbox.ToBoolean();

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
            dischargeSection.Comments = _ehsn.DisMeas.dischargeRemark;

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
            var channels = _ehsn.MidsecMeas?.DischargeMeasurement?.Channels ?? new Channel[0];

            if (!channels.Any()) return;

            var (edges, panels) = MergeAllChannels(channels.ToList());

            var startingEdge = edges.First();
            var endingEdge = edges.Last();

            dischargeSection.StartPoint = GetMappedEnum(startingEdge.LeftOrRight, KnownStartPointTypes);

            var meters = (_ehsn.MidsecMeas?.DischargeMeasurement?.MmtInitAndSummary?.MetersUsed ?? new Meter[0])
                .Select(CreateMeterCalibration)
                .ToList();

            var edgeMeter = FindMeter(meters);

            AddVertical(dischargeSection.Verticals, CreateEdgeVertical(startingEdge, VerticalType.StartEdgeNoWaterBefore, edgeMeter));

            foreach (var panel in panels)
            {
                AddVertical(dischargeSection.Verticals, CreatePanelVertical(panel, FindMeter(meters, panel.MeterNumber)));
            }

            AddVertical(dischargeSection.Verticals, CreateEdgeVertical(endingEdge, VerticalType.EndEdgeNoWaterAfter, edgeMeter));

            dischargeSection.VelocityObservationMethod = FindMostCommonVelocityMethod(dischargeSection.Verticals);
        }

        private (List<Edge> Edges, List<Panel> Panels) MergeAllChannels(List<Channel> channels)
        {
            var outerEdges = new List<Edge>();
            var innerPanels = new List<Panel>();

            foreach (var channel in channels)
            {
                var isFirstChannel = channel == channels.First();
                var isLastChannel = channel == channels.Last();

                var edges = channel.Edges ?? new Edge[0];
                var panels = channel.Panels ?? new Panel[0];

                if (edges.Length != 2)
                    throw new ArgumentException($"Only 2 edges expected but {edges.Length} were found.");

                var bankEdges = edges
                    .Where(IsBankEdge)
                    .ToList();

                var islandEdges = edges
                    .Where(edge => !bankEdges.Contains(edge))
                    .ToList();

                if (isFirstChannel)
                {
                    if (!bankEdges.Any())
                    {
                        bankEdges.Add(islandEdges.First());
                        islandEdges.RemoveAt(0);
                    }

                    outerEdges.Add(bankEdges.First());
                }
                else
                {
                    innerPanels.Add(CreateIslandPanel(islandEdges.First()));
                }

                innerPanels.AddRange(panels);

                if (isLastChannel)
                {
                    if (bankEdges.Count < 2)
                    {
                        bankEdges.Add(islandEdges.Last());
                        islandEdges.RemoveAt(islandEdges.Count - 1);
                    }

                    outerEdges.Add(bankEdges.Last());
                }
                else
                {
                    innerPanels.Add(CreateIslandPanel(islandEdges.Last()));
                }
            }

            return (outerEdges, innerPanels);
        }

        private static bool IsBankEdge(Edge edge)
        {
            return "Edge @ Bank".Equals(edge.EdgeType, StringComparison.InvariantCultureIgnoreCase);
        }

        private static Panel CreateIslandPanel(Edge edge)
        {
            return new Panel
            {
                Date = edge.Date,
                ReverseFlow = "False",
                Tagmark = edge.Tagmark,
                Discharge = edge.Discharge,
                Flow = edge.Flow,
                Width = edge.Width,
                PanelNum = edge.panelId,
                AverageVelocity = edge.Velocity,
                DepthReading = edge.Depth,
                DepthWithOffset = edge.Depth,
                Open = new EHSNMidsecMeasDischargeMeasurementChannelPanelOpen
                {
                    DeploymentMethod = "",
                    TotalDepth = edge.Depth
                },
                PointMeasurements = new PointMeasurement[0]
            };
        }

        private MeterCalibration FindMeter(List<MeterCalibration> meters, string serialNumber = null)
        {
            var meter = meters.FirstOrDefault(m => m.SerialNumber == serialNumber);

            if (meter != null)
                return meter;

            return new MeterCalibration
            {
                Manufacturer = Config.UnknownMeterPlaceholder,
                Model = Config.UnknownMeterPlaceholder,
                SerialNumber = Config.UnknownMeterPlaceholder,
                MeterType = MeterType.Unspecified,
                Equations = {new MeterCalibrationEquation {InterceptUnitId = Units.VelocityUnitId}}
            };
        }

        private MeterCalibration CreateMeterCalibration(Meter meter)
        {
            var meterCalibration = new MeterCalibration
            {
                FirmwareVersion = _ehsn.InstrumentDeployment?.GeneralInfo?.firmware,
                Manufacturer = _ehsn.InstrumentDeployment?.GeneralInfo?.manufacturer.WithDefaultValue(Config.UnknownMeterPlaceholder),
                Model = _ehsn.InstrumentDeployment?.GeneralInfo?.model.WithDefaultValue(Config.UnknownMeterPlaceholder),
                SerialNumber = meter.Number.WithDefaultValue(Config.UnknownMeterPlaceholder),
                Configuration = meter.MeterCalibDate,
                MeterType = GetMappedEnum(_ehsn.InstrumentDeployment?.GeneralInfo?.model, KnownMeterTypes),
            };

            foreach (var equation in meter.Equation ?? new MeterEquation[0])
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

        private void AddVertical(Collection<Vertical> verticals, Vertical vertical)
        {
            vertical.SequenceNumber = 1 + verticals.Count;

            verticals.Add(vertical);
        }

        private Vertical CreateEdgeVertical(Edge edge, VerticalType verticalType, MeterCalibration edgeMeter)
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
                MeasurementTime = TimeHelper.CoerceDateTimeIntoUtcOffset(edge.Date, LocationInfo.UtcOffset),
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

        private Vertical CreatePanelVertical(Panel panel, MeterCalibration meter)
        {
            var taglinePosition = panel.Tagmark.ToNullableDouble() ?? 0;
            var soundedDepth = panel.DepthReading.ToNullableDouble() ?? 0;
            var effectiveDepth = panel.DepthWithOffset.ToNullableDouble() ?? soundedDepth;
            var velocity = panel.AverageVelocity.ToNullableDouble() ?? 0;
            var discharge = panel.Discharge.ToNullableDouble() ?? 0;
            var width = panel.Width.ToNullableDouble() ?? 0;
            var percentFlow = panel.Flow.ToNullableDouble() ?? 0;

            var waterSurfaceToBottomOfIce = panel.IceCovered?.WSToBottomOfIceAdjusted.ToNullableDouble() ?? 0;
            var waterSurfaceToBottomOfSlush = panel.IceCovered?.WaterSurfaceToBottomOfSlush.ToNullableDouble() ?? waterSurfaceToBottomOfIce;

            var measurementCondition = panel.IceCovered != null
                ? (MeasurementConditionData) new IceCoveredData
                {
                    IceAssemblyType = panel.IceCovered.IceAssembly,
                    IceThickness = panel.IceCovered.IceThickness.ToNullableDouble(),
                    AboveFooting = panel.IceCovered.MeterAboveFooting.ToNullableDouble(),
                    BelowFooting = panel.IceCovered.MeterBelowFooting.ToNullableDouble(),
                    WaterSurfaceToBottomOfIce = waterSurfaceToBottomOfIce,
                    WaterSurfaceToBottomOfSlush = waterSurfaceToBottomOfSlush,
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

            var points = panel.PointMeasurements ?? new PointMeasurement[0];

            var fractionalDepths = string.Join("/", points.Select(p=>p.SamplingDepthCoefficient));

            if (!PointVelocityTypes.TryGetValue(fractionalDepths, out var pointVelocityObservationType))
            {
                if (!points.Any())
                {
                    pointVelocityObservationType = PointVelocityObservationType.Surface;
                    soundedDepth = effectiveDepth = 0;
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
                DeploymentMethod = GetPanelDeploymentMethod(panel),
                MeterCalibration = meter
            };

            if (!points.Any())
            {
                velocityObservation.Observations.Add(new VelocityDepthObservation
                {
                    Depth = 0,
                    Velocity = 0,
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
                MeasurementTime = TimeHelper.CoerceDateTimeIntoUtcOffset(panel.Date, LocationInfo.UtcOffset),
                SequenceNumber = panel.panelId,
                TaglinePosition = taglinePosition,
                SoundedDepth = soundedDepth,
                EffectiveDepth = effectiveDepth,
                MeasurementConditionData = measurementCondition,
                FlowDirection = panel.ReverseFlow.ToBoolean()
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

        private DeploymentMethodType? GetPanelDeploymentMethod(Panel panel)
        {
            if (panel.IceCovered != null)
                return DeploymentMethodType.Ice;

            var deploymentMethodText = panel.Open?.DeploymentMethod;

            if (string.IsNullOrWhiteSpace(deploymentMethodText))
                return DeploymentMethodType.Unspecified;

            if (KnownMidSectionDeploymentTypes.TryGetValue(deploymentMethodText.Trim(), out var deploymentMethod))
                return deploymentMethod;

            return DeploymentMethodType.Unspecified;
        }

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
                new Measurement(discharge, Units.DischargeUnitId),
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
                Comments = _ehsn.MovingBoatMeas?.ADCPMeasResults?.comments ?? _ehsn.DisMeas.dischargeRemark,
                BottomEstimateExponent = _ehsn.MovingBoatMeas?.velocityExponentCtrl.ToNullableDouble(),
                TopEstimateMethod = GetPicklistItem(_ehsn.MovingBoatMeas?.velocityTopCombo, Config.KnownTopEstimateMethods, s => new TopEstimateMethodPickList(s)),
                BottomEstimateMethod = GetPicklistItem(_ehsn.MovingBoatMeas?.velocityTopCombo, Config.KnownBottomEstimateMethods, s => new BottomEstimateMethodPickList(s)),
                NumberOfTransects = (_ehsn.MovingBoatMeas?.ADCPMeasTable ?? new EHSNMovingBoatMeasADCPMeasRow[0])
                    .Count(row => row.checkbox.ToBoolean()),
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
                {"Bridge", DeploymentMethodType.BridgeCrane},
                {"Boat", DeploymentMethodType.Boat},
                {"Ice", DeploymentMethodType.Ice},
                {"Ice_Bridge", DeploymentMethodType.Ice},
                {"Ice_Cableway", DeploymentMethodType.Ice},
                {"Ice_Wading", DeploymentMethodType.Ice},
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
