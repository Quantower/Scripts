// Copyright QUANTOWER LLC. © 2017-2022. All rights reserved.

using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace Oscillators
{
    /// <summary>
    /// Kairi Relative Index (KRI) calculates deviation of the current price from its simple moving average as a percent of the moving average.
    /// </summary>
    public sealed class IndicatorKairiRelativeIndex : Indicator, IWatchlistIndicator
    {
        // Displays Input Parameter as input field (or checkbox if value type is bolean).
        [InputParameter("Period", 0, 1, 999, 1, 0)]
        public int Period = 14;

        public int MinHistoryDepths => this.Period;
        public override string ShortName => "KRI (" + this.Period + ")";

        // Holds simple moving average values.
        private Indicator sma;

        /// <summary>
        /// Indicator's constructor. Contains general information: name, description, LineSeries etc. 
        /// </summary>
        public IndicatorKairiRelativeIndex()
            : base()
        {
            // Defines indicator's group, name and description.
            this.Name = "Kairi Relative Index";
            this.Description = "Kairi Relative Index (KRI) calculates deviation of the current price from its simple moving average as a percent of the moving average";

            // Defines line and level on demand with particular parameters.
            this.AddLineSeries("KRI'Line", Color.Red, 1, LineStyle.Solid);
            this.AddLineLevel(0, "0'Line", Color.Gray, 1, LineStyle.Solid);

            this.SeparateWindow = true;
        }

        /// <summary>
        /// This function will be called after creating an indicator as well as after its input params reset or chart (symbol or timeframe) updates.
        /// </summary>
        protected override void OnInit()
        {
            // Creates an instance of the proper indicator (SMA) from the default indicators list.
            this.sma = Core.Indicators.BuiltIn.SMA(this.Period, PriceType.Close);
            // Adds an auxiliary (SMA) indicator to the current one (KRI). 
            // This will let inner indicator (SMA) to be calculated in advance to the current one (KRI).
            this.AddIndicator(this.sma);
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
            // Checking, if current amount of bars less, than period of moving average - calculation is impossible.
            if (this.Count < this.Period)
                return;

            double val = this.sma.GetValue();
            this.SetValue((this.GetPrice(PriceType.Close) - val) / val * 100);
        }
    }
}