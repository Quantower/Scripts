using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using TradingPlatform.BusinessLayer;

namespace Fractals;

public class IndicatorFractals : Indicator
{
    [InputParameter("Period", 10, 1)]
    public int period = 3;

    [InputParameter("Local Maximum Color", 20)]
    public Color maximumColor = Color.Green;

    [InputParameter("Local Minimum Color", 30)]
    public Color minimumColor = Color.Red;

    [InputParameter("Draw marker lines untils intersection", 40)]
    public bool drawLinesUntilInterstion = false;

    [InputParameter("Local maximum icon type", 1, variants: new object[]{
        "Arrow", IndicatorLineMarkerIconType.UpArrow,
        "Flag", IndicatorLineMarkerIconType.Flag,
        "Circle", IndicatorLineMarkerIconType.FillCircle,
        "Pointer", IndicatorLineMarkerIconType.UpPointer,
        "Line", IndicatorLineMarkerIconType.Line,
    })]
    public IndicatorLineMarkerIconType localMaxIconType = IndicatorLineMarkerIconType.UpArrow;

    [InputParameter("Local minimum icon type", 1, variants: new object[]{
        "Arrow", IndicatorLineMarkerIconType.DownArrow,
        "Flag", IndicatorLineMarkerIconType.Flag,
        "Circle", IndicatorLineMarkerIconType.FillCircle,
        "Pointer", IndicatorLineMarkerIconType.DownPointer,
        "Line", IndicatorLineMarkerIconType.Line,
    })]
    public IndicatorLineMarkerIconType localMinIconType = IndicatorLineMarkerIconType.DownArrow;
    [InputParameter("Marker size", 1, variants: new object[]{
        "Small",  IndicatorLineMarkerMarkerSize.Small ,
        "Medium", IndicatorLineMarkerMarkerSize.Medium,
        "Large", IndicatorLineMarkerMarkerSize.Large,
    })]
    public IndicatorLineMarkerMarkerSize markerSize = IndicatorLineMarkerMarkerSize.Medium;


    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorFractals.cs";

    public IndicatorFractals()
        : base()
    {
        Name = "Fractals";
        Description = "My indicator's annotation";

        AddLineSeries("HighLine", Color.DarkOliveGreen, 1, LineStyle.Points);
        AddLineSeries("LowLine", Color.IndianRed, 1, LineStyle.Points);
        SeparateWindow = false;
    }

    protected override void OnInit()
    { }

    protected override void OnUpdate(UpdateArgs args)
    {
        if (Count < period * 2 + 1)
            return;

        double baseHigh = High(period);
        double baseLow = Low(period);

        int minTrendValue = 0;
        int maxTrendValue = 0;

        SetValue(High(), 0);
        SetValue(Low(), 1);

        for (int i = 1; i <= period; i++)
        {
            double leftHigh = High(period + i);
            double leftLow = Low(period + i);
            double rightHigh = High(period - i);
            double rightLow = Low(period - i);
            if (baseHigh >= leftHigh && baseHigh > rightHigh)
                maxTrendValue++;

            if (baseLow <= leftLow && baseLow < rightLow)
                minTrendValue++;

        }
        if (maxTrendValue == period)
            LinesSeries[0].SetMarker(period, new IndicatorLineMarker(this.maximumColor, upperIcon: this.localMaxIconType) { MarkerSize = this.markerSize });
        if (minTrendValue == period)
            LinesSeries[1].SetMarker(period, new IndicatorLineMarker(this.minimumColor, bottomIcon: this.localMinIconType) { MarkerSize = this.markerSize });

        if (maxTrendValue != period)
            LinesSeries[0].RemoveMarker(period);
        if (minTrendValue != period)
            LinesSeries[1].RemoveMarker(period);
    }
    #region Draw lines until intersection

    public override void OnPaintChart(PaintChartEventArgs args)
    {
        base.OnPaintChart(args);

        if (!this.drawLinesUntilInterstion)
            return;

        try
        {
            //
            Graphics gr = args.Graphics;
            List<MarkerLineItem> markersCache = new List<MarkerLineItem>();
            double topPrice = this.CurrentChart.MainWindow.CoordinatesConverter.GetPrice(this.CurrentChart.MainWindow.ClientRectangle.Top);
            double bottomPrice = this.CurrentChart.MainWindow.CoordinatesConverter.GetPrice(this.CurrentChart.MainWindow.ClientRectangle.Bottom);
            Pen maximumPen = new Pen(this.maximumColor, 1) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
            Pen minimumPen = new Pen(this.minimumColor, 1) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };

            // Check
            for (int i = 0; i < this.Count; i++)
            {
                int indexFromBegin = this.Count - i - 1;

                // Draw line for existing markers
                double highPrice = this.High(indexFromBegin);
                double lowPrice = this.Low(indexFromBegin);
                int barX = (int)this.CurrentChart.MainWindow.CoordinatesConverter.GetChartX(this.Time(indexFromBegin));
                int halfBarWidth = (int)(this.CurrentChart.BarsWidth / 2.0);
                int highY = (int)this.CurrentChart.MainWindow.CoordinatesConverter.GetChartY(highPrice);
                int lowY = (int)this.CurrentChart.MainWindow.CoordinatesConverter.GetChartY(lowPrice);

                for (int j = 0; j < markersCache.Count; j++)
                {
                    var currentMarker = markersCache[j];

                    if ((currentMarker.Price <= highPrice && currentMarker.Price >= lowPrice) || indexFromBegin == 0)
                    {
                        gr.DrawLine(currentMarker.IsMax ? maximumPen : minimumPen, currentMarker.StartX, currentMarker.Y, barX + ((indexFromBegin == 0) ? halfBarWidth : 0), currentMarker.Y);
                        currentMarker.Closed = true;
                    }
                }
                markersCache = markersCache.Where(x => !x.Closed).ToList();

                // Create Max markers
                var marker = this.LinesSeries[0].GetMarker(indexFromBegin);
                if (marker != null)
                {
                    if (highPrice > topPrice || highPrice < bottomPrice)
                        continue;

                    markersCache.Add(new MarkerLineItem()
                    {
                        Price = highPrice,
                        StartX = barX + halfBarWidth,
                        Y = highY,
                        IsMax = true
                    });
                }

                // Create Min markers
                marker = this.LinesSeries[1].GetMarker(indexFromBegin);
                if (marker != null)
                {
                    if (lowPrice > topPrice || lowPrice < bottomPrice)
                        continue;

                    markersCache.Add(new MarkerLineItem()
                    {
                        Price = lowPrice,
                        StartX = barX + halfBarWidth,
                        Y = lowY,
                        IsMax = false
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Core.Instance.Loggers.Log(ex);
        }
    }

    class MarkerLineItem
    {
        public double Price { get; set; }
        public int Y { get; set; }
        public int StartX { get; set; }
        public bool Closed { get; set; }
        public bool IsMax { get; set; }
    }

    #endregion
}