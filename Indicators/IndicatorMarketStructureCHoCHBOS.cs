// Copyright QUANTOWER LLC. © 2017-2024. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using TradingPlatform.BusinessLayer;
using TradingPlatform.BusinessLayer.Utils;

namespace TrendIndicators.IndicatorMarketStructureCHoCHBOS;

public class IndicatorMarketStructureCHoCHBOS : Indicator
{
    private int Period = 10;
    private readonly Pen BearishPen;
    private readonly Pen BullishPen;
    private readonly Pen BullishResistPen;
    private readonly Pen BearishResistPen;
    private bool showResist = false;
    private bool showLabel = false;
    public LineOptions BullishOptions { get; set; }
    public LineOptions BearishOptions { get; set; }
    public LineOptions BullishResistOptions { get; set; }
    public LineOptions BearishResistOptions { get; set; }
    private LabelLocation labelLocation = LabelLocation.Start;
    private Font labelFont = new Font("Arial", 12);

    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorMarketStructureCHoCHBOS.cs";

    List<PivotPoint> pivots = new List<PivotPoint>();
    public IndicatorMarketStructureCHoCHBOS()
        : base()
    {
        Name = "Market Structure CHoCH/BOS (Fractal)";
        SeparateWindow = false;

        this.BearishPen = new Pen(Color.Red);
        this.BullishPen = new Pen(Color.Green);
        this.BearishResistPen = new Pen(Color.Red);
        this.BullishResistPen = new Pen(Color.Green);
        this.BullishResistPen.DashStyle = DashStyle.Dash;
        this.BearishResistPen.DashStyle = DashStyle.Dash;
        this.BullishOptions = new LineOptions();
        this.BearishOptions = new LineOptions();
        this.BullishResistOptions = new LineOptions();
        this.BearishResistOptions = new LineOptions();
        this.BullishOptions.WithCheckBox = false;
        this.BearishOptions.WithCheckBox = false;
        this.BullishResistOptions.WithCheckBox = false;
        this.BearishResistOptions.WithCheckBox = false;
        this.BullishOptions.Color = Color.Green;
        this.BearishOptions.Color = Color.Red;
        this.BullishResistOptions.Color = Color.Green;
        this.BearishResistOptions.Color = Color.Red;
        this.BearishResistOptions.LineStyle = LineStyle.Dash;
        this.BullishResistOptions.LineStyle = LineStyle.Dash;
    }
    protected override void OnInit()
    {
        pivots.Clear();
    }
    protected override void OnUpdate(UpdateArgs args)
    {
        if (this.Count < Period * 2 + 1)
            return;
        int currBarIndex = this.Count - Period - 1;
        MaxOrMin maxOrMin = LocalMaxOrMin(currBarIndex);
        if (maxOrMin != MaxOrMin.Nothing && (pivots.Count < 1 || pivots[pivots.Count - 1].index != currBarIndex))
        {
            this.pivots.Add(new PivotPoint(maxOrMin, currBarIndex));
            if (this.pivots[this.pivots.Count - 1].MaxOrMin == MaxOrMin.LocalMaximum)
            {
                this.pivots[this.pivots.Count - 1].value = ((HistoryItemBar)this.HistoricalData[this.Count - this.Period - 1, SeekOriginHistory.Begin]).High;
            }
            else
                this.pivots[this.pivots.Count - 1].value = ((HistoryItemBar)this.HistoricalData[this.Count - this.Period - 1, SeekOriginHistory.Begin]).Low;
        }
    }
    public override void OnPaintChart(PaintChartEventArgs args)
    {
        base.OnPaintChart(args);

        if (this.CurrentChart == null)
            return;
        var gr = args.Graphics;
        Period currentPeriod = this.HistoricalData.Aggregation.GetPeriod;
        if (currentPeriod.BasePeriod == BasePeriod.Tick)
        {
            gr.DrawString("Indicator does not work on tick aggregation", new Font("Arial", 20), Brushes.Red, 20, 50);
            return;
        }
        var currWindow = this.CurrentChart.Windows[args.WindowIndex];
        RectangleF prevClipRectangle = gr.ClipBounds;
        gr.SetClip(args.Rectangle);
        try
        {
            PointF startPoint = new PointF();
            PointF endPoint = new PointF();
            PointF prevStartPoint = new PointF();
            PointF prevEndPoint = new PointF();
            PointF labelPoint = new PointF();
            PointF prevlabelPoint = new PointF();
            Pen currPen = null;
            Pen resistPen = null;
            string labelText = "";
            MaxOrMin currTrend = MaxOrMin.Nothing;
            bool changed = false;
            Brush labelBrush = new SolidBrush(Color.White);
            for (int i = 1; i < pivots.Count; i++)
            {
                for (int j = 1; j <=2; j++)
                {
                    if (i + j >= pivots.Count-1)
                        break;
                    var p1 = pivots[i];
                    var p2 = pivots[i+j];
                    if (p1.MaxOrMin == p2.MaxOrMin)
                    {
                        if (p1.MaxOrMin == MaxOrMin.LocalMaximum && p2.value > p1.value)
                        {
                            currPen = BullishPen;
                        }
                        else if (p1.MaxOrMin == MaxOrMin.LocalMinimum && p2.value < p1.value)
                        {
                            currPen = this.BearishPen;
                        }
                        else
                            continue;
                        startPoint.Y = (float)currWindow.CoordinatesConverter.GetChartY(p1.value);
                        endPoint.Y = startPoint.Y;
                        startPoint.X = (float)currWindow.CoordinatesConverter.GetChartX(this.HistoricalData[p1.index, SeekOriginHistory.Begin].TimeLeft) + this.CurrentChart.BarsWidth / 2;
                        endPoint.X = (float)currWindow.CoordinatesConverter.GetChartX(this.HistoricalData[p2.index, SeekOriginHistory.Begin].TimeLeft) + this.CurrentChart.BarsWidth / 2;
                        if (showLabel)
                            switch (labelLocation)
                            {
                                case LabelLocation.Start:
                                    labelPoint = startPoint;
                                    break;
                                case LabelLocation.Middle:
                                    labelPoint.X = (startPoint.X + endPoint.X) / 2;
                                    labelPoint.Y = (startPoint.Y + endPoint.Y) / 2;
                                    break;
                                case LabelLocation.End:
                                    labelPoint = endPoint;
                                    break;
                                default:
                                    labelPoint = startPoint;
                                    break;
                            }
                        labelBrush = new SolidBrush(currPen.Color);
                        if (p1.MaxOrMin == currTrend)
                        {
                            gr.DrawLine(currPen, startPoint, endPoint);
                            if (showLabel)
                            {
                                labelText = "BoS";
                                if (currTrend == MaxOrMin.LocalMaximum)
                                    labelPoint.Y -= gr.MeasureString(labelText, this.labelFont).Height;
                                gr.DrawString(labelText, this.labelFont, labelBrush, labelPoint);
                            }
                            if (changed)
                            {
                                changed = false;
                                gr.DrawLine(currPen, prevStartPoint, prevEndPoint);
                                if (showLabel)
                                {
                                    labelText = "CHoCH";
                                    if (currTrend == MaxOrMin.LocalMaximum)
                                        prevlabelPoint.Y -= gr.MeasureString(labelText, this.labelFont).Height;
                                    gr.DrawString(labelText, this.labelFont, labelBrush, prevlabelPoint);
                                }

                            }
                            break;
                        }
                        else
                        {
                            currTrend = p1.MaxOrMin;
                            changed = true;
                            prevStartPoint = startPoint;
                            prevEndPoint = endPoint;
                            prevlabelPoint = labelPoint;
                            if (showResist)
                            {
                                if (p1.MaxOrMin == MaxOrMin.LocalMaximum)
                                    resistPen = BullishResistPen;
                                else
                                    resistPen = BearishResistPen;
                                gr.DrawLine(resistPen, startPoint, endPoint);
                            }
                            break;
                        }
                    }
                }
            }
        }
        finally
        {
            gr.SetClip(prevClipRectangle);
        }
    }
    private MaxOrMin LocalMaxOrMin(int index = 0)
    {
        MaxOrMin maxOrMin = MaxOrMin.Nothing;
        for (int i = 1; i <= this.Period; i++)
        {
            HistoryItemBar leftBar = (HistoryItemBar)this.HistoricalData[index - i, SeekOriginHistory.Begin];
            HistoryItemBar rightBar = (HistoryItemBar)this.HistoricalData[index + i, SeekOriginHistory.Begin];
            HistoryItemBar currentBar = (HistoryItemBar)this.HistoricalData[index, SeekOriginHistory.Begin];
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
    public override IList<SettingItem> Settings
    {
        get
        {
            var settings = base.Settings;
            settings.Add(new SettingItemInteger("Period", this.Period)
            {
                Text = "Period",
                SortIndex = 1,
                Minimum = 2,
            });
            settings.Add(new SettingItemLineOptions("BullishOptions", this.BullishOptions)
            {
                Text = "Bullish Line Style",
                SortIndex = 2,
                ExcludedStyles = new LineStyle[] { LineStyle.Histogramm, LineStyle.Points },
                UseEnabilityToggler = true
            });
            settings.Add(new SettingItemLineOptions("BearishOptions", this.BearishOptions)
            {
                Text = "Bearish Line Style",
                SortIndex= 2,
                ExcludedStyles = new LineStyle[] { LineStyle.Histogramm, LineStyle.Points },
                UseEnabilityToggler = true
            });
            settings.Add(new SettingItemBoolean("showLabel", this.showLabel)
            {
                Text = "Show Label",
                SortIndex = 2
            });
            SettingItemRelationVisibility visibleLabel = new SettingItemRelationVisibility("showLabel", true);
            settings.Add(new SettingItemFont("Font", this.labelFont)
            {
                Text = "Label Font",
                SortIndex = 2,
                Relation = visibleLabel
            });
            settings.Add(new SettingItemSelectorLocalized("labelLocation", this.labelLocation, new List<SelectItem> { new SelectItem("Start", LabelLocation.Start), new SelectItem("Middle", LabelLocation.Middle), new SelectItem("End", LabelLocation.End) })
            {
                Text = "Label Location",
                SortIndex = 2,
                Relation = visibleLabel
            });
            settings.Add(new SettingItemBoolean("showResist", this.showResist)
            {
                Text = "Show Resistance",
                SortIndex = 2
            });
            SettingItemRelationVisibility visibleResistance = new SettingItemRelationVisibility("showResist", true);
            settings.Add(new SettingItemLineOptions("BullishResistOptions", this.BullishResistOptions)
            {
                Text = "Bullish Resistance Style",
                SortIndex = 2,
                ExcludedStyles = new LineStyle[] { LineStyle.Histogramm, LineStyle.Points },
                UseEnabilityToggler = true,
                Relation = visibleResistance
            });
            settings.Add(new SettingItemLineOptions("BearishResistOptions", this.BearishResistOptions)
            {
                Text = "Bearish Resistance  Style",
                SortIndex = 2,
                ExcludedStyles = new LineStyle[] { LineStyle.Histogramm, LineStyle.Points },
                UseEnabilityToggler = true,
                Relation=visibleResistance
            });
            return settings;
        }
        set
        {
            if (value.TryGetValue("Period", out int Period))
                this.Period = Period;
            if (value.TryGetValue("BullishOptions", out LineOptions BullishOptions))
            {
                this.BullishPen.Width = BullishOptions.Width;
                this.BullishPen.Color = BullishOptions.Color;
                this.BullishPen.DashStyle = (DashStyle)BullishOptions.LineStyle;
            }
            if (value.TryGetValue("BearishOptions", out LineOptions BearishOptions))
            {
                this.BearishPen.Width = BearishOptions.Width;
                this.BearishPen.Color = BearishOptions.Color;
                this.BearishPen.DashStyle = (DashStyle)BearishOptions.LineStyle;
            }
            if (value.TryGetValue("showLabel", out bool showLabel))
                this.showLabel = showLabel;
            if (value.TryGetValue("Font", out Font labelFont))
                this.labelFont = labelFont;
            if (value.TryGetValue("showResist", out bool showResist))
                this.showResist = showResist;
            if (value.TryGetValue("BullishResistOptions", out LineOptions BullishResistOptions))
            {
                this.BullishResistPen.Width = BullishResistOptions.Width;
                this.BullishResistPen.Color = BullishResistOptions.Color;
                this.BullishResistPen.DashStyle = (DashStyle)BullishResistOptions.LineStyle;
            }
            if (value.TryGetValue("BearishResistOptions", out LineOptions BearishResistOptions))
            {
                this.BearishResistPen.Width = BearishResistOptions.Width;
                this.BearishResistPen.Color = BearishResistOptions.Color;
                this.BearishResistPen.DashStyle = (DashStyle)BearishResistOptions.LineStyle;
            }
            if (value.TryGetValue("labelLocation", out LabelLocation labelLocation))
                this.labelLocation = labelLocation;
            this.OnSettingsUpdated();
        }
    }
}
internal enum MaxOrMin
{
    LocalMaximum,
    LocalMinimum,
    Nothing
}
internal enum LabelLocation
{
    Start,
    Middle,
    End
}
internal class PivotPoint
{
    public MaxOrMin MaxOrMin { get; set; }
    public int index { get; set; }
    public double value { get; set; }
    public PivotPoint(MaxOrMin maxOrMin, int index)
    {
        this.MaxOrMin = maxOrMin;
        this.index = index;
    }
}