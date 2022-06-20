// Copyright QUANTOWER LLC. © 2017-2022. All rights reserved.

using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace MovingAverages
{
    /// <summary>
    /// The weighted price calculation for the last N periods.
    /// </summary>
    public sealed class IndicatorExponentialMovingAverage : Indicator, IWatchlistIndicator
    {
        #region Parameters

        // Period of moving average. 
        [InputParameter("Period of Exponential Moving Average", 10, 1, 9999, 1, 0)]
        public int MaPeriod = 9;

        // Price type of moving average. 
        [InputParameter("Sources prices for MA", 20, variants: new object[]
        {
            "Close", PriceType.Close,
            "Open", PriceType.Open,
            "High", PriceType.High,
            "Low", PriceType.Low,
            "Typical", PriceType.Typical,
            "Median", PriceType.Median,
            "Weighted", PriceType.Weighted,
            "Volume", PriceType.Volume,
            "Open interest", PriceType.OpenInterest
        })]
        public PriceType SourcePrice = PriceType.Close;

        //
        [InputParameter("Calculation type", 30, variants: new object[]
        {
            "All available data", IndicatorCalculationType.AllAvailableData,
            "By period", IndicatorCalculationType.ByPeriod,
        })]
        public IndicatorCalculationType CalculationType = Indicator.DEFAULT_CALCULATION_TYPE;

        #endregion

        // EMA's calculation coefficient
        private double k;

        /// <summary>
        /// Indicator's constructor. Contains general information: name, description, LineSeries etc. 
        /// </summary>
        public IndicatorExponentialMovingAverage()
            : base()
        {
            // Defines indicator's group, name and description.            
            this.Name = "Exponential Moving Average";
            this.Description = "The weighted price calculation for the last N periods";

            // Defines line on demand with particular parameters.
            this.AddLineSeries("EMA", Color.DodgerBlue, 1, LineStyle.Solid);

            this.SeparateWindow = false;
        }

        public int MinHistoryDepths => this.CalculationType switch
        {
            IndicatorCalculationType.AllAvailableData => this.MaPeriod,
            _ => this.MaPeriod * 2
        };
        public override string ShortName => $"EMA ({this.MaPeriod}: {this.SourcePrice})";

        /// <summary>
        /// This function will be called after creating an indicator as well as after its input params reset or chart (symbol or timeframe) updates.
        /// </summary>
        protected override void OnInit()
        {
            // Calculation of a coefficient.
            this.k = 2.0 / (this.MaPeriod + 1);
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
            if (this.CalculationType == IndicatorCalculationType.ByPeriod)
                this.CalculateByPeriod();
            else
                this.CalcualteForAllData();
        }

        private void CalcualteForAllData(int offset = 0)
        {
            if (this.Count <= offset + 1)
                return;

            int prevOffset = offset + 1;

            // Getting previous EMA and display value. If it's NaN (start calculation) then get current close price (by default).
            double prevEMA = double.IsNaN(this.GetValue(prevOffset))
                ? this.GetPrice(this.SourcePrice, prevOffset)
                : this.GetValue(prevOffset);

            double ema = this.CalculateEMA(offset, prevEMA);

            if (this.Count <= this.MaPeriod)
                return;

            this.SetValue(ema);
        }
        private void CalculateByPeriod(int offset = 0)
        {
            int startOffset = offset + this.MaPeriod;

            if (this.Count <= startOffset + this.MaPeriod)
                return;

            // calcualte start value
            double ema = this.CalculateSMA(startOffset);

            for (int i = startOffset - 1; i >= offset; i--)
                ema = this.CalculateEMA(i, ema);

            this.SetValue(ema, 0, offset);
        }

        private double CalculateEMA(int offset, double prevEMA)
        {
            // Getting current price.
            double price = this.GetPrice(this.SourcePrice, offset);

            // Sets value for displaying on the chart.
            return prevEMA + this.k * (price - prevEMA);
        }
        private double CalculateSMA(int offset)
        {
            double sum = 0d;
            for (int i = 0; i < this.MaPeriod; i++)
                sum += this.GetPrice(this.SourcePrice, offset + i);

            return sum / this.MaPeriod;
        }
    }
}