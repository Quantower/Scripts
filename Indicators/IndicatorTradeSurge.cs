// Copyright QUANTOWER LLC. © 2017-2025. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Threading.Tasks;
using TradingPlatform.BusinessLayer;
using TradingPlatform.BusinessLayer.Utils;

namespace VolumeIndicators;

public class IndicatorTradeSurge : Indicator
{
    #region Parameters
    private Period tfPeriod = Period.SECOND30;
    private int averagePeriod = 5;
    private int firstTreshold = 200;
    private int secondTreshold = 300;
    private int thirdTreshold = 500;
    private bool useCustomDiameter = false;
    private int circleDiameter = 20;
    private Color firstColor = Color.FromArgb(85, Color.DarkCyan);
    private Color secondColor = Color.FromArgb(85, Color.Yellow);
    private Color thirdColor = Color.FromArgb(85, Color.Purple);
    private Color firstBorderColor = Color.FromArgb(255, Color.Cyan);
    private Color secondBorderColor = Color.FromArgb(255, Color.Yellow);
    private Color thirdBorderColor = Color.FromArgb(255, Color.Purple);
    private int borderWidth = 2;
    private bool showLabel = false;
    private Font labelFont;
    public Color labelColor
    {
        get => this.labelBrush.Color;
        set => this.labelBrush.Color = value;
    }
    private readonly SolidBrush labelBrush;

    private HistoricalData tickData;
    private Indicator maxSMA;
    private HistoricalDataCustom MaximumSource;
    private List<TickSegment> tickSegments = new List<TickSegment>();
    private bool historyDownloaded = false;
    private Task loadingTask;
    private CancellationTokenSource cancellationSource;
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
                    Core.Loggers.Log($"{this.Name}: Incorrect period. Bar period must be a multiple of indicator period.", LoggingLevel.Error);
                    break;
                case IndicatorState.OneTickNotAllowed:
                    Core.Loggers.Log($"{this.Name}: Tick aggregation is not supported.", LoggingLevel.Error);
                    break;
            }
        }
    }
    #endregion Parameters

    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorTradeSurge.cs";

    public IndicatorTradeSurge()
        : base()
    {
        Name = "Trade Surge";
        SeparateWindow = false;

        this.labelBrush = new SolidBrush(Color.FromArgb(255, Color.Green));
        this.labelFont = new Font("Tahoma", 12);
    }
    #region Overrides
    protected override void OnInit()
    {
        base.OnInit();

        this.AbortPreviousTask();
        this.tickSegments.Clear();
        this.historyDownloaded = false;
        this.State = IndicatorState.Ready;
        this.maxSMA = Core.Instance.Indicators.BuiltIn.SMA(this.averagePeriod, PriceType.Open);
        this.MaximumSource = new HistoricalDataCustom();
        this.MaximumSource.AddIndicator(maxSMA);
        if (this.Symbol == null || this.HistoricalData == null)
            return;

        if (this.HistoricalData.Aggregation is HistoryAggregationTick)
        {
            this.State = IndicatorState.OneTickNotAllowed;
            return;
        }

        if (this.HistoricalData.Aggregation is not HistoryAggregationTime hat)
            return;

        if (hat.Period.Duration.Ticks % this.tfPeriod.Duration.Ticks != 0)
        {
            this.State = IndicatorState.IncorrectPeriod;
            return;
        }

        RefreshIndicator(needRedownload: true);
    }


    protected override void OnUpdate(UpdateArgs args)
    {
        if (this.State != IndicatorState.Ready)
            return;
        if (args.Reason == UpdateReason.NewBar)
            this.MaximumSource.AddValue(0d, 0d, 0d, 0d);
        if (CanProcessSurges())
            CalculateSurgesForBar(this.Count - 1);
    }

    protected override void OnClear()
    {
        base.OnClear();
        this.AbortPreviousTask();

        this.tickData?.Dispose();
        this.tickData = null;

        this.tickSegments.Clear();
        this.historyDownloaded = false;
        this.State = IndicatorState.Ready;
    }


    public override void OnPaintChart(PaintChartEventArgs args)
    {
        base.OnPaintChart(args);
        var gr = args.Graphics;
        if (this.CurrentChart == null)
            return;

        var currWindow = this.CurrentChart.Windows[args.WindowIndex];
        RectangleF prevClipRectangle = gr.ClipBounds;
        gr.SetClip(args.Rectangle);
        try
        {
            switch (this.State)
            {
                case IndicatorState.Loading:
                    gr.DrawString("Loading data...", new Font("Arial", 20), Brushes.Blue, 20, 50);
                    return;
                case IndicatorState.Calculation:
                    gr.DrawString("Calculating...", new Font("Arial", 20), Brushes.Orange, 20, 50);
                    return;
                case IndicatorState.NoData:
                    gr.DrawString("No data to load", new Font("Arial", 20), Brushes.Red, 20, 50);
                    return;
                case IndicatorState.IncorrectPeriod:
                    gr.DrawString("Bar aggregation is not multiple of indicator period", new Font("Arial", 20), Brushes.Red, 20, 50);
                    return;
                case IndicatorState.OneTickNotAllowed:
                    gr.DrawString("Indicator does not work on tick aggregation", new Font("Arial", 20), Brushes.Red, 20, 50);
                    return;
                default:
                    break;
            }
            if (this.firstTreshold > secondTreshold || this.firstTreshold > this.thirdTreshold || this.secondTreshold > this.thirdTreshold)
            {
                gr.DrawString("The threshold of the previous level must be lower", new Font("Arial", 20), Brushes.Red, 20, 50);
                return;
            }

            for (int i = 0; i < this.tickSegments.Count; i++)
            {
                TickSegment currSegment = this.tickSegments[i];
                Color currColor = Color.White;
                Color currBorderColor = Color.White;
                switch (currSegment.tresholdLevel)
                {
                    case 1:
                        currColor = this.firstColor;
                        currBorderColor = this.firstBorderColor;
                        break;
                    case 2:
                        currColor = this.secondColor;
                        currBorderColor = this.secondBorderColor;
                        break;
                    case 3:
                        currColor = this.thirdColor;
                        currBorderColor = this.thirdBorderColor;
                        break;
                    default:
                        break;
                }
                float circleSize = !useCustomDiameter ? this.CurrentChart.BarsWidth : this.circleDiameter;
                RectangleF currRect = new RectangleF((float)currWindow.CoordinatesConverter.GetChartX(this.HistoricalData[currSegment.index, SeekOriginHistory.Begin].TimeLeft)+this.CurrentChart.BarsWidth/2 - circleSize/2, (float)currWindow.CoordinatesConverter.GetChartY(currSegment.value)-circleSize/2, circleSize, circleSize);
                Brush currBrush = new SolidBrush(currColor);
                Pen currPen = new Pen(currBorderColor, this.borderWidth);

                gr.FillEllipse(currBrush, currRect);
                gr.DrawEllipse(currPen, currRect);

                if (this.showLabel)
                {
                    double currTicks = this.MaximumSource.GetPrice(PriceType.High, 0);
                    double maxTicks = this.MaximumSource.GetPrice(PriceType.Open, 0);
                    double currAverage = this.maxSMA.GetValue();
                    string labelString = $"{this.Symbol.FormatQuantity(currTicks, true, true)}/{this.Symbol.FormatQuantity(maxTicks, true, true)}/{this.Symbol.FormatQuantity(currAverage, true, true)}";
                    PointF labelPoint = new PointF(0f, currWindow.ClientRectangle.Height);
                    labelPoint.Y -= gr.MeasureString(labelString, this.labelFont).Height;
                    gr.DrawString(labelString, labelFont, labelBrush, labelPoint);
                }
            }
        }
        finally
        {
            gr.SetClip(prevClipRectangle);
        }
    }

    public override IList<SettingItem> Settings
    {
        get
        {
            var settings = base.Settings;
            settings.Add(new SettingItemPeriod("tfPeriod", this.tfPeriod)
            {
                Text = "Period",
                SortIndex = 1,
            });
            settings.Add(new SettingItemInteger("averagePeriod", this.averagePeriod)
            {
                Text = "Average Period",
                SortIndex = 2,
                Minimum = 2,
            });
            settings.Add(new SettingItemInteger("firstTreshold", this.firstTreshold)
            {
                Text = "First level treshold",
                SortIndex = 3,
                Minimum = 0,
            });
            settings.Add(new SettingItemInteger("secondTreshold", this.secondTreshold)
            {
                Text = "Second level treshold",
                SortIndex = 4,
                Minimum = 0,
            });
            settings.Add(new SettingItemInteger("thirdTreshold", this.thirdTreshold)
            {
                Text = "Third level treshold",
                SortIndex = 5,
                Minimum = 0,
            });
            settings.Add(new SettingItemBoolean("useCustomDiameter", this.useCustomDiameter)
            {
                Text = "Use Custom Diameter",
                SortIndex = 6,
            });
            SettingItemRelationVisibility customSizeRelation = new SettingItemRelationVisibility("useCustomDiameter", true);
            settings.Add(new SettingItemInteger("circleDiameter", this.circleDiameter)
            {
                Text = "Cystom Circle Diameter",
                SortIndex = 7,
                Minimum = 2,
                Relation = customSizeRelation,
            });
            settings.Add(new SettingItemPairColor("firstColors", new PairColor(this.firstColor, this.firstBorderColor))
            {
                Text = "First Treshold Color",
                SortIndex = 8,
            });
            settings.Add(new SettingItemPairColor("secondColors", new PairColor(this.secondColor, this.secondBorderColor))
            {
                Text = "Second Treshold Color",
                SortIndex = 9,
            });
            settings.Add(new SettingItemPairColor("thirdColors", new PairColor(this.thirdColor, this.thirdBorderColor))
            {
                Text = "Third Treshold Color",
                SortIndex = 10,
            });
            settings.Add(new SettingItemInteger("borderWidth", this.borderWidth)
            {
                Text = "Border Width",
                SortIndex = 11,
                Minimum = 1,
            });

            settings.Add(new SettingItemBoolean("showLabel", this.showLabel)
            {
                Text = "Show Label",
                SortIndex = 12,
            });
            SettingItemRelationVisibility visibleRelationLabel = new SettingItemRelationVisibility("showLabel", true);
            settings.Add(new SettingItemFont("Font", this.labelFont)
            {
                Text = "Font",
                SortIndex = 12,
                Relation = visibleRelationLabel
            });
            settings.Add(new SettingItemColor("labelColor", this.labelColor)
            {
                Text = "Label Color",
                SortIndex = 12,
                Relation = visibleRelationLabel,
            });
            return settings;
        }
        set
        {
            base.Settings = value;

            bool needRecalc = false;
            bool needRedownload = false;

            if (value.TryGetValue("tfPeriod", out Period tfPeriod))
            {
                if (this.tfPeriod != tfPeriod)
                {
                    this.tfPeriod = tfPeriod;
                    this.historyDownloaded = false;
                    needRedownload = true;
                }
            }

            if (value.TryGetValue("averagePeriod", out int averagePeriod))
            {
                if (this.averagePeriod != averagePeriod)
                {
                    this.averagePeriod = averagePeriod;
                    needRecalc = true;
                }
            }


            if (value.TryGetValue("firstTreshold", out int firstTreshold))
            {
                this.firstTreshold = firstTreshold;
                needRecalc = true;
            }
            if (value.TryGetValue("secondTreshold", out int secondTreshold))
            {
                this.secondTreshold = secondTreshold;
                needRecalc = true;
            }
            if (value.TryGetValue("thirdTreshold", out int thirdTreshold))
            {
                this.thirdTreshold = thirdTreshold;
                needRecalc = true;
            }

            if (value.TryGetValue("useCustomDiameter", out bool useCustomDiameter))
                this.useCustomDiameter = useCustomDiameter;
            if (value.TryGetValue("circleDiameter", out int circleDiameter))
                this.circleDiameter = circleDiameter;

            if (value.TryGetValue("firstColors", out PairColor firstColors))
            {
                this.firstBorderColor = firstColors.Color2;
                this.firstColor = firstColors.Color1;
            }
            if (value.TryGetValue("secondColors", out PairColor secondColors))
            {
                this.secondBorderColor = secondColors.Color2;
                this.secondColor = secondColors.Color1;
            }
            if (value.TryGetValue("thirdColors", out PairColor thirdColors))
            {
                this.thirdBorderColor = thirdColors.Color2;
                this.thirdColor = thirdColors.Color1;
            }
            if (value.TryGetValue("borderWidth", out int borderWidth))
                this.borderWidth = borderWidth;

            if (value.TryGetValue("showLabel", out bool showLabel))
                this.showLabel = showLabel;
            if (value.TryGetValue("Font", out Font labelFont))
                this.labelFont = labelFont;
            if (value.TryGetValue("labelColor", out Color labelColor))
                this.labelColor = labelColor;

            if (needRedownload)
                RefreshIndicator(needRedownload: true);
            else if (needRecalc)
                RefreshIndicator(needRedownload: false);
        }
    }

    #endregion Overrides

    #region Calculation
    private void CalculateAllIndicator()
    {
        if (!CanProcessSurges())
        {
            this.state = IndicatorState.IncorrectPeriod;
            return;
        }
        for (int i = 0; i < this.Count; i++)
        {
            if(this.MaximumSource.Count < this.HistoricalData.Count)
                this.MaximumSource.AddValue(0d, 0d, 0d, 0d);
            var t = this.maxSMA;
            var b = this.Count;
            CalculateSurgesForBar(i);
        }
        this.State = IndicatorState.Ready;

    }
    private void CalculateSurgesForBar(int barIndex)
    {
        if (barIndex < 0 || barIndex >= this.Count)
            return;

        double barTicksMax = 0;
        double currTicks = 0;

        var currBar = this.HistoricalData[barIndex, SeekOriginHistory.Begin];
        DateTime startTime = currBar.TimeLeft;
        DateTime endTime = startTime + this.tfPeriod.Duration;
        DateTime lastTime = new DateTime(currBar.TicksRight);

        int startIndex = (int)this.tickData.GetIndexByTime(startTime.Ticks, SeekOriginHistory.Begin);
        int lastIndex = (int)this.tickData.GetIndexByTime(lastTime.Ticks, SeekOriginHistory.Begin);

        if (lastIndex < 0)
            lastIndex = this.tickData.Count - 1;
        if (startIndex < 0)
            startIndex = lastIndex-1;

        for (int i = startIndex; i < lastIndex; i++)
        {
            var currTickBar = (HistoryItemLast)this.tickData[i, SeekOriginHistory.Begin];

            if (currTickBar.TimeLeft >= endTime)
            {
                currTicks = 0;
                endTime  += this.tfPeriod.Duration;
            }

            currTicks++;
            if (currTicks >= barTicksMax)
                barTicksMax = currTicks;

            double currAverage = this.maxSMA.GetValue(barIndex, 0, SeekOriginHistory.Begin);
            double ratio = currAverage != 0 ? currTicks / currAverage : 0;
            if (ratio.IsNanOrDefault())
                ratio = 0;

            if (ratio >= this.firstTreshold / 100.0)
            {
                var segment = new TickSegment
                {
                    value = currTickBar.Price,
                    index = barIndex,
                    tresholdLevel =
                        (short)(ratio >= this.thirdTreshold  / 100.0 ? 3 :
                                ratio >= this.secondTreshold / 100.0 ? 2 : 1)
                };

                if (this.tickSegments.Count == 0 ||
                    !(this.tickSegments[0].index == segment.index &&
                      this.tickSegments[0].tresholdLevel >= segment.tresholdLevel))
                {
                    this.tickSegments.Insert(0, segment);
                }
            }
            this.MaximumSource.SetValue(barTicksMax, currTicks, 0f, 0f);
        }
    }
    private void RefreshIndicator(bool needRedownload)
    {
        this.tickSegments.Clear();
        this.AbortPreviousTask();
        this.cancellationSource = new CancellationTokenSource();
        var token = this.cancellationSource.Token;
        if (!needRedownload)
        {
            if (!this.historyDownloaded || this.tickData == null)
                return;
            this.State = IndicatorState.Calculation;
            CalculateAllIndicator();
            return;
        }
        this.loadingTask = Task.Factory.StartNew(() =>
        {
            if (token.IsCancellationRequested)
                return;

            try
            {
                this.State = IndicatorState.Loading;
                var fromTime = this.HistoricalData.FromTime.AddDays(-5);

                this.tickData?.Dispose();
                this.tickData = this.Symbol.GetHistory(
                    Period.TICK1,
                    this.Symbol.HistoryType,
                    fromTime);

                if (token.IsCancellationRequested)
                    return;

                if (!this.IsValidLoadedHistory(this.tickData))
                {
                    this.historyDownloaded = false;
                    this.State = IndicatorState.NoData;
                    return;
                }

                this.historyDownloaded = true;
                this.State = IndicatorState.Calculation;
                CalculateAllIndicator();

            }
            catch (Exception ex)
            {
                Core.Loggers.Log(ex);
                this.historyDownloaded = false;
                this.State = IndicatorState.NoData;
            }
        }, token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }
    #endregion Calculation

    #region Misc
    private bool CanProcessSurges()
    {
        if (this.HistoricalData == null || !this.historyDownloaded || this.tickData == null)
            return false;
        if (this.HistoricalData.Aggregation is not HistoryAggregationTime hat)
            return false;

        return hat.Period.Duration.Ticks % this.tfPeriod.Duration.Ticks == 0;
    }
    private void AbortPreviousTask()
    {
        this.cancellationSource?.Cancel();
        this.cancellationSource = new CancellationTokenSource();
    }
    private bool IsValidLoadedHistory(HistoricalData history)
    {
        if (history == null || history.Count == 0)
            return false;

        if (history.Count < averagePeriod)
            return false;

        return true;
    }
    #endregion Misc


}
#region Utils
internal class TickSegment
{
    public int index { get; set; }
    public double value { get; set; }
    public short tresholdLevel { get; set; }
    public TickSegment(int index, double value, short tresholdLevel)
    {
        this.value = value;
        this.index = index;
        this.tresholdLevel = tresholdLevel;
    }
    public TickSegment()
    {
    }
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