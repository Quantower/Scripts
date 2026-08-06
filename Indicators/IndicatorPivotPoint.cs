// Copyright QUANTOWER LLC. © 2017-2024. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TradingPlatform.BusinessLayer;
using TradingPlatform.BusinessLayer.Chart;
using TradingPlatform.BusinessLayer.Utils;

namespace TrendIndicators;

public class IndicatorPivotPoint : Indicator, IWatchlistIndicator
{
    #region Parameters
    private const string CURRENT_PERIOD_INPUT_PARAMETER = "Only current period";
    private const string BASE_PERIOD_INPUT_PARAMETER = "Base period";
    private const string RANGE_INPUT_PARAMETER = "Range";
    private const string CALCULATION_METHOD_INPUT_PARAMETER = "Calculation method";
    private const string EXTEND_TO_PERIOD_END_INPUT_PARAMETER = "Extend to period end";
    private const string DISPLAY_SEPARATOR_INPUT_PARAMETER = "Display separator";
    private const string DISPLAY_LABELS_LAST_PERIOD_ONLY_INPUT_PARAMETER = "Display labels last period only";

    private const string PP_LINE_OPTIONS_SI = "PPLineOptions";
    private const string R1_LINE_OPTIONS_SI = "R1LineOptions";
    private const string R2_LINE_OPTIONS_SI = "R2LineOptions";
    private const string R3_LINE_OPTIONS_SI = "R3LineOptions";
    private const string R4_LINE_OPTIONS_SI = "R4LineOptions";
    private const string R5_LINE_OPTIONS_SI = "R5LineOptions";
    private const string R6_LINE_OPTIONS_SI = "R6LineOptions";
    private const string S1_LINE_OPTIONS_SI = "S1LineOptions";
    private const string S2_LINE_OPTIONS_SI = "S2LineOptions";
    private const string S3_LINE_OPTIONS_SI = "S3LineOptions";
    private const string S4_LINE_OPTIONS_SI = "S4LineOptions";
    private const string S5_LINE_OPTIONS_SI = "S5LineOptions";
    private const string S6_LINE_OPTIONS_SI = "S6LineOptions";
    private const string MID_PP_R1_LINE_OPTIONS_SI = "PPR1MidLineOptions";
    private const string MID_R1_R2_LINE_OPTIONS_SI = "R1R2MidLineOptions";
    private const string MID_R2_R3_LINE_OPTIONS_SI = "R2R3MidLineOptions";
    private const string MID_R3_R4_LINE_OPTIONS_SI = "R3R4MidLineOptions";
    private const string MID_R4_R5_LINE_OPTIONS_SI = "R4R5MidLineOptions";
    private const string MID_R5_R6_LINE_OPTIONS_SI = "R5R6MidLineOptions";
    private const string MID_PP_S1_LINE_OPTIONS_SI = "PPS1MidLineOptions";
    private const string MID_S1_S2_LINE_OPTIONS_SI = "S1S2MidLineOptions";
    private const string MID_S2_S3_LINE_OPTIONS_SI = "S2S3MidLineOptions";
    private const string MID_S3_S4_LINE_OPTIONS_SI = "S3S4MidLineOptions";
    private const string MID_S4_S5_LINE_OPTIONS_SI = "S4S5MidLineOptions";
    private const string MID_S5_S6_LINE_OPTIONS_SI = "S5S6MidLineOptions";

    private const string LABEL_PP = "PP";
    private const string LABEL_R1 = "R1";
    private const string LABEL_R2 = "R2";
    private const string LABEL_R3 = "R3";
    private const string LABEL_R4 = "R4";
    private const string LABEL_R5 = "R5";
    private const string LABEL_R6 = "R6";

    private const string LABEL_S1 = "S1";
    private const string LABEL_S2 = "S2";
    private const string LABEL_S3 = "S3";
    private const string LABEL_S4 = "S4";
    private const string LABEL_S5 = "S5";
    private const string LABEL_S6 = "S6";

    private const string LABEL_MID_PP_R1 = "PP_R1_MID";
    private const string LABEL_MID_R1_R2 = "R1_R2_MID";
    private const string LABEL_MID_R2_R3 = "R2_R3_MID";
    private const string LABEL_MID_R3_R4 = "R3_R4_MID";
    private const string LABEL_MID_R4_R5 = "R4_R5_MID";
    private const string LABEL_MID_R5_R6 = "R5_R6_MID";

    private const string LABEL_MID_PP_S1 = "PP_S1_MID";
    private const string LABEL_MID_S1_S2 = "S1_S2_MID";
    private const string LABEL_MID_S2_S3 = "S2_S3_MID";
    private const string LABEL_MID_S3_S4 = "S3_S4_MID";
    private const string LABEL_MID_S4_S5 = "S4_S5_MID";
    private const string LABEL_MID_S5_S6 = "S5_S6_MID";

    private const int MIN_HISTORY_COUNT = 3;

    public bool ShowLabels = true;
    private readonly Font labelFont = new Font("Verdana", 10, GraphicsUnit.Pixel);
    private readonly StringFormat labelSF = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
    private const int LabelPadX = 6;
    private const int LabelPadY = 2;
    private const int LabelGapX = 4;
    public bool ShowPriceInLabels = true;
    public bool DrawLastPeriodOnly = true;
    public bool ExtendToPeriodEnd = false;
    public bool DisplaySeparator = false;

    public bool OnlyCurrentPeriod = false;

    private const string CHART_SESSION_CONTAINER_SELECT_ITEM = "Chart session";
    private const string SESSION_TEMPLATE_NAME_SI = "sessionsTemplate";
    private const string CUSTOM_OPEN_SESSION_NAME_SI = "Start time";
    private const string CUSTOM_CLOSE_SESSION_NAME_SI = "End time";

    private string specifiedSessionContainerId = CHART_SESSION_CONTAINER_SELECT_ITEM;

    private ISessionsContainer customSessionContainer;
    private ISessionsContainer fullDaySessionContainer;
    private ISessionsContainer selectedSessionContainer;
    private ISessionsContainer chartSessionContainer;

    private ISessionsContainer SessionContainer
    {
        get
        {
            switch (this.DailySessionType)
            {
                case DailySessionType.SpecifiedSession:
                    {
                        if (this.specifiedSessionContainerId == CHART_SESSION_CONTAINER_SELECT_ITEM)
                            return this.CurrentChart?.CurrentSessionContainer ?? this.fullDaySessionContainer;

                        return this.selectedSessionContainer ?? this.fullDaySessionContainer;
                    }

                case DailySessionType.CustomRange:
                    return this.customSessionContainer ?? this.fullDaySessionContainer;

                case DailySessionType.AllDay:
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

            return DateTime.SpecifyKind(this.customRangeStartTime, DateTimeKind.Local);
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

            return DateTime.SpecifyKind(this.customRangeEndTime, DateTimeKind.Local);
        }
        set => this.customRangeEndTime = value;
    }
    private DateTime customRangeEndTime;

    public BasePeriod BasePeriod = BasePeriod.Day;
    public int PeriodValue = 1;
    public CalculationMethod IndicatorCalculationMethod = CalculationMethod.Classic;
    public DailySessionType DailySessionType = DailySessionType.AllDay;
    public LineOptions PPLineOptions
    {
        get => this.ppLineOptions;
        private set => AssignLineOptions(ref this.ppLineOptions, ref this.ppLinePen, value);
    }
    private LineOptions ppLineOptions;
    private Pen ppLinePen;

    public LineOptions R1LineOptions
    {
        get => this.r1LineOptions;
        private set => AssignLineOptions(ref this.r1LineOptions, ref this.r1LinePen, value);
    }
    private LineOptions r1LineOptions;
    private Pen r1LinePen;

    public LineOptions R2LineOptions
    {
        get => this.r2LineOptions;
        private set => AssignLineOptions(ref this.r2LineOptions, ref this.r2LinePen, value);
    }
    private LineOptions r2LineOptions;
    private Pen r2LinePen;

    public LineOptions R3LineOptions
    {
        get => this.r3LineOptions;
        private set => AssignLineOptions(ref this.r3LineOptions, ref this.r3LinePen, value);
    }
    private LineOptions r3LineOptions;
    private Pen r3LinePen;

    public LineOptions R4LineOptions
    {
        get => this.r4LineOptions;
        private set => AssignLineOptions(ref this.r4LineOptions, ref this.r4LinePen, value);
    }
    private LineOptions r4LineOptions;
    private Pen r4LinePen;

    public LineOptions R5LineOptions
    {
        get => this.r5LineOptions;
        private set => AssignLineOptions(ref this.r5LineOptions, ref this.r5LinePen, value);
    }
    private LineOptions r5LineOptions;
    private Pen r5LinePen;

    public LineOptions R6LineOptions
    {
        get => this.r6LineOptions;
        private set => AssignLineOptions(ref this.r6LineOptions, ref this.r6LinePen, value);
    }
    private LineOptions r6LineOptions;
    private Pen r6LinePen;

    public LineOptions S1LineOptions
    {
        get => this.s1LineOptions;
        private set => AssignLineOptions(ref this.s1LineOptions, ref this.s1LinePen, value);
    }
    private LineOptions s1LineOptions;
    private Pen s1LinePen;

    public LineOptions S2LineOptions
    {
        get => this.s2LineOptions;
        private set => AssignLineOptions(ref this.s2LineOptions, ref this.s2LinePen, value);
    }
    private LineOptions s2LineOptions;
    private Pen s2LinePen;

    public LineOptions S3LineOptions
    {
        get => this.s3LineOptions;
        private set => AssignLineOptions(ref this.s3LineOptions, ref this.s3LinePen, value);
    }
    private LineOptions s3LineOptions;
    private Pen s3LinePen;

    public LineOptions S4LineOptions
    {
        get => this.s4LineOptions;
        private set => AssignLineOptions(ref this.s4LineOptions, ref this.s4LinePen, value);
    }
    private LineOptions s4LineOptions;
    private Pen s4LinePen;

    public LineOptions S5LineOptions
    {
        get => this.s5LineOptions;
        private set => AssignLineOptions(ref this.s5LineOptions, ref this.s5LinePen, value);
    }
    private LineOptions s5LineOptions;
    private Pen s5LinePen;

    public LineOptions S6LineOptions
    {
        get => this.s6LineOptions;
        private set => AssignLineOptions(ref this.s6LineOptions, ref this.s6LinePen, value);
    }
    private LineOptions s6LineOptions;
    private Pen s6LinePen;

    public LineOptions PPR1MidLineOptions
    {
        get => this.ppR1MidLineOptions;
        private set => AssignLineOptions(ref this.ppR1MidLineOptions, ref this.ppR1MidLinePen, value);
    }
    private LineOptions ppR1MidLineOptions;
    private Pen ppR1MidLinePen;

    public LineOptions R1R2MidLineOptions
    {
        get => this.r1R2MidLineOptions;
        private set => AssignLineOptions(ref this.r1R2MidLineOptions, ref this.r1R2MidLinePen, value);
    }
    private LineOptions r1R2MidLineOptions;
    private Pen r1R2MidLinePen;

    public LineOptions R2R3MidLineOptions
    {
        get => this.r2R3MidLineOptions;
        private set => AssignLineOptions(ref this.r2R3MidLineOptions, ref this.r2R3MidLinePen, value);
    }
    private LineOptions r2R3MidLineOptions;
    private Pen r2R3MidLinePen;

    public LineOptions R3R4MidLineOptions
    {
        get => this.r3R4MidLineOptions;
        private set => AssignLineOptions(ref this.r3R4MidLineOptions, ref this.r3R4MidLinePen, value);
    }
    private LineOptions r3R4MidLineOptions;
    private Pen r3R4MidLinePen;

    public LineOptions R4R5MidLineOptions
    {
        get => this.r4R5MidLineOptions;
        private set => AssignLineOptions(ref this.r4R5MidLineOptions, ref this.r4R5MidLinePen, value);
    }
    private LineOptions r4R5MidLineOptions;
    private Pen r4R5MidLinePen;

    public LineOptions R5R6MidLineOptions
    {
        get => this.r5R6MidLineOptions;
        private set => AssignLineOptions(ref this.r5R6MidLineOptions, ref this.r5R6MidLinePen, value);
    }
    private LineOptions r5R6MidLineOptions;
    private Pen r5R6MidLinePen;

    public LineOptions PPS1MidLineOptions
    {
        get => this.ppS1MidLineOptions;
        private set => AssignLineOptions(ref this.ppS1MidLineOptions, ref this.ppS1MidLinePen, value);
    }
    private LineOptions ppS1MidLineOptions;
    private Pen ppS1MidLinePen;

    public LineOptions S1S2MidLineOptions
    {
        get => this.s1S2MidLineOptions;
        private set => AssignLineOptions(ref this.s1S2MidLineOptions, ref this.s1S2MidLinePen, value);
    }
    private LineOptions s1S2MidLineOptions;
    private Pen s1S2MidLinePen;

    public LineOptions S2S3MidLineOptions
    {
        get => this.s2S3MidLineOptions;
        private set => AssignLineOptions(ref this.s2S3MidLineOptions, ref this.s2S3MidLinePen, value);
    }
    private LineOptions s2S3MidLineOptions;
    private Pen s2S3MidLinePen;

    public LineOptions S3S4MidLineOptions
    {
        get => this.s3S4MidLineOptions;
        private set => AssignLineOptions(ref this.s3S4MidLineOptions, ref this.s3S4MidLinePen, value);
    }
    private LineOptions s3S4MidLineOptions;
    private Pen s3S4MidLinePen;

    public LineOptions S4S5MidLineOptions
    {
        get => this.s4S5MidLineOptions;
        private set => AssignLineOptions(ref this.s4S5MidLineOptions, ref this.s4S5MidLinePen, value);
    }
    private LineOptions s4S5MidLineOptions;
    private Pen s4S5MidLinePen;

    public LineOptions S5S6MidLineOptions
    {
        get => this.s5S6MidLineOptions;
        private set => AssignLineOptions(ref this.s5S6MidLineOptions, ref this.s5S6MidLinePen, value);
    }
    private LineOptions s5S6MidLineOptions;
    private Pen s5S6MidLinePen;
    private Task loadingTask;


    private CancellationTokenSource cancellationSource;
    private HistoricalData history;
    private string formattedPeriod;
    private IndicatorState state;
    private IndicatorState State
    {
        get => this.state;
        set
        {
            this.state = value;

            switch (value)
            {
                case IndicatorState.IncorrectPeriod:
                    Core.Loggers.Log($"{this.Name}: Incorrect period. The 'Pivot point' period should be greater or equal than chart period.", LoggingLevel.Error);
                    break;
                case IndicatorState.OneTickNotAllowed:
                    Core.Loggers.Log($"{this.Name}: Incorrect chart period. The 'Pivot point' does not support chart with '1 Tick' aggregation.", LoggingLevel.Error);
                    break;
            }
        }
    }
    private List<PivotPointCalculationResponce> pivotPeriods;

    //private readonly StringFormat centerCenterSF;
    //private readonly Brush messageBrush;
    //private readonly Font font;

    public override string ShortName
    {
        get
        {
            switch (this.State)
            {
                case IndicatorState.Loading:
                    return $"PP (Loading)";
                case IndicatorState.NoData:
                    return $"PP (No data)";
                default:
                    return $"PP ({this.GetFormattedPeriod(this.BasePeriod, this.PeriodValue)}: {this.IndicatorCalculationMethod})";
            }
        }
    }
    public int MinHistoryDepths => 1;

    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorPivotPoint.cs";

    #endregion Parameters

    public IndicatorPivotPoint()
            : base()
    {
        this.Name = "Pivot Point";
        this.SeparateWindow = false;

        this.PPLineOptions = CreateLineOptions(
            enabled: true,
            color: Color.Gray,
            style: LineStyle.Solid,
            width: 1);

        this.R1LineOptions = CreateLineOptions(
            enabled: true,
            color: Color.Red,
            style: LineStyle.Solid,
            width: 1);

        this.R2LineOptions = CreateLineOptions(
            enabled: true,
            color: Color.Red,
            style: LineStyle.Solid,
            width: 1);

        this.R3LineOptions = CreateLineOptions(
            enabled: true,
            color: Color.Red,
            style: LineStyle.Solid,
            width: 1);

        this.R4LineOptions = CreateLineOptions(
            enabled: true,
            color: Color.Red,
            style: LineStyle.Solid,
            width: 1);

        this.R5LineOptions = CreateLineOptions(
            enabled: true,
            color: Color.Red,
            style: LineStyle.Solid,
            width: 1);

        this.R6LineOptions = CreateLineOptions(
            enabled: true,
            color: Color.Red,
            style: LineStyle.Solid,
            width: 1);

        this.S1LineOptions = CreateLineOptions(
            enabled: true,
            color: Color.DodgerBlue,
            style: LineStyle.Solid,
            width: 1);

        this.S2LineOptions = CreateLineOptions(
            enabled: true,
            color: Color.DodgerBlue,
            style: LineStyle.Solid,
            width: 1);

        this.S3LineOptions = CreateLineOptions(
            enabled: true,
            color: Color.DodgerBlue,
            style: LineStyle.Solid,
            width: 1);

        this.S4LineOptions = CreateLineOptions(
            enabled: true,
            color: Color.DodgerBlue,
            style: LineStyle.Solid,
            width: 1);

        this.S5LineOptions = CreateLineOptions(
            enabled: true,
            color: Color.DodgerBlue,
            style: LineStyle.Solid,
            width: 1);

        this.S6LineOptions = CreateLineOptions(
            enabled: true,
            color: Color.DodgerBlue,
            style: LineStyle.Solid,
            width: 1);

        this.PPR1MidLineOptions = CreateLineOptions(
            enabled: false,
            color: Color.FromArgb(128, 128, 128, 128),
            style: LineStyle.Dash,
            width: 1);

        this.R1R2MidLineOptions = CreateLineOptions(
            enabled: false,
            color: Color.FromArgb(128, 128, 128, 128),
            style: LineStyle.Dash,
            width: 1);

        this.R2R3MidLineOptions = CreateLineOptions(
            enabled: false,
            color: Color.FromArgb(128, 128, 128, 128),
            style: LineStyle.Dash,
            width: 1);

        this.R3R4MidLineOptions = CreateLineOptions(
            enabled: false,
            color: Color.FromArgb(128, 128, 128, 128),
            style: LineStyle.Dash,
            width: 1);

        this.R4R5MidLineOptions = CreateLineOptions(
            enabled: false,
            color: Color.FromArgb(128, 128, 128, 128),
            style: LineStyle.Dash,
            width: 1);

        this.R5R6MidLineOptions = CreateLineOptions(
            enabled: false,
            color: Color.FromArgb(128, 128, 128, 128),
            style: LineStyle.Dash,
            width: 1);

        this.PPS1MidLineOptions = CreateLineOptions(
            enabled: false,
            color: Color.FromArgb(128, 128, 128, 128),
            style: LineStyle.Dash,
            width: 1);

        this.S1S2MidLineOptions = CreateLineOptions(
            enabled: false,
            color: Color.FromArgb(128, 128, 128, 128),
            style: LineStyle.Dash,
            width: 1);

        this.S2S3MidLineOptions = CreateLineOptions(
            enabled: false,
            color: Color.FromArgb(128, 128, 128, 128),
            style: LineStyle.Dash,
            width: 1);

        this.S3S4MidLineOptions = CreateLineOptions(
            enabled: false,
            color: Color.FromArgb(128, 128, 128, 128),
            style: LineStyle.Dash,
            width: 1);

        this.S4S5MidLineOptions = CreateLineOptions(
            enabled: false,
            color: Color.FromArgb(128, 128, 128, 128),
            style: LineStyle.Dash,
            width: 1);

        this.S5S6MidLineOptions = CreateLineOptions(
            enabled: false,
            color: Color.FromArgb(128, 128, 128, 128),
            style: LineStyle.Dash,
            width: 1);

        this.InitializeLabels();

        //this.font = new Font("Tahoma", 8, FontStyle.Bold);
        //this.centerCenterSF = new StringFormat() { LineAlignment = StringAlignment.Center, Alignment = StringAlignment.Center };
        //this.messageBrush = new SolidBrush(Color.DodgerBlue);
    }

    #region Overrides
    protected override void OnInit()
    {
        base.OnInit();

        this.ResetLabels();
        this.AbortPreviousTask();
        this.pivotPeriods = new List<PivotPointCalculationResponce>();

        this.chartSessionContainer = this.CurrentChart?.CurrentSessionContainer;

        if (this.CurrentChart != null)
            this.CurrentChart.SettingsChanged += this.CurrentChartOnSettingsChanged;

        var token = this.cancellationSource.Token;

        if (this.Symbol == null)
            return;

        var timeZone = this.GetTimeZone();

        this.fullDaySessionContainer = new CustomSessionsContainer(
            "FullDaySession",
            timeZone,
            new[]
            {
                this.CreateCustomSession(TimeSpan.Zero, TimeSpan.Zero, timeZone.TimeZoneInfo)
            });

        switch (this.DailySessionType)
        {
            case DailySessionType.CustomRange:
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

            case DailySessionType.SpecifiedSession:
                {
                    if (!string.IsNullOrEmpty(this.specifiedSessionContainerId) &&
                        this.specifiedSessionContainerId != CHART_SESSION_CONTAINER_SELECT_ITEM)
                    {
                        this.selectedSessionContainer = Core.Instance.CustomSessions[this.specifiedSessionContainerId];
                    }
                    break;
                }

            case DailySessionType.AllDay:
            default:
                break;
        }

        var inputPeriod = new Period(this.BasePeriod, this.PeriodValue);
        this.formattedPeriod = this.GetFormattedPeriod(this.BasePeriod, this.PeriodValue);

        this.State = IndicatorState.Ready;

        if (this.HistoricalData.Aggregation is HistoryAggregationTick)
        {
            this.State = IndicatorState.OneTickNotAllowed;
            return;
        }

        HistoryType currHistoryType = HistoryType.Last;

        if (this.HistoricalData.Aggregation is HistoryAggregationTime historyAggregationTime)
        {
            currHistoryType = historyAggregationTime.HistoryType;
            if (inputPeriod.Ticks < historyAggregationTime.Period.Duration.Ticks)
            {
                this.State = IndicatorState.IncorrectPeriod;
                return;
            }
        }

        if (this.HistoricalData.Aggregation is HistoryAggregationTickBars historyAggregationTickBars)
        {
            currHistoryType = historyAggregationTickBars.HistoryType;
            if (inputPeriod.Ticks < historyAggregationTickBars.TicksCount)
            {
                this.State = IndicatorState.IncorrectPeriod;
                return;
            }
        }

        this.loadingTask = Task.Factory.StartNew(() =>
        {
            if (token.IsCancellationRequested)
                return;

            this.State = IndicatorState.Loading;
            var fromTime = this.HistoricalData.FromTime.Add(-2 * inputPeriod.Duration);

            var coefficient = 0;
            var prevHistoryCount = -1;
            var needReload = true;

            while (needReload)
            {
                needReload = false;

                if (token.IsCancellationRequested)
                    return;

                coefficient += 1;
                var minimumRangeInTicks = inputPeriod.Ticks * MIN_HISTORY_COUNT * coefficient;

                if (Core.TimeUtils.DateTimeUtcNow.Ticks - fromTime.Ticks < minimumRangeInTicks)
                    fromTime = Core.TimeUtils.DateTimeUtcNow - new TimeSpan(minimumRangeInTicks);

                var aggregation = new HistoryAggregationTime(inputPeriod, currHistoryType);

                if (this.BasePeriod == BasePeriod.Day && this.SessionContainer != null)
                    aggregation.SessionsContainer = this.SessionContainer;

                this.history = this.Symbol.GetHistory(new HistoryRequestParameters()
                {
                    Symbol = this.Symbol,
                    FromTime = fromTime,
                    CancellationToken = token,
                    Aggregation = aggregation,
                });
                if (token.IsCancellationRequested || prevHistoryCount == this.history.Count)
                {
                    this.State = IndicatorState.NoData;
                }
                else if (this.IsValidLoadedHistory(this.history))
                {
                    this.history.NewHistoryItem += this.History_NewHistoryItem;

                    if (this.OnlyCurrentPeriod)
                        this.CalculateLastPeriod();
                    else
                        this.CalculateAllIndicator();

                    this.State = IndicatorState.Calculation;
                }
                else
                {
                    prevHistoryCount = this.history.Count;
                    needReload = true;
                    this.history.Dispose();
                }
            }
        });
    }
    protected override void OnUpdate(UpdateArgs args)
    {
        base.OnUpdate(args);
    }
    protected override void OnClear()
    {
        base.OnClear();

        this.AbortPreviousTask();
        this.pivotPeriods?.Clear();
        this.ResetLabels();

        this.customSessionContainer = null;
        this.fullDaySessionContainer = null;
        this.selectedSessionContainer = null;

        if (this.CurrentChart != null)
            this.CurrentChart.SettingsChanged -= this.CurrentChartOnSettingsChanged;
    }
    public override void Dispose()
    {
        this.ppLinePen?.Dispose();
        this.r1LinePen?.Dispose();
        this.r2LinePen?.Dispose();
        this.r3LinePen?.Dispose();
        this.r4LinePen?.Dispose();
        this.r5LinePen?.Dispose();
        this.r6LinePen?.Dispose();
        this.s1LinePen?.Dispose();
        this.s2LinePen?.Dispose();
        this.s3LinePen?.Dispose();
        this.s4LinePen?.Dispose();
        this.s5LinePen?.Dispose();
        this.s6LinePen?.Dispose();
        this.ppR1MidLinePen?.Dispose();
        this.r1R2MidLinePen?.Dispose();
        this.r2R3MidLinePen?.Dispose();
        this.r3R4MidLinePen?.Dispose();
        this.r4R5MidLinePen?.Dispose();
        this.r5R6MidLinePen?.Dispose();
        this.ppS1MidLinePen?.Dispose();
        this.s1S2MidLinePen?.Dispose();
        this.s2S3MidLinePen?.Dispose();
        this.s3S4MidLinePen?.Dispose();
        this.s4S5MidLinePen?.Dispose();
        this.s5S6MidLinePen?.Dispose();
        this.labelFont?.Dispose();
        this.labelSF?.Dispose();

        base.Dispose();
    }

    public override IList<SettingItem> Settings
    {
        get
        {
            var settings = base.Settings;

            var hour = new SelectItem("Hour", BasePeriod.Hour);
            var day = new SelectItem("Day", BasePeriod.Day);
            var week = new SelectItem("Week", BasePeriod.Week);
            var month = new SelectItem("Month", BasePeriod.Month);

            var classic = new SelectItem("Classic", CalculationMethod.Classic);
            var camarilla = new SelectItem("Camarilla", CalculationMethod.Camarilla);
            var fibonacci = new SelectItem("Fibonacci", CalculationMethod.Fibonacci);
            var woodie = new SelectItem("Woodie", CalculationMethod.Woodie);
            var demark = new SelectItem("DeMark", CalculationMethod.DeMark);

            var defaultSeparator = settings.FirstOrDefault()?.SeparatorGroup;
            var allDay = new SelectItem("All day", DailySessionType.AllDay);
            var specifiedSession = new SelectItem("Specified session", DailySessionType.SpecifiedSession);
            var customRange = new SelectItem("Custom range", DailySessionType.CustomRange);

            var dailyPeriodTypeRelation = new SettingItemRelationVisibility(BASE_PERIOD_INPUT_PARAMETER, day);
            // Only current period
            settings.Add(new SettingItemBoolean(CURRENT_PERIOD_INPUT_PARAMETER, this.OnlyCurrentPeriod, 0)
            {
                SeparatorGroup = defaultSeparator,
                Text = loc._(CURRENT_PERIOD_INPUT_PARAMETER)
            });

            // Base period
            settings.Add(new SettingItemSelectorLocalized(
                BASE_PERIOD_INPUT_PARAMETER,
                new SelectItem(BASE_PERIOD_INPUT_PARAMETER, this.BasePeriod),
                new List<SelectItem> { hour, day, week, month })
            {
                SeparatorGroup = defaultSeparator,
                Text = loc._(BASE_PERIOD_INPUT_PARAMETER),
                SortIndex = 5,
                ValueChangingBehavior = SettingItemValueChangingBehavior.WithConfirmation
            });
            settings.Add(new SettingItemSelectorLocalized("Session type", new SelectItem("Session type", this.DailySessionType), new List<SelectItem>
                             {
                                 allDay,
                                 specifiedSession,
                                 customRange
                             })
            {
                SeparatorGroup = defaultSeparator,
                Text = "Session type",
                SortIndex = 10,
                Relation = dailyPeriodTypeRelation,
            });
            //
            var customRangeSimRelation = new SettingItemRelationVisibility("Session type", customRange);
            var customRangeMultRelation = new SettingItemMultipleRelation(dailyPeriodTypeRelation, customRangeSimRelation);
            var defaultItem = new SelectItem(CHART_SESSION_CONTAINER_SELECT_ITEM);
            var items = new List<SelectItem> { defaultItem };
            items.AddRange(Core.Instance.CustomSessions.Select(s => new SelectItem(s.Name, s.Id)));

            var selectedItem = items.FirstOrDefault(i => Equals(i.Value, this.specifiedSessionContainerId)) ?? items.First();

            settings.Add(new SettingItemSelectorLocalized(SESSION_TEMPLATE_NAME_SI, selectedItem, items, 20)
            {
                SeparatorGroup = defaultSeparator,
                Text = loc._("Sessions template"),
                Relation = new SettingItemRelationVisibility("Session type", specifiedSession)
            });
            settings.Add(new SettingItemDateTime(CUSTOM_OPEN_SESSION_NAME_SI, this.CustomRangeStartTime, 20)
            {
                SeparatorGroup = defaultSeparator,
                Text = loc._("Start time"),
                Format = DatePickerFormat.Time,
                ValueChangingBehavior = SettingItemValueChangingBehavior.WithConfirmation,
                Relation = new SettingItemRelationVisibility("Session type", customRange)
            });

            settings.Add(new SettingItemDateTime(CUSTOM_CLOSE_SESSION_NAME_SI, this.CustomRangeEndTime, 20)
            {
                SeparatorGroup = defaultSeparator,
                Text = loc._("End time"),
                Format = DatePickerFormat.Time,
                ValueChangingBehavior = SettingItemValueChangingBehavior.WithConfirmation,
                Relation = new SettingItemRelationVisibility("Session type", customRange)
            });
            settings.Add(new SettingItemInteger(RANGE_INPUT_PARAMETER, this.PeriodValue, 10)
            {
                SeparatorGroup = defaultSeparator,
                Text = loc._(RANGE_INPUT_PARAMETER),
                Minimum = 1,
                ValueChangingBehavior = SettingItemValueChangingBehavior.WithConfirmation
            });

            // Calculation method
            settings.Add(new SettingItemSelectorLocalized(
                CALCULATION_METHOD_INPUT_PARAMETER,
                new SelectItem(CALCULATION_METHOD_INPUT_PARAMETER, this.IndicatorCalculationMethod),
                new List<SelectItem> { classic, camarilla, fibonacci, woodie, demark })
            {
                SeparatorGroup = defaultSeparator,
                Text = loc._(CALCULATION_METHOD_INPUT_PARAMETER),
                SortIndex = 20
            });
            var pivotLineSeparator = new SettingItemSeparatorGroup("Pivot line", -999);
            var resistanceLineSeparator = new SettingItemSeparatorGroup("Resistance lines", -998);
            var supportLineSeparator = new SettingItemSeparatorGroup("Support lines", -997);
            var midResistanceLineSeparator = new SettingItemSeparatorGroup("Intermediate resistance lines", -996);
            var midSupportLineSeparator = new SettingItemSeparatorGroup("Intermediate support lines", -995);

            settings.Add(new SettingItemLineOptions(PP_LINE_OPTIONS_SI, this.PPLineOptions, 90)
            {
                SeparatorGroup = pivotLineSeparator,
                Text = "PP line",
                ExcludedStyles = new[] { LineStyle.Points, LineStyle.Histogramm }
            });

            settings.Add(new SettingItemLineOptions(R1_LINE_OPTIONS_SI, this.R1LineOptions, 91)
            {
                SeparatorGroup = resistanceLineSeparator,
                Text = "R1 line",
                ExcludedStyles = new[] { LineStyle.Points, LineStyle.Histogramm }
            });

            settings.Add(new SettingItemLineOptions(R2_LINE_OPTIONS_SI, this.R2LineOptions, 92)
            {
                SeparatorGroup = resistanceLineSeparator,
                Text = "R2 line",
                ExcludedStyles = new[] { LineStyle.Points, LineStyle.Histogramm }
            });

            settings.Add(new SettingItemLineOptions(R3_LINE_OPTIONS_SI, this.R3LineOptions, 93)
            {
                SeparatorGroup = resistanceLineSeparator,
                Text = "R3 line",
                ExcludedStyles = new[] { LineStyle.Points, LineStyle.Histogramm }
            });

            settings.Add(new SettingItemLineOptions(R4_LINE_OPTIONS_SI, this.R4LineOptions, 94)
            {
                SeparatorGroup = resistanceLineSeparator,
                Text = "R4 line",
                ExcludedStyles = new[] { LineStyle.Points, LineStyle.Histogramm }
            });

            settings.Add(new SettingItemLineOptions(R5_LINE_OPTIONS_SI, this.R5LineOptions, 95)
            {
                SeparatorGroup = resistanceLineSeparator,
                Text = "R5 line",
                ExcludedStyles = new[] { LineStyle.Points, LineStyle.Histogramm }
            });

            settings.Add(new SettingItemLineOptions(R6_LINE_OPTIONS_SI, this.R6LineOptions, 96)
            {
                SeparatorGroup = resistanceLineSeparator,
                Text = "R6 line",
                ExcludedStyles = new[] { LineStyle.Points, LineStyle.Histogramm }
            });

            settings.Add(new SettingItemLineOptions(S1_LINE_OPTIONS_SI, this.S1LineOptions, 97)
            {
                SeparatorGroup = supportLineSeparator,
                Text = "S1 line",
                ExcludedStyles = new[] { LineStyle.Points, LineStyle.Histogramm }
            });

            settings.Add(new SettingItemLineOptions(S2_LINE_OPTIONS_SI, this.S2LineOptions, 98)
            {
                SeparatorGroup = supportLineSeparator,
                Text = "S2 line",
                ExcludedStyles = new[] { LineStyle.Points, LineStyle.Histogramm }
            });

            settings.Add(new SettingItemLineOptions(S3_LINE_OPTIONS_SI, this.S3LineOptions, 99)
            {
                SeparatorGroup = supportLineSeparator,
                Text = "S3 line",
                ExcludedStyles = new[] { LineStyle.Points, LineStyle.Histogramm }
            });

            settings.Add(new SettingItemLineOptions(S4_LINE_OPTIONS_SI, this.S4LineOptions, 100)
            {
                SeparatorGroup = supportLineSeparator,
                Text = "S4 line",
                ExcludedStyles = new[] { LineStyle.Points, LineStyle.Histogramm }
            });

            settings.Add(new SettingItemLineOptions(S5_LINE_OPTIONS_SI, this.S5LineOptions, 101)
            {
                SeparatorGroup = supportLineSeparator,
                Text = "S5 line",
                ExcludedStyles = new[] { LineStyle.Points, LineStyle.Histogramm }
            });

            settings.Add(new SettingItemLineOptions(S6_LINE_OPTIONS_SI, this.S6LineOptions, 102)
            {
                SeparatorGroup = supportLineSeparator,
                Text = "S6 line",
                ExcludedStyles = new[] { LineStyle.Points, LineStyle.Histogramm }
            });

            settings.Add(new SettingItemLineOptions(MID_PP_R1_LINE_OPTIONS_SI, this.PPR1MidLineOptions, 103)
            {
                SeparatorGroup = midResistanceLineSeparator,
                Text = "PP–R1 line",
                ExcludedStyles = new[] { LineStyle.Points, LineStyle.Histogramm }
            });

            settings.Add(new SettingItemLineOptions(MID_R1_R2_LINE_OPTIONS_SI, this.R1R2MidLineOptions, 104)
            {
                SeparatorGroup = midResistanceLineSeparator,
                Text = "R1–R2 line",
                ExcludedStyles = new[] { LineStyle.Points, LineStyle.Histogramm }
            });

            settings.Add(new SettingItemLineOptions(MID_R2_R3_LINE_OPTIONS_SI, this.R2R3MidLineOptions, 105)
            {
                SeparatorGroup = midResistanceLineSeparator,
                Text = "R2–R3 line",
                ExcludedStyles = new[] { LineStyle.Points, LineStyle.Histogramm }
            });

            settings.Add(new SettingItemLineOptions(MID_R3_R4_LINE_OPTIONS_SI, this.R3R4MidLineOptions, 106)
            {
                SeparatorGroup = midResistanceLineSeparator,
                Text = "R3–R4 line",
                ExcludedStyles = new[] { LineStyle.Points, LineStyle.Histogramm }
            });

            settings.Add(new SettingItemLineOptions(MID_R4_R5_LINE_OPTIONS_SI, this.R4R5MidLineOptions, 107)
            {
                SeparatorGroup = midResistanceLineSeparator,
                Text = "R4–R5 line",
                ExcludedStyles = new[] { LineStyle.Points, LineStyle.Histogramm }
            });

            settings.Add(new SettingItemLineOptions(MID_R5_R6_LINE_OPTIONS_SI, this.R5R6MidLineOptions, 108)
            {
                SeparatorGroup = midResistanceLineSeparator,
                Text = "R5–R6 line",
                ExcludedStyles = new[] { LineStyle.Points, LineStyle.Histogramm }
            });

            settings.Add(new SettingItemLineOptions(MID_PP_S1_LINE_OPTIONS_SI, this.PPS1MidLineOptions, 109)
            {
                SeparatorGroup = midSupportLineSeparator,
                Text = "PP–S1 line",
                ExcludedStyles = new[] { LineStyle.Points, LineStyle.Histogramm }
            });

            settings.Add(new SettingItemLineOptions(MID_S1_S2_LINE_OPTIONS_SI, this.S1S2MidLineOptions, 110)
            {
                SeparatorGroup = midSupportLineSeparator,
                Text = "S1–S2 line",
                ExcludedStyles = new[] { LineStyle.Points, LineStyle.Histogramm }
            });

            settings.Add(new SettingItemLineOptions(MID_S2_S3_LINE_OPTIONS_SI, this.S2S3MidLineOptions, 111)
            {
                SeparatorGroup = midSupportLineSeparator,
                Text = "S2–S3 line",
                ExcludedStyles = new[] { LineStyle.Points, LineStyle.Histogramm }
            });

            settings.Add(new SettingItemLineOptions(MID_S3_S4_LINE_OPTIONS_SI, this.S3S4MidLineOptions, 112)
            {
                SeparatorGroup = midSupportLineSeparator,
                Text = "S3–S4 line",
                ExcludedStyles = new[] { LineStyle.Points, LineStyle.Histogramm }
            });

            settings.Add(new SettingItemLineOptions(MID_S4_S5_LINE_OPTIONS_SI, this.S4S5MidLineOptions, 113)
            {
                SeparatorGroup = midSupportLineSeparator,
                Text = "S4–S5 line",
                ExcludedStyles = new[] { LineStyle.Points, LineStyle.Histogramm }
            });

            settings.Add(new SettingItemLineOptions(MID_S5_S6_LINE_OPTIONS_SI, this.S5S6MidLineOptions, 114)
            {
                SeparatorGroup = midSupportLineSeparator,
                Text = "S5–S6 line",
                ExcludedStyles = new[] { LineStyle.Points, LineStyle.Histogramm }
            });

            settings.Add(new SettingItemBoolean("Display labels", this.ShowLabels, 94)
            {
                SeparatorGroup = defaultSeparator,
                Text = "Display labels"
            });
            settings.Add(new SettingItemBoolean("Display price", this.ShowPriceInLabels, 95)
            {
                SeparatorGroup = defaultSeparator,
                Text = "Display price"
            });
            settings.Add(new SettingItemBoolean(DISPLAY_LABELS_LAST_PERIOD_ONLY_INPUT_PARAMETER, this.DrawLastPeriodOnly, 96)
            {
                SeparatorGroup = defaultSeparator,
                Text = "Display labels last period only"
            });
            settings.Add(new SettingItemBoolean(EXTEND_TO_PERIOD_END_INPUT_PARAMETER, this.ExtendToPeriodEnd, 97)
            {
                SeparatorGroup = defaultSeparator,
                Text = "Extend lines to period end"
            });

            settings.Add(new SettingItemBoolean(DISPLAY_SEPARATOR_INPUT_PARAMETER, this.DisplaySeparator, 98)
            {
                SeparatorGroup = defaultSeparator,
                Text = "Display separator"
            });
            return settings;
        }
        set
        {
            var holder = new SettingsHolder(value);

            var needRefresh = false;

            if (holder.TryGetValue(PP_LINE_OPTIONS_SI, out var lineItem) && lineItem.Value is LineOptions pPLineOptionsValue)
                this.PPLineOptions = pPLineOptionsValue;

            if (holder.TryGetValue(R1_LINE_OPTIONS_SI, out lineItem) && lineItem.Value is LineOptions r1LineOptionsValue)
                this.R1LineOptions = r1LineOptionsValue;

            if (holder.TryGetValue(R2_LINE_OPTIONS_SI, out lineItem) && lineItem.Value is LineOptions r2LineOptionsValue)
                this.R2LineOptions = r2LineOptionsValue;

            if (holder.TryGetValue(R3_LINE_OPTIONS_SI, out lineItem) && lineItem.Value is LineOptions r3LineOptionsValue)
                this.R3LineOptions = r3LineOptionsValue;

            if (holder.TryGetValue(R4_LINE_OPTIONS_SI, out lineItem) && lineItem.Value is LineOptions r4LineOptionsValue)
                this.R4LineOptions = r4LineOptionsValue;

            if (holder.TryGetValue(R5_LINE_OPTIONS_SI, out lineItem) && lineItem.Value is LineOptions r5LineOptionsValue)
                this.R5LineOptions = r5LineOptionsValue;

            if (holder.TryGetValue(R6_LINE_OPTIONS_SI, out lineItem) && lineItem.Value is LineOptions r6LineOptionsValue)
                this.R6LineOptions = r6LineOptionsValue;

            if (holder.TryGetValue(S1_LINE_OPTIONS_SI, out lineItem) && lineItem.Value is LineOptions s1LineOptionsValue)
                this.S1LineOptions = s1LineOptionsValue;

            if (holder.TryGetValue(S2_LINE_OPTIONS_SI, out lineItem) && lineItem.Value is LineOptions s2LineOptionsValue)
                this.S2LineOptions = s2LineOptionsValue;

            if (holder.TryGetValue(S3_LINE_OPTIONS_SI, out lineItem) && lineItem.Value is LineOptions s3LineOptionsValue)
                this.S3LineOptions = s3LineOptionsValue;

            if (holder.TryGetValue(S4_LINE_OPTIONS_SI, out lineItem) && lineItem.Value is LineOptions s4LineOptionsValue)
                this.S4LineOptions = s4LineOptionsValue;

            if (holder.TryGetValue(S5_LINE_OPTIONS_SI, out lineItem) && lineItem.Value is LineOptions s5LineOptionsValue)
                this.S5LineOptions = s5LineOptionsValue;

            if (holder.TryGetValue(S6_LINE_OPTIONS_SI, out lineItem) && lineItem.Value is LineOptions s6LineOptionsValue)
                this.S6LineOptions = s6LineOptionsValue;

            if (holder.TryGetValue(MID_PP_R1_LINE_OPTIONS_SI, out lineItem) && lineItem.Value is LineOptions pPR1MidLineOptionsValue)
                this.PPR1MidLineOptions = pPR1MidLineOptionsValue;

            if (holder.TryGetValue(MID_R1_R2_LINE_OPTIONS_SI, out lineItem) && lineItem.Value is LineOptions r1R2MidLineOptionsValue)
                this.R1R2MidLineOptions = r1R2MidLineOptionsValue;

            if (holder.TryGetValue(MID_R2_R3_LINE_OPTIONS_SI, out lineItem) && lineItem.Value is LineOptions r2R3MidLineOptionsValue)
                this.R2R3MidLineOptions = r2R3MidLineOptionsValue;

            if (holder.TryGetValue(MID_R3_R4_LINE_OPTIONS_SI, out lineItem) && lineItem.Value is LineOptions r3R4MidLineOptionsValue)
                this.R3R4MidLineOptions = r3R4MidLineOptionsValue;

            if (holder.TryGetValue(MID_R4_R5_LINE_OPTIONS_SI, out lineItem) && lineItem.Value is LineOptions r4R5MidLineOptionsValue)
                this.R4R5MidLineOptions = r4R5MidLineOptionsValue;

            if (holder.TryGetValue(MID_R5_R6_LINE_OPTIONS_SI, out lineItem) && lineItem.Value is LineOptions r5R6MidLineOptionsValue)
                this.R5R6MidLineOptions = r5R6MidLineOptionsValue;

            if (holder.TryGetValue(MID_PP_S1_LINE_OPTIONS_SI, out lineItem) && lineItem.Value is LineOptions pPS1MidLineOptionsValue)
                this.PPS1MidLineOptions = pPS1MidLineOptionsValue;

            if (holder.TryGetValue(MID_S1_S2_LINE_OPTIONS_SI, out lineItem) && lineItem.Value is LineOptions s1S2MidLineOptionsValue)
                this.S1S2MidLineOptions = s1S2MidLineOptionsValue;

            if (holder.TryGetValue(MID_S2_S3_LINE_OPTIONS_SI, out lineItem) && lineItem.Value is LineOptions s2S3MidLineOptionsValue)
                this.S2S3MidLineOptions = s2S3MidLineOptionsValue;

            if (holder.TryGetValue(MID_S3_S4_LINE_OPTIONS_SI, out lineItem) && lineItem.Value is LineOptions s3S4MidLineOptionsValue)
                this.S3S4MidLineOptions = s3S4MidLineOptionsValue;

            if (holder.TryGetValue(MID_S4_S5_LINE_OPTIONS_SI, out lineItem) && lineItem.Value is LineOptions s4S5MidLineOptionsValue)
                this.S4S5MidLineOptions = s4S5MidLineOptionsValue;

            if (holder.TryGetValue(MID_S5_S6_LINE_OPTIONS_SI, out lineItem) && lineItem.Value is LineOptions s5S6MidLineOptionsValue)
                this.S5S6MidLineOptions = s5S6MidLineOptionsValue;

            if (holder.TryGetValue(CURRENT_PERIOD_INPUT_PARAMETER, out var item) && item.Value is bool onlyCurrent)
            {
                if (this.OnlyCurrentPeriod != onlyCurrent)
                {
                    this.OnlyCurrentPeriod = onlyCurrent;
                    // Если меняли вручную — обновим расчёты
                    needRefresh |= item.ValueChangingReason == SettingItemValueChangingReason.Manually;
                }
            }

            if (holder.TryGetValue(BASE_PERIOD_INPUT_PARAMETER, out item))
            {
                var newBase = item.GetValue<BasePeriod>();
                if (this.BasePeriod != newBase)
                {
                    this.BasePeriod = newBase;
                    needRefresh |= item.ValueChangingReason == SettingItemValueChangingReason.Manually;
                }
            }

            if (holder.TryGetValue(RANGE_INPUT_PARAMETER, out item) && item.Value is int range)
            {
                if (this.PeriodValue != range)
                {
                    this.PeriodValue = range;
                    needRefresh |= item.ValueChangingReason == SettingItemValueChangingReason.Manually;
                }
            }

            if (holder.TryGetValue(CALCULATION_METHOD_INPUT_PARAMETER, out item))
            {
                var newMethod = item.GetValue<CalculationMethod>();
                if (this.IndicatorCalculationMethod != newMethod)
                {
                    this.IndicatorCalculationMethod = newMethod;
                    needRefresh |= item.ValueChangingReason == SettingItemValueChangingReason.Manually;
                }
            }
            if (holder.TryGetValue("Session type", out item) && item.GetValue<DailySessionType>() != this.DailySessionType)
            {
                this.DailySessionType = item.GetValue<DailySessionType>();
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
                var newValue = Core.Instance.TimeUtils.ConvertFromUTCToSelectedTimeZone(item.GetValue<DateTime>());

                if (this.CustomRangeStartTime != newValue)

                if (this.CustomRangeStartTime.TimeOfDay != newValue.TimeOfDay)
                {
                    this.CustomRangeStartTime = newValue;
                    needRefresh |= item.ValueChangingReason == SettingItemValueChangingReason.Manually;
                }
            }

            if (holder.TryGetValue(CUSTOM_CLOSE_SESSION_NAME_SI, out item))
            {
                var newValue = Core.Instance.TimeUtils.ConvertFromUTCToSelectedTimeZone(item.GetValue<DateTime>());

                if (this.CustomRangeEndTime.TimeOfDay != newValue.TimeOfDay)
                {
                    this.CustomRangeEndTime = newValue;
                    needRefresh |= item.ValueChangingReason == SettingItemValueChangingReason.Manually;
                }
            }
            if (holder.TryGetValue("Display price", out item) && item.Value is bool showPrice)
                this.ShowPriceInLabels = showPrice;
            if (holder.TryGetValue("Display labels", out item) && item.Value is bool showLabels)
                this.ShowLabels = showLabels;
            if (holder.TryGetValue(DISPLAY_LABELS_LAST_PERIOD_ONLY_INPUT_PARAMETER, out item) && item.Value is bool displayLabelsLastPeriodOnly)
                this.DrawLastPeriodOnly = displayLabelsLastPeriodOnly;
            if (holder.TryGetValue(EXTEND_TO_PERIOD_END_INPUT_PARAMETER, out item) && item.Value is bool extend)
            {
                if (this.ExtendToPeriodEnd != extend)
                {
                    this.ExtendToPeriodEnd = extend;
                    needRefresh |= item.ValueChangingReason == SettingItemValueChangingReason.Manually;
                }
            }

            if (holder.TryGetValue(DISPLAY_SEPARATOR_INPUT_PARAMETER, out item) && item.Value is bool sep)
            {
                if (this.DisplaySeparator != sep)
                {
                    this.DisplaySeparator = sep;
                    needRefresh |= item.ValueChangingReason == SettingItemValueChangingReason.Manually;
                }
            }
            if (needRefresh)
                this.Refresh();

            base.Settings = value;
        }
    }

    public override void OnPaintChart(PaintChartEventArgs args)
    {
        base.OnPaintChart(args);

        if (this.CurrentChart == null || this.pivotPeriods == null || this.pivotPeriods.Count == 0)
            return;

        var wnd = this.CurrentChart.Windows?[args.WindowIndex];
        if (wnd == null)
            return;

        var g = args.Graphics;
        var savedClip = g.ClipBounds;

        try
        {
            g.SetClip(args.Rectangle);

            var conv = wnd.CoordinatesConverter;
            var orderedPeriods = this.pivotPeriods
                .OrderBy(period => period.From)
                .ThenBy(period => period.To)
                .ToList();

            if (orderedPeriods.Count == 0)
                return;

            var latestPeriod = orderedPeriods[orderedPeriods.Count - 1];

            var periodsToDraw = this.OnlyCurrentPeriod
                ? new List<PivotPointCalculationResponce> { latestPeriod }
                : orderedPeriods;

            foreach (var p in periodsToDraw)
            {
                DateTime lastBarRightTime = DateTime.MinValue;

                int fromIndex = (int)this.HistoricalData.GetIndexByTime(p.From.Ticks);
                int toIndex = (int)this.HistoricalData.GetIndexByTime(p.To.Ticks);
                if (fromIndex == -1)
                    fromIndex = this.Count - 1;
                if (toIndex == -1)
                    toIndex = 0;

                for (int y = fromIndex; y >= toIndex; y--)
                {
                    var barLeft = DateTime.SpecifyKind(new DateTime(this.HistoricalData[y].TicksLeft), DateTimeKind.Utc);
                    var barRight = DateTime.SpecifyKind(new DateTime(this.HistoricalData[y].TicksRight), DateTimeKind.Utc);

                    if (barRight <= p.From || barLeft >= p.To)
                        continue;

                    DateTime clippedBarRight = barRight > p.To ? p.To : barRight;
                    if (clippedBarRight > lastBarRightTime)
                        lastBarRightTime = clippedBarRight;
                }

                bool hasChartBars = lastBarRightTime != DateTime.MinValue;
                if (!hasChartBars && !p.IsProjected)
                    continue;

                float startX = (float)conv.GetChartX(p.From);

                bool periodIsFinished = Core.Instance.TimeUtils.DateTimeUtcNow >= p.To;
                DateTime endTimeForLine;

                if (periodIsFinished || this.ExtendToPeriodEnd)
                    endTimeForLine = p.To;
                else if (hasChartBars)
                    endTimeForLine = lastBarRightTime;
                else
                    endTimeForLine = p.To;

                if (endTimeForLine <= p.From)
                    continue;

                float lineEndX = (float)conv.GetChartX(endTimeForLine);
                float actualEndX = hasChartBars
                    ? (float)conv.GetChartX(lastBarRightTime)
                    : lineEndX;

                bool hasR2 = IsValidLevel(p.R2);
                bool hasR3 = IsValidLevel(p.R3);
                bool hasR4 = IsValidLevel(p.R4);
                bool hasR5 = IsValidLevel(p.R5);
                bool hasR6 = IsValidLevel(p.R6);

                bool hasS2 = IsValidLevel(p.S2);
                bool hasS3 = IsValidLevel(p.S3);
                bool hasS4 = IsValidLevel(p.S4);
                bool hasS5 = IsValidLevel(p.S5);
                bool hasS6 = IsValidLevel(p.S6);

                DrawHLine(g, p.PP, this.ppLinePen, startX, lineEndX, conv, args.Rectangle);
                DrawHLine(g, p.R1, this.r1LinePen, startX, lineEndX, conv, args.Rectangle);
                DrawHLine(g, p.S1, this.s1LinePen, startX, lineEndX, conv, args.Rectangle);
                DrawHLine(g, Mid(p.PP, p.R1), this.ppR1MidLinePen, startX, lineEndX, conv, args.Rectangle);
                DrawHLine(g, Mid(p.PP, p.S1), this.ppS1MidLinePen, startX, lineEndX, conv, args.Rectangle);

                if (p.Method != CalculationMethod.DeMark)
                {
                    if (hasR2) DrawHLine(g, p.R2, this.r2LinePen, startX, lineEndX, conv, args.Rectangle);
                    if (hasR3) DrawHLine(g, p.R3, this.r3LinePen, startX, lineEndX, conv, args.Rectangle);
                    if (hasS2) DrawHLine(g, p.S2, this.s2LinePen, startX, lineEndX, conv, args.Rectangle);
                    if (hasS3) DrawHLine(g, p.S3, this.s3LinePen, startX, lineEndX, conv, args.Rectangle);

                    if (hasR2) DrawHLine(g, Mid(p.R1, p.R2), this.r1R2MidLinePen, startX, lineEndX, conv, args.Rectangle);
                    if (hasR3) DrawHLine(g, Mid(p.R2, p.R3), this.r2R3MidLinePen, startX, lineEndX, conv, args.Rectangle);
                    if (hasS2) DrawHLine(g, Mid(p.S1, p.S2), this.s1S2MidLinePen, startX, lineEndX, conv, args.Rectangle);
                    if (hasS3) DrawHLine(g, Mid(p.S2, p.S3), this.s2S3MidLinePen, startX, lineEndX, conv, args.Rectangle);
                }

                if (p.Method == CalculationMethod.Camarilla)
                {
                    if (hasR4) DrawHLine(g, p.R4, this.r4LinePen, startX, lineEndX, conv, args.Rectangle);
                    if (hasR5) DrawHLine(g, p.R5, this.r5LinePen, startX, lineEndX, conv, args.Rectangle);
                    if (hasR6) DrawHLine(g, p.R6, this.r6LinePen, startX, lineEndX, conv, args.Rectangle);
                    if (hasS4) DrawHLine(g, p.S4, this.s4LinePen, startX, lineEndX, conv, args.Rectangle);
                    if (hasS5) DrawHLine(g, p.S5, this.s5LinePen, startX, lineEndX, conv, args.Rectangle);
                    if (hasS6) DrawHLine(g, p.S6, this.s6LinePen, startX, lineEndX, conv, args.Rectangle);

                    if (hasR4) DrawHLine(g, Mid(p.R3, p.R4), this.r3R4MidLinePen, startX, lineEndX, conv, args.Rectangle);
                    if (hasR5) DrawHLine(g, Mid(p.R4, p.R5), this.r4R5MidLinePen, startX, lineEndX, conv, args.Rectangle);
                    if (hasR6) DrawHLine(g, Mid(p.R5, p.R6), this.r5R6MidLinePen, startX, lineEndX, conv, args.Rectangle);
                    if (hasS4) DrawHLine(g, Mid(p.S3, p.S4), this.s3S4MidLinePen, startX, lineEndX, conv, args.Rectangle);
                    if (hasS5) DrawHLine(g, Mid(p.S4, p.S5), this.s4S5MidLinePen, startX, lineEndX, conv, args.Rectangle);
                    if (hasS6) DrawHLine(g, Mid(p.S5, p.S6), this.s5S6MidLinePen, startX, lineEndX, conv, args.Rectangle);
                }

                bool isLatestPeriod = p.From == latestPeriod.From && p.To == latestPeriod.To;
                bool shouldDrawLabels = this.ShowLabels && (!this.DrawLastPeriodOnly || isLatestPeriod);
                if (!shouldDrawLabels)
                    continue;

                // Labels of the newest period are anchored to the actual end of the line
                // and placed to its right. Historical labels remain on the left side.
                float labelAnchorX = isLatestPeriod ? lineEndX : actualEndX;
                bool placeLabelRight = isLatestPeriod;

                Draw(g, args.Rectangle, "PP", p.PP, this.PPLineOptions, labelAnchorX, conv, placeLabelRight);
                Draw(g, args.Rectangle, "R1", p.R1, this.R1LineOptions, labelAnchorX, conv, placeLabelRight);
                Draw(g, args.Rectangle, "S1", p.S1, this.S1LineOptions, labelAnchorX, conv, placeLabelRight);
                Draw(g, args.Rectangle, "PP–R1", Mid(p.PP, p.R1), this.PPR1MidLineOptions, labelAnchorX, conv, placeLabelRight);
                Draw(g, args.Rectangle, "PP–S1", Mid(p.PP, p.S1), this.PPS1MidLineOptions, labelAnchorX, conv, placeLabelRight);

                if (p.Method != CalculationMethod.DeMark)
                {
                    if (hasR2) Draw(g, args.Rectangle, "R2", p.R2, this.R2LineOptions, labelAnchorX, conv, placeLabelRight);
                    if (hasR3) Draw(g, args.Rectangle, "R3", p.R3, this.R3LineOptions, labelAnchorX, conv, placeLabelRight);
                    if (hasS2) Draw(g, args.Rectangle, "S2", p.S2, this.S2LineOptions, labelAnchorX, conv, placeLabelRight);
                    if (hasS3) Draw(g, args.Rectangle, "S3", p.S3, this.S3LineOptions, labelAnchorX, conv, placeLabelRight);

                    if (hasR2) Draw(g, args.Rectangle, "R1–R2", Mid(p.R1, p.R2), this.R1R2MidLineOptions, labelAnchorX, conv, placeLabelRight);
                    if (hasR3) Draw(g, args.Rectangle, "R2–R3", Mid(p.R2, p.R3), this.R2R3MidLineOptions, labelAnchorX, conv, placeLabelRight);
                    if (hasS2) Draw(g, args.Rectangle, "S1–S2", Mid(p.S1, p.S2), this.S1S2MidLineOptions, labelAnchorX, conv, placeLabelRight);
                    if (hasS3) Draw(g, args.Rectangle, "S2–S3", Mid(p.S2, p.S3), this.S2S3MidLineOptions, labelAnchorX, conv, placeLabelRight);
                }

                if (p.Method == CalculationMethod.Camarilla)
                {
                    if (hasR4) Draw(g, args.Rectangle, "R4", p.R4, this.R4LineOptions, labelAnchorX, conv, placeLabelRight);
                    if (hasR5) Draw(g, args.Rectangle, "R5", p.R5, this.R5LineOptions, labelAnchorX, conv, placeLabelRight);
                    if (hasR6) Draw(g, args.Rectangle, "R6", p.R6, this.R6LineOptions, labelAnchorX, conv, placeLabelRight);
                    if (hasS4) Draw(g, args.Rectangle, "S4", p.S4, this.S4LineOptions, labelAnchorX, conv, placeLabelRight);
                    if (hasS5) Draw(g, args.Rectangle, "S5", p.S5, this.S5LineOptions, labelAnchorX, conv, placeLabelRight);
                    if (hasS6) Draw(g, args.Rectangle, "S6", p.S6, this.S6LineOptions, labelAnchorX, conv, placeLabelRight);

                    if (hasR4) Draw(g, args.Rectangle, "R3–R4", Mid(p.R3, p.R4), this.R3R4MidLineOptions, labelAnchorX, conv, placeLabelRight);
                    if (hasR5) Draw(g, args.Rectangle, "R4–R5", Mid(p.R4, p.R5), this.R4R5MidLineOptions, labelAnchorX, conv, placeLabelRight);
                    if (hasR6) Draw(g, args.Rectangle, "R5–R6", Mid(p.R5, p.R6), this.R5R6MidLineOptions, labelAnchorX, conv, placeLabelRight);
                    if (hasS4) Draw(g, args.Rectangle, "S3–S4", Mid(p.S3, p.S4), this.S3S4MidLineOptions, labelAnchorX, conv, placeLabelRight);
                    if (hasS5) Draw(g, args.Rectangle, "S4–S5", Mid(p.S4, p.S5), this.S4S5MidLineOptions, labelAnchorX, conv, placeLabelRight);
                    if (hasS6) Draw(g, args.Rectangle, "S5–S6", Mid(p.S5, p.S6), this.S5S6MidLineOptions, labelAnchorX, conv, placeLabelRight);
                }
            }

            // Draw period-to-period connectors once, after horizontal lines.
            if (this.DisplaySeparator && periodsToDraw.Count > 1)
            {
                for (int i = 1; i < periodsToDraw.Count; i++)
                {
                    var prev = periodsToDraw[i - 1];
                    var next = periodsToDraw[i];
                    float xBoundary = (float)conv.GetChartX(next.From);

                    DrawSeparatorLine(g, prev.PP, next.PP, this.ppLinePen, xBoundary, conv, args.Rectangle);
                    DrawSeparatorLine(g, prev.R1, next.R1, this.r1LinePen, xBoundary, conv, args.Rectangle);
                    DrawSeparatorLine(g, prev.S1, next.S1, this.s1LinePen, xBoundary, conv, args.Rectangle);
                    DrawSeparatorLine(g, Mid(prev.PP, prev.R1), Mid(next.PP, next.R1), this.ppR1MidLinePen, xBoundary, conv, args.Rectangle);
                    DrawSeparatorLine(g, Mid(prev.PP, prev.S1), Mid(next.PP, next.S1), this.ppS1MidLinePen, xBoundary, conv, args.Rectangle);

                    if (prev.Method != CalculationMethod.DeMark && next.Method != CalculationMethod.DeMark)
                    {
                        DrawSeparatorLine(g, prev.R2, next.R2, this.r2LinePen, xBoundary, conv, args.Rectangle);
                        DrawSeparatorLine(g, prev.R3, next.R3, this.r3LinePen, xBoundary, conv, args.Rectangle);
                        DrawSeparatorLine(g, prev.S2, next.S2, this.s2LinePen, xBoundary, conv, args.Rectangle);
                        DrawSeparatorLine(g, prev.S3, next.S3, this.s3LinePen, xBoundary, conv, args.Rectangle);

                        DrawSeparatorLine(g, Mid(prev.R1, prev.R2), Mid(next.R1, next.R2), this.r1R2MidLinePen, xBoundary, conv, args.Rectangle);
                        DrawSeparatorLine(g, Mid(prev.R2, prev.R3), Mid(next.R2, next.R3), this.r2R3MidLinePen, xBoundary, conv, args.Rectangle);
                        DrawSeparatorLine(g, Mid(prev.S1, prev.S2), Mid(next.S1, next.S2), this.s1S2MidLinePen, xBoundary, conv, args.Rectangle);
                        DrawSeparatorLine(g, Mid(prev.S2, prev.S3), Mid(next.S2, next.S3), this.s2S3MidLinePen, xBoundary, conv, args.Rectangle);
                    }

                    if (prev.Method == CalculationMethod.Camarilla && next.Method == CalculationMethod.Camarilla)
                    {
                        DrawSeparatorLine(g, prev.R4, next.R4, this.r4LinePen, xBoundary, conv, args.Rectangle);
                        DrawSeparatorLine(g, prev.R5, next.R5, this.r5LinePen, xBoundary, conv, args.Rectangle);
                        DrawSeparatorLine(g, prev.R6, next.R6, this.r6LinePen, xBoundary, conv, args.Rectangle);
                        DrawSeparatorLine(g, prev.S4, next.S4, this.s4LinePen, xBoundary, conv, args.Rectangle);
                        DrawSeparatorLine(g, prev.S5, next.S5, this.s5LinePen, xBoundary, conv, args.Rectangle);
                        DrawSeparatorLine(g, prev.S6, next.S6, this.s6LinePen, xBoundary, conv, args.Rectangle);

                        DrawSeparatorLine(g, Mid(prev.R3, prev.R4), Mid(next.R3, next.R4), this.r3R4MidLinePen, xBoundary, conv, args.Rectangle);
                        DrawSeparatorLine(g, Mid(prev.R4, prev.R5), Mid(next.R4, next.R5), this.r4R5MidLinePen, xBoundary, conv, args.Rectangle);
                        DrawSeparatorLine(g, Mid(prev.R5, prev.R6), Mid(next.R5, next.R6), this.r5R6MidLinePen, xBoundary, conv, args.Rectangle);
                        DrawSeparatorLine(g, Mid(prev.S3, prev.S4), Mid(next.S3, next.S4), this.s3S4MidLinePen, xBoundary, conv, args.Rectangle);
                        DrawSeparatorLine(g, Mid(prev.S4, prev.S5), Mid(next.S4, next.S5), this.s4S5MidLinePen, xBoundary, conv, args.Rectangle);
                        DrawSeparatorLine(g, Mid(prev.S5, prev.S6), Mid(next.S5, next.S6), this.s5S6MidLinePen, xBoundary, conv, args.Rectangle);
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
            g.SetClip(savedClip);
        }
    }
    #endregion Overrides

    #region Calculation
    private void CalculateAllIndicator()
    {
        var pivotPoints = new List<PivotPointCalculationResponce>();
        for (int i = 0; i < this.history.Count; i++)
        {
            var responce = this.CalculatePivotPoint(this.history, i);

            if (responce != null)
            {
                pivotPoints.Add(responce);
            }
        }
        pivotPoints.Reverse();
        this.pivotPeriods.Clear();
        this.pivotPeriods.AddRange(pivotPoints);

        this.EnsureLatestDailySessionPeriod();
        this.UpdateLabelsFromLastPivot();
    }
    private PivotPointCalculationResponce CalculatePivotPoint(HistoricalData hd, int hdOffset)
    {
        if (hd == null)
            return null;

        if (hd.Count <= hdOffset || hd.Count <= hdOffset + 1)
            return null;

        var historyItem = hd[hdOffset, SeekOriginHistory.End] as HistoryItemBar;
        if (historyItem == null)
            return null;

        var periodInterval = this.GetPeriodInterval(historyItem);
        if (periodInterval.To <= periodInterval.From)
            return null;

        return this.CalculatePivotPointForInterval(
            hd,
            sourceOffset: hdOffset + 1,
            deMarkOpenOffset: hdOffset + 2,
            startTime: periodInterval.From,
            endTime: periodInterval.To,
            isProjected: false);
    }

    private PivotPointCalculationResponce CalculatePivotPointForInterval(
        HistoricalData hd,
        int sourceOffset,
        int deMarkOpenOffset,
        DateTime startTime,
        DateTime endTime,
        bool isProjected)
    {
        if (hd == null ||
            sourceOffset < 0 ||
            sourceOffset >= hd.Count ||
            endTime <= startTime)
        {
            return null;
        }

        var sourceItem = hd[sourceOffset, SeekOriginHistory.End];
        if (sourceItem == null)
            return null;

        double close = sourceItem[PriceType.Close];
        double high = sourceItem[PriceType.High];
        double low = sourceItem[PriceType.Low];

        double pp, r1, r2, r3, r4, r5, r6, s1, s2, s3, s4, s5, s6;
        pp = r1 = r2 = r3 = r4 = r5 = r6 = s1 = s2 = s3 = s4 = s5 = s6 = 0;

        switch (this.IndicatorCalculationMethod)
        {
            case CalculationMethod.Classic:
                {
                    pp = (high + low + close) / 3;
                    double range = high - low;

                    r1 = 2 * pp - low;
                    r2 = pp + range;
                    r3 = r2 + range;
                    r4 = r3 + range;

                    s1 = 2 * pp - high;
                    s2 = pp - range;
                    s3 = s2 - range;
                    s4 = s3 - range;
                    break;
                }

            case CalculationMethod.Camarilla:
                {
                    pp = (high + low + close) / 3;

                    r1 = close + 0.0916 * (high - low);
                    r2 = close + 0.183 * (high - low);
                    r3 = close + 0.275 * (high - low);
                    r4 = close + 0.55 * (high - low);
                    r5 = r4 + 1.168 * (r4 - r3);
                    r6 = (high / low) * close;

                    s1 = close - 0.0916 * (high - low);
                    s2 = close - 0.183 * (high - low);
                    s3 = close - 0.275 * (high - low);
                    s4 = close - 0.55 * (high - low);
                    s5 = s4 - 1.168 * (s3 - s4);
                    s6 = close - (r6 - close);
                    break;
                }

            case CalculationMethod.Fibonacci:
                {
                    pp = (high + low + close) / 3;

                    r1 = pp + 0.382 * (high - low);
                    r2 = pp + 0.618 * (high - low);
                    r3 = pp + (high - low);

                    s1 = pp - 0.382 * (high - low);
                    s2 = pp - 0.618 * (high - low);
                    s3 = pp - (high - low);
                    break;
                }

            case CalculationMethod.Woodie:
                {
                    pp = (high + low + 2 * close) / 4;

                    r1 = 2 * pp - low;
                    r2 = pp + high - low;
                    r3 = high + 2 * (pp - low);

                    s1 = 2 * pp - high;
                    s2 = pp + low - high;
                    s3 = low - 2 * (high - pp);
                    break;
                }

            case CalculationMethod.DeMark:
                {
                    var openItem =
                        deMarkOpenOffset >= 0 && deMarkOpenOffset < hd.Count
                            ? hd[deMarkOpenOffset, SeekOriginHistory.End]
                            : sourceItem;

                    double open = openItem != null
                        ? openItem[PriceType.Open]
                        : sourceItem[PriceType.Open];
                    double x;

                    if (close < open)
                        x = high + 2 * low + close;
                    else if (close > open)
                        x = 2 * high + low + close;
                    else
                        x = high + low + 2 * close;

                    pp = x / 4;
                    r1 = x / 2 - low;
                    s1 = x / 2 - high;
                    break;
                }
        }

        return new PivotPointCalculationResponce(startTime, endTime)
        {
            PP = pp,
            R1 = r1,
            R2 = r2,
            R3 = r3,
            R4 = r4,
            R5 = r5,
            R6 = r6,
            S1 = s1,
            S2 = s2,
            S3 = s3,
            S4 = s4,
            S5 = s5,
            S6 = s6,
            Method = this.IndicatorCalculationMethod,
            Period = new Period(this.BasePeriod, this.PeriodValue),
            IsProjected = isProjected
        };
    }

    private void CalculateLastPeriod()
    {
        var last = this.CalculatePivotPoint(this.history, 0);
        if (last == null)
            return;

        if (this.pivotPeriods == null)
            this.pivotPeriods = new List<PivotPointCalculationResponce>();

        int idx = this.pivotPeriods.FindIndex(pp => pp.From == last.From && pp.To == last.To);
        if (idx >= 0)
            this.pivotPeriods[idx] = last;
        else
            this.pivotPeriods.Insert(0, last);

        this.EnsureLatestDailySessionPeriod();
        this.UpdateLabelsFromLastPivot();

    }

    #endregion Calculation 

    #region Drawing
    private void DrawHLine(Graphics g, double value, Pen pen, float x1, float x2, IChartWindowCoordinatesConverter conv, Rectangle rect)
    {
        if (pen == null || !IsValidLevel(value))
            return;

        float y = (float)conv.GetChartY(value);

        // Do not draw a horizontal segment when it is completely outside
        // the visible chart rectangle. A segment whose ends are outside on
        // opposite sides is still drawn because it crosses the visible area.
        if (IsLineSegmentOutsideRectangle(x1, y, x2, y, rect))
            return;

        x1 = Math.Max(rect.Left, Math.Min(rect.Right, x1));
        x2 = Math.Max(rect.Left, Math.Min(rect.Right, x2));

        g.DrawLine(pen, x1, y, x2, y);
    }

    private void DrawSeparatorLine(Graphics g, double prevValue, double nextValue, Pen pen, float x, IChartWindowCoordinatesConverter conv, Rectangle rect)
    {
        if (pen == null || !IsValidLevel(prevValue) || !IsValidLevel(nextValue))
            return;

        float y1 = (float)conv.GetChartY(prevValue);
        float y2 = (float)conv.GetChartY(nextValue);

        // The same visibility test is applied to vertical period separators.
        if (IsLineSegmentOutsideRectangle(x, y1, x, y2, rect))
            return;

        x = Math.Max(rect.Left, Math.Min(rect.Right, x));
        y1 = Math.Max(rect.Top, Math.Min(rect.Bottom, y1));
        y2 = Math.Max(rect.Top, Math.Min(rect.Bottom, y2));

        g.DrawLine(pen, x, y1, x, y2);
    }

    private static bool IsLineSegmentOutsideRectangle(
        float x1,
        float y1,
        float x2,
        float y2,
        Rectangle rect)
    {
        if (float.IsNaN(x1) || float.IsInfinity(x1) ||
            float.IsNaN(y1) || float.IsInfinity(y1) ||
            float.IsNaN(x2) || float.IsInfinity(x2) ||
            float.IsNaN(y2) || float.IsInfinity(y2))
        {
            return true;
        }

        float minX = Math.Min(x1, x2);
        float maxX = Math.Max(x1, x2);
        float minY = Math.Min(y1, y2);
        float maxY = Math.Max(y1, y2);

        return maxX < rect.Left ||
               minX > rect.Right ||
               maxY < rect.Top ||
               minY > rect.Bottom;
    }

    private void Draw(
        Graphics g,
        Rectangle rect,
        string name,
        double value,
        LineOptions lineOptions,
        float anchorX,
        IChartWindowCoordinatesConverter conv,
        bool placeRight)
    {
        if (lineOptions == null || !lineOptions.Enabled || !IsValidLevel(value))
            return;

        float y = (float)conv.GetChartY(value);
        string label = this.ShowPriceInLabels ? $"{name} {this.Symbol.FormatPrice(value)}" : name;
        this.DrawLevelLabel(g, rect, y, label, lineOptions.Color, anchorX, placeRight);
    }

    private void DrawLevelLabel(
    Graphics g,
    Rectangle chartRect,
    float y,
    string text,
    Color backColor,
    float anchorX,
    bool placeRight)
    {
        if (float.IsNaN(y) || y <= chartRect.Top || y >= chartRect.Bottom)
            return;

        var size = g.MeasureString(text, this.labelFont);
        float labelWidth = size.Width + LabelPadX * 2f;
        float labelHeight = size.Height + LabelPadY * 2f;

        if (!placeRight && anchorX <= chartRect.Left)
            return;

        float visibleAnchorX = Math.Max(chartRect.Left, Math.Min(chartRect.Right, anchorX));

        const float rightLabelGap = LabelGapX;
        const float leftLabelGap = 1f;

        float x = placeRight
            ? visibleAnchorX + rightLabelGap
            : visibleAnchorX - labelWidth - leftLabelGap+2;

        if (placeRight && x + labelWidth > chartRect.Right)
            x = visibleAnchorX - labelWidth - leftLabelGap;

        if (!placeRight && x < chartRect.Left)
            return;

        float top = y - size.Height / 2f - LabelPadY;
        top = Math.Max(chartRect.Top, Math.Min(chartRect.Bottom - labelHeight, top));

        x = Math.Max(chartRect.Left, Math.Min(chartRect.Right - labelWidth, x));

        var labelRect = new RectangleF(x, top, labelWidth, labelHeight);

        using (var brush = new SolidBrush(backColor))
            g.FillRectangle(brush, labelRect);

        g.DrawString(text, this.labelFont, Brushes.White, labelRect, this.labelSF);
    }

    private static bool IsValidLevel(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value) && value != 0.0;

    private static LineOptions CreateLineOptions(bool enabled, Color color, LineStyle style, int width) => new LineOptions
    {
        Enabled = enabled,
        WithCheckBox = true,
        Color = color,
        LineStyle = style,
        Width = width
    };

    private static void AssignLineOptions(ref LineOptions targetOptions, ref Pen targetPen, LineOptions value)
    {
        targetOptions = value;
        targetPen = ProcessPen(targetPen, value);
    }

    private static Pen ProcessPen(Pen pen, LineOptions lineOptions)
    {
        if (lineOptions == null || !lineOptions.Enabled)
        {
            pen?.Dispose();
            return null;
        }

        if (pen == null)
            pen = new Pen(lineOptions.Color);

        pen.Color = lineOptions.Color;
        pen.Width = lineOptions.Width;

        try
        {
            switch (lineOptions.LineStyle)
            {
                case LineStyle.Solid:
                    pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Solid;
                    break;

                case LineStyle.Dot:
                    pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dot;
                    break;

                case LineStyle.Dash:
                    pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                    break;

                case LineStyle.DashDot:
                    pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Custom;
                    pen.DashPattern = new[] { 2f, 4f, 7f, 4f };
                    break;

                default:
                    pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Solid;
                    break;
            }
        }
        catch
        {
            pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Solid;
        }

        return pen;
    }

    //private void DrawMessage(Graphics gr, string message, Rectangle rectangle) => gr.DrawString(message, this.font, this.messageBrush, rectangle, this.centerCenterSF);
    #endregion Drawing

    private void History_NewHistoryItem(object sender, HistoryEventArgs e) => this.CalculateLastPeriod();
    private double Mid(double a, double b) => (a + b) * 0.5;
    #region Misc
    private void AbortPreviousTask()
    {
        if (this.history != null)
        {
            this.history.NewHistoryItem -= this.History_NewHistoryItem;
            this.history.Dispose();
        }

        if (this.cancellationSource != null)
            this.cancellationSource.Cancel();

        this.cancellationSource = new CancellationTokenSource();
    }
    private bool IsValidLoadedHistory(HistoricalData history)
    {
        if (this.history == null || this.history.Count == 0)
            return false;

        if (this.history.Count < MIN_HISTORY_COUNT)
            return false;

        return true;
    }
    private string GetFormattedPeriod(BasePeriod basePeriod, int range)
    {
        var period = basePeriod.ToString();
        if (range > 1)
            period += "s";

        return $"{range} {period}";
    }

    private void InitializeLabels()
    {
        this.AddLabel(LABEL_PP, ComparingType.Double, "PP");

        this.AddLabel(LABEL_R1, ComparingType.Double, "R1");
        this.AddLabel(LABEL_R2, ComparingType.Double, "R2");
        this.AddLabel(LABEL_R3, ComparingType.Double, "R3");
        this.AddLabel(LABEL_R4, ComparingType.Double, "R4");
        this.AddLabel(LABEL_R5, ComparingType.Double, "R5");
        this.AddLabel(LABEL_R6, ComparingType.Double, "R6");

        this.AddLabel(LABEL_S1, ComparingType.Double, "S1");
        this.AddLabel(LABEL_S2, ComparingType.Double, "S2");
        this.AddLabel(LABEL_S3, ComparingType.Double, "S3");
        this.AddLabel(LABEL_S4, ComparingType.Double, "S4");
        this.AddLabel(LABEL_S5, ComparingType.Double, "S5");
        this.AddLabel(LABEL_S6, ComparingType.Double, "S6");

        this.AddLabel(LABEL_MID_PP_R1, ComparingType.Double, "PP–R1");
        this.AddLabel(LABEL_MID_R1_R2, ComparingType.Double, "R1–R2");
        this.AddLabel(LABEL_MID_R2_R3, ComparingType.Double, "R2–R3");
        this.AddLabel(LABEL_MID_R3_R4, ComparingType.Double, "R3–R4");
        this.AddLabel(LABEL_MID_R4_R5, ComparingType.Double, "R4–R5");
        this.AddLabel(LABEL_MID_R5_R6, ComparingType.Double, "R5–R6");

        this.AddLabel(LABEL_MID_PP_S1, ComparingType.Double, "PP–S1");
        this.AddLabel(LABEL_MID_S1_S2, ComparingType.Double, "S1–S2");
        this.AddLabel(LABEL_MID_S2_S3, ComparingType.Double, "S2–S3");
        this.AddLabel(LABEL_MID_S3_S4, ComparingType.Double, "S3–S4");
        this.AddLabel(LABEL_MID_S4_S5, ComparingType.Double, "S4–S5");
        this.AddLabel(LABEL_MID_S5_S6, ComparingType.Double, "S5–S6");
    }

    private void UpdateLabelsFromLastPivot()
    {
        if (this.pivotPeriods == null || this.pivotPeriods.Count == 0)
        {
            this.ResetLabels();
            return;
        }

        var lastPivot = this.pivotPeriods
            .OrderBy(p => p.To)
            .LastOrDefault();

        if (lastPivot == null)
        {
            this.ResetLabels();
            return;
        }

        this.SetLabelValue(LABEL_PP, NormalizeLabelValue(lastPivot.PP));

        this.SetLabelValue(LABEL_R1, NormalizeLabelValue(lastPivot.R1));
        this.SetLabelValue(LABEL_R2, NormalizeLabelValue(lastPivot.R2));
        this.SetLabelValue(LABEL_R3, NormalizeLabelValue(lastPivot.R3));
        this.SetLabelValue(LABEL_R4, NormalizeLabelValue(lastPivot.R4));
        this.SetLabelValue(LABEL_R5, NormalizeLabelValue(lastPivot.R5));
        this.SetLabelValue(LABEL_R6, NormalizeLabelValue(lastPivot.R6));

        this.SetLabelValue(LABEL_S1, NormalizeLabelValue(lastPivot.S1));
        this.SetLabelValue(LABEL_S2, NormalizeLabelValue(lastPivot.S2));
        this.SetLabelValue(LABEL_S3, NormalizeLabelValue(lastPivot.S3));
        this.SetLabelValue(LABEL_S4, NormalizeLabelValue(lastPivot.S4));
        this.SetLabelValue(LABEL_S5, NormalizeLabelValue(lastPivot.S5));
        this.SetLabelValue(LABEL_S6, NormalizeLabelValue(lastPivot.S6));

        this.SetLabelValue(LABEL_MID_PP_R1, NormalizeLabelValue(MidIfPossible(lastPivot.PP, lastPivot.R1)));
        this.SetLabelValue(LABEL_MID_R1_R2, NormalizeLabelValue(MidIfPossible(lastPivot.R1, lastPivot.R2)));
        this.SetLabelValue(LABEL_MID_R2_R3, NormalizeLabelValue(MidIfPossible(lastPivot.R2, lastPivot.R3)));
        this.SetLabelValue(LABEL_MID_R3_R4, NormalizeLabelValue(MidIfPossible(lastPivot.R3, lastPivot.R4)));
        this.SetLabelValue(LABEL_MID_R4_R5, NormalizeLabelValue(MidIfPossible(lastPivot.R4, lastPivot.R5)));
        this.SetLabelValue(LABEL_MID_R5_R6, NormalizeLabelValue(MidIfPossible(lastPivot.R5, lastPivot.R6)));

        this.SetLabelValue(LABEL_MID_PP_S1, NormalizeLabelValue(MidIfPossible(lastPivot.PP, lastPivot.S1)));
        this.SetLabelValue(LABEL_MID_S1_S2, NormalizeLabelValue(MidIfPossible(lastPivot.S1, lastPivot.S2)));
        this.SetLabelValue(LABEL_MID_S2_S3, NormalizeLabelValue(MidIfPossible(lastPivot.S2, lastPivot.S3)));
        this.SetLabelValue(LABEL_MID_S3_S4, NormalizeLabelValue(MidIfPossible(lastPivot.S3, lastPivot.S4)));
        this.SetLabelValue(LABEL_MID_S4_S5, NormalizeLabelValue(MidIfPossible(lastPivot.S4, lastPivot.S5)));
        this.SetLabelValue(LABEL_MID_S5_S6, NormalizeLabelValue(MidIfPossible(lastPivot.S5, lastPivot.S6)));
    }

    private void ResetLabels()
    {
        this.SetLabelValue(LABEL_PP, double.NaN);

        this.SetLabelValue(LABEL_R1, double.NaN);
        this.SetLabelValue(LABEL_R2, double.NaN);
        this.SetLabelValue(LABEL_R3, double.NaN);
        this.SetLabelValue(LABEL_R4, double.NaN);
        this.SetLabelValue(LABEL_R5, double.NaN);
        this.SetLabelValue(LABEL_R6, double.NaN);

        this.SetLabelValue(LABEL_S1, double.NaN);
        this.SetLabelValue(LABEL_S2, double.NaN);
        this.SetLabelValue(LABEL_S3, double.NaN);
        this.SetLabelValue(LABEL_S4, double.NaN);
        this.SetLabelValue(LABEL_S5, double.NaN);
        this.SetLabelValue(LABEL_S6, double.NaN);

        this.SetLabelValue(LABEL_MID_PP_R1, double.NaN);
        this.SetLabelValue(LABEL_MID_R1_R2, double.NaN);
        this.SetLabelValue(LABEL_MID_R2_R3, double.NaN);
        this.SetLabelValue(LABEL_MID_R3_R4, double.NaN);
        this.SetLabelValue(LABEL_MID_R4_R5, double.NaN);
        this.SetLabelValue(LABEL_MID_R5_R6, double.NaN);

        this.SetLabelValue(LABEL_MID_PP_S1, double.NaN);
        this.SetLabelValue(LABEL_MID_S1_S2, double.NaN);
        this.SetLabelValue(LABEL_MID_S2_S3, double.NaN);
        this.SetLabelValue(LABEL_MID_S3_S4, double.NaN);
        this.SetLabelValue(LABEL_MID_S4_S5, double.NaN);
        this.SetLabelValue(LABEL_MID_S5_S6, double.NaN);
    }

    private double NormalizeLabelValue(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value == 0.0)
            return 0d;

        return this.RoundToTickSize(value);
    }

    private double MidIfPossible(double a, double b)
    {
        if (double.IsNaN(a) || double.IsNaN(b) || a == 0.0 || b == 0.0)
            return 0d;

        return (a + b) * 0.5;
    }

    private double RoundToTickSize(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            return value;

        if (this.Symbol == null || this.Symbol.TickSize <= 0)
            return value;

        double steps = Math.Round(value / this.Symbol.TickSize, MidpointRounding.AwayFromZero);
        return steps * this.Symbol.TickSize;
    }

    private void CurrentChartOnSettingsChanged(object sender, ChartEventArgs e)
    {
        if (this.BasePeriod == BasePeriod.Day &&
            this.DailySessionType == DailySessionType.SpecifiedSession &&
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

    private Interval<DateTime> GetPeriodInterval(HistoryItemBar historyItem)
    {
        var timeZone = this.GetTimeZone();
        DateTime itemStartUtc = DateTime.SpecifyKind(new DateTime(historyItem.TicksLeft), DateTimeKind.Utc);
        DateTime itemEndUtc = DateTime.SpecifyKind(new DateTime(historyItem.TicksRight), DateTimeKind.Utc);

        if (this.BasePeriod == BasePeriod.Day &&
            (this.DailySessionType == DailySessionType.CustomRange ||
             this.DailySessionType == DailySessionType.AllDay))
        {
            TimeSpan open = this.DailySessionType == DailySessionType.AllDay
                ? TimeSpan.Zero
                : this.CustomRangeStartTime.TimeOfDay;
            TimeSpan close = this.DailySessionType == DailySessionType.AllDay
                ? TimeSpan.Zero
                : this.CustomRangeEndTime.TimeOfDay;

            TimeSpan normalizedClose = NormalizeSessionClose(open, close);
            DateTime itemStartLocal = TimeZoneInfo.ConvertTimeFromUtc(itemStartUtc, timeZone.TimeZoneInfo);

            DateTime startLocal = DateTime.SpecifyKind(itemStartLocal.Date + open, DateTimeKind.Unspecified);
            if (itemStartLocal < startLocal)
                startLocal = startLocal.AddDays(-1);

            DateTime endLocal = DateTime.SpecifyKind(
                startLocal.Date + normalizedClose + TimeSpan.FromDays(Math.Max(0, this.PeriodValue - 1)),
                DateTimeKind.Unspecified);

            return new Interval<DateTime>(
                Core.Instance.TimeUtils.ConvertFromTimeZoneToUTC(startLocal, timeZone),
                Core.Instance.TimeUtils.ConvertFromTimeZoneToUTC(endLocal, timeZone));
        }
        if (itemEndUtc <= itemStartUtc)
            return new Interval<DateTime>(itemStartUtc, itemStartUtc.AddDays(1));

        return new Interval<DateTime>(itemStartUtc, itemEndUtc);
    }

    private void EnsureLatestDailySessionPeriod()
    {
        if (this.BasePeriod != BasePeriod.Day ||
            (this.DailySessionType != DailySessionType.AllDay &&
             this.DailySessionType != DailySessionType.CustomRange) ||
            this.history == null ||
            this.history.Count == 0)
        {
            return;
        }

        var newestHistoryBar = this.history[0, SeekOriginHistory.End] as HistoryItemBar;
        if (newestHistoryBar == null)
            return;

        Interval<DateTime> newestInterval = this.GetPeriodInterval(newestHistoryBar);
        Interval<DateTime> expectedInterval = this.GetExpectedLatestDailySessionInterval(newestInterval);

        if (expectedInterval.To <= expectedInterval.From ||
            expectedInterval.From <= newestInterval.From)
        {
            return;
        }

        int existingIndex = this.pivotPeriods.FindIndex(
            period => period.From == expectedInterval.From &&
                      period.To == expectedInterval.To);

        if (existingIndex >= 0)
            return;

        var projected = this.CalculatePivotPointForInterval(
            this.history,
            sourceOffset: 0,
            deMarkOpenOffset: 0,
            startTime: expectedInterval.From,
            endTime: expectedInterval.To,
            isProjected: true);

        if (projected != null)
            this.pivotPeriods.Add(projected);
    }

    private Interval<DateTime> GetExpectedLatestDailySessionInterval(
        Interval<DateTime> newestInterval)
    {
        var timeZone = this.GetTimeZone();

        DateTime utcNow = DateTime.SpecifyKind(
            Core.Instance.TimeUtils.DateTimeUtcNow,
            DateTimeKind.Utc);

        DateTime localNow = TimeZoneInfo.ConvertTimeFromUtc(
            utcNow,
            timeZone.TimeZoneInfo);

        TimeSpan open = this.DailySessionType == DailySessionType.AllDay
            ? TimeSpan.Zero
            : this.CustomRangeStartTime.TimeOfDay;

        TimeSpan close = this.DailySessionType == DailySessionType.AllDay
            ? TimeSpan.Zero
            : this.CustomRangeEndTime.TimeOfDay;

        bool crossesMidnight = close <= open;
        TimeSpan normalizedClose = NormalizeSessionClose(open, close);

        DateTime expectedStartLocal;
        if (crossesMidnight)
        {
            expectedStartLocal = localNow.TimeOfDay >= open
                ? localNow.Date + open
                : localNow.Date.AddDays(-1) + open;
        }
        else
        {
            expectedStartLocal = localNow.Date + open;
        }

        expectedStartLocal = DateTime.SpecifyKind(
            expectedStartLocal,
            DateTimeKind.Unspecified);

        DateTime newestStartUtc = DateTime.SpecifyKind(
            newestInterval.From,
            DateTimeKind.Utc);

        DateTime newestEndUtc = DateTime.SpecifyKind(
            newestInterval.To,
            DateTimeKind.Utc);

        DateTime newestStartLocal = TimeZoneInfo.ConvertTimeFromUtc(
            newestStartUtc,
            timeZone.TimeZoneInfo);

        DateTime newestEndLocal = TimeZoneInfo.ConvertTimeFromUtc(
            newestEndUtc,
            timeZone.TimeZoneInfo);

        if (expectedStartLocal >= newestStartLocal &&
            expectedStartLocal < newestEndLocal)
        {
            return newestInterval;
        }

        DateTime candidateStartLocal = DateTime.SpecifyKind(
            newestStartLocal.AddDays(Math.Max(1, this.PeriodValue)),
            DateTimeKind.Unspecified);

        DateTime candidateEndLocal = DateTime.SpecifyKind(
            candidateStartLocal.Date +
            normalizedClose +
            TimeSpan.FromDays(Math.Max(0, this.PeriodValue - 1)),
            DateTimeKind.Unspecified);
        if (expectedStartLocal < candidateStartLocal ||
            expectedStartLocal >= candidateEndLocal)
        {
            return newestInterval;
        }

        return new Interval<DateTime>(
            Core.Instance.TimeUtils.ConvertFromTimeZoneToUTC(
                candidateStartLocal,
                timeZone),
            Core.Instance.TimeUtils.ConvertFromTimeZoneToUTC(
                candidateEndLocal,
                timeZone));
    }

    private static TimeSpan NormalizeSessionClose(TimeSpan open, TimeSpan close)
    {
        return close <= open ? close.Add(TimeSpan.FromDays(1)) : close;
    }

    private CustomSession CreateCustomSession(TimeSpan open, TimeSpan close, TimeZoneInfo info)
    {
        var session = new CustomSession
        {
            OpenOffset = open,
            CloseOffset = NormalizeSessionClose(open, close),
            IsActive = true,
            Name = "Main",
            Days = Enum.GetValues(typeof(DayOfWeek)).Cast<DayOfWeek>().ToArray(),
            Type = SessionType.Main
        };

        session.RecalculateOpenCloseTime(info);
        return session;
    }
    #endregion Misc
}

#region Utils
internal class PivotPointCalculationResponce
{
    public PivotPointCalculationResponce(DateTime from, DateTime to)
    {
        this.From = from;
        this.To = to;
    }

    public double PP { get; set; }
    public double R1 { get; set; }
    public double R2 { get; set; }
    public double R3 { get; set; }
    public double R4 { get; set; }
    public double R5 { get; set; }
    public double R6 { get; set; }
    public double S1 { get; set; }
    public double S2 { get; set; }
    public double S3 { get; set; }
    public double S4 { get; set; }
    public double S5 { get; set; }
    public double S6 { get; set; }
    public DateTime From { get; private set; }
    public DateTime To { get; private set; }
    public Period Period { get; set; }
    public CalculationMethod Method { get; internal set; }
    public bool IsProjected { get; set; }
}
public enum CalculationMethod
{
    Classic,
    Camarilla,
    Fibonacci,
    Woodie,
    DeMark
}
public enum IndicatorState
{
    Ready,
    Loading,
    Calculation,
    NoData,
    IncorrectPeriod,
    OneTickNotAllowed
}
public enum DailySessionType { AllDay, SpecifiedSession, CustomRange, }
#endregion Utils