// Copyright QUANTOWER LLC. © 2017-2022. All rights reserved.

using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace Oscillators
{
    public sealed class IndicatorAroon : Indicator, IWatchlistIndicator
    {
        // Displays Input Parameter as input field (or checkbox if value type is bolean).
        [InputParameter("AROON Period", 0, 1, 999, 1, 0)]
        public int Period = 14;

        public int MinHistoryDepths => this.Period;
        public override string ShortName => "AROON (" + this.Period + ")";

        public override string HelpLink => "https://help.quantower.com/analytics-panels/chart/technical-indicators/oscillators/aroon-indicator";

        /// <summary>
        /// Indicator's constructor. Contains general information: name, description, LineSeries etc. 
        /// </summary>
        public IndicatorAroon()
            : base()
        {
            // Serves for an identification of related indicators with different parameters.
            this.Name = "Aroon";
            this.Description = "Reveals the beginning of a new trend";

            // Defines line on demand with particular parameters.
            this.AddLineSeries("Aroon up Line", Color.Blue, 1, LineStyle.Solid);
            this.AddLineSeries("Aroon down Line", Color.Blue, 1, LineStyle.Dot);
            this.AddLineLevel(70, "Upper Limit", Color.Red, 1, LineStyle.Solid);
            this.AddLineLevel(30, "Lower Limit", Color.Red, 1, LineStyle.Dot);

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
            if (this.Count < this.MinHistoryDepths)
                return;

            // Getting max and min prices for period
            double high = this.GetPrice(PriceType.High);
            double low = this.GetPrice(PriceType.Low);
            double perHigh = 0;
            double perLow = 0;
            for (int i = 1; i < this.Period; i++)
            {
                double price = this.GetPrice(PriceType.High, i);
                if (price > high)
                {
                    high = price;
                    perHigh = i;
                }

                price = this.GetPrice(PriceType.Low, i);
                if (price < low)
                {
                    low = price;
                    perLow = i;
                }
            }
            // Getting Aroon up and down lines
            this.SetValue((1.0 - perHigh / this.Period) * 100, 0);
            this.SetValue((1.0 - perLow / this.Period) * 100, 1);
        }
    }
}