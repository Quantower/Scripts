// Copyright QUANTOWER LLC. © 2017-2024. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using TradingPlatform.BusinessLayer;

namespace VolumeIndicators;

public class IndicatorPowerBars : Indicator
{
    [InputParameter("MA period", 0, 2, 10000, 1, 0)]
    public int volumeMAPeriod = 21;
    [InputParameter("Volume treshold (%)", 1, 2, 10000, 1, 0)]
    public int volumeTreshold = 200;
    [InputParameter("Price source", 2, variants: new object[] {
            "SMA", MaMode.SMA,
            "EMA", MaMode.EMA,
            "SMMA", MaMode.SMMA,
            "LWMA", MaMode.LWMA,
    })]
    public MaMode volumeMaMode = MaMode.SMA;
    [InputParameter("Exceeded treshold color", 3)]
    public Color tresholdColor = Color.Red;

    Indicator volumeMa;

    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorPowerBars.cs";

    public IndicatorPowerBars()
        : base()
    {
        Name = "Power Bars";
        SeparateWindow = false;
    }
    protected override void OnInit()
    {
        this.volumeMa = Core.Indicators.BuiltIn.MA(this.volumeMAPeriod, PriceType.Volume, this.volumeMaMode);
        this.AddIndicator(this.volumeMa);
    }
    protected override void OnUpdate(UpdateArgs args)
    {
    }
    public override void OnPaintChart(PaintChartEventArgs args)
    {
        base.OnPaintChart(args);

        if (this.CurrentChart == null || this.HistoricalData == null || this.Count < this.volumeMAPeriod)
            return;

        var wnd = this.CurrentChart.Windows?[args.WindowIndex];
        if (wnd == null)
            return;

        var g = args.Graphics;
        var savedClip = g.ClipBounds;

        try
        {
            g.SetClip(args.Rectangle);

            var leftTime = wnd.CoordinatesConverter.GetTime(args.Rectangle.Left);
            var rightTime = wnd.CoordinatesConverter.GetTime(args.Rectangle.Right);

            int leftIndex = (int)this.HistoricalData.GetIndexByTime(leftTime.Ticks, SeekOriginHistory.Begin);
            int rightIndex = (int)this.HistoricalData.GetIndexByTime(rightTime.Ticks, SeekOriginHistory.Begin);

            if (leftIndex  < 0) leftIndex  = 0;
            if (rightIndex < 0) rightIndex = this.Count - 1;

            leftIndex  = Math.Max(0, leftIndex - 1);
            rightIndex = Math.Min(this.Count - 1, rightIndex + 1);

            int bodyWidth = this.CurrentChart.BarsWidth;
            int barLeftOffset = 0;
            if (bodyWidth > 5)
            {
                if (bodyWidth % 2 == 1)
                {
                    barLeftOffset = 1;
                    bodyWidth -= 2;
                }
                else
                {
                    bodyWidth -= 1;
                }
            }

            var fillBrush = new SolidBrush(this.tresholdColor);

            for (int i = leftIndex; i <= rightIndex; i++)
            {
                int shift = this.Count - 1 - i;

                double vol = this.GetPrice(PriceType.Volume, shift);
                double ma = this.volumeMa.GetValue(shift);

                if (ma <= 0 || double.IsNaN(ma) || double.IsNaN(vol))
                    continue;

                double ratio = (vol / ma) * 100.0;
                if (ratio < this.volumeTreshold)
                    continue;

                var bar = (HistoryItemBar)this.HistoricalData[i, SeekOriginHistory.Begin];

                int x = (int)wnd.CoordinatesConverter.GetChartX(bar.TimeLeft);

                int yOpen = (int)wnd.CoordinatesConverter.GetChartY(bar.Open);
                int yClose = (int)wnd.CoordinatesConverter.GetChartY(bar.Close);

                float top = Math.Min(yOpen, yClose);
                float height = Math.Abs(yOpen - yClose);
                if (height < 1f) height = 1f; 

                g.FillRectangle(fillBrush, x+barLeftOffset, top, bodyWidth, height);
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
    }

}
