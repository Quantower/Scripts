// Copyright QUANTOWER LLC. © 2017-2022. All rights reserved.

using System;
using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace MovingAverages
{
    /// <summary>
    /// KAMA is an exponential style average with a smoothing that varies according to recent data.
    /// </summary>
    public sealed class IndicatorKaufmanAdaptiveMovingAverage : Indicator
    {
        // Defines initial parameters.
        [InputParameter("Period", 0, 1, 9999)]
        public int periodAMA = 10;

        [InputParameter("Fast EMA", 1, 1, 500, 1, 0)]
        public double nfast = 2.0;

        [InputParameter("Slow EMA", 2, 1, 500, 1, 0)]
        public double nslow = 30.0;

        //[InputParameter("G", 3, 1, 10, 0.1)]
        public double G = 2.0;

        //[InputParameter("dK", 4, 1, 10, 0.1)]
        public double dK = 2.0;

        // Displays Input Parameter as dropdown list.
        [InputParameter("Sources prices for MA", 5, variants: new object[] {
             "Close", PriceType.Close,
             "Open", PriceType.Open,
             "High", PriceType.High,
             "Low", PriceType.Low,
             "Typical", PriceType.Typical,
             "Median", PriceType.Median,
             "Weighted", PriceType.Weighted}
        )]
        public PriceType SourcePrice = PriceType.Close;

        // Displays Input Parameter as dropdown list.
        //[InputParameter("AMA Trend Type", 6, variants: new object[] {
        //       "Fixed", AMATrendType.Fixed,
        //     "Average", AMATrendType.Average }
        //)]
        public AMATrendType AMA_Trend_Type;

        //[InputParameter("Up Trend Marker", 7, 1)]
        //public Color upTrendColor = Color.Blue;

        //[InputParameter("Down Trend Marker", 8, 1)]
        //public Color downTrendColor = Color.Red;

        public override string ShortName => $"KAMA ({this.periodAMA}: {this.nfast}: {this.nslow})";

        private HistoricalDataCustom customHDabsDiff;
        private HistoricalDataCustom customHDama;
        private Indicator ma;
        private Indicator stdDev;
        private double slowSC, fastSC, dFS;

        /// <summary>
        /// Indicator's constructor. Contains general information: name, description, LineSeries etc. 
        /// </summary>
        public IndicatorKaufmanAdaptiveMovingAverage()
            : base()
        {
            // Defines indicator's group, name and description.            
            this.Name = "Kaufman Adaptive Moving Average";
            this.Description = "KAMA is an exponential style average with a smoothing that varies according to recent data.";

            // Defines line on demand with particular parameters.
            this.AddLineSeries("KAMA'Line", Color.Orange, 2, LineStyle.Solid);
            //AddLineSeries("Up'Line", Color.Gray, 1, LineStyle.Solid);
            //AddLineSeries("Down'Line", Color.Gray, 1, LineStyle.Solid);
        }

        /// <summary>
        /// This function will be called after creating an indicator as well as after its input params reset or chart (symbol or timeframe) updates.
        /// </summary>
        protected override void OnInit()
        {
            // Creates an instances of the custom historical data which will be syncronized by the current indicator instance.
            this.customHDabsDiff = new HistoricalDataCustom(this);
            this.customHDama = new HistoricalDataCustom(this);

            // Creates a smoothing indicator which will keep smoothed custom data.
            this.ma = Core.Indicators.BuiltIn.SMA(this.periodAMA, PriceType.Close);

            // Adds the smoothing indicator to the custom historical data.
            this.customHDabsDiff.AddIndicator(this.ma);

            // Creates an instance of the SD indicator in case when inticator has initialized with Average AMATrendType. 
            if (this.AMA_Trend_Type == AMATrendType.Average)
            {
                // Creates an indicator which will keep custom data.
                this.stdDev = Core.Indicators.BuiltIn.SD(this.periodAMA, PriceType.Close, MaMode.SMA);
                // Adds the indicator to the custom historical data.
                this.customHDama.AddIndicator(this.stdDev);
            }

            // Calculates initial parameters.
            this.slowSC = 2.0 / (this.nslow + 1);
            this.fastSC = 2.0 / (this.nfast + 1);
            this.dFS = this.fastSC - this.slowSC;
        }

        /// <summary>
        /// Calculation entry point. This function is called when a price data updates. 
        /// Will be runing under the HistoricalBar mode during history loading. 
        /// Under NewTick during realtime. 
        /// Under NewBar if start of the new bar is required.
        /// </summary>
        /// <param name="args">Provides data of updating reason and incoming price.</param>
        protected override void OnUpdate(UpdateArgs args)
        {
            double price = this.GetPrice(this.SourcePrice);

            // Populates custom HistoricalData objects. 
            // For Abs(currentPrice - prevPrice)
            this.customHDabsDiff[PriceType.Close] = Math.Abs((this.Count > 1) ? price - this.GetPrice(this.SourcePrice, 1) : 0);
            // For AMA (given indicator's base line).
            this.customHDama[PriceType.Close] = price;

            // Skips calculations for the correct results.
            if (this.Count <= this.periodAMA)
                return;

            double maValue = this.ma.GetValue();
            double prevAma = this.customHDama[PriceType.Close, 1];

            double Noise = maValue * this.periodAMA;
            double ER = (Noise != 0) ? Math.Abs(price - this.GetPrice(this.SourcePrice, this.periodAMA)) / Noise : 0;
            double SSC = Math.Min(Math.Pow(ER * this.dFS + this.slowSC, this.G), 1);

            // Calculates current AMA.
            double ama = price * SSC + prevAma * (1 - SSC);

            // Sets given value into custom historical data and for displaying on the chart. 
            this.customHDama[PriceType.Close] = ama;
            this.SetValue(ama);

            // Calculates level.
            //double level = dK * ((AMA_Trend_Type == AMATrendType.Fixed) ? Symbol.GetTickSize(price) : stdDev.GetValue());

            //SetValue(ama + level, 1);
            //SetValue(ama - level, 2);

            // KAMA markers setting block.
            //if (Math.Abs(ama - prevAma) > level)
            //{
            //    if (ama - prevAma > 0)
            //    {
            //        // Uptrend markers realization.
            //        // Sets color.
            //        LinesSeries[0].Color = upTrendColor;
            //    }
            //    else
            //    {
            //        // Downtrend markers realization.
            //        // Sets color.
            //        LinesSeries[0].Color = downTrendColor;
            //    }
            //}
        }
    }
}