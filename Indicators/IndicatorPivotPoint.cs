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

    [InputParameter(CURRENT_PERIOD_INPUT_PARAMETER, 0)]
    public bool OnlyCurrentPeriod = false;

    [InputParameter(BASE_PERIOD_INPUT_PARAMETER, 1, variants: new object[]
    {
        "Hour", BasePeriod.Hour,
        "Day", BasePeriod.Day,
        "Week", BasePeriod.Week,
        "Month", BasePeriod.Month
    })]
    public BasePeriod BasePeriod = BasePeriod.Day;

    [InputParameter(RANGE_INPUT_PARAMETER, 2, 1, 60, 1, 0)]
    public int PeriodValue = 1;

    [InputParameter(CALCULATION_METHOD_INPUT_PARAMETER, 3, variants: new object[]
    {
        "Classic", CalculationMethod.Classic,
        "Camarilla", CalculationMethod.Camarilla,
        "Fibonacci", CalculationMethod.Fibonacci,
        "Woodie", CalculationMethod.Woodie,
        "DeMark", CalculationMethod.DeMark
    })]
    public CalculationMethod IndicatorCalculationMethod = CalculationMethod.Classic;

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

        var inputPeriod = new Period(this.BasePeriod, this.PeriodValue);
        this.formattedPeriod = this.GetFormattedPeriod(this.BasePeriod, this.PeriodValue);

        this.State = IndicatorState.Ready;

        if (inputPeriod.Ticks < this.HistoricalData.Aggregation.GetPeriod.Ticks)
        {
            this.State = IndicatorState.IncorrectPeriod;
            return;
        }

        if (this.HistoricalData.Aggregation is not HistoryAggregationTime historyAggregationTime)
        {
            this.State = IndicatorState.OneTickNotAllowed;
            return;
        }

        this.loadingTask = Task.Factory.StartNew(() =>
        {
            if (token.IsCancellationRequested)
                return;

            this.State = IndicatorState.Loading;
            var fromTime = this.HistoricalData.FromTime;

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
                    Aggregation = new HistoryAggregationTime(inputPeriod, historyAggregationTime.HistoryType),
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

            if (settings.GetItemByName(RANGE_INPUT_PARAMETER) is SettingItemInteger rangeSi)
                rangeSi.ValueChangingBehavior = SettingItemValueChangingBehavior.WithConfirmation;

            if (settings.GetItemByName(BASE_PERIOD_INPUT_PARAMETER) is SettingItemSelectorLocalized periodSi)
                periodSi.ValueChangingBehavior = SettingItemValueChangingBehavior.WithConfirmation;

            return settings;
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
                    r5 = r4+1.168*(r4-r3);
                    r6 = (high/low)*close;

                    s1 = close - 0.0916 * (high - low);
                    s2 = close - 0.183 * (high - low);
                    s3 = close - 0.275 * (high - low);
                    s4 = close - 0.55 * (high - low);
                    s5 = s4-1.168*(s3-s4);
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

        for (int y = fromIndex; y >= toIndex; y--)
        {
            this.SetValue(ppItem.PP, PP_LINE_INDEX, y);
            this.SetValue(ppItem.R1, R1_LINE_INDEX, y);
            this.SetValue(ppItem.S1, S1_LINE_INDEX, y);

            if (ppItem.Method != CalculationMethod.DeMark)
            {
                this.SetValue(ppItem.R2, R2_LINE_INDEX, y);
                this.SetValue(ppItem.R3, R3_LINE_INDEX, y);
                this.SetValue(ppItem.S2, S2_LINE_INDEX, y);
                this.SetValue(ppItem.S3, S3_LINE_INDEX, y);
            }

            if (ppItem.Method == CalculationMethod.Camarilla)
            {
                this.SetValue(ppItem.R4, R4_LINE_INDEX, y);
                this.SetValue(ppItem.R5, R5_LINE_INDEX, y);
                this.SetValue(ppItem.R6, R6_LINE_INDEX, y);

                this.SetValue(ppItem.S4, S4_LINE_INDEX, y);
                this.SetValue(ppItem.S5, S5_LINE_INDEX, y);
                this.SetValue(ppItem.S6, S6_LINE_INDEX, y);
            }
        }
    }
    //private void DrawMessage(Graphics gr, string message, Rectangle rectangle) => gr.DrawString(message, this.font, this.messageBrush, rectangle, this.centerCenterSF);
    #endregion Drawing

    private void History_NewHistoryItem(object sender, HistoryEventArgs e) => this.CalculateLastPeriod();

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
#endregion Utils