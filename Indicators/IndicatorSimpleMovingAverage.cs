// Copyright QUANTOWER LLC. © 2017-2022. All rights reserved.

using TradingPlatform.BusinessLayer;
using System.Drawing;

namespace MovingAverages
{
    public sealed class IndicatorSimpleMovingAverage : Indicator, IWatchlistIndicator
    {
        // Displays Input Parameter as input field.
        [InputParameter("Period of Simple Moving Average", 0, 1, 9999, 1, 1)]
        public int Period = 20;

        // Displays Input Parameter as dropdown list.
        [InputParameter("Sources prices for MA", 1, variants: new object[]{
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

        /// <summary>
        /// Indicator's constructor. Contains general information: name, description, LineSeries etc. 
        /// </summary>
        public IndicatorSimpleMovingAverage()
            : base()
        {
            // Defines indicator's name and description.
            this.Name = "Simple Moving Average";
            this.Description = "Average price for the last N periods";

            // Define one line with particular parameters 
            this.AddLineSeries("SMA", Color.Red, 1, LineStyle.Solid);

            this.SeparateWindow = false;
        }

        public int MinHistoryDepths => this.Period;
        public override string ShortName => $"SMA ({this.Period}:{this.SourcePrice})";

        /// <summary>
        /// Calculation entry point. This function is called when a price data updates. 
        /// Will be runing under the HistoricalBar mode during history loading. 
        /// Under NewTick during realtime. 
        /// Under NewBar if start of the new bar is required.
        /// </summary>
        /// <param name="args">Provides data of updating reason and incoming price.</param>
        protected override void OnUpdate(UpdateArgs args)
        {
            // Checking, if current amount of bars
            // more, than period of moving average. If it is
            // then the calculation is possible
            if (this.Count < this.MinHistoryDepths)
                return;

            double sum = 0.0; // Sum of prices
            for (int i = 0; i < this.Period; i++)
                // Adding bar's price to the sum
                sum += this.GetPrice(this.SourcePrice, i);

            // Set value to the "SMA" line buffer.
            this.SetValue(sum / this.Period);
        }
    }
}