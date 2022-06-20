// Copyright QUANTOWER LLC. © 2017-2022. All rights reserved.

using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace Oscillators
{
    public sealed class IndicatorMomentum : Indicator, IWatchlistIndicator
    {
        // Displays Input Parameter as input field (or checkbox if value type is bolean).
        [InputParameter("Period for Momentum", 0, 1, 999, 1, 0)]
        public int Period = 10;

        // Displays Input Parameter as dropdown list.
        [InputParameter("Sources prices for Momentum", 1, variants: new object[] {
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
        public override string ShortName => $"Momentum ({this.Period}: {this.SourcePrice})";

        public override string HelpLink => "https://help.quantower.com/analytics-panels/chart/technical-indicators/oscillators/momentum";

        /// <summary>
        /// Indicator's constructor. Contains general information: name, description, LineSeries etc. 
        /// </summary>
        public IndicatorMomentum()
            : base()
        {
            // Serves for an identification of related indicators with different parameters.
            this.Name = "Momentum";
            this.Description = "Momentum compares where the current price is in relation to where the price was in the past.";

            // Defines line on demand with particular parameters.
            this.AddLineSeries("Momentum Line", Color.Blue, 1, LineStyle.Solid);
            this.AddLineLevel(0, "Threshold Line", Color.Gray, 1, LineStyle.Dot);

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

            this.SetValue(this.GetPrice(this.SourcePrice) - this.GetPrice(this.SourcePrice, this.Period));
        }
    }
}