// Copyright QUANTOWER LLC. © 2017-2024. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using TradingPlatform.BusinessLayer;
using TradingPlatform.BusinessLayer.Chart;
using TradingPlatform.BusinessLayer.Utils;
using TradingPlatform.BusinessLayer.Utils.IntervalGeneration;

namespace ChanneIsIndicators;

public class IndicatorDailyOHLC : Indicator
{
    #region Parameters

    public DailyOHLCSessionType DailySessionType = DailyOHLCSessionType.AllDay;
    private const string SESSION_TYPE_NAME_SI = "Session type";
    private const string ALL_DAY_SESSION_TYPE = "All day";
    private const string SPECIFIED_SESSION_TYPE = "Specified session";
    private const string CUSTOM_RANGE_SESSION_TYPE = "Custom range";

    private const string CHART_SESSION_CONTAINER_SELECT_ITEM = "Chart session";
    private const string SESSION_TEMPLATE_NAME_SI = "sessionsTemplate";
    private const string CUSTOM_OPEN_SESSION_NAME_SI = "Open time";
    private const string CUSTOM_CLOSE_SESSION_NAME_SI = "Close time";

    private const int SERIES_DYNAMIC_HIGH = 0;
    private const int SERIES_DYNAMIC_LOW = 1;
    private const int SERIES_DYNAMIC_MIDDLE = 2;

    private string specifiedSessionContainerId = CHART_SESSION_CONTAINER_SELECT_ITEM;
    private const string PERIOD_TYPE_NAME_SI = "Period type";
    private const string PERIODS_COUNT_NAME_SI = "Number of periods to calculate";
    private const string PREVIOUS_DATA_OFFSET_NAME_SI = "Use previous data (offset in periods)";
    public DailyOHLCPeriodType PeriodType { get; set; } = DailyOHLCPeriodType.Daily;

    private ISessionsContainer fullDaySessionContainer;
    private ISessionsContainer customSessionContainer;
    private ISessionsContainer selectedSessionContainer;

    private IntervalGenerator intervalGenerator;
    private Interval<DateTime> currentSessionRange;
    private ISessionsContainer SessionContainer
    {
        get
        {
            if (this.PeriodType != DailyOHLCPeriodType.Daily)
                return this.fullDaySessionContainer;

            switch (this.DailySessionType)
            {
                case DailyOHLCSessionType.SpecifiedSession:
                    {
                        if (this.specifiedSessionContainerId == CHART_SESSION_CONTAINER_SELECT_ITEM)
                            return this.CurrentChart?.CurrentSessionContainer ?? this.fullDaySessionContainer;

                        return this.selectedSessionContainer ?? this.fullDaySessionContainer;
                    }

                case DailyOHLCSessionType.CustomRange:
                    return this.customSessionContainer ?? this.fullDaySessionContainer;

                case DailyOHLCSessionType.AllDay:
                default:
                    return this.fullDaySessionContainer;
            }
        }
    }
    public DateTime CustomRangeStartTime
    {
        get
        {
            if (this.customRangeStartTime == default)
                this.customRangeStartTime = DateTime.Today;

            return Core.Instance.TimeUtils.ConvertFromUTCToSelectedTimeZone(this.customRangeStartTime);
        }
        set => this.customRangeStartTime = value;
    }
    private DateTime customRangeStartTime;

    public DateTime CustomRangeEndTime
    {
        get
        {
            if (this.customRangeEndTime == default)
                this.customRangeEndTime = DateTime.Today;

            return Core.Instance.TimeUtils.ConvertFromUTCToSelectedTimeZone(this.customRangeEndTime);
        }
        set => this.customRangeEndTime = value;
    }
    private DateTime customRangeEndTime;

    public bool DynamicMode { get; set; } = false;

    public int DaysCount = 10;
    public int PreviousDataOffset = 0;

    public bool UseExtendLines = false;
    public bool AllowToDrawExtendLines => this.UseExtendLines;

    public bool ShowPriceForLabel { get; set; } = true;
    public DateTime ExtendRangeStartTime
    {
        get
        {
            if (this.extendRangeStartTime == default)
                this.extendRangeStartTime = DateTime.Today;

            return Core.Instance.TimeUtils.ConvertFromUTCToSelectedTimeZone(this.extendRangeStartTime);
        }
        set => this.extendRangeStartTime = value;
    }
    private DateTime extendRangeStartTime;

    public DateTime ExtendRangeEndTime
    {
        get
        {
            if (this.extendRangeEndTime == default)
                this.extendRangeEndTime = DateTime.Today;

            return Core.Instance.TimeUtils.ConvertFromUTCToSelectedTimeZone(this.extendRangeEndTime);
        }
        set => this.extendRangeEndTime = value;
    }
    private DateTime extendRangeEndTime;

    public NativeAlignment LabelAlignment { get; set; }
    public int labelFormat { get; set; }
    public int labelPosition { get; set; }
    public int LabelHorizontalMode { get; set; } = 0;
    public int LastLabelsCount { get; set; } = 1;
    public int OpenLabelOffsetPx { get; set; } = 0;
    public int HighLabelOffsetPx { get; set; } = 0;
    public int LowLabelOffsetPx { get; set; } = 0;
    public int CloseLabelOffsetPx { get; set; } = 0;
    public int MiddleLabelOffsetPx { get; set; } = 0;


    public bool ShowLabel { get; private set; }

    // --- Open ---
    public LineOptions OpenLineOptions
    {
        get => this.openLineOptions;
        private set
        {
            this.openLineOptions = value;
            this.openLinePen = ProcessPen(this.openLinePen, value);
        }
    }
    private LineOptions openLineOptions;
    private Pen openLinePen;

    public LineOptions OpenExtendLineOptions
    {
        get => this.openExtendLineOptions;
        private set
        {
            this.openExtendLineOptions = value;
            this.openExtendLinePen = ProcessPen(this.openExtendLinePen, value);
        }
    }
    private LineOptions openExtendLineOptions;
    private Pen openExtendLinePen;

    public bool ShowOpenLineLabel { get; private set; }

    public int OpenLabelHorizontalMode { get; set; } = 0; // Right / Left / Center / RightEdge / PriceScale
    public int OpenLabelPosition { get; set; } = 1;       // Below(0) / Above(1) / Center(2)
    public int OpenLastLabelsCount { get; set; } = 1;

    // --- High ---
    public LineOptions HighLineOptions
    {
        get => this.highLineOptions;
        private set
        {
            this.highLineOptions = value;
            this.highLinePen = ProcessPen(this.highLinePen, value);
        }
    }
    private LineOptions highLineOptions;
    private Pen highLinePen;

    public LineOptions HighExtendLineOptions
    {
        get => this.highExtendLineOptions;
        private set
        {
            this.highExtendLineOptions = value;
            this.highExtendLinePen = ProcessPen(this.highExtendLinePen, value);
        }
    }
    private LineOptions highExtendLineOptions;
    private Pen highExtendLinePen;

    public bool ShowHighLineLabel { get; private set; }

    public int HighLabelHorizontalMode { get; set; } = 0;
    public int HighLabelPosition { get; set; } = 1;
    public int HighLastLabelsCount { get; set; } = 1;

    // --- Low ---
    public LineOptions LowLineOptions
    {
        get => this.lowLineOptions;
        private set
        {
            this.lowLineOptions = value;
            this.lowLinePen = ProcessPen(this.lowLinePen, value);
        }
    }
    private LineOptions lowLineOptions;
    private Pen lowLinePen;

    public LineOptions LowExtendLineOptions
    {
        get => this.lowExtendLineOptions;
        private set
        {
            this.lowExtendLineOptions = value;
            this.lowExtendLinePen = ProcessPen(this.lowExtendLinePen, value);
        }
    }
    private LineOptions lowExtendLineOptions;
    private Pen lowExtendLinePen;

    public bool ShowLowLineLabel { get; private set; }

    public int LowLabelHorizontalMode { get; set; } = 0;
    public int LowLabelPosition { get; set; } = 1;
    public int LowLastLabelsCount { get; set; } = 1;

    // --- Close ---
    public LineOptions CloseLineOptions
    {
        get => this.closeLineOptions;
        private set
        {
            this.closeLineOptions = value;
            this.closeLinePen = ProcessPen(this.closeLinePen, value);
        }
    }
    private LineOptions closeLineOptions;
    private Pen closeLinePen;

    public LineOptions CloseExtendLineOptions
    {
        get => this.closeExtendLineOptions;
        private set
        {
            this.closeExtendLineOptions = value;
            this.closeExtendLinePen = ProcessPen(this.closeExtendLinePen, value);
        }
    }
    private LineOptions closeExtendLineOptions;
    private Pen closeExtendLinePen;

    public bool ShowCloseLineLabel { get; private set; }

    public int CloseLabelHorizontalMode { get; set; } = 0;
    public int CloseLabelPosition { get; set; } = 1;
    public int CloseLastLabelsCount { get; set; } = 1;

    // --- Middle ---
    public LineOptions MiddleLineOptions
    {
        get => this.middleLineOptions;
        private set
        {
            this.middleLineOptions = value;
            this.middleLinePen = ProcessPen(this.middleLinePen, value);
        }
    }
    private LineOptions middleLineOptions;
    private Pen middleLinePen;

    public LineOptions MiddleExtendLineOptions
    {
        get => this.middleExtendLineOptions;
        private set
        {
            this.middleExtendLineOptions = value;
            this.middleExtendLinePen = ProcessPen(this.middleExtendLinePen, value);
        }
    }
    private LineOptions middleExtendLineOptions;
    private Pen middleExtendLinePen;

    public bool ShowMiddleLineLabel { get; private set; }

    public int MiddleLabelHorizontalMode { get; set; } = 0;
    public int MiddleLabelPosition { get; set; } = 1;
    public int MiddleLastLabelsCount { get; set; } = 1;

    public Font CurrentFont { get; private set; }

    public string OpenCustomText = "O: ", HighCustomText = "H: ", LowCustomText = "L: ", CloseCustomText = "C: ", MiddleCustomText = "M: ";

    private readonly IList<DailyRangeItem> rangeCache;

    private DailyRangeItem currentRange;
    private ISession extendSession;
    private ISessionsContainer chartSessionContainer;
    private readonly StringFormat centerNearSF;

    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorDailyOHLC.cs";

    #endregion Parameters

    public IndicatorDailyOHLC()
    {
        this.Name = "Daily OHLC";

        this.AllowFitAuto = true;
        this.SeparateWindow = false;
        this.LabelAlignment = NativeAlignment.Right;
        this.ShowLabel = true;
        this.labelFormat = 1;
        this.labelPosition = 1;
        this.rangeCache = new List<DailyRangeItem>();

        this.OpenLineOptions = new LineOptions()
        {
            Enabled = true,
            WithCheckBox = true,
            Color = Color.Orange,
            LineStyle = LineStyle.Solid,
            Width = 1
        };
        this.OpenExtendLineOptions = new LineOptions()
        {
            Enabled = true,
            WithCheckBox = true,
            Color = Color.Orange,
            LineStyle = LineStyle.Dash,
            Width = 1
        };
        this.ShowOpenLineLabel = true;

        this.HighLineOptions = new LineOptions()
        {
            Enabled = true,
            WithCheckBox = true,
            Color = Color.Red,
            LineStyle = LineStyle.Solid,
            Width = 1
        };
        this.HighExtendLineOptions = new LineOptions()
        {
            Enabled = true,
            WithCheckBox = true,
            Color = Color.Red,
            LineStyle = LineStyle.Dash,
            Width = 1
        };
        this.ShowHighLineLabel = true;

        this.LowLineOptions = new LineOptions()
        {
            Enabled = true,
            WithCheckBox = true,
            Color = Color.Green,
            LineStyle = LineStyle.Solid,
            Width = 1
        };
        this.LowExtendLineOptions = new LineOptions()
        {
            Enabled = true,
            WithCheckBox = true,
            Color = Color.Green,
            LineStyle = LineStyle.Dash,
            Width = 1
        };
        this.ShowLowLineLabel = true;

        this.CloseLineOptions = new LineOptions()
        {
            Enabled = true,
            WithCheckBox = true,
            Color = Color.FromArgb(33, 150, 243),
            LineStyle = LineStyle.Solid,
            Width = 1
        };
        this.CloseExtendLineOptions = new LineOptions()
        {
            Enabled = true,
            WithCheckBox = true,
            Color = Color.FromArgb(33, 150, 243),
            LineStyle = LineStyle.Dash,
            Width = 1
        };
        this.ShowCloseLineLabel = true;

        this.MiddleLineOptions = new LineOptions()
        {
            Enabled = true,
            WithCheckBox = true,
            Color = Color.Gray,
            LineStyle = LineStyle.DashDot,
            Width = 1
        };
        this.MiddleExtendLineOptions = new LineOptions()
        {
            Enabled = true,
            WithCheckBox = true,
            Color = Color.Gray,
            LineStyle = LineStyle.Dash,
            Width = 1
        };
        this.ShowMiddleLineLabel = true;

        this.CurrentFont = new Font("Verdana", 10, GraphicsUnit.Pixel);
        this.centerNearSF = new StringFormat()
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Near
        };

        this.AddLineSeries("Dynamic High", this.HighLineOptions.Color, this.HighLineOptions.Width, this.HighLineOptions.LineStyle);
        this.AddLineSeries("Dynamic Low", this.LowLineOptions.Color, this.LowLineOptions.Width, this.LowLineOptions.LineStyle);
        this.AddLineSeries("Dynamic Middle", this.MiddleLineOptions.Color, this.MiddleLineOptions.Width, this.MiddleLineOptions.LineStyle);
    }

    #region Base overrides

    protected override void OnInit()
    {
        this.chartSessionContainer = this.CurrentChart?.CurrentSessionContainer;

        if (this.CurrentChart != null)
            this.CurrentChart.SettingsChanged += this.CurrentChartOnSettingsChanged;

        var timeZone = this.GetTimeZone();

        this.fullDaySessionContainer = new CustomSessionsContainer(
            "FullDaySession",
            timeZone,
            new[]
            {
            this.CreateCustomSession(TimeSpan.Zero, new TimeSpan(23, 59, 59), timeZone.TimeZoneInfo)
            });

        this.customSessionContainer = null;
        this.selectedSessionContainer = null;

        if (this.PeriodType == DailyOHLCPeriodType.Daily)
        {
            switch (this.DailySessionType)
            {
                case DailyOHLCSessionType.CustomRange:
                    {
                        this.customSessionContainer = new CustomSessionsContainer(
                            "CustomSession",
                            timeZone,
                            new[]
                            {
                        this.CreateCustomSession(
                            this.CustomRangeStartTime.TimeOfDay,
                            this.CustomRangeEndTime.TimeOfDay,
                            timeZone.TimeZoneInfo)
                            });
                        break;
                    }

                case DailyOHLCSessionType.SpecifiedSession:
                    {
                        if (!string.IsNullOrEmpty(this.specifiedSessionContainerId) &&
                            this.specifiedSessionContainerId != CHART_SESSION_CONTAINER_SELECT_ITEM)
                        {
                            this.selectedSessionContainer = Core.Instance.CustomSessions[this.specifiedSessionContainerId];
                        }
                        break;
                    }

                case DailyOHLCSessionType.AllDay:
                default:
                    break;
            }
        }

        this.intervalGenerator = null;
        this.currentSessionRange = default;
        this.currentRange = null;

        if (this.UseExtendLines)
        {
            var extendStart = this.ExtendRangeStartTime.TimeOfDay;
            var extendEnd = this.ExtendRangeEndTime.TimeOfDay;

            if (extendStart == extendEnd)
            {
                extendStart = TimeSpan.Zero;
                extendEnd = new TimeSpan(23, 59, 59);
            }
            else if (extendStart > extendEnd)
            {
                extendEnd = extendEnd.Add(new TimeSpan(1, 0, 0, 0));
            }

            this.extendSession = new Session("Extend session", extendStart, extendEnd);
        }

        this.OpenLabelHorizontalMode   = this.LabelHorizontalMode;
        this.HighLabelHorizontalMode   = this.LabelHorizontalMode;
        this.LowLabelHorizontalMode    = this.LabelHorizontalMode;
        this.CloseLabelHorizontalMode  = this.LabelHorizontalMode;
        this.MiddleLabelHorizontalMode = this.LabelHorizontalMode;

        this.OpenLabelPosition   = this.labelPosition;
        this.HighLabelPosition   = this.labelPosition;
        this.LowLabelPosition    = this.labelPosition;
        this.CloseLabelPosition  = this.labelPosition;
        this.MiddleLabelPosition = this.labelPosition;

        this.OpenLastLabelsCount   = this.LastLabelsCount;
        this.HighLastLabelsCount   = this.LastLabelsCount;
        this.LowLastLabelsCount    = this.LastLabelsCount;
        this.CloseLastLabelsCount  = this.LastLabelsCount;
        this.MiddleLastLabelsCount = this.LastLabelsCount;
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        var sessionContainer = this.SessionContainer;
        if (sessionContainer == null || this.Count == 0)
            return;


        var barTime = this.Time();

        if (!sessionContainer.ContainsDate(barTime))
        {
            if (this.DynamicMode)
            {
                this.SetValue(double.NaN, SERIES_DYNAMIC_HIGH);
                this.SetValue(double.NaN, SERIES_DYNAMIC_LOW);
                this.SetValue(double.NaN, SERIES_DYNAMIC_MIDDLE);
            }
            return;
        }

        var calculationPeriod = this.PeriodType switch
        {
            DailyOHLCPeriodType.Weekly => new Period(BasePeriod.Week, 1),
            DailyOHLCPeriodType.Monthly => new Period(BasePeriod.Month, 1),
            _ => new Period(BasePeriod.Day, 1)
        };

        if (this.intervalGenerator == null)
        {
            this.intervalGenerator = new IntervalGenerator(barTime, calculationPeriod, sessionContainer, this.GetTimeZone());
            this.currentSessionRange = this.intervalGenerator.Current;

            if (this.currentSessionRange.IsEmpty)
                return;

            this.currentRange = null;
        }
        if (!this.currentSessionRange.Contains(barTime))
        {
            this.intervalGenerator.MoveUntil(barTime);
            this.currentSessionRange = this.intervalGenerator.Current;

            if (this.currentSessionRange.IsEmpty)
                return;
            this.currentRange = null;
        }

        if (this.currentRange == null)
        {
            this.rangeCache.Insert(0,
                this.currentRange = new DailyRangeItem(this.currentSessionRange.From, this.Open()));
        }

        this.currentRange.TryUpdate(this.High(), this.Low(), this.Close());
        this.currentRange.EndDateTime = barTime;

        if (this.UseExtendLines && this.extendSession != null)
        {
            var mainStartUtc = this.currentRange.StartDateTime;

            var startTimeLocal = DateTime.SpecifyKind(
                this.currentRange.EndDateTime.Date.AddTicks(this.extendSession.OpenTime.Ticks),
                DateTimeKind.Local);

            var endTimeLocal = DateTime.SpecifyKind(
                this.currentRange.EndDateTime.Date.AddTicks(this.extendSession.CloseTime.Ticks),
                DateTimeKind.Local);

            var extendStartUtc = Core.Instance.TimeUtils.ConvertFromSelectedTimeZoneToUTC(startTimeLocal);
            var extendEndUtc = Core.Instance.TimeUtils.ConvertFromSelectedTimeZoneToUTC(endTimeLocal);

            if (extendStartUtc >= extendEndUtc)
                extendEndUtc = extendEndUtc.AddDays(1);

            if (extendStartUtc < mainStartUtc && extendEndUtc < mainStartUtc)
            {
                extendStartUtc = extendStartUtc.AddDays(1);
                extendEndUtc = extendEndUtc.AddDays(1);
            }

            this.currentRange.ExtendStartDateTime = extendStartUtc;
            this.currentRange.ExtendEndDateTime = extendEndUtc;
        }

        if (!this.DynamicMode)
            return;

        int availableVisibleSlots = this.rangeCache.Count - this.PreviousDataOffset;
        if (availableVisibleSlots < 0)
            availableVisibleSlots = 0;

        int visibleSlots = Math.Min(this.DaysCount, availableVisibleSlots);

        int drawBegin = this.Count;
        if (visibleSlots > 0)
        {
            int oldestVisibleSlotIndex = visibleSlots - 1;

            int drawBeginIndex = (int)this.HistoricalData.GetIndexByTime(
                this.rangeCache[oldestVisibleSlotIndex].StartDateTime.Ticks,
                SeekOriginHistory.Begin);

            if (drawBeginIndex < 0)
                drawBeginIndex = 0;

            drawBegin = drawBeginIndex;
        }

        this.LinesSeries[SERIES_DYNAMIC_HIGH].DrawBegin = drawBegin;
        this.LinesSeries[SERIES_DYNAMIC_LOW].DrawBegin = drawBegin;
        this.LinesSeries[SERIES_DYNAMIC_MIDDLE].DrawBegin = drawBegin;

        if (visibleSlots <= 0 || this.PreviousDataOffset >= this.rangeCache.Count)
        {
            this.SetValue(double.NaN, SERIES_DYNAMIC_HIGH);
            this.SetValue(double.NaN, SERIES_DYNAMIC_LOW);
            this.SetValue(double.NaN, SERIES_DYNAMIC_MIDDLE);
            return;
        }

        var sourceRange = this.rangeCache[this.PreviousDataOffset];

        this.SetValue(sourceRange.High, SERIES_DYNAMIC_HIGH);
        this.SetValue(sourceRange.Low, SERIES_DYNAMIC_LOW);
        this.SetValue(sourceRange.MiddlePrice, SERIES_DYNAMIC_MIDDLE);
    }

    protected override void OnClear()
    {
        this.currentRange = null;

        this.intervalGenerator = null;
        this.currentSessionRange = default;

        this.fullDaySessionContainer = null;
        this.customSessionContainer = null;
        this.selectedSessionContainer = null;

        if (this.CurrentChart != null)
            this.CurrentChart.SettingsChanged -= this.CurrentChartOnSettingsChanged;

        this.rangeCache?.Clear();
    }

    public override void Dispose()
    {
        if (this.CurrentChart != null)
            this.CurrentChart.SettingsChanged -= this.CurrentChartOnSettingsChanged;

        base.Dispose();
    }

    public override IList<SettingItem> Settings
    {
        get
        {
            var settings = base.Settings;

            var belowTL = new SelectItem("Below the line", 0);
            var aboveTL = new SelectItem("Above the line", 1);
            var centerTL = new SelectItem("Center on the line", 2);

            var labRight = new SelectItem("Right", 0);
            var labLeft = new SelectItem("Left", 1);
            var labCentered = new SelectItem("Centered", 2);
            var labRightEdge = new SelectItem("Right Edge", 3);
            var labPriceScale = new SelectItem("Price Scale", 4);

            var formatPrice = new SelectItem("Price", 0);
            var formatTextPrice = new SelectItem("Text and Price", 1);
            var formatText = new SelectItem("Text", 2);

            var daily = new SelectItem("Daily", DailyOHLCPeriodType.Daily);
            var weekly = new SelectItem("Weekly", DailyOHLCPeriodType.Weekly);
            var monthly = new SelectItem("Monthly", DailyOHLCPeriodType.Monthly);

            //
            var defaultSeparator = settings.FirstOrDefault()?.SeparatorGroup;

            var allDay = new SelectItem(ALL_DAY_SESSION_TYPE, DailyOHLCSessionType.AllDay);
            var specifiedSession = new SelectItem(SPECIFIED_SESSION_TYPE, DailyOHLCSessionType.SpecifiedSession);
            var customRange = new SelectItem(CUSTOM_RANGE_SESSION_TYPE, DailyOHLCSessionType.CustomRange);
            var dailyPeriodRelation = new SettingItemRelationVisibility(PERIOD_TYPE_NAME_SI, daily);
            var specifiedSessionRelation = new SettingItemRelationVisibility(SESSION_TYPE_NAME_SI, specifiedSession);
            var customRangeRelation = new SettingItemRelationVisibility(SESSION_TYPE_NAME_SI, customRange);

            var specifiedSessionDailyRelation = new SettingItemMultipleRelation(dailyPeriodRelation, specifiedSessionRelation);
            var customRangeDailyRelation = new SettingItemMultipleRelation(dailyPeriodRelation, customRangeRelation);

            var dynamicModeOffRelation = new SettingItemRelationVisibility("Dynamic mode", false);
            var dynamicModeOnRelation = new SettingItemRelationVisibility("Dynamic mode", true);

            settings.Add(new SettingItemSelectorLocalized(
                PERIOD_TYPE_NAME_SI,
                new SelectItem(PERIOD_TYPE_NAME_SI, this.PeriodType),
                new List<SelectItem> { daily, weekly, monthly })
            {
                SeparatorGroup = defaultSeparator,
                Text = PERIOD_TYPE_NAME_SI,
                SortIndex = 5,
            });


            settings.Add(new SettingItemSelectorLocalized(
           SESSION_TYPE_NAME_SI,
           new SelectItem(SESSION_TYPE_NAME_SI, this.DailySessionType),
           new List<SelectItem> { allDay, specifiedSession, customRange })
            {
                SeparatorGroup = defaultSeparator,
                Text = SESSION_TYPE_NAME_SI,
                SortIndex = 10,
                Relation = dailyPeriodRelation
            });

            var defaultItem = new SelectItem(CHART_SESSION_CONTAINER_SELECT_ITEM);
            var items = new List<SelectItem> { defaultItem };
            items.AddRange(Core.Instance.CustomSessions.Select(s => new SelectItem(s.Name, s.Id)));

            var selectedItem = items.FirstOrDefault(i => Equals(i.Value, this.specifiedSessionContainerId)) ?? items.First();

            settings.Add(new SettingItemSelectorLocalized(SESSION_TEMPLATE_NAME_SI, selectedItem, items, 20)
            {
                SeparatorGroup = defaultSeparator,
                Text = loc._("Sessions template"),
                Relation = specifiedSessionDailyRelation
            });

            settings.Add(new SettingItemDateTime(CUSTOM_OPEN_SESSION_NAME_SI, this.CustomRangeStartTime, 20)
            {
                SeparatorGroup = defaultSeparator,
                Text = loc._("Start time"),
                Format = DatePickerFormat.LongTime,
                ValueChangingBehavior = SettingItemValueChangingBehavior.WithConfirmation,
                Relation = customRangeDailyRelation
            });

            settings.Add(new SettingItemDateTime(CUSTOM_CLOSE_SESSION_NAME_SI, this.CustomRangeEndTime, 20)
            {
                SeparatorGroup = defaultSeparator,
                Text = loc._("End time"),
                Format = DatePickerFormat.LongTime,
                ValueChangingBehavior = SettingItemValueChangingBehavior.WithConfirmation,
                Relation = customRangeDailyRelation
            });

            settings.Add(new SettingItemInteger(PERIODS_COUNT_NAME_SI, this.DaysCount, 20)
            {
                SeparatorGroup = defaultSeparator,
                Text = loc._(PERIODS_COUNT_NAME_SI),
                Minimum = 1
            });

            settings.Add(new SettingItemInteger(PREVIOUS_DATA_OFFSET_NAME_SI, this.PreviousDataOffset, 20)
            {
                SeparatorGroup = defaultSeparator,
                Text = loc._(PREVIOUS_DATA_OFFSET_NAME_SI),
                Minimum = 0
            });
            settings.Add(new SettingItemBoolean("Show extend lines", this.UseExtendLines, 40)
            {
                SeparatorGroup = defaultSeparator,
                Text = loc._("Show extend lines"),
            });
            settings.Add(new SettingItemDateTime("Start extend time", this.ExtendRangeStartTime, 45)
            {
                SeparatorGroup = defaultSeparator,
                Text = loc._("Start extend time"),
                Format = DatePickerFormat.LongTime,
                ValueChangingBehavior = SettingItemValueChangingBehavior.WithConfirmation,
                Relation = new SettingItemRelationVisibility("Show extend lines", true)
            });
            settings.Add(new SettingItemDateTime("End extend time", this.ExtendRangeEndTime, 50)
            {
                SeparatorGroup = defaultSeparator,
                Text = loc._("End extend time"),
                Format = DatePickerFormat.LongTime,
                ValueChangingBehavior = SettingItemValueChangingBehavior.WithConfirmation,
                Relation = new SettingItemRelationVisibility("Show extend lines", true)
            });
            settings.Add(new SettingItemBoolean("ShowLabel", this.ShowLabel, 60)
            {
                SeparatorGroup = defaultSeparator,
                Text = loc._("Show label")
            });
            settings.Add(new SettingItemFont("Font", this.CurrentFont, 60)
            {
                SeparatorGroup = defaultSeparator,
                Text = loc._("Font"),
                Relation = new SettingItemRelationVisibility("ShowLabel", true)
            });

            // --- OPEN group ---
            var openLineStyleSeparator = new SettingItemSeparatorGroup("Open line style", -999);
            settings.Add(new SettingItemLineOptions("OpenLineOptions", this.OpenLineOptions, 60)
            {
                SeparatorGroup = openLineStyleSeparator,
                Text = loc._("Main line style"),
                ExcludedStyles = new LineStyle[] { LineStyle.Points }
            });
            settings.Add(new SettingItemLineOptions("OpenExtendLineOptions", this.OpenExtendLineOptions, 60)
            {
                SeparatorGroup = openLineStyleSeparator,
                Text = loc._("Extend line style"),
                ExcludedStyles = new LineStyle[] { LineStyle.Points }
            });
            settings.Add(new SettingItemBoolean("ShowOpenLineLabel", this.ShowOpenLineLabel, 60)
            {
                SeparatorGroup = openLineStyleSeparator,
                Text = loc._("Show line label")
            });
            settings.Add(new SettingItemString("OpenCustomText", this.OpenCustomText, 60)
            {
                SeparatorGroup = openLineStyleSeparator,
                Text = loc._("Custom text"),
                Relation = new SettingItemRelationVisibility("Format", formatText, formatTextPrice)
            });
            settings.Add(new SettingItemSelectorLocalized(
                "Open label alignment",
                new SelectItem("Open label alignment", this.OpenLabelHorizontalMode),
                new List<SelectItem> { labRight, labLeft, labCentered, labRightEdge, labPriceScale })
            {
                SeparatorGroup = openLineStyleSeparator,
                Text = loc._("Label alignment"),
                Relation = new SettingItemRelationVisibility("ShowLabel", true)
            });
            settings.Add(new SettingItemInteger("Open last labels count", this.OpenLastLabelsCount, 61)
            {
                SeparatorGroup = openLineStyleSeparator,
                Text = loc._("Last labels count"),
                Relation = new SettingItemRelationVisibility("Open label alignment", labRightEdge, labPriceScale),
                Minimum = 1
            });
            settings.Add(new SettingItemSelectorLocalized(
                "Open label position",
                new SelectItem("Open label position", this.OpenLabelPosition),
                new List<SelectItem> { belowTL, aboveTL, centerTL })
            {
                SeparatorGroup = openLineStyleSeparator,
                Text = loc._("Label position"),
                Relation = new SettingItemRelationVisibility("ShowLabel", true)
            });
            settings.Add(new SettingItemInteger("Open label offset (px)", this.OpenLabelOffsetPx, 62)
            {
                SeparatorGroup = openLineStyleSeparator,
                Text = "Label offset (last day, px)",
                Relation = new SettingItemRelationVisibility("ShowOpenLineLabel", true),
                Minimum = -2000
            });
            // --- HIGH group ---

            var highLineStyleSeparator = new SettingItemSeparatorGroup("High line style", -999);
            settings.Add(new SettingItemLineOptions("HighLineOptions", this.HighLineOptions, 60)
            {
                SeparatorGroup = highLineStyleSeparator,
                Text = loc._("Main line style"),
                ExcludedStyles = new LineStyle[] { LineStyle.Points },
            });
            settings.Add(new SettingItemLineOptions("HighExtendLineOptions", this.HighExtendLineOptions, 60)
            {
                SeparatorGroup = highLineStyleSeparator,
                Text = loc._("Extend line style"),
                ExcludedStyles = new LineStyle[] { LineStyle.Points },
            });
            settings.Add(new SettingItemBoolean("ShowHighLineLabel", this.ShowHighLineLabel, 60)
            {
                SeparatorGroup = highLineStyleSeparator,
                Text = loc._("Show line label")
            });
            settings.Add(new SettingItemString("HighCustomText", this.HighCustomText, 60)
            {
                SeparatorGroup = highLineStyleSeparator,
                Text = loc._("Custom text"),
                Relation = new SettingItemRelationVisibility("Format", formatText, formatTextPrice)
            });
            settings.Add(new SettingItemSelectorLocalized(
            "High label alignment",
            new SelectItem("High label alignment", this.HighLabelHorizontalMode),
            new List<SelectItem> { labRight, labLeft, labCentered, labRightEdge, labPriceScale })
            {
                SeparatorGroup = highLineStyleSeparator,
                Text = loc._("Label alignment"),
                Relation = new SettingItemRelationVisibility("ShowLabel", true)
            });

            settings.Add(new SettingItemInteger("High last labels count", this.HighLastLabelsCount, 61)
            {
                SeparatorGroup = highLineStyleSeparator,
                Text = loc._("Last labels count"),
                Relation = new SettingItemRelationVisibility("High label alignment", labRightEdge, labPriceScale),
                Minimum = 1
            });

            settings.Add(new SettingItemSelectorLocalized(
                "High label position",
                new SelectItem("High label position", this.HighLabelPosition),
                new List<SelectItem> { belowTL, aboveTL, centerTL })
            {
                SeparatorGroup = highLineStyleSeparator,
                Text = loc._("Label position"),
                Relation = new SettingItemRelationVisibility("ShowLabel", true)
            });
            settings.Add(new SettingItemInteger("High label offset (px)", this.HighLabelOffsetPx, 62)
            {
                SeparatorGroup = highLineStyleSeparator,
                Text = "Label offset (last day, px)",
                Relation = new SettingItemRelationVisibility("ShowHighLineLabel", true),
                Minimum = -2000
            });


            // --- LOW group ---
            var lowLineStyleSeparator = new SettingItemSeparatorGroup("Low line style", -999);
            settings.Add(new SettingItemLineOptions("LowLineOptions", this.LowLineOptions, 60)
            {
                SeparatorGroup = lowLineStyleSeparator,
                Text = loc._("Main line style"),
                ExcludedStyles = new LineStyle[] { LineStyle.Points },
            });
            settings.Add(new SettingItemLineOptions("LowExtendLineOptions", this.LowExtendLineOptions, 60)
            {
                SeparatorGroup = lowLineStyleSeparator,
                Text = loc._("Extend line style"),
                ExcludedStyles = new LineStyle[] { LineStyle.Points },
            });
            settings.Add(new SettingItemBoolean("ShowLowLineLabel", this.ShowLowLineLabel, 60)
            {
                SeparatorGroup = lowLineStyleSeparator,
                Text = loc._("Show line label")
            });
            settings.Add(new SettingItemString("LowCustomText", this.LowCustomText, 60)
            {
                SeparatorGroup = lowLineStyleSeparator,
                Text = loc._("Custom text"),
                Relation = new SettingItemRelationVisibility("Format", formatText, formatTextPrice)
            });
            settings.Add(new SettingItemSelectorLocalized(
                "Low label alignment",
                new SelectItem("Low label alignment", this.LowLabelHorizontalMode),
                new List<SelectItem> { labRight, labLeft, labCentered, labRightEdge, labPriceScale })
            {
                SeparatorGroup = lowLineStyleSeparator,
                Text = loc._("Label alignment"),
                Relation = new SettingItemRelationVisibility("ShowLabel", true)
            });
            settings.Add(new SettingItemInteger("Low last labels count", this.LowLastLabelsCount, 61)
            {
                SeparatorGroup = lowLineStyleSeparator,
                Text = loc._("Last labels count"),
                Relation = new SettingItemRelationVisibility("Low label alignment", labRightEdge, labPriceScale),
                Minimum = 1
            });
            settings.Add(new SettingItemSelectorLocalized(
                "Low label position",
                new SelectItem("Low label position", this.LowLabelPosition),
                new List<SelectItem> { belowTL, aboveTL, centerTL })
            {
                SeparatorGroup = lowLineStyleSeparator,
                Text = loc._("Label position"),
                Relation = new SettingItemRelationVisibility("ShowLabel", true)
            });
            settings.Add(new SettingItemInteger("Low label offset (px)", this.LowLabelOffsetPx, 62)
            {
                SeparatorGroup = lowLineStyleSeparator,
                Text = "Label offset (last day, px)",
                Relation = new SettingItemRelationVisibility("ShowLowLineLabel", true),
                Minimum = -2000
            });


            // --- CLOSE group ---
            var closeLineStyleSeparator = new SettingItemSeparatorGroup("Close line style", -999);
            settings.Add(new SettingItemLineOptions("CloseLineOptions", this.CloseLineOptions, 60)
            {
                SeparatorGroup = closeLineStyleSeparator,
                Text = loc._("Main line style"),
                ExcludedStyles = new LineStyle[] { LineStyle.Points }
            });
            settings.Add(new SettingItemLineOptions("CloseExtendLineOptions", this.CloseExtendLineOptions, 60)
            {
                SeparatorGroup = closeLineStyleSeparator,
                Text = loc._("Extend line style"),
                ExcludedStyles = new LineStyle[] { LineStyle.Points }
            });
            settings.Add(new SettingItemBoolean("ShowCloseLineLabel", this.ShowCloseLineLabel, 60)
            {
                SeparatorGroup = closeLineStyleSeparator,
                Text = loc._("Show line label")
            });
            settings.Add(new SettingItemString("CloseCustomText", this.CloseCustomText, 60)
            {
                SeparatorGroup = closeLineStyleSeparator,
                Text = loc._("Custom text"),
                Relation = new SettingItemRelationVisibility("Format", formatText, formatTextPrice)
            });
            //
            settings.Add(new SettingItemSelectorLocalized(
                "Close label alignment",
                new SelectItem("Close label alignment", this.CloseLabelHorizontalMode),
                new List<SelectItem> { labRight, labLeft, labCentered, labRightEdge, labPriceScale })
            {
                SeparatorGroup = closeLineStyleSeparator,
                Text = loc._("Label alignment"),
                Relation = new SettingItemRelationVisibility("ShowLabel", true)
            });
            settings.Add(new SettingItemInteger("Close last labels count", this.CloseLastLabelsCount, 61)
            {
                SeparatorGroup = closeLineStyleSeparator,
                Text = loc._("Last labels count"),
                Relation = new SettingItemRelationVisibility("Close label alignment", labRightEdge, labPriceScale),
                Minimum = 1
            });
            settings.Add(new SettingItemSelectorLocalized(
                "Close label position",
                new SelectItem("Close label position", this.CloseLabelPosition),
                new List<SelectItem> { belowTL, aboveTL, centerTL })
            {
                SeparatorGroup = closeLineStyleSeparator,
                Text = loc._("Label position"),
                Relation = new SettingItemRelationVisibility("ShowLabel", true)
            });
            settings.Add(new SettingItemInteger("Close label offset (px)", this.CloseLabelOffsetPx, 62)
            {
                SeparatorGroup = closeLineStyleSeparator,
                Text = "Label offset (last day, px)",
                Relation = new SettingItemRelationVisibility("ShowCloseLineLabel", true),
                Minimum = -2000
            });

            // --- MIDDLE group ---
            var middleLineStyleSeparator = new SettingItemSeparatorGroup("Middle line style", -999);
            settings.Add(new SettingItemLineOptions("MiddleLineOptions", this.MiddleLineOptions, 60)
            {
                SeparatorGroup = middleLineStyleSeparator,
                Text = loc._("Main line style"),
                ExcludedStyles = new LineStyle[] { LineStyle.Points },
            });
            settings.Add(new SettingItemLineOptions("MiddleExtendLineOptions", this.MiddleExtendLineOptions, 60)
            {
                SeparatorGroup = middleLineStyleSeparator,
                Text = loc._("Extend line style"),
                ExcludedStyles = new LineStyle[] { LineStyle.Points },
            });
            settings.Add(new SettingItemBoolean("ShowMiddleLineLabel", this.ShowMiddleLineLabel, 60)
            {
                SeparatorGroup = middleLineStyleSeparator,
                Text = loc._("Show line label")
            });
            settings.Add(new SettingItemString("MiddleCustomText", this.MiddleCustomText, 60)
            {
                SeparatorGroup = middleLineStyleSeparator,
                Text = loc._("Custom text"),
                Relation = new SettingItemRelationVisibility("Format", formatText, formatTextPrice)
            });
            settings.Add(new SettingItemSelectorLocalized(
                "Middle label alignment",
                new SelectItem("Middle label alignment", this.MiddleLabelHorizontalMode),
                new List<SelectItem> { labRight, labLeft, labCentered, labRightEdge, labPriceScale })
            {
                SeparatorGroup = middleLineStyleSeparator,
                Text = loc._("Label alignment"),
                Relation = new SettingItemRelationVisibility("ShowLabel", true)
            });
            settings.Add(new SettingItemInteger("Middle last labels count", this.MiddleLastLabelsCount, 61)
            {
                SeparatorGroup = middleLineStyleSeparator,
                Text = loc._("Last labels count"),
                Relation = new SettingItemRelationVisibility("Middle label alignment", labRightEdge, labPriceScale),
                Minimum = 1
            });
            settings.Add(new SettingItemSelectorLocalized(
                "Middle label position",
                new SelectItem("Middle label position", this.MiddleLabelPosition),
                new List<SelectItem> { belowTL, aboveTL, centerTL })
            {
                SeparatorGroup = middleLineStyleSeparator,
                Text = loc._("Label position"),
                Relation = new SettingItemRelationVisibility("ShowLabel", true)
            });
            settings.Add(new SettingItemInteger("Middle label offset (px)", this.MiddleLabelOffsetPx, 62)
            {
                SeparatorGroup = middleLineStyleSeparator,
                Text = "Label offset (last day, px)",
                Relation = new SettingItemRelationVisibility("ShowMiddleLineLabel", true),
                Minimum = -2000
            });

            settings.Add(new SettingItemBoolean("Dynamic mode", this.DynamicMode, 39)
            {
                SeparatorGroup = defaultSeparator,
                Text = "Dynamic mode"
            });

            settings.Add(new SettingItemBoolean("Show price for label", this.ShowPriceForLabel, 60)
            {
                SeparatorGroup = defaultSeparator,
                Text = loc._("Show price for label"),
                Relation = new SettingItemRelationVisibility("ShowLabel", true)
            });

            return settings;
        }
        set
        {
            var holder = new SettingsHolder(value);

            // Open
            if (holder.TryGetValue("OpenLineOptions", out SettingItem item) && item.Value is LineOptions openOptions)
                this.OpenLineOptions = openOptions;
            if (holder.TryGetValue("OpenExtendLineOptions", out item) && item.Value is LineOptions openExtendOptions)
                this.OpenExtendLineOptions = openExtendOptions;
            if (holder.TryGetValue("ShowOpenLineLabel", out item) && item.Value is bool showOpenLabel)
                this.ShowOpenLineLabel = showOpenLabel;
            if (holder.TryGetValue("OpenCustomText", out item) && item.Value is string openCustomText)
                this.OpenCustomText = openCustomText;
            if (holder.TryGetValue("Open label offset (px)", out item)   && item.Value is int oOff)
                this.OpenLabelOffsetPx   = oOff;

            // High
            if (holder.TryGetValue("HighLineOptions", out item) && item.Value is LineOptions highOptions)
                this.HighLineOptions = highOptions;
            if (holder.TryGetValue("HighExtendLineOptions", out item) && item.Value is LineOptions highExtendOptions)
                this.HighExtendLineOptions = highExtendOptions;
            if (holder.TryGetValue("ShowHighLineLabel", out item) && item.Value is bool showHighLabel)
                this.ShowHighLineLabel = showHighLabel;
            if (holder.TryGetValue("HighCustomText", out item) && item.Value is string highCustomText)
                this.HighCustomText = highCustomText;
            if (holder.TryGetValue("High label offset (px)", out item)   && item.Value is int hOff)
                this.HighLabelOffsetPx   = hOff;

            // Low
            if (holder.TryGetValue("LowLineOptions", out item) && item.Value is LineOptions lowOptions)
                this.LowLineOptions = lowOptions;
            if (holder.TryGetValue("LowExtendLineOptions", out item) && item.Value is LineOptions lowExtendOptions)
                this.LowExtendLineOptions = lowExtendOptions;
            if (holder.TryGetValue("ShowLowLineLabel", out item) && item.Value is bool showLowLabel)
                this.ShowLowLineLabel = showLowLabel;
            if (holder.TryGetValue("LowCustomText", out item) && item.Value is string lowCustomText)
                this.LowCustomText = lowCustomText;
            if (holder.TryGetValue("Low label offset (px)", out item)    && item.Value is int lOff)
                this.LowLabelOffsetPx    = lOff;


            // Close
            if (holder.TryGetValue("CloseLineOptions", out item) && item.Value is LineOptions closeOptions)
                this.CloseLineOptions = closeOptions;
            if (holder.TryGetValue("CloseExtendLineOptions", out item) && item.Value is LineOptions closeExtendOptions)
                this.CloseExtendLineOptions = closeExtendOptions;
            if (holder.TryGetValue("ShowCloseLineLabel", out item) && item.Value is bool showCloseLabel)
                this.ShowCloseLineLabel = showCloseLabel;
            if (holder.TryGetValue("CloseCustomText", out item) && item.Value is string closeCustomText)
                this.CloseCustomText = closeCustomText;
            if (holder.TryGetValue("Close label offset (px)", out item)  && item.Value is int cOff)
                this.CloseLabelOffsetPx  = cOff;


            // Middle
            if (holder.TryGetValue("MiddleLineOptions", out item) && item.Value is LineOptions middleOptions)
                this.MiddleLineOptions = middleOptions;
            if (holder.TryGetValue("MiddleExtendLineOptions", out item) && item.Value is LineOptions middleExtendOptions)
                this.MiddleExtendLineOptions = middleExtendOptions;
            if (holder.TryGetValue("ShowMiddleLineLabel", out item) && item.Value is bool showMiddleLabel)
                this.ShowMiddleLineLabel = showMiddleLabel;
            if (holder.TryGetValue("MiddleCustomText", out item) && item.Value is string middleCustomText)
                this.MiddleCustomText = middleCustomText;
            if (holder.TryGetValue("Middle label offset (px)", out item) && item.Value is int mOff)
                this.MiddleLabelOffsetPx = mOff;


            var needRefresh = false;
            if (holder.TryGetValue(PERIOD_TYPE_NAME_SI, out item))
            {
                var newValue = item.GetValue<DailyOHLCPeriodType>();

                if (this.PeriodType != newValue)
                {
                    this.PeriodType = newValue;
                    needRefresh |= item.ValueChangingReason == SettingItemValueChangingReason.Manually;
                }
            }

            if (holder.TryGetValue(SESSION_TYPE_NAME_SI, out item) &&
                item.GetValue<DailyOHLCSessionType>() != this.DailySessionType)
            {
                this.DailySessionType = item.GetValue<DailyOHLCSessionType>();
                needRefresh |= item.ValueChangingReason == SettingItemValueChangingReason.Manually;
            }

            if (holder.TryGetValue(SESSION_TEMPLATE_NAME_SI, out item))
            {
                var newContainerId = item.GetValue<string>();

                if (newContainerId != this.specifiedSessionContainerId)
                {
                    this.specifiedSessionContainerId = newContainerId;

                    if (newContainerId == CHART_SESSION_CONTAINER_SELECT_ITEM)
                        this.selectedSessionContainer = null;
                    else
                        this.selectedSessionContainer = Core.Instance.CustomSessions[this.specifiedSessionContainerId];

                    needRefresh |= item.ValueChangingReason == SettingItemValueChangingReason.Manually;
                }
            }

            if (holder.TryGetValue(CUSTOM_OPEN_SESSION_NAME_SI, out item))
            {
                var newValue = Core.Instance.TimeUtils.ConvertFromSelectedTimeZoneToUTC(item.GetValue<DateTime>());

                if (this.CustomRangeStartTime != newValue)
                {
                    this.CustomRangeStartTime = newValue;
                    needRefresh |= item.ValueChangingReason == SettingItemValueChangingReason.Manually;
                }
            }

            if (holder.TryGetValue(CUSTOM_CLOSE_SESSION_NAME_SI, out item))
            {
                var newValue = Core.Instance.TimeUtils.ConvertFromSelectedTimeZoneToUTC(item.GetValue<DateTime>());

                if (this.CustomRangeEndTime != newValue)
                {
                    this.CustomRangeEndTime = newValue;
                    needRefresh |= item.ValueChangingReason == SettingItemValueChangingReason.Manually;
                }
            }
            if (holder.TryGetValue(PERIODS_COUNT_NAME_SI, out item) && item.Value is int daysCount)
            {
                this.DaysCount = daysCount;
                needRefresh = true;
            }
            if (holder.TryGetValue(PREVIOUS_DATA_OFFSET_NAME_SI, out item) && item.Value is int previousDataOffset)
            {
                this.PreviousDataOffset = previousDataOffset;
                needRefresh = true;
            }
            if (holder.TryGetValue("Show extend lines", out item) && item.Value is bool useExtendLines)
                this.UseExtendLines = useExtendLines;
            if (holder.TryGetValue("Start extend time", out item))
            {
                var newValue = Core.Instance.TimeUtils.ConvertFromSelectedTimeZoneToUTC(item.GetValue<DateTime>());

                if (this.ExtendRangeStartTime != newValue)
                {
                    this.ExtendRangeStartTime = newValue;
                    needRefresh |= item.ValueChangingReason == SettingItemValueChangingReason.Manually;
                }
            }

            if (holder.TryGetValue("End extend time", out item))
            {
                var newValue = Core.Instance.TimeUtils.ConvertFromSelectedTimeZoneToUTC(item.GetValue<DateTime>());

                if (this.ExtendRangeEndTime != newValue)
                {
                    this.ExtendRangeEndTime = newValue;
                    needRefresh |= item.ValueChangingReason == SettingItemValueChangingReason.Manually;
                }
            }

            if (holder.TryGetValue("Font", out item) && item.Value is Font font)
                this.CurrentFont = font;
            if (holder.TryGetValue("ShowLabel", out item) && item.Value is bool showLabel)
                this.ShowLabel = showLabel;
            if (holder.TryGetValue("Last labels count", out item) && item.Value is int lastLabels)
                this.LastLabelsCount = lastLabels;

            // Open
            if (holder.TryGetValue("Open label alignment", out var oAlign))
                this.OpenLabelHorizontalMode = oAlign.GetValue<int>();
            if (holder.TryGetValue("Open label position", out var oPos))
                this.OpenLabelPosition = oPos.GetValue<int>();
            if (holder.TryGetValue("Open last labels count", out var oCnt) && oCnt.Value is int olc)
                this.OpenLastLabelsCount = olc;
            // High
            if (holder.TryGetValue("High label alignment", out var hAlign))
                this.HighLabelHorizontalMode = hAlign.GetValue<int>();
            if (holder.TryGetValue("High label position", out var hPos))
                this.HighLabelPosition = hPos.GetValue<int>();
            if (holder.TryGetValue("High last labels count", out var hCnt) && hCnt.Value is int hlc)
                this.HighLastLabelsCount = hlc;
            // Low
            if (holder.TryGetValue("Low label alignment", out var lAlign))
                this.LowLabelHorizontalMode = lAlign.GetValue<int>();
            if (holder.TryGetValue("Low label position", out var lPos))
                this.LowLabelPosition = lPos.GetValue<int>();
            if (holder.TryGetValue("Low last labels count", out var lCnt) && lCnt.Value is int llc)
                this.LowLastLabelsCount = llc;
            // Close
            if (holder.TryGetValue("Close label alignment", out var cAlign))
                this.CloseLabelHorizontalMode = cAlign.GetValue<int>();
            if (holder.TryGetValue("Close label position", out var cPos))
                this.CloseLabelPosition = cPos.GetValue<int>();
            if (holder.TryGetValue("Close last labels count", out var cCnt) && cCnt.Value is int clc)
                this.CloseLastLabelsCount = clc;
            // Middle
            if (holder.TryGetValue("Middle label alignment", out var mAlign))
                this.MiddleLabelHorizontalMode = mAlign.GetValue<int>();
            if (holder.TryGetValue("Middle label position", out var mPos))
                this.MiddleLabelPosition = mPos.GetValue<int>();
            if (holder.TryGetValue("Middle last labels count", out var mCnt) && mCnt.Value is int mlc)
                this.MiddleLastLabelsCount = mlc;

            if (holder.TryGetValue("Dynamic mode", out item) && item.Value is bool dynamicMode)
            {
                if (this.DynamicMode != dynamicMode)
                {
                    this.DynamicMode = dynamicMode;
                    needRefresh |= item.ValueChangingReason == SettingItemValueChangingReason.Manually;
                }
            }
            if (holder.TryGetValue("Show price for label", out item) && item.Value is bool showPriceForLabel)
                this.ShowPriceForLabel = showPriceForLabel;

            if (needRefresh)
                this.Refresh();

            base.Settings = value;
        }
    }

    public override void OnPaintChart(PaintChartEventArgs args)
    {
        var gr = args.Graphics;
        var restoredClip = gr.ClipBounds;

        gr.SetClip(args.Rectangle);

        try
        {
            var currentWindow = this.CurrentChart.Windows[args.WindowIndex];

            var leftTime = currentWindow.CoordinatesConverter.GetTime(args.Rectangle.Left);
            var rightTime = currentWindow.CoordinatesConverter.GetTime(args.Rectangle.Right);

            int halfBarWidth = this.CurrentChart.BarsWidth / 2;
            for (int i = 0; i < this.DaysCount; i++)
            {
                if (i >= this.rangeCache.Count)
                    break;
                var range = this.rangeCache[i];
                bool isMainRangeOutside = range.EndDateTime < leftTime || range.StartDateTime > rightTime;

                bool anyEdgeLabelWanted =
                    (this.ShowHighLineLabel   && IsEdgeMode(this.HighLabelHorizontalMode))   ||
                    (this.ShowLowLineLabel    && IsEdgeMode(this.LowLabelHorizontalMode))    ||
                    (this.ShowOpenLineLabel   && IsEdgeMode(this.OpenLabelHorizontalMode))   ||
                    (this.ShowCloseLineLabel  && IsEdgeMode(this.CloseLabelHorizontalMode))  ||
                    (this.ShowMiddleLineLabel && IsEdgeMode(this.MiddleLabelHorizontalMode));

                bool needDrawExtendLines =
                    this.UseExtendLines &&
                    range.ExtendStartDateTime != range.ExtendEndDateTime &&
                    range.ExtendEndDateTime >= this.HistoricalData[0, SeekOriginHistory.Begin].TimeLeft &&
                    range.ExtendStartDateTime >= this.HistoricalData[0, SeekOriginHistory.Begin].TimeLeft;

                if (isMainRangeOutside && !needDrawExtendLines && !anyEdgeLabelWanted)
                    continue;

                int prevDailyRangeOffset = i + this.PreviousDataOffset;
                bool needUsePreviosRange = prevDailyRangeOffset != i;

                if (needUsePreviosRange && prevDailyRangeOffset >= this.rangeCache.Count)
                    break;

                float leftX = (float)currentWindow.CoordinatesConverter.GetChartX(range.StartDateTime) + halfBarWidth;
                if (leftX < args.Rectangle.Left)
                    leftX = args.Rectangle.Left;

                float rightX = (float)currentWindow.CoordinatesConverter.GetChartX(range.EndDateTime) + halfBarWidth;
                if (rightX > args.Rectangle.Right)
                    rightX = args.Rectangle.Right;

                float leftExtendX = default;
                float rightExtendX = default;

                if (needDrawExtendLines)
                {
                    leftExtendX = (float)currentWindow.CoordinatesConverter.GetChartX(range.ExtendStartDateTime) + halfBarWidth;
                    if (leftExtendX < args.Rectangle.Left)
                        leftExtendX = args.Rectangle.Left;

                    rightExtendX = (float)currentWindow.CoordinatesConverter.GetChartX(range.ExtendEndDateTime) + halfBarWidth;
                    if (rightExtendX > args.Rectangle.Right)
                        rightExtendX = args.Rectangle.Right;
                }

                int top = args.Rectangle.Top;
                int bottom = args.Rectangle.Bottom;

                // get previous date
                if (needUsePreviosRange)
                    range = this.rangeCache[prevDailyRangeOffset];


                // --- HIGH ---
                if (this.HighLineOptions.Enabled || this.HighExtendLineOptions.Enabled || (this.ShowHighLineLabel && IsEdgeMode(this.HighLabelHorizontalMode)))
                {
                    float highYReal = (float)currentWindow.CoordinatesConverter.GetChartY(range.High);
                    bool yVisible = highYReal > top && highYReal < bottom;
                    if (!this.DynamicMode && yVisible && !isMainRangeOutside && this.HighLineOptions.Enabled)
                        gr.DrawLine(this.highLinePen, leftX, highYReal, rightX, highYReal);

                    if (yVisible && needDrawExtendLines && this.HighExtendLineOptions.Enabled)
                        gr.DrawLine(this.highExtendLinePen, leftExtendX, highYReal, rightExtendX, highYReal);

                    bool edgeMode = IsEdgeMode(this.HighLabelHorizontalMode);
                    int maxHighDays = edgeMode ? Math.Max(0, Math.Min(this.HighLastLabelsCount, this.DaysCount)) : int.MaxValue;
                    bool allowHighLabel = this.ShowHighLineLabel && (((yVisible && !isMainRangeOutside)) || edgeMode) && i < maxHighDays;

                    if (allowHighLabel)
                    {
                        float yForLabel = edgeMode ? ClampY(highYReal, args.Rectangle, this.highLineOptions.Width + 1) : highYReal;
                        int offsetForLastDay = (i == 0) ? this.HighLabelOffsetPx : 0;

                        this.DrawBillet(gr, range.High, ref leftX, ref rightX, ref yForLabel,
                            this.CurrentFont, this.highLineOptions, this.highLinePen, this.centerNearSF, args.Rectangle, HighCustomText,
                            this.HighLabelPosition, this.HighLabelHorizontalMode, offsetForLastDay);
                    }
                }

                // --- LOW ---
                if (this.LowLineOptions.Enabled || this.LowExtendLineOptions.Enabled || (this.ShowLowLineLabel && IsEdgeMode(this.LowLabelHorizontalMode)))
                {
                    float lowYReal = (float)currentWindow.CoordinatesConverter.GetChartY(range.Low);
                    bool yVisible = lowYReal > top && lowYReal < bottom;

                    if (!this.DynamicMode && yVisible && !isMainRangeOutside && this.LowLineOptions.Enabled)
                        gr.DrawLine(this.lowLinePen, leftX, lowYReal, rightX, lowYReal);

                    if (yVisible && needDrawExtendLines && this.LowExtendLineOptions.Enabled)
                        gr.DrawLine(this.lowExtendLinePen, leftExtendX, lowYReal, rightExtendX, lowYReal);

                    bool edgeMode = IsEdgeMode(this.LowLabelHorizontalMode);
                    int maxLowDays = edgeMode ? Math.Max(0, Math.Min(this.LowLastLabelsCount, this.DaysCount)) : int.MaxValue;
                    bool allowLowLabel = this.ShowLowLineLabel && (((yVisible && !isMainRangeOutside)) || edgeMode) && i < maxLowDays;

                    if (allowLowLabel)
                    {
                        float yForLabel = edgeMode ? ClampY(lowYReal, args.Rectangle, this.lowLineOptions.Width + 1) : lowYReal;
                        int offsetForLastDay = (i == 0) ? this.LowLabelOffsetPx : 0;
                        this.DrawBillet(gr, range.Low, ref leftX, ref rightX, ref yForLabel,
                            this.CurrentFont, this.lowLineOptions, this.lowLinePen, this.centerNearSF, args.Rectangle, LowCustomText,
                            this.LowLabelPosition, this.LowLabelHorizontalMode, offsetForLastDay);
                    }
                }

                // --- MIDDLE ---
                if (this.MiddleLineOptions.Enabled || this.MiddleExtendLineOptions.Enabled || (this.ShowMiddleLineLabel && IsEdgeMode(this.MiddleLabelHorizontalMode)))
                {
                    float middleYReal = (float)currentWindow.CoordinatesConverter.GetChartY(range.MiddlePrice);
                    bool yVisible = middleYReal > top && middleYReal < bottom;

                    if (!this.DynamicMode && yVisible && !isMainRangeOutside && this.MiddleLineOptions.Enabled)
                        gr.DrawLine(this.middleLinePen, leftX, middleYReal, rightX, middleYReal);

                    if (yVisible && needDrawExtendLines && this.MiddleExtendLineOptions.Enabled)
                        gr.DrawLine(this.middleExtendLinePen, leftExtendX, middleYReal, rightExtendX, middleYReal);

                    bool edgeMode = IsEdgeMode(this.MiddleLabelHorizontalMode);
                    int maxMiddleDays = edgeMode ? Math.Max(0, Math.Min(this.MiddleLastLabelsCount, this.DaysCount)) : int.MaxValue;
                    bool allowMiddleLabel = this.ShowMiddleLineLabel && (((yVisible && !isMainRangeOutside)) || edgeMode) && i < maxMiddleDays;

                    if (allowMiddleLabel)
                    {
                        float yForLabel = edgeMode ? ClampY(middleYReal, args.Rectangle, this.middleLineOptions.Width + 1) : middleYReal;
                        int offsetForLastDay = (i == 0) ? this.MiddleLabelOffsetPx : 0;
                        this.DrawBillet(gr, range.MiddlePrice, ref leftX, ref rightX, ref yForLabel,
                            this.CurrentFont, this.middleLineOptions, this.middleLinePen, this.centerNearSF, args.Rectangle, MiddleCustomText,
                            this.MiddleLabelPosition, this.MiddleLabelHorizontalMode, offsetForLastDay);
                    }
                }

                // --- OPEN ---
                if (this.OpenLineOptions.Enabled || this.OpenExtendLineOptions.Enabled || (this.ShowOpenLineLabel && IsEdgeMode(this.OpenLabelHorizontalMode)))
                {
                    float openYReal = (float)currentWindow.CoordinatesConverter.GetChartY(range.Open);
                    bool yVisible = openYReal > top && openYReal < bottom;

                    if (yVisible && !isMainRangeOutside && this.OpenLineOptions.Enabled)
                        gr.DrawLine(this.openLinePen, leftX, openYReal, rightX, openYReal);

                    if (yVisible && needDrawExtendLines && this.OpenExtendLineOptions.Enabled)
                        gr.DrawLine(this.openExtendLinePen, leftExtendX, openYReal, rightExtendX, openYReal);

                    bool edgeMode = IsEdgeMode(this.OpenLabelHorizontalMode);
                    int maxOpenDays = edgeMode ? Math.Max(0, Math.Min(this.OpenLastLabelsCount, this.DaysCount)) : int.MaxValue;
                    bool allowOpenLabel = this.ShowOpenLineLabel && (((yVisible && !isMainRangeOutside)) || edgeMode) && i < maxOpenDays;

                    if (allowOpenLabel)
                    {
                        float yForLabel = edgeMode ? ClampY(openYReal, args.Rectangle, this.openLineOptions.Width + 1) : openYReal;
                        int offsetForLastDay = (i == 0) ? this.OpenLabelOffsetPx : 0;
                        this.DrawBillet(gr, range.Open, ref leftX, ref rightX, ref yForLabel,
                            this.CurrentFont, this.openLineOptions, this.openLinePen, this.centerNearSF, args.Rectangle, OpenCustomText,
                            this.OpenLabelPosition, this.OpenLabelHorizontalMode, offsetForLastDay);

                    }
                }


                // --- CLOSE ---
                if (this.CloseLineOptions.Enabled || this.CloseExtendLineOptions.Enabled || (this.ShowCloseLineLabel && IsEdgeMode(this.CloseLabelHorizontalMode)))
                {
                    float closeYReal = (float)currentWindow.CoordinatesConverter.GetChartY(range.Close);
                    bool yVisible = closeYReal > top && closeYReal < bottom;

                    if (yVisible && !isMainRangeOutside && this.CloseLineOptions.Enabled)
                        gr.DrawLine(this.closeLinePen, leftX, closeYReal, rightX, closeYReal);

                    if (yVisible && needDrawExtendLines && this.CloseExtendLineOptions.Enabled)
                        gr.DrawLine(this.closeExtendLinePen, leftExtendX, closeYReal, rightExtendX, closeYReal);

                    bool edgeMode = IsEdgeMode(this.CloseLabelHorizontalMode);
                    int maxCloseDays = edgeMode ? Math.Max(0, Math.Min(this.CloseLastLabelsCount, this.DaysCount)) : int.MaxValue;
                    bool allowCloseLabel = this.ShowCloseLineLabel && (((yVisible && !isMainRangeOutside)) || edgeMode) && i < maxCloseDays;

                    if (allowCloseLabel)
                    {
                        float yForLabel = edgeMode ? ClampY(closeYReal, args.Rectangle, this.closeLineOptions.Width + 1) : closeYReal;
                        int offsetForLastDay = (i == 0) ? this.CloseLabelOffsetPx : 0;
                        this.DrawBillet(gr, range.Close, ref leftX, ref rightX, ref yForLabel,
                            this.CurrentFont, this.closeLineOptions, this.closeLinePen, this.centerNearSF, args.Rectangle, CloseCustomText,
                            this.CloseLabelPosition, this.CloseLabelHorizontalMode, offsetForLastDay);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Core.Loggers.Log(ex);
        }
        finally
        {
            gr.SetClip(restoredClip);
        }
    }

    protected override bool OnTryGetMinMax(int fromOffset, int toOffset, out double min, out double max)
    {
        min = Const.DOUBLE_UNDEFINED;
        max = Const.DOUBLE_UNDEFINED;
        if (fromOffset >= this.HistoricalData.Count)
            fromOffset = this.HistoricalData.Count - 1;
        var fromTime = this.HistoricalData[toOffset].TicksLeft;
        var toTime = this.HistoricalData[fromOffset].TicksLeft;

        var minPrice = double.MaxValue;
        var maxPrice = double.MinValue;
        var hasMinPrice = false;
        var hasMaxPrice = false;

        for (int i = 0; i < this.DaysCount; i++)
        {
            if (i >= this.rangeCache.Count)
                break;

            var range = this.rangeCache[i];
            if (range.StartDateTime.Ticks > toTime || range.EndDateTime.Ticks < fromTime)
                continue;

            if (maxPrice < range.High)
            {
                hasMaxPrice = true;
                maxPrice = range.High;
            }

            if (minPrice > range.Low)
            {
                hasMinPrice = true;
                minPrice = range.Low;
            }
        }

        if (hasMinPrice)
            min = minPrice;

        if (hasMaxPrice)
            max = maxPrice;

        return hasMaxPrice || hasMinPrice;
    }

    #endregion Base overrides

    private void CurrentChartOnSettingsChanged(object sender, ChartEventArgs e)
    {
        if (this.DailySessionType == DailyOHLCSessionType.SpecifiedSession &&
            this.specifiedSessionContainerId == CHART_SESSION_CONTAINER_SELECT_ITEM)
        {
            if (this.CurrentChart?.CurrentSessionContainer == null || this.chartSessionContainer == null)
                return;

            if (!this.chartSessionContainer.Equals(this.CurrentChart.CurrentSessionContainer))
            {
                this.chartSessionContainer = this.CurrentChart.CurrentSessionContainer;
                this.Refresh();
            }
        }
    }
    private TradingPlatform.BusinessLayer.TimeZone GetTimeZone()
    {
        return this.CurrentChart?.CurrentTimeZone ?? Core.Instance.TimeUtils.SelectedTimeZone;
    }

    private CustomSession CreateCustomSession(TimeSpan open, TimeSpan close, TimeZoneInfo info)
    {
        var session = new CustomSession
        {
            OpenOffset = open,
            CloseOffset = close,
            IsActive = true,
            Name = "Main",
            Days = Enum.GetValues(typeof(DayOfWeek)).Cast<DayOfWeek>().ToArray(),
            Type = SessionType.Main
        };

        session.RecalculateOpenCloseTime(info);
        return session;
    }

    private void DrawBillet(Graphics gr, double price, ref float leftX, ref float rightX, ref float priceY,
                             Font font, LineOptions lineOptions, Pen pen, StringFormat stringFormat,
                             Rectangle chartRect, string prefix,
                             int labelPosition, int horizontalMode, int offsetPx = 0)
    {
        string label = "";
        if (ShowLabel)
            label = this.ShowPriceForLabel
                ? prefix + this.Symbol.FormatPrice(price)
                : prefix;

        var labelSize = gr.MeasureString(label, font);

        var rect = new RectangleF()
        {
            Height = labelSize.Height,
            Width = labelSize.Width + 5
        };

        if (labelPosition == 1)            // Above
            rect.Y = priceY - labelSize.Height - lineOptions.Width;
        else if (labelPosition == 2)       // Center on the line
            rect.Y = priceY - labelSize.Height / 2f;
        else                               // Below (0)
            rect.Y = priceY - lineOptions.Width + 1;

        switch (horizontalMode)
        {
            case 2: // Centered
                rect.X = (rightX - leftX) / 2f + leftX - rect.Width / 2f + offsetPx;
                break;

            case 1: // Left
                rect.X = leftX + offsetPx;
                break;

            case 0: // Right
                rect.X = rightX - rect.Width + offsetPx;
                break;

            case 3: // Right Edge
                rect.X = chartRect.Right - rect.Width + offsetPx;
                break;

            case 4: // Price Scale
                {
                    var savedClip = gr.Clip;
                    try
                    {
                        gr.ResetClip();
                        rect.X = chartRect.Right - 13 + offsetPx;
                        gr.FillRectangle(pen.Brush, rect);
                        gr.DrawString(label, font, Brushes.White, rect, stringFormat);
                        return;
                    }
                    finally
                    {
                        gr.SetClip(savedClip, System.Drawing.Drawing2D.CombineMode.Replace);
                    }
                }

            default:
                rect.X = rightX - rect.Width + offsetPx; // дефолт
                break;
        }


        gr.FillRectangle(pen.Brush, rect);
        gr.DrawString(label, font, Brushes.White, rect, stringFormat);
    }

    private static bool IsEdgeMode(int horizontalMode) => horizontalMode == 3 || horizontalMode == 4;
    private static float ClampY(float y, Rectangle rect, int padPx)
    {
        float minY = rect.Top + padPx;
        float maxY = rect.Bottom - padPx;
        if (y < minY) return minY;
        if (y > maxY) return maxY;
        return y;
    }


    private static Pen ProcessPen(Pen pen, LineOptions lineOptions)
    {
        if (pen == null)
            pen = new Pen(Color.Empty);

        pen.Color = lineOptions.Color;
        pen.Width = lineOptions.Width;

        try
        {
            switch (lineOptions.LineStyle)
            {
                case LineStyle.Solid:
                    {
                        pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Solid;
                        break;
                    }
                case LineStyle.Dot:
                    {
                        pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dot;
                        break;
                    }
                case LineStyle.Dash:
                    {
                        pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                        break;
                    }
                case LineStyle.DashDot:
                    {
                        pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Custom;
                        float[] dp = new float[] { 2, 4, 7, 4 };
                        pen.DashPattern = dp;
                        break;
                    }
                case LineStyle.Histogramm:
                    {
                        pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Custom;
                        float[] dp = new float[] { 0.25F, 1 };
                        pen.DashPattern = dp;
                        pen.Width = 4;
                        break;
                    }
            }
        }
        catch { }
        return pen;
    }
}

#region Nested

internal class DailyRangeItem
{
    public double High { get; private set; }
    public double Low { get; private set; }
    public double Open { get; private set; }
    public double Close { get; private set; }
    public double MiddlePrice => (this.High + this.Low) / 2;

    public DateTime StartDateTime { get; internal set; }
    public DateTime EndDateTime { get; internal set; }
    public DateTime ExtendStartDateTime { get; internal set; }
    public DateTime ExtendEndDateTime { get; internal set; }

    public DailyRangeItem(DateTime startDateTime, double openPrice)
    {
        this.StartDateTime = startDateTime;
        this.Open = openPrice;

        this.High = double.MinValue;
        this.Low = double.MaxValue;
    }

    public bool TryUpdate(double high, double low, double close)
    {
        bool updated = false;

        if (this.High < high)
        {
            this.High = high;
            updated = true;
        }

        if (this.Low > low)
        {
            this.Low = low;
            updated = true;
        }

        if (this.Close != close)
        {
            this.Close = close;
            updated = true;
        }

        return updated;
    }
    public bool TryUpdate(double close)
    {
        bool updated = false;

        if (this.High < close)
        {
            this.High = close;
            updated = true;
        }

        if (this.Low > close)
        {
            this.Low = close;
            updated = true;
        }

        if (this.Close != close)
        {
            this.Close = close;
            updated = true;
        }
        return updated;
    }
}

public enum DailyOHLCSessionType { AllDay, SpecifiedSession, CustomRange, }
public enum DailyOHLCLabelPosition { Left, Center, Right }

public enum DailyOHLCPeriodType { Daily, Weekly, Monthly }

#endregion Nested
