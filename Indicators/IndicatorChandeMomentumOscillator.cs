// Copyright QUANTOWER LLC. © 2017-2022. All rights reserved.

using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace Volatility
{
    public sealed class IndicatorChandeMomentumOscillator : Indicator, IWatchlistIndicator
    {
        // Displays Input Parameter as input field.
        [InputParameter("Period of MA for envelopes", 0, 1, 999, 1, 0)]
        public int Period = 9;

        // Displays Input Parameter as dropdown list.
        [InputParameter("Sources prices for MA", 1, variants: new object[] {
             "Close", PriceType.Close,
             "Open", PriceType.Open,
             "High", PriceType.High,
             "Low", PriceType.Low,
             "Typical", PriceType.Typical,
             "Medium", PriceType.Median,
             "Weighted", PriceType.Weighted,
             "Volume", PriceType.Volume,
             "Open interest", PriceType.OpenInterest
        })]
        public PriceType SourcePrice = PriceType.Close;

        public int MinHistoryDepths => this.Period + 1;
        public override string ShortName => $"CMO ({this.Period}: {this.SourcePrice})";

        /// <summary>
        /// Indicator's constructor. Contains general information: name, description, LineSeries etc. 
        /// </summary>
        public IndicatorChandeMomentumOscillator()
            : base()
        {
            // Defines indicator's name and description.
            this.Name = "Chande Momentum Oscillator";
            this.Description = "Calculates the dividing of difference between the sum of all recent gains and the sum of all recent losses by the sum of all price movement over the period.";

            // Defines line on demand with particular parameters.
            this.AddLineSeries("CMO", Color.Purple, 2, LineStyle.Solid);
            this.AddLineLevel(50, "Up", Color.Blue, 1, LineStyle.Dash);
            this.AddLineLevel(-50, "Down", Color.Blue, 1, LineStyle.Dash);
            this.SeparateWindow = true;
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
            // Skip some period for correct calculation.  
            if (this.Count < this.MinHistoryDepths)
                return;

            // Calculate the sum 
            double sum1 = 0d;
            double sum2 = 0d;
            for (int i = 0; i < this.Period; i++)
            {
                double diff = this.GetPrice(this.SourcePrice, i) - this.GetPrice(this.SourcePrice, i + 1);
                if (diff > 0)
                    sum1 += diff;
                else
                    sum2 -= diff;
            }

            // Compute the cmo value and set its to the 'CMO' line. 
            this.SetValue(100.0 * ((sum1 - sum2) / (sum1 + sum2)));
        }
    }
}