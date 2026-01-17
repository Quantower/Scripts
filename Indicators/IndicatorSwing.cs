// Copyright QUANTOWER LLC. © 2017-2023. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using TradingPlatform.BusinessLayer;
using TradingPlatform.BusinessLayer.Utils;

namespace IndicatorSwing
{
    public class IndicatorSwing : Indicator
    {
        private int Period = 20;

        private bool drawLine = true;
        private int Strength = 10;

        private readonly Pen topLinePen = new Pen(Color.Green);
        private readonly Pen bottomLinePen = new Pen(Color.Red);

        private LineOptions _topLineOptions;
        public LineOptions TopLineOptions
        {
            get => this._topLineOptions;
            set => ApplyLineOptions(value, this.topLinePen, ref this._topLineOptions);
        }

        private LineOptions _bottomLineOptions;
        public LineOptions BottomLineOptions
        {
            get => this._bottomLineOptions;
            set => ApplyLineOptions(value, this.bottomLinePen, ref this._bottomLineOptions);
        }

        private bool toCross = true;
        private bool toEnd = false;

        private bool drawCircles = false;
        private bool useBarWidth = true;
        private bool drawCircleBorder = true;
        private int circleDimeter = 10;
        private readonly List<PivotPoint> pivots = new();
        private readonly Pen topCirclePen = new Pen(Color.Green);
        private readonly Pen bottomCirclePen = new Pen(Color.Red);
        private Color circleTopBodyColor = Color.FromArgb(85, Color.Green);
        private Color circleBottomBodyColor = Color.FromArgb(85, Color.Red);


        private LineOptions _topCircleOptions;
        public LineOptions TopCircleOptions
        {
            get => this._topLineOptions;
            set => ApplyLineOptions(value, this.topCirclePen, ref this._topCircleOptions);
        }

        private LineOptions _bottomCircleOptions;
        public LineOptions BottomCircleOptions
        {
            get => this._bottomCircleOptions;
            set => ApplyLineOptions(value, this.bottomCirclePen, ref this._bottomCircleOptions);
        }

        public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorSwing.cs";
        public IndicatorSwing()
        {
            this.Name = "Swing";
            this.SeparateWindow = false;

            this.TopLineOptions = CreateDefaultLineOptions(Color.Green);
            this.BottomLineOptions = CreateDefaultLineOptions(Color.Red);
            this.TopCircleOptions = CreateDefaultLineOptions(Color.Green);
            this.BottomCircleOptions = CreateDefaultLineOptions(Color.Red);
        }

        protected override void OnInit() => this.HistoryCalculation();

        protected override void OnUpdate(UpdateArgs args)
        {
            if (this.HistoricalData.Aggregation is HistoryAggregationTick || args.Reason == UpdateReason.HistoricalBar)
                return;

            this.PivotsEnding();

            int currBarIndex = this.Count - this.Period - 1;
            PivotPoint currPivot = this.BarPivotCalculation(currBarIndex);

            if (currPivot != null && (this.pivots.Count == 0 || this.pivots[^1].StartIndex != currPivot.StartIndex))
            {
                this.GetEndIndex(currPivot);
                this.pivots.Add(currPivot);
            }
            else if (currPivot == null && this.pivots.Count > 0 && this.pivots[^1].StartIndex == currBarIndex)
            {
                this.pivots.RemoveAt(this.pivots.Count - 1);
            }
        }

        public override void OnPaintChart(PaintChartEventArgs args)
        {
            Graphics graphics = args.Graphics;
            RectangleF originalClip = graphics.ClipBounds;
            graphics.SetClip(args.Rectangle);

            try
            {
                if (this.HistoricalData.Aggregation is HistoryAggregationTick)
                {
                    graphics.DrawString("Indicator does not work on tick aggregation", new Font("Arial", 20), Brushes.Red, 20, 50);
                    return;
                }
                for (int i = 0; i < this.pivots.Count; i++)
                {
                    var pivot = this.pivots[i];
                    int halfBarWidth = this.CurrentChart.BarsWidth / 2;

                    PointF startPoint = new()
                    {
                        X = (float)this.CurrentChart.MainWindow.CoordinatesConverter.GetChartX(this.HistoricalData[pivot.StartIndex, SeekOriginHistory.Begin].TimeLeft) + halfBarWidth,
                        Y = (float)this.CurrentChart.MainWindow.CoordinatesConverter.GetChartY(pivot.Value),
                    };
                    if (this.drawLine)
                    {
                        Pen currLinePen = pivot.MaxOrMin == MaxOrMin.LocalMaximum ? topLinePen : bottomLinePen;
                        PointF endPoint = new()
                        {
                            X = (float)this.CurrentChart.MainWindow.CoordinatesConverter.GetChartX(this.HistoricalData[pivot.EndIndex, SeekOriginHistory.Begin].TimeLeft) + halfBarWidth,
                            Y = startPoint.Y,
                        };
                        graphics.DrawLine(currLinePen, startPoint, endPoint);
                    }
                    if (this.drawCircles)
                    {
                        float diameter = this.useBarWidth ? halfBarWidth*2 : this.circleDimeter;
                        PointF circlePoint = new()
                        {
                            X = startPoint.X - diameter / 2,
                            Y = startPoint.Y - diameter / 2,
                        };
                        RectangleF circleRect = new(circlePoint, new SizeF(diameter, diameter));
                        SolidBrush currBodyBrush = new SolidBrush(pivot.MaxOrMin == MaxOrMin.LocalMaximum ? circleTopBodyColor : circleBottomBodyColor);
                        graphics.FillEllipse(currBodyBrush, circleRect);
                        if (this.drawCircleBorder)
                        {
                            Pen currCircleBorderPen = pivot.MaxOrMin == MaxOrMin.LocalMaximum ? topCirclePen : bottomCirclePen;
                            graphics.DrawEllipse(currCircleBorderPen, circleRect);
                        }
                    }
                }
            }
            finally
            {
                graphics.SetClip(originalClip);
            }
        }

        public override IList<SettingItem> Settings
        {
            get
            {
                var settings = base.Settings;
                var lineSettingsSeparatorGroup = new SettingItemSeparatorGroup("Lines Settings", 1);
                settings.Add(new SettingItemInteger("Period", this.Period)
                {
                    Text = "Period",
                    SortIndex = 1,
                    Minimum = 1,
                    SeparatorGroup = lineSettingsSeparatorGroup
                });
                settings.Add(new SettingItemBoolean("drawLine", this.drawLine)
                {
                    Text = "Draw Line",
                    SortIndex = 1,
                    SeparatorGroup = lineSettingsSeparatorGroup
                });
                var drawLineRelation = new SettingItemRelationVisibility("drawLine", true);
                settings.Add(new SettingItemBoolean("toEnd", this.toEnd)
                {
                    Text = "Continue to End",
                    SortIndex = 1,
                    Relation = drawLineRelation,
                    SeparatorGroup = lineSettingsSeparatorGroup
                });
                var notToEndRelation = new SettingItemRelationVisibility("toEnd", false);
                var useStrengthRelation = new SettingItemMultipleRelation([drawLineRelation, notToEndRelation]);
                settings.Add(new SettingItemInteger("Strength", this.Strength)
                {
                    Text = "Strength",
                    SortIndex = 1,
                    Minimum = 1,
                    Relation = useStrengthRelation,
                    SeparatorGroup = lineSettingsSeparatorGroup
                });
                settings.Add(new SettingItemBoolean("toCross", this.toCross)
                {
                    Text = "Continue to Cross",
                    SortIndex = 2,
                    Relation = drawLineRelation,
                    SeparatorGroup = lineSettingsSeparatorGroup
                });
                settings.Add(new SettingItemLineOptions("topLineOptions", this._topLineOptions)
                {
                    Text = "Top line options",
                    SortIndex = 3,
                    ExcludedStyles = new[] { LineStyle.Histogramm, LineStyle.Points },
                    UseEnabilityToggler = true,
                    Relation = drawLineRelation,
                    SeparatorGroup = lineSettingsSeparatorGroup
                });
                settings.Add(new SettingItemLineOptions("bottomLineOptions", this._bottomLineOptions)
                {
                    Text = "Bottom line options",
                    SortIndex = 3,
                    ExcludedStyles = new[] { LineStyle.Histogramm, LineStyle.Points },
                    UseEnabilityToggler = true,
                    Relation = drawLineRelation,
                    SeparatorGroup = lineSettingsSeparatorGroup
                });

                var circleSettingsSeparatorGroup = new SettingItemSeparatorGroup("Circles Settings", 2);
                settings.Add(new SettingItemBoolean("drawCircles", this.drawCircles)
                {
                    Text = "Draw Circle on level",
                    SortIndex = 4,
                    SeparatorGroup = circleSettingsSeparatorGroup
                });
                var drawCirclesRelation = new SettingItemRelationVisibility("drawCircles", true);
                settings.Add(new SettingItemBoolean("useBarWidth", this.useBarWidth)
                {
                    Text = "Use bar width for circle diameter",
                    SortIndex = 4,
                    Relation = drawCirclesRelation,
                    SeparatorGroup = circleSettingsSeparatorGroup
                });
                var notBarWidthRelation = new SettingItemRelationVisibility("useBarWidth", false);
                var customDiameterRelation = new SettingItemMultipleRelation([drawCirclesRelation, notBarWidthRelation]);
                settings.Add(new SettingItemInteger("circleDimeter", this.circleDimeter)
                {
                    Text = "Circle Diameter",
                    SortIndex = 4,
                    Minimum = 1,
                    Dimension = "px",
                    Relation = customDiameterRelation,
                    SeparatorGroup = circleSettingsSeparatorGroup
                });
                settings.Add(new SettingItemColor("circleTopBodyColor", this.circleTopBodyColor)
                {
                    Text = "Top Circle Color",
                    SortIndex = 4,
                    Relation = drawCirclesRelation,
                    SeparatorGroup = circleSettingsSeparatorGroup
                });
                settings.Add(new SettingItemColor("circleBottomBodyColor", this.circleBottomBodyColor)
                {
                    Text = "Bottom Circle Color",
                    SortIndex = 4,
                    Relation = drawCirclesRelation,
                    SeparatorGroup = circleSettingsSeparatorGroup
                });
                settings.Add(new SettingItemBoolean("drawCircleBorder", this.drawCircleBorder)
                {
                    Text = "Draw Borders",
                    SortIndex = 4,
                    Relation = drawCirclesRelation,
                    SeparatorGroup = circleSettingsSeparatorGroup
                });
                var drawCirclesBorderRelation = new SettingItemRelationVisibility("drawCircleBorder", true);
                var drawBordersRelation = new SettingItemMultipleRelation([drawCirclesRelation, drawCirclesBorderRelation]);
                settings.Add(new SettingItemLineOptions("topCircleOptions", this._topCircleOptions)
                {
                    Text = "Top circle options",
                    SortIndex = 5,
                    ExcludedStyles = new[] { LineStyle.Histogramm, LineStyle.Points },
                    UseEnabilityToggler = true,
                    Relation = drawBordersRelation,
                    SeparatorGroup = circleSettingsSeparatorGroup
                });
                settings.Add(new SettingItemLineOptions("bottomCircleOptions", this._bottomCircleOptions)
                {
                    Text = "Bottom circle options",
                    SortIndex = 5,
                    ExcludedStyles = new[] { LineStyle.Histogramm, LineStyle.Points },
                    UseEnabilityToggler = true,
                    Relation = drawBordersRelation,
                    SeparatorGroup = circleSettingsSeparatorGroup
                });

                return settings;
            }
            set
            {
                base.Settings = value;
                bool needHistoryRecalculation = false;
                if (value.TryGetValue("drawLine", out bool drawLine))
                    this.drawLine = drawLine;
                if (value.TryGetValue("Period", out int period))
                {
                    this.Period = period;
                    needHistoryRecalculation = true;
                }
                if (value.TryGetValue("toEnd", out bool end))
                {
                    this.toEnd = end;
                    needHistoryRecalculation = true;
                }
                if (value.TryGetValue("Strength", out int strength))
                {
                    needHistoryRecalculation = true;
                    this.Strength = strength;
                }
                if (value.TryGetValue("toCross", out bool cross))
                {
                    needHistoryRecalculation = true;
                    this.toCross = cross;
                }
                if (value.TryGetValue("topLineOptions", out LineOptions top))
                    this.TopLineOptions = top;
                if (value.TryGetValue("bottomLineOptions", out LineOptions bottom))
                    this.BottomLineOptions = bottom;

                if (value.TryGetValue("drawCircles", out bool drawCircles))
                    this.drawCircles = drawCircles;
                if (value.TryGetValue("useBarWidth", out bool useBarWidth))
                    this.useBarWidth = useBarWidth;
                if (value.TryGetValue("drawCircleBorder", out bool drawCircleBorder))
                    this.drawCircleBorder = drawCircleBorder;
                if (value.TryGetValue("circleDimeter", out int circleDimeter))
                    this.circleDimeter = circleDimeter;
                if (value.TryGetValue("circleTopBodyColor", out Color circleTopBodyColor))
                    this.circleTopBodyColor = circleTopBodyColor;
                if (value.TryGetValue("circleBottomBodyColor", out Color circleBottomBodyColor))
                    this.circleBottomBodyColor = circleBottomBodyColor;
                if (value.TryGetValue("topCircleOptions", out LineOptions topCircleOptions))
                    this.TopCircleOptions = topCircleOptions;
                if (value.TryGetValue("bottomCircleOptions", out LineOptions bottomCircleOptions))
                    this.BottomCircleOptions = bottomCircleOptions;
                if (needHistoryRecalculation)
                    this.HistoryCalculation();
            }
        }

        private void HistoryCalculation()
        {
            this.pivots.Clear();

            if (this.HistoricalData!=null)
            {
                for (int i = 0; i < this.HistoricalData.Count; i++)
                {
                    var pivot = this.BarPivotCalculation(i);
                    if (pivot != null && (this.pivots.Count == 0 || this.pivots[^1].StartIndex != pivot.StartIndex))
                    {
                        this.GetEndIndex(pivot);
                        this.pivots.Add(pivot);
                    }
                }
            }
        }

        private void GetEndIndex(PivotPoint pivot)
        {
            if (pivot.Ended) return;

            int targetStrength = pivot.StartIndex + this.Strength;
            int workingLimit = this.toEnd ? this.HistoricalData.Count - 1 : Math.Min(targetStrength, this.HistoricalData.Count - 1);

            if (!this.toCross)
            {
                pivot.EndIndex = workingLimit;
                pivot.Ended = true;
                return;
            }

            for (int i = pivot.StartIndex + 1; i < workingLimit; i++)
            {
                var bar = (HistoryItemBar)this.HistoricalData[i, SeekOriginHistory.Begin];
                if (pivot.Value <= bar.High && pivot.Value >= bar.Low)
                {
                    pivot.EndIndex = i;
                    pivot.Ended = true;
                    return;
                }
            }

            pivot.EndIndex = workingLimit;
            if (!toEnd && workingLimit == targetStrength)
                pivot.Ended = true;
        }

        private void PivotsEnding()
        {
            for (int i = 0; i < this.pivots.Count; i++)
            {
                var pivot = this.pivots[i];
                if (!pivot.Ended)
                    this.GetEndIndex(pivot);
            }
        }

        private PivotPoint BarPivotCalculation(int index)
        {
            var type = this.LocalMaxOrMin(index);
            if (type == MaxOrMin.Nothing)
                return null;

            var bar = (HistoryItemBar)this.HistoricalData[index, SeekOriginHistory.Begin];
            return new PivotPoint(type, index)
            {
                Value = type == MaxOrMin.LocalMaximum ? bar.High : bar.Low
            };
        }

        private MaxOrMin LocalMaxOrMin(int index)
        {
            if (index < Period || index >= this.HistoricalData.Count - Period)
                return MaxOrMin.Nothing;

            var current = (HistoryItemBar)this.HistoricalData[index, SeekOriginHistory.Begin];
            MaxOrMin result = MaxOrMin.Nothing;

            for (int i = 1; i <= Period; i++)
            {
                var left = (HistoryItemBar)this.HistoricalData[index - i, SeekOriginHistory.Begin];
                var right = (HistoryItemBar)this.HistoricalData[index + i, SeekOriginHistory.Begin];

                bool isMax = left.High < current.High && right.High < current.High;
                bool isMin = left.Low > current.Low && right.Low > current.Low;

                if (isMax)
                {
                    if (result == MaxOrMin.LocalMinimum) return MaxOrMin.Nothing;
                    result = MaxOrMin.LocalMaximum;
                }
                else if (isMin)
                {
                    if (result == MaxOrMin.LocalMaximum) return MaxOrMin.Nothing;
                    result = MaxOrMin.LocalMinimum;
                }
                else
                    return MaxOrMin.Nothing;
            }

            return result;
        }

        protected override void OnClear()
        {
            base.OnClear();
            pivots.Clear();
        }

        private static void ApplyLineOptions(LineOptions value, Pen pen, ref LineOptions field)
        {
            field = value;
            pen.Color = value.Color;
            pen.Width = value.Width;
            pen.DashStyle = (DashStyle)value.LineStyle;
        }

        private static LineOptions CreateDefaultLineOptions(Color color) => new LineOptions
        {
            Color = color,
            Width = 1,
            LineStyle = LineStyle.Solid,
            WithCheckBox = false,
            Enabled = false
        };
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
        public int StartIndex { get; set; }
        public int EndIndex { get; set; }
        public double Value { get; set; }
        public bool Ended { get; set; }

        public PivotPoint(MaxOrMin maxOrMin, int index)
        {
            MaxOrMin = maxOrMin;
            StartIndex = index;
            Ended = false;
        }
    }
}