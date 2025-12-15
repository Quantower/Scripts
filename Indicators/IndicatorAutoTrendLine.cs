// Copyright QUANTOWER LLC. © 2017-2023. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Numerics;
using TradingPlatform.BusinessLayer;
using TradingPlatform.BusinessLayer.Chart;
using TradingPlatform.BusinessLayer.Utils;

namespace Trend
{
    public class IndicatorAutoTrendLine : Indicator
    {
        public int Period = 20;
        public int linesCount = 4;

        public LineOptions TopOptions
        {
            get => CreateLineOptions(this.TopPen);
            set => ApplyLineOptions(this.TopPen, value);
        }

        public LineOptions BottomOptions
        {
            get => CreateLineOptions(this.BottomPen);
            set => ApplyLineOptions(this.BottomPen, value);
        }

        private readonly Pen TopPen = new Pen(Color.Red);
        private readonly Pen BottomPen = new Pen(Color.Green);

        private readonly List<IndicatorAutoTrendLinePivotPoint> pivots = new();

        public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorAutoTrendLine.cs";
        public IndicatorAutoTrendLine() : base()
        {
            this.Name = "Auto Trend Line";
            this.SeparateWindow = false;
        }

        protected override void OnInit() => this.pivots.Clear();

        protected override void OnUpdate(UpdateArgs args)
        {
            if (this.Count < this.Period * 2 + 1) return;

            int currBarIndex = this.Count - this.Period - 1;
            IndicatorAutoTrendLineMaxOrMin maxOrMin = this.LocalMaxOrMin(currBarIndex);

            if (maxOrMin != IndicatorAutoTrendLineMaxOrMin.Nothing && (this.pivots.Count == 0 || this.pivots[0].index != currBarIndex))
            {
                var pivot = new IndicatorAutoTrendLinePivotPoint(maxOrMin, currBarIndex);
                var bar = (HistoryItemBar)this.HistoricalData[currBarIndex, SeekOriginHistory.Begin];
                pivot.value = (maxOrMin == IndicatorAutoTrendLineMaxOrMin.LocalMaximum) ? bar.High : bar.Low;
                this.pivots.Insert(0, pivot);
            }
        }

        public override void OnPaintChart(PaintChartEventArgs args)
        {
            base.OnPaintChart(args);
            if (this.CurrentChart == null || this.HistoricalData.Aggregation is HistoryAggregationTick) return;

            var gr = args.Graphics;
            var currWindow = this.CurrentChart.Windows[args.WindowIndex];
            var prevClip = gr.ClipBounds;
            gr.SetClip(args.Rectangle);

            try
            {
                int drawnLines = 0;

                for (int i = 0; i < this.pivots.Count - 1 && drawnLines < this.linesCount; i++)
                {
                    var endPivot = this.pivots[i];
                    var endPoint = this.GetPoint(currWindow, endPivot);

                    for (int j = i + 1; j < this.pivots.Count; j++)
                    {
                        var startPivot = this.pivots[j];
                        if (endPivot.MaxOrMin != startPivot.MaxOrMin ||
                            (endPivot.MaxOrMin == IndicatorAutoTrendLineMaxOrMin.LocalMaximum && startPivot.value <= endPivot.value) ||
                            (endPivot.MaxOrMin == IndicatorAutoTrendLineMaxOrMin.LocalMinimum && startPivot.value >= endPivot.value))
                            continue;

                        var startPoint = this.GetPoint(currWindow, startPivot);
                        Pen pen = endPivot.MaxOrMin == IndicatorAutoTrendLineMaxOrMin.LocalMaximum ? this.TopPen : this.BottomPen;

                        PointF extendedEnd = this.ExtendLine(endPoint, startPoint, currWindow.ClientRectangle);
                        gr.DrawLine(pen, startPoint, extendedEnd);
                        drawnLines++;
                        break;
                    }
                }
            }
            finally
            {
                gr.SetClip(prevClip);
            }
        }

        private PointF GetPoint(IChartWindow window, IndicatorAutoTrendLinePivotPoint pivot)
        {
            float x = (float)window.CoordinatesConverter.GetChartX(this.HistoricalData[pivot.index, SeekOriginHistory.Begin].TimeLeft) + this.CurrentChart.BarsWidth / 2;
            float y = (float)window.CoordinatesConverter.GetChartY(pivot.value);
            return new PointF(x, y);
        }

        private PointF ExtendLine(PointF start, PointF end, RectangleF bounds)
        {
            PointF result = new PointF(bounds.Width, end.Y);
            float k = (end.Y - start.Y) / (end.X - start.X);
            float b = start.Y - k * start.X;
            result.Y = k * result.X + b;
            return result;
        }

        private IndicatorAutoTrendLineMaxOrMin LocalMaxOrMin(int index)
        {
            IndicatorAutoTrendLineMaxOrMin result = IndicatorAutoTrendLineMaxOrMin.Nothing;
            var current = (HistoryItemBar)this.HistoricalData[index, SeekOriginHistory.Begin];

            for (int i = 1; i <= this.Period; i++)
            {
                var left = (HistoryItemBar)this.HistoricalData[index - i, SeekOriginHistory.Begin];
                var right = (HistoryItemBar)this.HistoricalData[index + i, SeekOriginHistory.Begin];

                if (left.High <= current.High && right.High <= current.High)
                    result = result == IndicatorAutoTrendLineMaxOrMin.LocalMinimum ? IndicatorAutoTrendLineMaxOrMin.Nothing : IndicatorAutoTrendLineMaxOrMin.LocalMaximum;
                else if (left.Low >= current.Low && right.Low >= current.Low)
                    result = result == IndicatorAutoTrendLineMaxOrMin.LocalMaximum ? IndicatorAutoTrendLineMaxOrMin.Nothing : IndicatorAutoTrendLineMaxOrMin.LocalMinimum;
                else
                    return IndicatorAutoTrendLineMaxOrMin.Nothing;
            }

            return result;
        }

        public override IList<SettingItem> Settings
        {
            get
            {
                var settings = base.Settings;
                settings.Add(new SettingItemInteger("Period", this.Period) { Text = "Period", SortIndex = 1, Minimum = 2 });
                settings.Add(new SettingItemInteger("linesCount", this.linesCount) { Text = "Max Lines Count", SortIndex = 1, Minimum = 2 });
                settings.Add(new SettingItemLineOptions("TopOptions", this.TopOptions)
                {
                    Text = "Resistance Line Style",
                    SortIndex = 2,
                    ExcludedStyles = new[] { LineStyle.Histogramm, LineStyle.Points, LineStyle.Columns, LineStyle.StepLine },
                    UseEnabilityToggler = false
                });
                settings.Add(new SettingItemLineOptions("BottomOptions", this.BottomOptions)
                {
                    Text = "Support Line Style",
                    SortIndex = 2,
                    ExcludedStyles = new[] { LineStyle.Histogramm, LineStyle.Points, LineStyle.Columns, LineStyle.StepLine },
                    UseEnabilityToggler = false
                });
                return settings;
            }
            set
            {
                base.Settings = value;
                if (value.TryGetValue("Period", out int p)) this.Period = p;
                if (value.TryGetValue("linesCount", out int l)) this.linesCount = l;
                if (value.TryGetValue("TopOptions", out LineOptions t)) this.TopOptions = t;
                if (value.TryGetValue("BottomOptions", out LineOptions b)) this.BottomOptions = b;
                this.OnSettingsUpdated();
            }
        }

        private static LineOptions CreateLineOptions(Pen pen) => new()
        {
            Color = pen.Color,
            Width = (int)pen.Width,
            LineStyle = (LineStyle)pen.DashStyle,
            WithCheckBox = false
        };

        private static void ApplyLineOptions(Pen pen, LineOptions options)
        {
            pen.Color = options.Color;
            pen.Width = options.Width;
            pen.DashStyle = (DashStyle)options.LineStyle;
        }
    }

    internal enum IndicatorAutoTrendLineMaxOrMin
    {
        LocalMaximum,
        LocalMinimum,
        Nothing
    }

    internal class IndicatorAutoTrendLinePivotPoint
    {
        public IndicatorAutoTrendLineMaxOrMin MaxOrMin { get; set; }
        public int index { get; set; }
        public double value { get; set; }
        public IndicatorAutoTrendLinePivotPoint(IndicatorAutoTrendLineMaxOrMin maxOrMin, int index)
        {
            this.MaxOrMin = maxOrMin;
            this.index = index;
        }
    }
}