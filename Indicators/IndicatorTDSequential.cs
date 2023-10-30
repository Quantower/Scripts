// Copyright QUANTOWER LLC. © 2017-2023. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace TrendIndicators;

public class IndicatorTDSequential : Indicator
{
    #region Consts

    private const int MIN_PERIOD = 6;
    private const int MAX_TREND_COUNTER = 9;
    private const string SHOW_NUMBERS_SI = "Show numbers";
    private const string FROM_VALUE_SI = "Value";

    private const int ZERO = 0;
    private const int ONE = 1;
    private const int FIVE = 5;
    private const int TEN = 10;

    #endregion Consts

    #region Parameters

    private byte upValue;
    private byte downValue;

    private readonly Font defaultFont;
    private readonly Font extraFont;

    [InputParameter("Up color", 10)]
    public Color DefaultUpColor
    {
        get => this.defaultUpColor;
        set
        {
            this.defaultUpColor = value;
            this.defaultUpPen = new Pen(value);
        }
    }
    private Color defaultUpColor;
    private Pen defaultUpPen;

    [InputParameter("Down color", 20)]
    public Color DefaultDownColor
    {
        get => this.defaultDownColor;
        set
        {
            this.defaultDownColor = value;
            this.defaultDownPen = new Pen(value);
        }
    }
    private Color defaultDownColor;
    private Pen defaultDownPen;

    [InputParameter(SHOW_NUMBERS_SI, 30, variants: new object[]
    {
        "None", TDSVisualMode.None,
        "All", TDSVisualMode.All,
        "From value", TDSVisualMode.FromValue,
    })]
    public TDSVisualMode VisualMode = TDSVisualMode.All;

    [InputParameter(FROM_VALUE_SI, 40, 1, MAX_TREND_COUNTER, 1, 0)]
    public int FromValue = 8;

    private static readonly StringFormat centerCenterSF = new()
    {
        Alignment = StringAlignment.Center,
        LineAlignment = StringAlignment.Center
    };

    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorTDSequential.cs";

    #endregion Parameters

    public IndicatorTDSequential()
    {
        this.Name = "TD Sequential";

        this.defaultFont = new Font("Verdana", 10, FontStyle.Regular);
        this.extraFont = new Font("Verdana", 16, FontStyle.Bold);

        this.DefaultUpColor = Color.FromArgb(55, 219, 186); // dark green
        this.DefaultDownColor = Color.FromArgb(235, 96, 47); // dark red

        this.AddLineSeries("Up", this.DefaultUpColor, 1, LineStyle.Histogramm).Visible = false;
        this.AddLineSeries("Down", this.DefaultDownColor, 1, LineStyle.Histogramm).Visible = false;
    }

    #region Overrides

    protected override void OnInit()
    {
        this.upValue = default;
        this.downValue = default;
    }
    protected override void OnUpdate(UpdateArgs args)
    {
        if (this.Count < MIN_PERIOD)
            return;

        if (args.Reason != UpdateReason.NewBar && args.Reason != UpdateReason.HistoricalBar)
            return;

        var prevClose = this.Close(ONE);
        var prevFiveClose = this.Close(FIVE);

        //
        // up values
        //
        if (prevClose > prevFiveClose)
            this.upValue += ONE;
        else
            this.upValue = ZERO;

        if (this.IsCorrectValue(this.upValue))
            this.SetValue(this.upValue, ZERO, ONE);

        //
        // down values
        //
        if (prevClose < prevFiveClose)
            this.downValue += ONE;
        else
            this.downValue = ZERO;

        if (this.IsCorrectValue(this.downValue))
            this.SetValue(this.downValue, ONE, ONE);
    }
    public override IList<SettingItem> Settings
    {
        get
        {
            var settings = base.Settings;

            if (settings.GetItemByName(FROM_VALUE_SI) is var si)
                si.Relation = new SettingItemRelationVisibility(SHOW_NUMBERS_SI, new SelectItem("", (int)TDSVisualMode.FromValue));

            return settings;
        }
    }
    public override void OnPaintChart(PaintChartEventArgs args)
    {
        var gr = args.Graphics;
        var prevClip = gr.ClipBounds;
        var prevTextHint = gr.TextRenderingHint;

        try
        {
            gr.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            gr.SetClip(this.CurrentChart.MainWindow.ClientRectangle);

            var endUpLinePointX = double.NaN;
            var endDownLinePointX = double.NaN;
            var point = default(Point);

            // find correct right offset
            var t1 = this.CurrentChart.MainWindow.CoordinatesConverter.GetTime(this.CurrentChart.MainWindow.ClientRectangle.Right);
            var rightIndex = (int)this.CurrentChart.MainWindow.CoordinatesConverter.GetBarIndex(t1);
            var startOffset = Math.Max(this.HistoricalData.Count - rightIndex - 1, 0);


            // from right to left
            for (int i = startOffset; i < this.HistoricalData.Count - MIN_PERIOD - 1; i++)
            {
                if (this.HistoricalData[i, SeekOriginHistory.End] is not HistoryItemBar item)
                    break;

                var startBarPointX = this.CurrentChart.MainWindow.CoordinatesConverter.GetChartX(item.TimeLeft);
                var endBarPointX = startBarPointX + this.CurrentChart.BarsWidth;

                var upValue = this.GetValue(i, ZERO);
                var downValue = this.GetValue(i, ONE);

                //
                // Draw numbers. Only on visible chart area
                //
                if (endBarPointX > ZERO && endBarPointX <= this.CurrentChart.MainWindow.ClientRectangle.Right)
                {
                    if (this.IsCorrectValue(downValue))
                    {
                        var value = downValue.ToString();
                        var font = downValue != MAX_TREND_COUNTER ? this.defaultFont : this.extraFont;
                        var height = (int)gr.MeasureString(value, font).Height;

                        point.X = (int)(startBarPointX + this.CurrentChart.BarsWidth / 2);
                        point.Y = (int)this.CurrentChart.MainWindow.CoordinatesConverter.GetChartY(item.Low) + height / 2;

                        gr.DrawString(value, font, this.defaultDownPen.Brush, point, centerCenterSF);
                    }
                    else if (this.IsCorrectValue(upValue))
                    {
                        var value = upValue.ToString();
                        var font = upValue != MAX_TREND_COUNTER ? this.defaultFont : this.extraFont;
                        var height = (int)gr.MeasureString(value, font).Height;

                        point.X = (int)(startBarPointX + this.CurrentChart.BarsWidth / 2);
                        point.Y = (int)this.CurrentChart.MainWindow.CoordinatesConverter.GetChartY(item.High) - height / 2;

                        gr.DrawString(value, font, this.defaultUpPen.Brush, point, centerCenterSF);
                    }
                }

                //
                // Draw lines
                //
                if (double.IsNaN(endUpLinePointX))
                {
                    endUpLinePointX = endBarPointX;
                    endDownLinePointX = endBarPointX;
                }

                if (startBarPointX < this.CurrentChart.MainWindow.ClientRectangle.Left)
                    startBarPointX = this.CurrentChart.MainWindow.ClientRectangle.Left - TEN;

                if (endDownLinePointX > this.CurrentChart.MainWindow.ClientRectangle.Right + TEN)
                    endDownLinePointX = this.CurrentChart.MainWindow.ClientRectangle.Right + TEN;

                if (endUpLinePointX > this.CurrentChart.MainWindow.ClientRectangle.Right + TEN)
                    endUpLinePointX = this.CurrentChart.MainWindow.ClientRectangle.Right + TEN;

                if (downValue == MAX_TREND_COUNTER && endDownLinePointX > ZERO && startBarPointX < this.CurrentChart.MainWindow.ClientRectangle.Right)
                {
                    var pointY = (int)this.CurrentChart.MainWindow.CoordinatesConverter.GetChartY(item.Low);
                    gr.DrawLine(this.defaultDownPen, (int)startBarPointX, pointY, (int)endDownLinePointX, pointY);
                    endDownLinePointX = startBarPointX;
                }
                else if (upValue == MAX_TREND_COUNTER && endUpLinePointX > ZERO && startBarPointX < this.CurrentChart.MainWindow.ClientRectangle.Right)
                {
                    var pointY = (int)this.CurrentChart.MainWindow.CoordinatesConverter.GetChartY(item.High);
                    gr.DrawLine(this.defaultUpPen, (int)startBarPointX, pointY, (int)endUpLinePointX, pointY);
                    endUpLinePointX = startBarPointX;
                }

                //
                // all elements are outside.
                //
                if (endBarPointX < ZERO && endDownLinePointX < ZERO && endUpLinePointX < ZERO)
                    break;
            }
        }
        catch { }
        finally
        {
            gr.TextRenderingHint = prevTextHint;
            gr.SetClip(prevClip);
        }
    }

    #endregion Overrides

    private bool IsCorrectValue(double value)
    {
        if (double.IsNaN(value) || value <= 0 || value > MAX_TREND_COUNTER)
            return false;

        return this.VisualMode switch
        {
            TDSVisualMode.All => true,
            TDSVisualMode.None => false,
            TDSVisualMode.FromValue => value >= this.FromValue,

            _ => true,
        };
    }

    public enum TDSVisualMode { All, None, FromValue }
}