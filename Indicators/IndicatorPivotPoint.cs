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
            {
                var session = this.GetFullDayTimeInterval(this.GetTimeZone());
                this.customRangeStartTime = session.From;
            }

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
            {
                var session = this.GetFullDayTimeInterval(this.GetTimeZone());
                this.customRangeEndTime = session.To;
            }

            return DateTime.SpecifyKind(this.customRangeEndTime, DateTimeKind.Local);
        }
        set => this.customRangeEndTime = value;
    }
    private DateTime customRangeEndTime;

    public BasePeriod BasePeriod = BasePeriod.Day;
    public int PeriodValue = 1;
    public CalculationMethod IndicatorCalculationMethod = CalculationMethod.Classic;
    public DailySessionType DailySessionType = DailySessionType.AllDay;
    private readonly Color MidColor = Color.FromArgb(128, 128, 128, 128);
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
            this.CreateCustomSession(TimeSpan.Zero, new TimeSpan(23, 59, 59), timeZone.TimeZoneInfo)
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

                if (this.DailySessionType != DailySessionType.AllDay)
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
            settings.Add(new SettingItemBoolean("Draw last period only", this.DrawLastPeriodOnly, 96)
            {
                SeparatorGroup = defaultSeparator,
                Text = "Draw last period only"
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
                {
                    this.CustomRangeStartTime = newValue;
                    needRefresh |= item.ValueChangingReason == SettingItemValueChangingReason.Manually;
                }
            }

            if (holder.TryGetValue(CUSTOM_CLOSE_SESSION_NAME_SI, out item))
            {
                var newValue = Core.Instance.TimeUtils.ConvertFromUTCToSelectedTimeZone(item.GetValue<DateTime>());

                if (this.CustomRangeEndTime != newValue)
                {
                    this.CustomRangeEndTime = newValue;
                    needRefresh |= item.ValueChangingReason == SettingItemValueChangingReason.Manually;
                }
            }
            if (holder.TryGetValue("Display price", out item) && item.Value is bool showPrice)
                this.ShowPriceInLabels = showPrice;
            if (holder.TryGetValue("Display labels", out item) && item.Value is bool showLabels)
                this.ShowLabels = showLabels;
            if (holder.TryGetValue("Draw last period only", out item) && item.Value is bool drawLastOnly)
                this.DrawLastPeriodOnly = drawLastOnly;
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

            IEnumerable<PivotPointCalculationResponce> periodsToDraw =
                this.OnlyCurrentPeriod
                    ? new[] { this.pivotPeriods[this.pivotPeriods.Count-1] }
                    : (this.pivotPeriods ?? Enumerable.Empty<PivotPointCalculationResponce>());

            foreach (var p in periodsToDraw)
            {
                DateTime firstBarTime = DateTime.MinValue;
                DateTime lastBarRightTime = DateTime.MinValue;

                int fromIndex = (int)this.HistoricalData.GetIndexByTime(p.From.Ticks);
                int toIndex = (int)this.HistoricalData.GetIndexByTime(p.To.Ticks);
                if (fromIndex == -1) fromIndex = this.Count - 1;
                if (toIndex   == -1) toIndex   = 0;

                for (int y = fromIndex; y >= toIndex; y--)
                {
                    var barLeft = DateTime.SpecifyKind(new DateTime(this.HistoricalData[y].TicksLeft), DateTimeKind.Utc);
                    var barRight = DateTime.SpecifyKind(new DateTime(this.HistoricalData[y].TicksRight), DateTimeKind.Utc);

                    if (barRight <= p.From || barLeft >= p.To)
                        continue;

                    if (this.DailySessionType != DailySessionType.AllDay)
                    {
                        if (this.SessionContainer == null || !this.SessionContainer.ContainsDate(barLeft))
                            continue;
                    }

                    if (firstBarTime == DateTime.MinValue || barLeft < firstBarTime)
                        firstBarTime = barLeft;

                    if (barRight > lastBarRightTime)
                        lastBarRightTime = barRight;
                }

                if (firstBarTime == DateTime.MinValue || lastBarRightTime == DateTime.MinValue)
                    continue;

                float startX = (float)conv.GetChartX(firstBarTime);
                DateTime endTimeForLine = (this.ExtendToPeriodEnd && DateTime.Now < p.To) ? p.To : lastBarRightTime;
                float lineEndX = (float)conv.GetChartX(endTimeForLine);
                float endX = (float)conv.GetChartX(lastBarRightTime);

                bool hasR2 = !double.IsNaN(p.R2) && p.R2 != 0.0;
                bool hasR3 = !double.IsNaN(p.R3) && p.R3 != 0.0;
                bool hasR4 = !double.IsNaN(p.R4) && p.R4 != 0.0;
                bool hasR5 = !double.IsNaN(p.R5) && p.R5 != 0.0;
                bool hasR6 = !double.IsNaN(p.R6) && p.R6 != 0.0;

                bool hasS2 = !double.IsNaN(p.S2) && p.S2 != 0.0;
                bool hasS3 = !double.IsNaN(p.S3) && p.S3 != 0.0;
                bool hasS4 = !double.IsNaN(p.S4) && p.S4 != 0.0;
                bool hasS5 = !double.IsNaN(p.S5) && p.S5 != 0.0;
                bool hasS6 = !double.IsNaN(p.S6) && p.S6 != 0.0;

                DrawHLine(g, p.PP, Color.Gray, startX, lineEndX, conv, args.Rectangle);
                DrawHLine(g, p.R1, Color.Red, startX, lineEndX, conv, args.Rectangle);
                DrawHLine(g, p.S1, Color.DodgerBlue, startX, lineEndX, conv, args.Rectangle);
                DrawHLine(g, Mid(p.PP, p.S1), this.MidColor, startX, lineEndX, conv, args.Rectangle);
                DrawHLine(g, Mid(p.PP, p.R1), this.MidColor, startX, lineEndX, conv, args.Rectangle);

                if (p.Method != CalculationMethod.DeMark)
                {
                    if (hasR2) DrawHLine(g, p.R2, Color.Red, startX, lineEndX, conv, args.Rectangle);
                    if (hasR3) DrawHLine(g, p.R3, Color.Red, startX, lineEndX, conv, args.Rectangle);
                    if (hasS2) DrawHLine(g,p.S2, Color.DodgerBlue, startX, lineEndX, conv, args.Rectangle);
                    if (hasS3) DrawHLine(g, p.S3, Color.DodgerBlue, startX, lineEndX, conv, args.Rectangle);

                    if (hasS2) DrawHLine(g, Mid(p.S1, p.S2), this.MidColor, startX, lineEndX, conv, args.Rectangle);
                    if (hasS3) DrawHLine(g, Mid(p.S2, p.S3), this.MidColor, startX, lineEndX, conv, args.Rectangle);

                    if (hasR2) DrawHLine(g, Mid(p.R1, p.R2), this.MidColor, startX, lineEndX, conv, args.Rectangle);
                    if (hasR3) DrawHLine(g, Mid(p.R2, p.R3), this.MidColor, startX, lineEndX, conv, args.Rectangle);

                }

                if (p.Method == CalculationMethod.Camarilla)
                {
                    if (hasR4) DrawHLine(g, p.R4, Color.Red, startX, lineEndX, conv, args.Rectangle);
                    if (hasR5) DrawHLine(g, p.R5, Color.Red, startX, lineEndX, conv, args.Rectangle);
                    if (hasR6) DrawHLine(g, p.R6, Color.Red, startX, lineEndX, conv, args.Rectangle);

                    if (hasS4) DrawHLine(g, p.S4, Color.DodgerBlue, startX, lineEndX, conv, args.Rectangle);
                    if (hasS5) DrawHLine(g, p.S5, Color.DodgerBlue, startX, lineEndX, conv, args.Rectangle);
                    if (hasS6) DrawHLine(g, p.S6, Color.DodgerBlue, startX, lineEndX, conv, args.Rectangle);

                    if (hasS4) DrawHLine(g, Mid(p.S3, p.S4), this.MidColor, startX, lineEndX, conv, args.Rectangle);
                    if (hasS5) DrawHLine(g, Mid(p.S4, p.S5), this.MidColor, startX, lineEndX, conv, args.Rectangle);
                    if (hasS6) DrawHLine(g, Mid(p.S5, p.S6), this.MidColor, startX, lineEndX, conv, args.Rectangle);

                    if (hasR4) DrawHLine(g, Mid(p.R3, p.R4), this.MidColor, startX, lineEndX, conv, args.Rectangle);
                    if (hasR5) DrawHLine(g, Mid(p.R4, p.R5), this.MidColor, startX, lineEndX, conv, args.Rectangle);
                    if (hasR6) DrawHLine(g, Mid(p.R5, p.R6), this.MidColor, startX, lineEndX, conv, args.Rectangle);
                }
                if (this.DisplaySeparator)
                {
                    var ordered = periodsToDraw.OrderBy(x => x.From).ToList();
                    for (int i = 1; i < ordered.Count; i++)
                    {
                        var prev = ordered[i - 1];
                        var next = ordered[i];

                        float xBoundary = (float)conv.GetChartX(next.From);

                        DrawSeparatorLine(g, prev.PP, next.PP, Color.Gray, xBoundary, conv, args.Rectangle);
                        DrawSeparatorLine(g, prev.R1, next.R1, Color.Red, xBoundary, conv, args.Rectangle);
                        DrawSeparatorLine(g, prev.S1, next.S1, Color.DodgerBlue, xBoundary, conv, args.Rectangle);
                        DrawSeparatorLine(g, Mid(prev.PP, prev.S1), Mid(next.PP, next.S1), this.MidColor, xBoundary, conv, args.Rectangle);
                        DrawSeparatorLine(g, Mid(prev.PP, prev.R1), Mid(next.PP, next.R1), this.MidColor, xBoundary, conv, args.Rectangle);

                        if (prev.Method != CalculationMethod.DeMark && next.Method != CalculationMethod.DeMark)
                        {
                            if (hasR2) DrawSeparatorLine(g, prev.R2, next.R2, Color.Red, xBoundary, conv, args.Rectangle);
                            if (hasR3) DrawSeparatorLine(g, prev.R3, next.R3, Color.Red, xBoundary, conv, args.Rectangle);

                            if (hasS2) DrawSeparatorLine(g, prev.S2, next.S2, Color.DodgerBlue, xBoundary, conv, args.Rectangle);
                            if (hasS3) DrawSeparatorLine(g, prev.S3, next.S3, Color.DodgerBlue, xBoundary, conv, args.Rectangle);

                            if (hasS2) DrawSeparatorLine(g, Mid(prev.S1, prev.S2), Mid(next.S1, next.S2), this.MidColor, xBoundary, conv, args.Rectangle);
                            if (hasS3) DrawSeparatorLine(g, Mid(prev.S2, prev.S3), Mid(next.S2, next.S3), this.MidColor, xBoundary, conv, args.Rectangle);

                            if (hasR2) DrawSeparatorLine(g, Mid(prev.R1, prev.R2), Mid(next.R1, next.R2), this.MidColor, xBoundary, conv, args.Rectangle);
                            if (hasR3) DrawSeparatorLine(g, Mid(prev.R2, prev.R3), Mid(next.R2, next.R3), this.MidColor, xBoundary, conv, args.Rectangle);
                        }

                        if (prev.Method == CalculationMethod.Camarilla && next.Method == CalculationMethod.Camarilla)
                        {
                            if (hasR4) DrawSeparatorLine(g, prev.R4, next.R4, Color.Red, xBoundary, conv, args.Rectangle);
                            if (hasR5) DrawSeparatorLine(g, prev.R5, next.R5, Color.Red, xBoundary, conv, args.Rectangle);
                            if (hasR6) DrawSeparatorLine(g, prev.R6, next.R6, Color.Red, xBoundary, conv, args.Rectangle);

                            if (hasS4) DrawSeparatorLine(g, prev.S4, next.S4, Color.DodgerBlue, xBoundary, conv, args.Rectangle);
                            if (hasS5) DrawSeparatorLine(g, prev.S5, next.S5, Color.DodgerBlue, xBoundary, conv, args.Rectangle);
                            if (hasS6) DrawSeparatorLine(g, prev.S6, next.S6, Color.DodgerBlue, xBoundary, conv, args.Rectangle);

                            if (hasS4) DrawSeparatorLine(g, Mid(prev.S3, prev.S4), Mid(next.S3, next.S4), this.MidColor, xBoundary, conv, args.Rectangle);
                            if (hasS5) DrawSeparatorLine(g, Mid(prev.S4, prev.S5), Mid(next.S4, next.S5), this.MidColor, xBoundary, conv, args.Rectangle);
                            if (hasS6) DrawSeparatorLine(g, Mid(prev.S5, prev.S6), Mid(next.S5, next.S6), this.MidColor, xBoundary, conv, args.Rectangle);

                            if (hasR4) DrawSeparatorLine(g, Mid(prev.R3, prev.R4), Mid(next.R3, next.R4), this.MidColor, xBoundary, conv, args.Rectangle);
                            if (hasR5) DrawSeparatorLine(g, Mid(prev.R4, prev.R5), Mid(next.R4, next.R5), this.MidColor, xBoundary, conv, args.Rectangle);
                            if (hasR6) DrawSeparatorLine(g, Mid(prev.R5, prev.R6), Mid(next.R5, next.R6), this.MidColor, xBoundary, conv, args.Rectangle);
                        }
                    }
                }
                if (this.ShowLabels && !(this.DrawLastPeriodOnly && p != periodsToDraw.Last()))
                {
                    Draw(g, args.Rectangle, "PP", p.PP, Color.Gray, endX, conv);
                    Draw(g, args.Rectangle, "R1", p.R1, Color.Red, endX, conv);
                    Draw(g, args.Rectangle, "S1", p.S1, Color.DodgerBlue, endX, conv);

                    if (p.Method != CalculationMethod.DeMark)
                    {
                        if (hasR2) Draw(g, args.Rectangle, "R2", p.R2, Color.Red, endX, conv);
                        if (hasR3) Draw(g, args.Rectangle, "R3", p.R3, Color.Red, endX, conv);
                        if (hasS2) Draw(g, args.Rectangle, "S2", p.S2, Color.DodgerBlue, endX, conv);
                        if (hasS2) Draw(g, args.Rectangle, "S3", p.S3, Color.DodgerBlue, endX, conv);
                        if (hasS2) Draw(g, args.Rectangle, "S1–S2", Mid(p.S1, p.S2), this.MidColor, endX, conv);
                        if (hasS3) Draw(g, args.Rectangle, "S2–S3", Mid(p.S2, p.S3), this.MidColor, endX, conv);
                        if (hasR2) Draw(g, args.Rectangle, "R1–R2", Mid(p.R1, p.R2), this.MidColor, endX, conv);
                        if (hasR3) Draw(g, args.Rectangle, "R2–R3", Mid(p.R2, p.R3), this.MidColor, endX, conv);
                    }

                    if (p.Method == CalculationMethod.Camarilla)
                    {
                        if (hasR4) Draw(g, args.Rectangle, "R4", p.R4, Color.Red, endX, conv);
                        if (hasR5) Draw(g, args.Rectangle, "R5", p.R5, Color.Red, endX, conv);
                        if (hasR6) Draw(g, args.Rectangle, "R6", p.R6, Color.Red, endX, conv);

                        if (hasR4) Draw(g, args.Rectangle, "R3–R4", Mid(p.R3, p.R4), this.MidColor, endX, conv);
                        if (hasR5) Draw(g, args.Rectangle, "R4–R5", Mid(p.R4, p.R5), this.MidColor, endX, conv);
                        if (hasR6) Draw(g, args.Rectangle, "R5–R6", Mid(p.R5, p.R6), this.MidColor, endX, conv);

                        if (hasS4) Draw(g, args.Rectangle, "S4", p.S4, Color.DodgerBlue, endX, conv);
                        if (hasS5) Draw(g, args.Rectangle, "S5", p.S5, Color.DodgerBlue, endX, conv);
                        if (hasS6) Draw(g, args.Rectangle, "S6", p.S6, Color.DodgerBlue, endX, conv);

                        if (hasS4) Draw(g, args.Rectangle, "S3–S4", Mid(p.S3, p.S4), this.MidColor, endX, conv);
                        if (hasS5) Draw(g, args.Rectangle, "S4–S5", Mid(p.S4, p.S5), this.MidColor, endX, conv);
                        if (hasS6) Draw(g, args.Rectangle, "S5–S6", Mid(p.S5, p.S6), this.MidColor, endX, conv);
                    }

                    if (!double.IsNaN(p.R1) && p.R1 != 0.0) Draw(g, args.Rectangle, "PP–R1", Mid(p.PP, p.R1), this.MidColor, endX, conv);
                    if (!double.IsNaN(p.S1) && p.S1 != 0.0) Draw(g, args.Rectangle, "PP–S1", Mid(p.PP, p.S1), this.MidColor, endX, conv);
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
        //if (this.State == IndicatorState.Ready)
        //    return;

        //var gr = Graphics.FromHdc(args.Hdc);
        //gr.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
        //gr.SetClip(args.Rectangle);
        //switch (this.State)
        //{
        //    case IndicatorState.Loading:
        //        this.DrawMessage(gr, $"Loading {this.Symbol.Name} ({this.formattedPeriod} aggregation).", args.Rectangle);
        //        break;
        //    case IndicatorState.NoData:
        //        this.DrawMessage(gr, $"No data.", args.Rectangle);
        //        break;
        //    case IndicatorState.IncorrectPeriod:
        //        this.DrawMessage(gr, $"Incorrect period.\n The 'Pivot point' period should be greater or equal than chart period.", args.Rectangle);
        //        break;
        //    case IndicatorState.OneTickNotAllowed:
        //        this.DrawMessage(gr, $"Incorrect chart period.\n The 'Pivot point' does not support chart with '1 Tick' aggregation.", args.Rectangle);
        //        break;
        //    case IndicatorState.Calculation:
        //        {
        //            // draw lines
        //        }
        //        break;
        //}
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

        this.UpdateLabelsFromLastPivot();
    }
    private PivotPointCalculationResponce CalculatePivotPoint(HistoricalData hd, int hdOffset)
    {
        if (hd == null)
            return null;

        if (hd.Count <= hdOffset || hd.Count <= hdOffset + 1)
            return null;

        var currentItem = hd[hdOffset + 1];

        var close = currentItem[PriceType.Close];
        var high = currentItem[PriceType.High];
        var low = currentItem[PriceType.Low];
        double pp, r1, r2, r3, r4, r5, r6, s1, s2, s3, s4, s5, s6;
        pp = r1 = r2 = r3 = r4 = r5 = r6 = s1 = s2 = s3 = s4 = s5 = s6 = 0;
            switch (this.IndicatorCalculationMethod)
            {
                case CalculationMethod.Classic:
                    {
                        pp = (high + low + close) / 3;
                        var range = high - low;
                        r1 = 2 * pp - low;
                        r2 = pp + range;
                        r3 = r2+range;
                        r4 = r3+range;

                        s1 = 2 * pp - high;
                        s2 = pp - range;
                        s3 = s2 - range;
                        s4 = s3 - range;
                    }
                    break;
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
                    }
                    break;
                case CalculationMethod.Fibonacci:
                    {
                        pp = (high + low + close) / 3;

                        r1 = pp + 0.382 * (high - low);
                        r2 = pp + 0.618 * (high - low);
                        r3 = pp + (high - low);

                        s1 = pp - 0.382 * (high - low);
                        s2 = pp - 0.618 * (high - low);
                        s3 = pp - (high - low);
                    }
                    break;
                case CalculationMethod.Woodie:
                    {
                        pp = (high + low + 2 * close) / 4;

                        r1 = 2 * pp - low;
                        r2 = pp + high - low;
                        r3 = high + 2 * (pp - low);

                        s1 = 2 * pp - high;
                        s2 = pp + low - high;
                        s3 = low - 2 * (high - pp);
                    }
                    break;
                case CalculationMethod.DeMark:
                    {
                        var x = 0D;
                        if (hd.Count > hdOffset + 2)
                        {
                            var open0 = hd[hdOffset + 2][PriceType.Open];

                            if (close < open0)
                                x = high + 2 * low + close;
                            else if (close > open0)
                                x = 2 * high + low + close;
                            else
                                x = high + low + 2 * close;
                        }

                        pp = x / 4;
                        r1 = x / 2 - low;
                        s1 = x / 2 - high;

                    }
                    break;
            }
        var timeZone = this.GetTimeZone();
        var historyItem = (HistoryItemBar)hd[hdOffset, SeekOriginHistory.End];

        var leftUtc = DateTime.SpecifyKind(new DateTime(historyItem.TicksLeft), DateTimeKind.Utc);
        var leftLocal = TimeZoneInfo.ConvertTimeFromUtc(leftUtc, timeZone.TimeZoneInfo);

        DateTime startLocal;
        DateTime endLocal;

        switch (this.BasePeriod)
        {
            case BasePeriod.Hour:
                {
                    int alignedHour = leftLocal.Hour - (leftLocal.Hour % this.PeriodValue);
                    startLocal = new DateTime(leftLocal.Year, leftLocal.Month, leftLocal.Day, alignedHour, 0, 0, DateTimeKind.Unspecified);
                    endLocal = startLocal.AddHours(this.PeriodValue);
                    break;
                }

            case BasePeriod.Day:
                {
                    startLocal = new DateTime(leftLocal.Year, leftLocal.Month, leftLocal.Day, 0, 0, 0, DateTimeKind.Unspecified);
                    endLocal = startLocal.AddDays(this.PeriodValue);
                    break;
                }

            case BasePeriod.Week:
                {
                    var baseDate = new DateTime(leftLocal.Year, leftLocal.Month, leftLocal.Day, 0, 0, 0, DateTimeKind.Unspecified);
                    int delta = (int)baseDate.DayOfWeek;
                    startLocal = baseDate.AddDays(-delta);
                    endLocal = startLocal.AddDays(7 * this.PeriodValue);
                    break;
                }

            case BasePeriod.Month:
                {
                    startLocal = new DateTime(leftLocal.Year, leftLocal.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
                    endLocal = startLocal.AddMonths(this.PeriodValue);
                    break;
                }

            default:
                {
                    startLocal = new DateTime(leftLocal.Year, leftLocal.Month, leftLocal.Day, 0, 0, 0, DateTimeKind.Unspecified);
                    endLocal = startLocal.AddDays(1);
                    break;
                }
        }

        DateTime startTime = Core.Instance.TimeUtils.ConvertFromTimeZoneToUTC(startLocal, timeZone);
        DateTime endTime = Core.Instance.TimeUtils.ConvertFromTimeZoneToUTC(endLocal, timeZone);

        return new PivotPointCalculationResponce(startTime, endTime)
        {
            PP = pp,
            R1 = r1,
            R2 = r2,
            R3 = r3,
            R4 = r4,
            S1 = s1,
            S2 = s2,
            S3 = s3,
            S4 = s4,
            S5 = s5,
            S6 = s6,
            R5 = r5,
            R6 = r6,
            Method = this.IndicatorCalculationMethod,
            Period = new Period(this.BasePeriod, this.PeriodValue)
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

        this.UpdateLabelsFromLastPivot();

    }

    #endregion Calculation 

    #region Drawing
    private void DrawHLine(Graphics g, double value, Color color, float x1, float x2, IChartWindowCoordinatesConverter conv, Rectangle rect, float width = 1f)
    {
        if (double.IsNaN(value) || value == 0.0)
            return;

        float y = (float)conv.GetChartY(value);
        if (float.IsNaN(y) || y <= rect.Top || y >= rect.Bottom)
            return;

        // clamp X to visible rect (optional, но удобно)
        x1 = Math.Max(rect.Left, Math.Min(rect.Right, x1));
        x2 = Math.Max(rect.Left, Math.Min(rect.Right, x2));

        using var pen = new Pen(color, width);
        g.DrawLine(pen, x1, y, x2, y);
    }

    private void DrawSeparatorLine(Graphics g, double prevValue, double nextValue, Color color, float x, IChartWindowCoordinatesConverter conv, Rectangle rect, float width = 1f)
    {
        if (double.IsNaN(prevValue) || prevValue == 0.0) return;
        if (double.IsNaN(nextValue) || nextValue == 0.0) return;

        float y1 = (float)conv.GetChartY(prevValue);
        float y2 = (float)conv.GetChartY(nextValue);

        if (float.IsNaN(y1) || float.IsNaN(y2)) return;

        x = Math.Max(rect.Left, Math.Min(rect.Right, x));

        using var pen = new Pen(color, width);
        g.DrawLine(pen, x, y1, x, y2);
    }
    void Draw(Graphics g, Rectangle rect, string name, double value, Color color, float endX, IChartWindowCoordinatesConverter conv)
    {
        if (double.IsNaN(value) || value == 0.0)
            return;

        float y = (float)conv.GetChartY(value);
        string label = this.ShowPriceInLabels ? $"{name} {this.Symbol.FormatPrice(value)}" : name;
        this.DrawLevelLabel(g, rect, y, label, color, endX);
    }
    private void DrawLevelLabel(Graphics g, Rectangle chartRect, float y, string text, Color backColor, float anchorX)
    {
        if (float.IsNaN(y) || y <= chartRect.Top || y >= chartRect.Bottom)
            return;

        var size = g.MeasureString(text, this.labelFont);

        var rect = new RectangleF(
            anchorX - (size.Width + LabelPadX * 2f),           // X
            y - size.Height / 2f - LabelPadY,                  // Y
            size.Width + LabelPadX * 2f,                       // W
            size.Height + LabelPadY * 2f                       // H
        );

        using (var brush = new SolidBrush(backColor))
            g.FillRectangle(brush, rect);

        g.DrawString(text, this.labelFont, Brushes.White, rect, this.labelSF);
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
        if (this.DailySessionType == DailySessionType.SpecifiedSession &&
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

    private Interval<DateTime> GetFullDayTimeInterval(TradingPlatform.BusinessLayer.TimeZone timeZone)
    {
        var openTime = new DateTime(DateTime.UtcNow.Date.Ticks, DateTimeKind.Unspecified);
        var closeTime = new DateTime(DateTime.UtcNow.Date.AddDays(-1).Ticks, DateTimeKind.Unspecified);

        return new Interval<DateTime>(
            Core.Instance.TimeUtils.ConvertFromTimeZoneToUTC(openTime, timeZone),
            Core.Instance.TimeUtils.ConvertFromTimeZoneToUTC(closeTime, timeZone));
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