// Copyright QUANTOWER LLC. © 2017-2024. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace TrendIndicators;

public class IndicatorDoubleTopBottom : Indicator
{
    /// <summary>
    /// Period for local maximum and mimimum
    /// </summary>
    private int smallPeriod = 3;

    /// <summary>
    /// Period for Double top and bottom
    /// </summary>
    private int bigPeriod = 10;
    private double difference = 100;
    private Color doubleTopColor = Color.Green;
    private Color doubleBottomColor = Color.Red;
    private Pen doubleTopPen = new Pen(Color.Green);
    private Pen doubleBottomPen = new Pen(Color.Red);

    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorDoubleTopBottom.cs";

    public IndicatorDoubleTopBottom()
        : base()
    {
        this.Name = "Double Top/Bottom";
    }
    protected override void OnInit()
    { }


    protected override void OnUpdate(UpdateArgs args)
    { }

    public override void OnPaintChart(PaintChartEventArgs args)
    {
        var graphics = args.Graphics;

        if (this.HistoricalData.Aggregation is HistoryAggregationTick)
        {
            graphics.DrawString("Indicator does not work on tick aggregation", new Font("Arial", 20), Brushes.Red, 20, 50);
            return;
        }

        var prevClipRectangle = graphics.ClipBounds;
        graphics.SetClip(args.Rectangle);

        try
        {
            // Calculate left and right indexes of bars for further processing
            var leftBorderTime = this.CurrentChart.MainWindow.CoordinatesConverter.GetTime(this.CurrentChart.MainWindow.ClientRectangle.Left);
            var rightBorderTime = this.CurrentChart.MainWindow.CoordinatesConverter.GetTime(this.CurrentChart.MainWindow.ClientRectangle.Right);
            int leftIndex, rightIndex;
            if (this.Time() <= rightBorderTime)
                rightIndex = 0 + this.smallPeriod;
            else
            {
                int barIndex = (int)this.CurrentChart.MainWindow.CoordinatesConverter.GetBarIndex(rightBorderTime);
                if (this.Count - barIndex - this.bigPeriod * 3 >= this.smallPeriod)
                    rightIndex = this.Count - barIndex;
                else
                    rightIndex = 0 + this.smallPeriod;
            }
            if (this.Time(this.Count - 1) >= leftBorderTime)
                leftIndex = this.Count - 1 - this.smallPeriod;
            else
            {
                int barIndex = (int)this.CurrentChart.MainWindow.CoordinatesConverter.GetBarIndex(leftBorderTime);
                if (this.Count - barIndex + this.bigPeriod * 2 <= this.Count - 1 - this.smallPeriod - this.bigPeriod * 2)
                    leftIndex = this.Count - barIndex;
                else
                    leftIndex = this.Count - 1 - this.smallPeriod;
            }

            // Process visible bars
            var pivots = new List<PivotPoint>();
            for (int i = rightIndex; i <= leftIndex; i++)
            {
                var maxOrMin = this.LocalMaxOrMin(i);
                if (maxOrMin != MaxOrMin.Nothing)
                    pivots.Add(new PivotPoint(maxOrMin, i));
            }

            //Drawing
            if (pivots.Count <= 2)
                return;
            double differenceInTicks = this.difference * this.Symbol.TickSize;
            for (int i = 0; i < pivots.Count - 2; i++)
            {
                var currentBar = (HistoryItemBar)this.HistoricalData[pivots[i].index];
                var nextBar = (HistoryItemBar)this.HistoricalData[pivots[i + 2].index];
                int halfBarWidth = this.CurrentChart.BarsWidth / 2;
                if (pivots[i].MaxOrMin == MaxOrMin.LocalMaximum && pivots[i + 1].MaxOrMin == MaxOrMin.LocalMinimum && pivots[i + 2].MaxOrMin == MaxOrMin.LocalMaximum && pivots[i + 2].index - pivots[i].index <= this.bigPeriod)
                {
                    double currentHigh = currentBar.High;
                    double nexttHigh = nextBar.High;
                    if (nexttHigh - currentHigh >= 0 && nexttHigh - currentHigh <= differenceInTicks)
                    {
                        var firstPoint = new Point((int)(this.CurrentChart.MainWindow.CoordinatesConverter.GetChartX(this.HistoricalData[pivots[i].index].TimeLeft) + halfBarWidth), (int)this.CurrentChart.MainWindow.CoordinatesConverter.GetChartY(currentHigh));
                        var secondPoint = new Point((int)(this.CurrentChart.MainWindow.CoordinatesConverter.GetChartX(this.HistoricalData[pivots[i + 2].index].TimeLeft) + halfBarWidth), (int)this.CurrentChart.MainWindow.CoordinatesConverter.GetChartY(nexttHigh));
                        graphics.DrawLine(this.doubleTopPen, firstPoint, secondPoint);
                        i += 2;
                    }
                }
                else if (pivots[i].MaxOrMin == MaxOrMin.LocalMinimum && pivots[i + 1].MaxOrMin == MaxOrMin.LocalMaximum && pivots[i + 2].MaxOrMin == MaxOrMin.LocalMinimum && pivots[i + 2].index - pivots[i].index <= this.bigPeriod)
                {
                    double currentLow = currentBar.Low;
                    double nexttLow = nextBar.Low;
                    if (currentLow - nexttLow >= 0 && currentLow - nexttLow <= differenceInTicks)
                    {
                        var firstPoint = new Point((int)(this.CurrentChart.MainWindow.CoordinatesConverter.GetChartX(this.HistoricalData[pivots[i].index].TimeLeft) + halfBarWidth), (int)this.CurrentChart.MainWindow.CoordinatesConverter.GetChartY(currentLow));
                        var secondPoint = new Point((int)(this.CurrentChart.MainWindow.CoordinatesConverter.GetChartX(this.HistoricalData[pivots[i + 2].index].TimeLeft) + halfBarWidth), (int)this.CurrentChart.MainWindow.CoordinatesConverter.GetChartY(nexttLow));
                        graphics.DrawLine(this.doubleBottomPen, firstPoint, secondPoint);
                        i += 2;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Core.Instance.Loggers.Log(ex);
        }
        finally
        {
            graphics.SetClip(prevClipRectangle);
        }
    }
    public override IList<SettingItem> Settings
    {
        get
        {
            var settings = base.Settings;
            settings.Add(new SettingItemDouble("Difference", this.difference)
            {
                Text = "Max pivot difference",
                Description = "Difference between first and second top/bottom",
                Dimension = "ticks",
                Minimum = 0
            });
            settings.Add(new SettingItemInteger("SmallPeriod", this.smallPeriod)
            {
                Text = "Small Period",
                Description = "Period for finding local maximum and mimimum",
                Dimension = "bars",
                Minimum = 0
            });
            settings.Add(new SettingItemInteger("BigPeriod", this.bigPeriod)
            {
                Text = "Max distance between pivots",
                Description = "Max Distance between two Tops/Bottoms",
                Dimension = "bars",
                Minimum = 0
            });
            settings.Add(new SettingItemColor("DoubleTopColor", this.doubleTopColor)
            {
                Text = "Double Top Color",
            });
            settings.Add(new SettingItemColor("DoubleBottomColor", this.doubleBottomColor)
            {
                Text = "Double Bottom Color",
            });
            return settings;
        }
        set
        {
            base.Settings = value;
            if (value.TryGetValue("Difference", out double difference))
                this.difference = difference;
            if (value.TryGetValue("SmallPeriod", out int smallPeriod))
                this.smallPeriod = smallPeriod;
            if (value.TryGetValue("BigPeriod", out int bigPeriod))
                this.bigPeriod = bigPeriod;
            if (value.TryGetValue("DoubleTopColor", out Color doubleTopColor))
            {
                this.doubleTopColor = doubleTopColor;
                this.doubleTopPen.Color = doubleTopColor;
            }
            if (value.TryGetValue("DoubleBottomColor", out Color doubleBottomColor))
            {
                this.doubleBottomColor = doubleBottomColor;
                this.doubleBottomPen.Color = doubleBottomColor;
            }

        }
    }
    private MaxOrMin LocalMaxOrMin(int index = 0)
    {
        var maxOrMin = MaxOrMin.Nothing;
        for (int i = 1; i <= this.smallPeriod; i++)
        {
            var leftBar = (HistoryItemBar)this.HistoricalData[index + i];
            var rightBar = (HistoryItemBar)this.HistoricalData[index - i];
            var currentBar = (HistoryItemBar)this.HistoricalData[index];
            if (leftBar.High <= currentBar.High && rightBar.High <= currentBar.High)
            {
                if (maxOrMin == MaxOrMin.LocalMaximum || maxOrMin == MaxOrMin.Nothing)
                    maxOrMin = MaxOrMin.LocalMaximum;
                else
                {
                    maxOrMin = MaxOrMin.Nothing;
                    break;
                }
            }
            else if (leftBar.Low >= currentBar.Low && rightBar.Low >= currentBar.Low)
            {
                if (maxOrMin == MaxOrMin.LocalMinimum || maxOrMin == MaxOrMin.Nothing)
                    maxOrMin = MaxOrMin.LocalMinimum;
                else
                {
                    maxOrMin = MaxOrMin.Nothing;
                    break;
                }
            }
            else
            {
                maxOrMin = MaxOrMin.Nothing;
                break;
            }
        }

        return maxOrMin;
    }
}

internal enum MaxOrMin
{
    LocalMaximum,
    LocalMinimum,
    Nothing
}

internal class PivotPoint
{
    public MaxOrMin MaxOrMin { get; set; }
    public int index { get; set; }
    public PivotPoint(MaxOrMin maxOrMin, int index)
    {
        this.MaxOrMin = maxOrMin;
        this.index = index;
    }
}