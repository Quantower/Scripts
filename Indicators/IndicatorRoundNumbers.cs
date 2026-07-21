// Copyright QUANTOWER LLC. © 2017-2024. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using TradingPlatform.BusinessLayer;
using TradingPlatform.BusinessLayer.Utils;

namespace ChanneIsIndicators;

public class IndicatorRoundNumbers : Indicator
{
    public const string ROUND_NUMBERS_SI = "Step of lines (ticks)";
    public const string NUMBER_OF_LINES_SI = "Number of lines";

    [InputParameter(ROUND_NUMBERS_SI, 10, 1, int.MaxValue, 1, 0)]
    public int Step = 50;
    [InputParameter("Offset (ticks)", 15, int.MinValue, int.MaxValue, 1, 0)]
    public int OffsetTicks = 0;
    [InputParameter(NUMBER_OF_LINES_SI, 20, 1, int.MaxValue, 1, 0)]
    public int NumberOfLines = 20;

    public override string ShortName => $"{this.Name} ({this.Step})";

    public LineOptions LineOptions
    {
        get => this.lineOptions;
        set
        {
            this.lineOptions = value;
            this.currentPen = ProcessPen(this.currentPen, value);
        }
    }
    private LineOptions lineOptions;
    private Pen currentPen;

    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorRoundNumbers.cs";

    public IndicatorRoundNumbers()
    {
        this.Name = "Round numbers";

        this.LineOptions = new LineOptions()
        {
            LineStyle = LineStyle.Solid,
            Color = Color.Orange,
            Width = 2,
            WithCheckBox = false,
            Enabled = true
        };

        this.OnBackGround = true;
        this.SeparateWindow = false;
    }

    #region Overrides

    public override void OnPaintChart(PaintChartEventArgs args)
    {
        var gr = args.Graphics;

        double tickSize = this.CurrentChart.TickSize;
        double levelStep = this.Step * tickSize;
        double offsetPrice = this.OffsetTicks * tickSize;

        float deltaPriceY = (float)(levelStep * this.CurrentChart.MainWindow.YScaleFactor);
        if (deltaPriceY <= 0)
            return;

        int bottom = this.CurrentChart.MainWindow.ClientRectangle.Bottom;
        int top = this.CurrentChart.MainWindow.ClientRectangle.Top;
        int right = this.CurrentChart.MainWindow.ClientRectangle.Right;

        double middlePrice = this.Close();

        double correctedMiddleLevel =
            Math.Floor((middlePrice - offsetPrice) / levelStep) * levelStep + offsetPrice;

        correctedMiddleLevel = Math.Round(correctedMiddleLevel / tickSize) * tickSize;

        float startLevel = (float)(
            this.CurrentChart.MainWindow.CoordinatesConverter.GetChartY(correctedMiddleLevel)
            - Math.Ceiling(this.NumberOfLines / 2d) * deltaPriceY
        );
        for (int i = 0; i < this.NumberOfLines; i++)
        {
            if (startLevel >= top && startLevel <= bottom)
                gr.DrawLine(this.currentPen, 0f, startLevel, right, startLevel);

            startLevel += deltaPriceY;
        }
    }
    public override IList<SettingItem> Settings
    {
        get
        {
            var settings = base.Settings;

            var inputParametersSepar = settings.GetItemByName(ROUND_NUMBERS_SI) is SettingItem si
                ? si.SeparatorGroup
                : new SettingItemSeparatorGroup("", 10);

            settings.Add(new SettingItemLineOptions("LineStyle", this.LineOptions, 10)
            {
                Text = loc._("Line style"),
                SeparatorGroup = inputParametersSepar
            });

            return settings;
        }
        set
        {
            base.Settings = value;

            if (value.GetItemByName("LineStyle") is SettingItemLineOptions si)
                this.LineOptions = si.Value as LineOptions;
        }
    }

    #endregion Overrides

    private static Pen ProcessPen(Pen pen, LineOptions lineOptions)
    {
        if (pen == null)
            pen = new Pen(Color.Empty);

        pen.Color = lineOptions.Color;
        pen.Width = lineOptions.Width;

        try
        {
            switch (lineOptions.LineStyle)
            {
                case LineStyle.Solid:
                    {
                        pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Solid;
                        break;
                    }
                case LineStyle.Dot:
                    {
                        pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dot;
                        break;
                    }
                case LineStyle.Dash:
                    {
                        pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                        break;
                    }
                case LineStyle.DashDot:
                    {
                        pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Custom;
                        float[] dp = new float[] { 2, 4, 7, 4 };
                        pen.DashPattern = dp;
                        break;
                    }
                case LineStyle.Histogramm:
                    {
                        pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Custom;
                        float[] dp = new float[] { 0.25F, 1 };
                        pen.DashPattern = dp;
                        pen.Width = 4;
                        break;
                    }
            }
        }
        catch (Exception ex)
        {
            Core.Loggers.Log(ex);
        }
        return pen;
    }
}