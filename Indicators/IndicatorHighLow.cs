// Copyright QUANTOWER LLC. © 2017-2024. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TradingPlatform.BusinessLayer;

namespace ChanneIsIndicators;

public class IndicatorHighLow : Indicator, IWatchlistIndicator
{
    #region Parameters
    public const string RANGE_INPUT_PARAMETER = "Range";
    public const string PERIOD_INPUT_PARAMETER = "Period";

    public const double DEFAULT_HIGH_PRICE = double.MinValue;
    public const double DEFAULT_LOW_PRICE = double.MaxValue;

    [InputParameter(RANGE_INPUT_PARAMETER, 0, 1, 999, 1, 0)]
    public int Range = 5;

    [InputParameter(PERIOD_INPUT_PARAMETER, 0, variants: new object[]
    {
        "Minute", BasePeriod.Minute,
        "Hour", BasePeriod.Hour,
        "Day", BasePeriod.Day,
        "Week", BasePeriod.Week,
        "Month", BasePeriod.Month
    })]
    public BasePeriod BasePeriod = BasePeriod.Day;

    public int MinHistoryDepths => 0;

    public override string ShortName
    {
        get
        {
            string additionalInfo = this.GetFormattedPeriod(this.BasePeriod, this.Range);

            if (this.State == IndicatorState.Loading)
                additionalInfo = "Loading";
            else if (this.State == IndicatorState.NoData)
                additionalInfo = "No data";

            return $"HL ({additionalInfo})";
        }
    }

    public IndicatorState State { get; private set; }
    public double HighPrice { get; private set; }
    public double LowPrice { get; private set; }

    private long historyTicksRange;
    private string formattedPeriod;
    private CancellationTokenSource cancellationSource;
    private HistoricalData history;

    private readonly Font font;
    private SolidBrush highLabelBrush;
    private SolidBrush lowLabelBrush;

    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorHighLow.cs";

    #endregion Parameters

    public IndicatorHighLow()
    {
        this.Name = "HighLow indicator";

        this.AddLineSeries("High line", Color.FromArgb(239, 83, 80), 2, LineStyle.Solid);
        this.AddLineSeries("Low line", Color.FromArgb(33, 150, 243), 2, LineStyle.Solid);

        this.SeparateWindow = false;
        this.font = new Font("Tahoma", 8, FontStyle.Bold);

        this.UpdateBrushes();
    }

    #region Overrides

    protected override void OnInit()
    {
        base.OnInit();

        this.AbortPreviousTask();
        var token = this.cancellationSource.Token;

        this.historyTicksRange = this.GetTicksRange(this.BasePeriod, this.Range);
        this.formattedPeriod = this.GetFormattedPeriod(this.BasePeriod, this.Range);

        // clear indicator
        this.DrawIndicator(this.Count - 1, double.NaN, double.NaN);

        if (this.Symbol == null)
            return;

        this.State = IndicatorState.Ready;
        int coefficient = 0;
        int prevHistoryCount = 0;

        Task.Factory.StartNew(() =>
        {
            bool needReload = true;
            while (needReload)
            {
                needReload = false;
                if (token.IsCancellationRequested)
                {
                    this.State = IndicatorState.NoData;
                    break;
                }

                var period = this.GetAggregationPeriod(this.BasePeriod);

                coefficient += 1;
                this.State = IndicatorState.Loading;
                this.history = this.Symbol.GetHistory(new HistoryRequestParameters()
                {
                    Period = period,
                    Symbol = this.Symbol,
                    HistoryType = this.HistoricalData.HistoryType,
                    FromTime = Core.TimeUtils.DateTimeUtcNow.AddTicks(-this.historyTicksRange * coefficient),
                    CancellationToken = token,
                    Aggregation = new HistoryAggregationTime(period),
                });

                if (token.IsCancellationRequested || prevHistoryCount == this.history.Count)
                {
                    this.State = IndicatorState.NoData;
                    break;
                }
                else if (this.CheckHistory(this.history, this.historyTicksRange))
                {
                    this.State = IndicatorState.Loaded;
                    this.history.NewHistoryItem += this.History_NewHistoryItem;
                    this.CalculateIndicator();
                    break;
                }
                else
                {
                    prevHistoryCount = this.history.Count;
                    needReload = true;
                    this.history.Dispose();
                }
            }
        }, token);
    }
    protected override void OnUpdate(UpdateArgs args)
    {
        base.OnUpdate(args);

        bool isNewItem = this.HistoricalData.Period == Period.TICK1
            ? args.Reason == UpdateReason.NewTick
            : args.Reason == UpdateReason.NewBar;

        double currentHigh = this.GetPrice(PriceType.High);
        double currentLow = this.GetPrice(PriceType.Low);
        if (this.State == IndicatorState.AllGreat)
        {
            bool needRedrawIndicator = true;
            if (this.HighPrice < currentHigh)
                this.HighPrice = currentHigh;
            else if (this.LowPrice > currentLow)
                this.LowPrice = currentLow;
            else
                needRedrawIndicator = false;

            if (needRedrawIndicator)
                this.DrawIndicator(this.Count - 1, this.HighPrice, this.LowPrice);
            else if (isNewItem)
                this.DrawIndicator(0, this.HighPrice, this.LowPrice);
        }
    }
    protected override void OnClear()
    {
        base.OnClear();

        this.AbortPreviousTask();
    }
    public override void OnPaintChart(PaintChartEventArgs args)
    {
        base.OnPaintChart(args);

        if (this.Symbol == null)
            return;

        if (this.State == IndicatorState.AllGreat)
        {
            var gr = args.Graphics;
            gr.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
            gr.SetClip(args.Rectangle);

            double zeroBarHighLineX = 0d;
            double zeroBarLowLineX = 0d;

            if (this.LinesSeries.Any(l => l.TimeShift != 0))
            {
                zeroBarHighLineX = this.GetLabelRightX(args.Rectangle, this.LinesSeries[0].TimeShift);
                zeroBarLowLineX = this.GetLabelRightX(args.Rectangle, this.LinesSeries[1].TimeShift);
            }
            else
            {
                zeroBarHighLineX = zeroBarLowLineX = this.GetLabelRightX(args.Rectangle, this.LinesSeries[0].TimeShift);
            }

            float highPriceY = (float)this.CurrentChart.MainWindow.CoordinatesConverter.GetChartY(this.HighPrice);
            float lowPriceY = (float)this.CurrentChart.MainWindow.CoordinatesConverter.GetChartY(this.LowPrice);

            this.UpdateBrushes();

            if (this.LinesSeries[0].Visible)
                this.DrawLabel(gr, (float)zeroBarHighLineX, highPriceY, $"High ({this.formattedPeriod}): {this.Symbol.FormatPrice(this.HighPrice)}", this.highLabelBrush, LabelPosition.Upper);

            if (this.LinesSeries[1].Visible)
                this.DrawLabel(gr, (float)zeroBarLowLineX, lowPriceY, $"Low ({this.formattedPeriod}): {this.Symbol.FormatPrice(this.LowPrice)}", this.lowLabelBrush, LabelPosition.Bottom);
        }
    }
    public override IList<SettingItem> Settings
    {
        get
        {
            var settings = base.Settings;

            if (settings.GetItemByName(RANGE_INPUT_PARAMETER) is SettingItemInteger rangeSi)
                rangeSi.ValueChangingBehavior = SettingItemValueChangingBehavior.WithConfirmation;

            if (settings.GetItemByName(PERIOD_INPUT_PARAMETER) is SettingItemSelectorLocalized periodSi)
                periodSi.ValueChangingBehavior = SettingItemValueChangingBehavior.WithConfirmation;

            return settings;
        }
        set
        {
            base.Settings = value;
        }
    }

    #endregion Overrides

    private void History_NewHistoryItem(object sender, HistoryEventArgs e) => this.CalculateIndicator();
    private void DrawIndicator(int fromIndex, double highPrice, double lowPrice)
    {
        for (int i = fromIndex; i >= 0; i--)
        {
            this.SetValue(highPrice, 0, i);
            this.SetValue(lowPrice, 1, i);
        }
    }
    private void CalculateIndicator()
    {
        bool isHistoryValid = this.CheckHistory(this.history, this.historyTicksRange);

        if (!isHistoryValid)
        {
            this.State = IndicatorState.NoData;
            return;
        }

        this.HighPrice = DEFAULT_HIGH_PRICE;
        this.LowPrice = DEFAULT_LOW_PRICE;

        for (int i = 0; i < this.ConvertRangeToPeriodCount(this.BasePeriod, this.Range, this.history.Period); i++)
        {
            this.HighPrice = Math.Max(((HistoryItemBar)this.history[i]).High, this.HighPrice);
            this.LowPrice = Math.Min(((HistoryItemBar)this.history[i]).Low, this.LowPrice);
        }

        if (isHistoryValid && this.HighPrice != DEFAULT_HIGH_PRICE && this.LowPrice != DEFAULT_LOW_PRICE)
            this.State = IndicatorState.AllGreat;
        else
            this.State = IndicatorState.NoData;

        // Redraw
        if (this.State == IndicatorState.AllGreat)
            this.DrawIndicator(this.Count - 1, this.HighPrice, this.LowPrice);
    }

    #region Misc

    private long GetTicksRange(BasePeriod basePeriod, int range) => new Period(this.BasePeriod, this.Range).Ticks;
    private bool CheckHistory(HistoricalData history, long historyTicksRange)
    {
        if (this.history == null || this.history.Count == 0)
            return false;

        if (this.history.Count < this.ConvertRangeToPeriodCount(this.BasePeriod, this.Range, this.history.Period))
            return false;

        return true;
    }

    private long ConvertRangeToPeriodCount(BasePeriod basePeriod, int range, Period period) => this.GetTicksRange(this.BasePeriod, this.Range) / period.Ticks;
    private Period GetAggregationPeriod(BasePeriod basePeriod) => basePeriod switch
    {
        BasePeriod.Minute => Period.MIN1,
        BasePeriod.Hour => Period.HOUR1,
        _ => Period.DAY1,
    };
    private double GetLabelRightX(Rectangle rectangle, int timeShift)
    {
        if (this.CurrentChart.RightOffset > 0)
            return this.CurrentChart.MainWindow.CoordinatesConverter.GetChartX(this.Time()) + this.CurrentChart.BarsWidth / 2 + timeShift * this.CurrentChart.BarsWidth;
        else
            return rectangle.Right - this.CurrentChart.BarsWidth / 2;
    }

    private void AbortPreviousTask()
    {
        if (this.history != null)
            this.history.NewHistoryItem -= this.History_NewHistoryItem;

        if (this.cancellationSource != null)
            this.cancellationSource.Cancel();

        this.cancellationSource = new CancellationTokenSource();
    }
    private string GetFormattedPeriod(BasePeriod basePeriod, int range)
    {
        string period = basePeriod.ToString();
        if (range > 1)
            period += "s";

        return $"{range} {period}";
    }
    private void DrawLabel(Graphics gr, float zeroBarX, float priceY, string label, SolidBrush brush, LabelPosition labelPosition)
    {
        var labelSize = gr.MeasureString(label, this.font);

        float posY = labelPosition == LabelPosition.Bottom
            ? priceY + 2
            : priceY - labelSize.Height - 2;

        var rect = new RectangleF()
        {
            Height = labelSize.Height,
            Width = labelSize.Width + 5,
            X = zeroBarX - labelSize.Width - 5,
            Y = posY
        };

        gr.FillRectangle(brush, rect);
        gr.DrawString(label, this.font, Brushes.White, rect, new StringFormat()
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Near
        });
    }
    private void UpdateBrushes()
    {
        if (this.highLabelBrush == null || !this.highLabelBrush.Color.Equals(this.LinesSeries[0].Color))
            this.highLabelBrush = new SolidBrush(this.LinesSeries[0].Color);

        if (this.lowLabelBrush == null || !this.lowLabelBrush.Color.Equals(this.LinesSeries[1].Color))
            this.lowLabelBrush = new SolidBrush(this.LinesSeries[1].Color);
    }

    #endregion Misc
}

public enum IndicatorState
{
    Ready,
    Loading,
    Loaded,
    NoData,
    AllGreat,
}

public enum LabelPosition
{
    Upper,
    Bottom
}