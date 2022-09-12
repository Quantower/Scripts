// Copyright QUANTOWER LLC. © 2017-2022. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace TrendIndicators
{
    public class IndicatorTDSequential : Indicator
    {
        #region Parameters

        public const byte TDS_NAN = byte.MaxValue;
        private const int MIN_PERIOD = 6;
        private const int MAX_TREND_COUNTER = 9;
        private const string SHOW_NUMBERS_SI = "Show numbers";
        private const string FROM_VALUE_SI = "Value";

        public IList<byte> UpValueBuffer { get; private set; }
        public IList<byte> DownValueBuffer { get; private set; }

        private byte upValue;
        private byte downValue;
        private Point downCenterPoint;
        private Point upCenterPoint;

        private readonly Font defaultFont;
        private readonly Font extraFont;

        [InputParameter(SHOW_NUMBERS_SI, 10, variants: new object[]
        {
            "None", TDSVisualMode.None,
            "All", TDSVisualMode.All,
            "From value", TDSVisualMode.FromValue,
        })]
        public TDSVisualMode VisualMode = TDSVisualMode.All;

        [InputParameter(FROM_VALUE_SI, 20, 1, 9, 1, 0)]
        public int FromValue = 8;

        [InputParameter("Up color")]
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

        [InputParameter("Down color")]
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

        private readonly StringFormat centerCenterSF;

        public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorTDSequential.cs";

        #endregion Parameters

        public IndicatorTDSequential()
        {
            this.Name = "TD Sequential";

            this.defaultFont = new Font("Verdana", 10, FontStyle.Regular);
            this.extraFont = new Font("Verdana", 16, FontStyle.Bold);

            this.DefaultUpColor = Color.FromArgb(55, 219, 186); // dark green
            this.DefaultDownColor = Color.FromArgb(235, 96, 47); // dark red

            this.centerCenterSF = new StringFormat() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        }

        #region Overrides

        protected override void OnInit()
        {
            this.UpValueBuffer = new List<byte>()
            {
                new byte()
            };
            this.DownValueBuffer = new List<byte>()
            {
                new byte()
            };
        }
        protected override void OnUpdate(UpdateArgs args)
        {
            if (this.Count < MIN_PERIOD)
                return;

            if (args.Reason == UpdateReason.NewBar || args.Reason == UpdateReason.HistoricalBar)
            {
                this.UpValueBuffer.Insert(0, TDS_NAN);
                this.DownValueBuffer.Insert(0, TDS_NAN);

                // up values
                if (this.Close(1) > this.Close(5))
                    this.upValue += 1;
                else
                    this.upValue = 0;

                if (this.upValue > 0 && this.upValue < 10)
                    this.UpValueBuffer[1] = this.upValue;

                // down values
                if (this.Close(1) < this.Close(5))
                    this.downValue += 1;
                else
                    this.downValue = 0;

                if (this.downValue > 0 && this.downValue < 10)
                    this.DownValueBuffer[1] = this.downValue;
            }
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
            gr.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

            gr.SetClip(this.CurrentChart.MainWindow.ClientRectangle);

            var endUpLinePointX = double.NaN;
            var endDownLinePointX = double.NaN;

            // find correct right offset
            var t1 = this.CurrentChart.MainWindow.CoordinatesConverter.GetTime(args.Rectangle.Right);
            var rightIndex = (int)this.CurrentChart.MainWindow.CoordinatesConverter.GetBarIndex(t1);
            var startOffset = Math.Max(this.HistoricalData.Count - rightIndex - 1, 0);

            // from right to left
            for (int i = startOffset; i < this.HistoricalData.Count - MIN_PERIOD - 1; i++)
            {
                if (this.HistoricalData[i] is not HistoryItemBar item)
                    break;

                var startBarPointX = this.CurrentChart.MainWindow.CoordinatesConverter.GetChartX(item.TimeLeft);
                var endBarPointX = this.CurrentChart.MainWindow.CoordinatesConverter.GetChartX(item.TimeRight);

                //
                // Draw numbers. Only on visible chart area
                //
                if (endBarPointX > 0 && endBarPointX <= this.CurrentChart.MainWindow.ClientRectangle.Right)
                {
                    if (this.IsCorrectValue(this.DownValueBuffer[i]))
                    {
                        var value = this.DownValueBuffer[i].ToString();
                        var font = this.DownValueBuffer[i] != MAX_TREND_COUNTER ? this.defaultFont : this.extraFont;
                        var height = (int)gr.MeasureString(value, font).Height;

                        this.downCenterPoint.X = (int)(startBarPointX + this.CurrentChart.BarsWidth / 2);
                        this.downCenterPoint.Y = (int)this.CurrentChart.MainWindow.CoordinatesConverter.GetChartY(item.Low) + height / 2;

                        gr.DrawString(value, font, this.defaultDownPen.Brush, this.downCenterPoint, this.centerCenterSF);
                    }
                    else if (this.IsCorrectValue(this.UpValueBuffer[i]))
                    {
                        var value = this.UpValueBuffer[i].ToString();
                        var font = this.UpValueBuffer[i] != MAX_TREND_COUNTER ? this.defaultFont : this.extraFont;
                        var height = (int)gr.MeasureString(value, font).Height;

                        this.upCenterPoint.X = (int)(startBarPointX + this.CurrentChart.BarsWidth / 2);
                        this.upCenterPoint.Y = (int)this.CurrentChart.MainWindow.CoordinatesConverter.GetChartY(item.High) - height / 2;

                        gr.DrawString(value, font, this.defaultUpPen.Brush, this.upCenterPoint, this.centerCenterSF);
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

                if (this.DownValueBuffer[i] == MAX_TREND_COUNTER && endDownLinePointX > 0 && startBarPointX < this.CurrentChart.MainWindow.ClientRectangle.Right)
                {
                    var pointY = (int)this.CurrentChart.MainWindow.CoordinatesConverter.GetChartY(item.Low);
                    gr.DrawLine(this.defaultDownPen, (int)startBarPointX, pointY, (int)endDownLinePointX, pointY);
                    endDownLinePointX = endBarPointX;
                }
                else if (this.UpValueBuffer[i] == MAX_TREND_COUNTER && endUpLinePointX > 0 && startBarPointX < this.CurrentChart.MainWindow.ClientRectangle.Right)
                {
                    var pointY = (int)this.CurrentChart.MainWindow.CoordinatesConverter.GetChartY(item.High);
                    gr.DrawLine(this.defaultUpPen, (int)startBarPointX, pointY, (int)endUpLinePointX, pointY);
                    endUpLinePointX = endBarPointX;
                }

                // all elements are outside.
                if (endBarPointX < 0 && endDownLinePointX < 0 && endUpLinePointX < 0)
                    break;
            }

            gr.ResetClip();
        }

        private bool IsCorrectValue(byte value)
        {
            if (value == TDS_NAN || value <= 0)
                return false;

            switch (this.VisualMode)
            {
                case TDSVisualMode.All:
                    return true;
                case TDSVisualMode.None:
                    return false;
                case TDSVisualMode.FromValue:
                    return value >= this.FromValue;
            }

            return true;
        }

        #endregion Overrides

        public enum TDSVisualMode { All, None, FromValue }
    }
}