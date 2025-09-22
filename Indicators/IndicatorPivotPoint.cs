// Copyright QUANTOWER LLC. © 2017-2024. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TradingPlatform.BusinessLayer;

namespace TrendIndicators;

public class IndicatorPivotPoint : Indicator, IWatchlistIndicator
{
    #region Parameters
    private const string CURRENT_PERIOD_INPUT_PARAMETER = "Only current period";
    private const string BASE_PERIOD_INPUT_PARAMETER = "Base period";
    private const string RANGE_INPUT_PARAMETER = "Range";
    private const string CALCULATION_METHOD_INPUT_PARAMETER = "Calculation method";

    private const int MIN_HISTORY_COUNT = 3;

    private const int PP_LINE_INDEX = 0;

    private const int R1_LINE_INDEX = 1;
    private const int R2_LINE_INDEX = 2;
    private const int R3_LINE_INDEX = 3;
    private const int R4_LINE_INDEX = 4;
    private const int R5_LINE_INDEX = 5;
    private const int R6_LINE_INDEX = 6;

    private const int S1_LINE_INDEX = 7;
    private const int S2_LINE_INDEX = 8;
    private const int S3_LINE_INDEX = 9;
    private const int S4_LINE_INDEX = 10;
    private const int S5_LINE_INDEX = 11;
    private const int S6_LINE_INDEX = 12;

    private const int MID_PP_R1 = 13;
    private const int MID_R1_R2 = 14;
    private const int MID_R2_R3 = 15;
    private const int MID_R3_R4 = 16;
    private const int MID_R4_R5 = 17;
    private const int MID_R5_R6 = 18;
    private const int MID_PP_S1 = 19;
    private const int MID_S1_S2 = 20;
    private const int MID_S2_S3 = 21;
    private const int MID_S3_S4 = 22;
    private const int MID_S4_S5 = 23;
    private const int MID_S5_S6 = 24;

    public bool OnlyCurrentPeriod = false;
    public string CustomSessionName = string.Empty;
    public DateTime CustomRangeStartTime
    {
        get
        {
            if (this.customRangeStartTime == default)
            {
                var session = this.CreateDefaultSession();
                this.customRangeStartTime = Core.Instance.TimeUtils.DateTimeUtcNow.Date.AddTicks(session.OpenTime.Ticks);
            }
            return this.customRangeStartTime;
        }
        set => this.customRangeStartTime = value;
    }
    private DateTime customRangeStartTime;
    public DateTime CustomRangeEndTime
    {
        get
        {
            if (this.customRangeEndTime == default)
                this.customRangeEndTime = this.CustomRangeStartTime;

            return this.customRangeEndTime;
        }
        set => this.customRangeEndTime = value;
    }
    private DateTime customRangeEndTime;
    public BasePeriod BasePeriod = BasePeriod.Day;
    public int PeriodValue = 1;
    public CalculationMethod IndicatorCalculationMethod = CalculationMethod.Classic;
    public bool ShowMidPivots = true;
    public DailySessionType DailySessionType = DailySessionType.AllDay;
    private readonly Color MidColor = Color.FromArgb(128, 128, 128, 128);
    private Task loadingTask;
    private ISession currentSession;
    private ISessionsContainer chartSessionContainer;
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
    private PivotPointCalculationResponce lastPivotPeriod;
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
    {
        this.Name = "Pivot Point";

        this.AddLineSeries("PP", Color.Gray, 1, LineStyle.Solid);   // 0
        this.AddLineSeries("R1", Color.Red, 1, LineStyle.Solid);    // 1
        this.AddLineSeries("R2", Color.Red, 1, LineStyle.Solid);    // 2
        this.AddLineSeries("R3", Color.Red, 1, LineStyle.Solid);    // 3
        this.AddLineSeries("R4", Color.Red, 1, LineStyle.Solid);    // 4
        this.AddLineSeries("R5", Color.Red, 1, LineStyle.Solid);    // 5
        this.AddLineSeries("R6", Color.Red, 1, LineStyle.Solid);    // 6

        this.AddLineSeries("S1", Color.DodgerBlue, 1, LineStyle.Solid); // 7
        this.AddLineSeries("S2", Color.DodgerBlue, 1, LineStyle.Solid); // 8
        this.AddLineSeries("S3", Color.DodgerBlue, 1, LineStyle.Solid); // 9
        this.AddLineSeries("S4", Color.DodgerBlue, 1, LineStyle.Solid); // 10
        this.AddLineSeries("S5", Color.DodgerBlue, 1, LineStyle.Solid); // 11
        this.AddLineSeries("S6", Color.DodgerBlue, 1, LineStyle.Solid); // 12

        this.SeparateWindow = false;
        this.AddLineSeries("R1-PP", MidColor, 1, LineStyle.Solid);  // 13
        this.AddLineSeries("R2-R1", MidColor, 1, LineStyle.Solid);  // 14
        this.AddLineSeries("R3-R2", MidColor, 1, LineStyle.Solid);  // 15
        this.AddLineSeries("R4-R3", MidColor, 1, LineStyle.Solid);  // 16
        this.AddLineSeries("R5-R4", MidColor, 1, LineStyle.Solid);  // 17
        this.AddLineSeries("R6-R5", MidColor, 1, LineStyle.Solid);  // 18

        this.AddLineSeries("S1-PP", MidColor, 1, LineStyle.Solid);  // 19
        this.AddLineSeries("S2-S1", MidColor, 1, LineStyle.Solid);  // 20
        this.AddLineSeries("S3-S2", MidColor, 1, LineStyle.Solid);  // 21
        this.AddLineSeries("S4-S3", MidColor, 1, LineStyle.Solid);  // 22
        this.AddLineSeries("S5-S4", MidColor, 1, LineStyle.Solid);  // 23
        this.AddLineSeries("S6-S5", MidColor, 1, LineStyle.Solid);  // 24
        //this.font = new Font("Tahoma", 8, FontStyle.Bold);
        //this.centerCenterSF = new StringFormat() { LineAlignment = StringAlignment.Center, Alignment = StringAlignment.Center };
        //this.messageBrush = new SolidBrush(Color.DodgerBlue);
    }

    #region Overrides
    protected override void OnInit()
    {
        base.OnInit();
        this.AbortPreviousTask();
        var token = this.cancellationSource.Token;

        if (this.Symbol == null)
            return;
        if (this.DailySessionType == DailySessionType.AllDay)
        {
            // default session
            this.currentSession = this.CreateDefaultSession();
        }
        else if (this.DailySessionType == DailySessionType.SpecifiedSession)
        {
            if (!string.IsNullOrEmpty(this.CustomSessionName))
            {
                // selected chart session
                var sessions = this.GetAvailableCustomChartSessions().Concat(this.GetAvailableSymbolSessions()).ToList();
                if (sessions.Count > 0)
                    this.currentSession = sessions.FirstOrDefault(s => s.Name.Equals(this.CustomSessionName) && s.Type == SessionType.Main);
            }
        }
        else if (this.DailySessionType == DailySessionType.CustomRange)
            this.currentSession = new Session("Custom session", this.CustomRangeStartTime.TimeOfDay, this.CustomRangeEndTime.TimeOfDay);

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
            if (inputPeriod.Ticks < ((HistoryAggregationTime)this.HistoricalData.Aggregation).Period.Duration.Ticks)
            {
                this.State = IndicatorState.IncorrectPeriod;
                return;
            }
        }
        if (this.HistoricalData.Aggregation is HistoryAggregationTickBars historyAggregationTickBars)
        {
            currHistoryType = historyAggregationTickBars.HistoryType;
            if (inputPeriod.Ticks < ((HistoryAggregationTickBars)this.HistoricalData.Aggregation).TicksCount)
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

            //Core.Loggers.Log($"-------------------------------------");

            var needReload = true;
            while (needReload)
            {
                needReload = false;

                if (token.IsCancellationRequested)
                    return;
                // 
                coefficient += 1;
                var minimumRangeInTicks = inputPeriod.Ticks * MIN_HISTORY_COUNT * coefficient;
                if (Core.TimeUtils.DateTimeUtcNow.Ticks - fromTime.Ticks < minimumRangeInTicks)
                    fromTime = Core.TimeUtils.DateTimeUtcNow - new TimeSpan(minimumRangeInTicks);

                //Core.Loggers.Log($"Pivot point. Period:({inputPeriod}); From:({fromTime}); {this.Symbol} - START");

                this.history = this.Symbol.GetHistory(new HistoryRequestParameters()
                {
                    Symbol = this.Symbol,
                    FromTime = fromTime,
                    CancellationToken = token,
                    Aggregation = new HistoryAggregationTime(inputPeriod, currHistoryType),
                });

                //Core.Loggers.Log($"Pivot point. Period:({inputPeriod}); From:({this.history.FromTime}); LoadedCount:{this.history.Count}; try:{coefficient}; {this.Symbol}");

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


        if (args.Reason == UpdateReason.NewBar && this.State == IndicatorState.Calculation)
            this.DrawIndicatorLines(this.lastPivotPeriod, 0, 0);
    }
    protected override void OnClear()
    {
        base.OnClear();

        this.AbortPreviousTask();

        this.lastPivotPeriod = null;
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
            settings.Add(new SettingItemString("Custom session name", this.CustomSessionName, 20)
            {
                SeparatorGroup = defaultSeparator,
                Text = loc._("Custom session name"),
                Relation = new SettingItemRelationVisibility("Session type", specifiedSession)
            });
            settings.Add(new SettingItemDateTime("Start time", this.customRangeStartTime, 20)
            {
                SeparatorGroup = defaultSeparator,
                Text = loc._("Start time"),
                Format = DatePickerFormat.LongTime,
                ValueChangingBehavior = SettingItemValueChangingBehavior.WithConfirmation,
                Relation = customRangeMultRelation
            });
            settings.Add(new SettingItemDateTime("End time", this.customRangeEndTime, 20)
            {
                SeparatorGroup = defaultSeparator,
                Text = loc._("End time"),
                Format = DatePickerFormat.LongTime,
                ValueChangingBehavior = SettingItemValueChangingBehavior.WithConfirmation,
                Relation = customRangeMultRelation
            });
            settings.Add(new SettingItemInteger(RANGE_INPUT_PARAMETER, this.PeriodValue, 10)
            {
                SeparatorGroup = defaultSeparator,
                Text = loc._(RANGE_INPUT_PARAMETER),
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

            // Show mid pivot points
            settings.Add(new SettingItemBoolean("Show mid pivot points", this.ShowMidPivots, 90)
            {
                SeparatorGroup = defaultSeparator,
                Text = loc._("Show mid pivot points")
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
            if (holder.TryGetValue("Custom session name", out item) && item.Value is string customSessionName)
                this.CustomSessionName = customSessionName;
            if (holder.TryGetValue("Start time", out item) && item.Value is DateTime dtStartTime)
            {
                this.customRangeStartTime = dtStartTime;
                needRefresh |= item.ValueChangingReason == SettingItemValueChangingReason.Manually;
            }
            if (holder.TryGetValue("End time", out item) && item.Value is DateTime dtEndTime)
            {
                this.customRangeEndTime = dtEndTime;
                needRefresh |= item.ValueChangingReason == SettingItemValueChangingReason.Manually;
            }
            if (holder.TryGetValue("Show mid pivot points", out item) && item.Value is bool showMid)
                this.ShowMidPivots = showMid;

            if (needRefresh)
                this.Refresh();

            base.Settings = value;
        }
    }

    public override void OnPaintChart(PaintChartEventArgs args)
    {
        base.OnPaintChart(args);

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
                if (i == 0)
                    this.lastPivotPeriod = responce;

                pivotPoints.Add(responce);
            }
        }

        this.DrawIndicator(pivotPoints.ToArray());
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

                    r1 = 2 * pp - low;
                    r2 = pp + high - low;
                    r3 = 2 * pp + high - 2 * low;

                    s1 = 2 * pp - high;
                    s2 = pp + low - high;
                    s3 = 2 * pp + low - 2 * high;
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

        var historyItem = hd[hdOffset, SeekOriginHistory.End];
        return new PivotPointCalculationResponce(historyItem.TimeLeft, new DateTime(historyItem.TicksRight, DateTimeKind.Utc))
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
        this.lastPivotPeriod = this.CalculatePivotPoint(this.history, 0);

        if (this.lastPivotPeriod != null)
            this.DrawIndicator(this.lastPivotPeriod);
    }
    #endregion Calculation 

    #region Drawing
    private void DrawIndicator(params PivotPointCalculationResponce[] pivotPointValues)
    {
        // 
        if (this.HistoricalData == null)
            return;

        // new collection
        for (int i = 0; i < pivotPointValues.Count(); i++)
        {
            var ppItem = pivotPointValues[i];
            var fromIndex = (int)this.HistoricalData.GetIndexByTime(ppItem.From.Ticks);
            var toIndex = (int)this.HistoricalData.GetIndexByTime(ppItem.To.Ticks);

            if (fromIndex == -1)
                fromIndex = this.Count - 1;
            if (toIndex == -1)
                toIndex = 0;

            this.DrawIndicatorLines(ppItem, fromIndex, toIndex);
        }

    }
    private void DrawIndicatorLines(PivotPointCalculationResponce ppItem, int fromIndex, int toIndex)
    {
        if (ppItem == null)
            return;
        double pp = ppItem.PP;
        double r1 = ppItem.R1, r2 = ppItem.R2, r3 = ppItem.R3,
               r4 = ppItem.R4, r5 = ppItem.R5, r6 = ppItem.R6;
        double s1 = ppItem.S1, s2 = ppItem.S2, s3 = ppItem.S3,
               s4 = ppItem.S4, s5 = ppItem.S5, s6 = ppItem.S6;
        for (int y = fromIndex; y >= toIndex; y--)
        {
            var currentBarTime = this.HistoricalData[y].TimeLeft;
            var inSession = this.currentSession.ContainsDate(currentBarTime);
            if (!inSession)
                continue;
            this.SetValue(pp, PP_LINE_INDEX, y);
            this.SetValue(r1, R1_LINE_INDEX, y);
            this.SetValue(s1, S1_LINE_INDEX, y);

            if (ppItem.Method != CalculationMethod.DeMark)
            {
                this.SetValue(r2, R2_LINE_INDEX, y);
                this.SetValue(r3, R3_LINE_INDEX, y);
                this.SetValue(s2, S2_LINE_INDEX, y);
                this.SetValue(s3, S3_LINE_INDEX, y);
            }

            if (ppItem.Method == CalculationMethod.Camarilla)
            {
                this.SetValue(r4, R4_LINE_INDEX, y);
                this.SetValue(r5, R5_LINE_INDEX, y);
                this.SetValue(r6, R6_LINE_INDEX, y);

                this.SetValue(s4, S4_LINE_INDEX, y);
                this.SetValue(s5, S5_LINE_INDEX, y);
                this.SetValue(s6, S6_LINE_INDEX, y);
            }
            if (this.ShowMidPivots)
            {
                bool hasR2 = !double.IsNaN(r2) && r2 != 0.0;
                bool hasR3 = !double.IsNaN(r3) && r3 != 0.0;
                bool hasR4 = !double.IsNaN(r4) && r4 != 0.0;
                bool hasR5 = !double.IsNaN(r5) && r5 != 0.0;
                bool hasR6 = !double.IsNaN(r6) && r6 != 0.0;

                bool hasS2 = !double.IsNaN(s2) && s2 != 0.0;
                bool hasS3 = !double.IsNaN(s3) && s3 != 0.0;
                bool hasS4 = !double.IsNaN(s4) && s4 != 0.0;
                bool hasS5 = !double.IsNaN(s5) && s5 != 0.0;
                bool hasS6 = !double.IsNaN(s6) && s6 != 0.0;

                if (!double.IsNaN(r1) && r1 != 0.0)
                    this.SetValue(Mid(pp, r1), MID_PP_R1, y);
                if (hasR2)
                    this.SetValue(Mid(r1, r2), MID_R1_R2, y);
                if (hasR3)
                    this.SetValue(Mid(r2, r3), MID_R2_R3, y);
                if (hasR4)
                    this.SetValue(Mid(r3, r4), MID_R3_R4, y);
                if (hasR5)
                    this.SetValue(Mid(r4, r5), MID_R4_R5, y);
                if (hasR6)
                    this.SetValue(Mid(r5, r6), MID_R5_R6, y);

                if (!double.IsNaN(s1) && s1 != 0.0)
                    this.SetValue(Mid(pp, s1), MID_PP_S1, y);
                if (hasS2)
                    this.SetValue(Mid(s1, s2), MID_S1_S2, y);
                if (hasS3)
                    this.SetValue(Mid(s2, s3), MID_S2_S3, y);
                if (hasS4)
                    this.SetValue(Mid(s3, s4), MID_S3_S4, y);
                if (hasS5)
                    this.SetValue(Mid(s4, s5), MID_S4_S5, y);
                if (hasS6)
                    this.SetValue(Mid(s5, s6), MID_S5_S6, y);
            }
        }
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
    private Session CreateDefaultSession()
    {
        // 00:00
        var startTime = new DateTime(Core.Instance.TimeUtils.DateTimeUtcNow.Date.Ticks, DateTimeKind.Unspecified);
        // 23:59:59
        var endTime = new DateTime(Core.Instance.TimeUtils.DateTimeUtcNow.Date.AddHours(23).AddMinutes(59).AddSeconds(59).Ticks, DateTimeKind.Unspecified);

        var timeZone = this.CurrentChart?.CurrentTimeZone ?? Core.Instance.TimeUtils.SelectedTimeZone;
        return new Session("Default",
            Core.Instance.TimeUtils.ConvertFromTimeZoneToUTC(startTime, timeZone).TimeOfDay,
            Core.Instance.TimeUtils.ConvertFromTimeZoneToUTC(endTime, timeZone).TimeOfDay);
    }
    private IList<ISession> GetAvailableCustomChartSessions() => this.chartSessionContainer?.ActiveSessions?.ToList() ?? new List<ISession>();
    private IList<ISession> GetAvailableSymbolSessions() => this.Symbol?.CurrentSessionsInfo?.ActiveSessions?.ToList() ?? new List<ISession>();
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