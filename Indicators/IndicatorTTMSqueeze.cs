// Copyright QUANTOWER LLC. © 2017-2024. All rights reserved.

using System;
using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace IndicatorTTMSqueeze
{
    public class IndicatorTTMSqueeze : Indicator
    {
        [InputParameter("BB Period", 0, 1, 999)]
        public int bbPeriod = 20;
        [InputParameter("BB Coefficient", 0, 1, 999)]
        public double bbCoefficient = 2d;
        [InputParameter("BB Price type", 0, variants: new object[]{
            "Close", PriceType.Close,
            "Open", PriceType.Open,
            "High", PriceType.High,
            "Low", PriceType.Low,
            "Typical", PriceType.Typical,
            "Median", PriceType.Median,
            "Weighted", PriceType.Weighted,
            "Base asset volume", PriceType.Volume,
            "Quote asset volume", PriceType.QuoteAssetVolume,
            "Open interest", PriceType.OpenInterest
        })]
        public PriceType bbPriceType = PriceType.Close;

        [InputParameter("BB MA Type", 0, variants: new object[]{
            "Simple Moving Average", MaMode.SMA,
            "Exponential Moving Average", MaMode.EMA,
            "Smoothed Moving Average", MaMode.SMMA,
            "Linearly Weighted Moving Average", MaMode.LWMA,
        })]
        public MaMode bbMaMode = MaMode.SMA;

        [InputParameter("Keltner Period", 0, 1, 999)]
        public int kPeriod = 20;
        [InputParameter("Keltner Coefficient", 0, 1, 999)]
        public double kOffset = 2d;
        [InputParameter("Keltner Price type", 0, variants: new object[]{
            "Close", PriceType.Close,
            "Open", PriceType.Open,
            "High", PriceType.High,
            "Low", PriceType.Low,
            "Typical", PriceType.Typical,
            "Median", PriceType.Median,
            "Weighted", PriceType.Weighted,
            "Base asset volume", PriceType.Volume,
            "Quote asset volume", PriceType.QuoteAssetVolume,
            "Open interest", PriceType.OpenInterest
        })]
        public PriceType kPriceType = PriceType.Close;

        [InputParameter("Keltner MA Type", 0, variants: new object[]{
            "Simple Moving Average", MaMode.SMA,
            "Exponential Moving Average", MaMode.EMA,
            "Smoothed Moving Average", MaMode.SMMA,
            "Linearly Weighted Moving Average", MaMode.LWMA,
        })]
        public MaMode kMaMode = MaMode.SMA;

        [InputParameter("Compression Color")]
        public Color compressionColor = Color.Red;
        [InputParameter("No Compression Color")]
        public Color noCompressionColor = Color.Green;
        [InputParameter("Compression Markers Size", 0, 1, 999)]
        public int compressSize = 10;

        [InputParameter("Positive ascending Color")]
        public Color positiveAscendColor = Color.FromArgb(0, 0, 255);
        [InputParameter("Positive downward Color")]
        public Color positiveDownwColor = Color.FromArgb(115, 115, 255);
        [InputParameter("Negative ascending Color")]
        public Color negativeAscendColor = Color.FromArgb(255, 115, 115);
        [InputParameter("Negative downward Color")]
        public Color negativeDownColor = Color.FromArgb(255, 0, 0);

        [InputParameter("Pulse Period", 0, 1, 999)]
        public int pulsePeriod = 20;
        [InputParameter("Pulse Smoothing Period", 0, 1, 999)]
        public int pulseSmoothingPeriod = 20;

        private Indicator BB, Keltner, pulseMA, regression;
        private HistoricalDataCustom SmoothingSource;
        public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorTTMSqueeze.cs";
        public IndicatorTTMSqueeze()
        {
            this.Name = "TTM Squeeze";
            this.AddLineSeries("Pulse", Color.CadetBlue, 1, LineStyle.Histogramm);
            this.SeparateWindow = true;
        }

        protected override void OnInit()
        {
            this.BB = Core.Instance.Indicators.BuiltIn.BB(this.bbPeriod, this.bbCoefficient, this.bbPriceType, this.bbMaMode);
            this.Keltner = Core.Instance.Indicators.BuiltIn.Keltner(this.kPeriod, this.kOffset, this.kPriceType, this.kMaMode);
            this.pulseMA = Core.Instance.Indicators.BuiltIn.MA(this.pulsePeriod, PriceType.Close, MaMode.SMA);
            this.regression = Core.Instance.Indicators.BuiltIn.Regression(this.pulsePeriod, PriceType.Open);

            this.HistoricalData.AddIndicator(this.BB);
            this.HistoricalData.AddIndicator(this.Keltner);
            this.HistoricalData.AddIndicator(this.pulseMA);

            this.SmoothingSource = new HistoricalDataCustom(this);
            this.SmoothingSource.AddIndicator(this.regression);
        }

        protected override void OnUpdate(UpdateArgs args)
        {
            if (this.Count < Math.Max(Math.Max(this.bbPeriod, this.kPeriod), this.pulsePeriod))
                return;

            double max = this.High();
            double min = this.Low();
            for (int i = 0; i < this.pulsePeriod; i++)
            {
                double high = this.High(i);
                double low = this.Low(i);
                if (high > max) max = high;
                if (low < min) min = low;
            }

            double midline = (max + min) / 2.0;

            double delta = this.Close() - (midline + this.pulseMA.GetValue(0)) / 2.0;
            this.SmoothingSource.SetValue(delta, 0, 0, 0);

            this.SetValue(this.regression.GetValue());

            double currValue = this.GetValue(0);
            double prevValue = this.GetValue(1);

            var lineMarker = currValue > 0
                ? new IndicatorLineMarker(this.noCompressionColor, upperIcon: IndicatorLineMarkerIconType.None)
                : new IndicatorLineMarker(this.noCompressionColor, bottomIcon: IndicatorLineMarkerIconType.None);

            if (currValue > 0)
                lineMarker.Color = currValue > prevValue ? this.positiveAscendColor : this.positiveDownwColor;
            else
                lineMarker.Color = currValue > prevValue ? this.negativeAscendColor : this.negativeDownColor;

            this.LinesSeries[0].SetMarker(0, lineMarker);
        }

        public override void OnPaintChart(PaintChartEventArgs args)
        {
            base.OnPaintChart(args);

            if (this.CurrentChart == null)
                return;

            var gr = args.Graphics;
            var currWindow = this.CurrentChart.Windows[args.WindowIndex];
            RectangleF prevClipRectangle = gr.ClipBounds;
            gr.SetClip(args.Rectangle);
            try
            {
                int leftIndex = (int)currWindow.CoordinatesConverter.GetBarIndex(currWindow.CoordinatesConverter.GetTime(0));
                int rightIndex = (int)currWindow.CoordinatesConverter.GetBarIndex(currWindow.CoordinatesConverter.GetTime(args.Rectangle.Width));

                if (leftIndex < 0) leftIndex = 0;
                if (rightIndex >= this.HistoricalData.Count) rightIndex = this.HistoricalData.Count - 1;

                double bbHigh, bbLow, kHigh, kLow;
                Brush markerBrush;
                int barWidth = this.CurrentChart.BarsWidth;
                RectangleF markerRectangle = new RectangleF(0, 0, this.compressSize, this.compressSize);

                for (int i = leftIndex; i <= rightIndex; i++)
                {
                    bbHigh = this.BB.GetValue(this.HistoricalData.Count - i, 0);
                    bbLow = this.BB.GetValue(this.HistoricalData.Count - i, 2);
                    kHigh = this.Keltner.GetValue(this.HistoricalData.Count - i, 1);
                    kLow = this.Keltner.GetValue(this.HistoricalData.Count - i, 2);

                    markerRectangle.Y = (float)currWindow.CoordinatesConverter.GetChartY(0) - this.compressSize / 2;
                    markerRectangle.X = (float)currWindow.CoordinatesConverter.GetChartX(this.HistoricalData[i, SeekOriginHistory.Begin].TimeLeft) + barWidth / 2 - this.compressSize / 2;

                    markerBrush = new SolidBrush(bbHigh < kHigh && bbLow > kLow ? this.compressionColor : this.noCompressionColor);
                    gr.FillEllipse(markerBrush, markerRectangle);
                }
            }
            finally
            {
                gr.SetClip(prevClipRectangle);
            }
        }
    }
}