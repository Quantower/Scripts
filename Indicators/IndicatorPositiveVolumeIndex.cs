// Copyright QUANTOWER LLC. © 2017-2022. All rights reserved.

using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace Volume
{
    /// <summary>
    /// Changes on the periods in which value of volume has increased in comparison with the previous period.
    /// </summary>
    public sealed class IndicatorPositiveVolumeIndex : Indicator, IWatchlistIndicator
    {
        private const int MIN_PERIOD = 1;

        // Displays Input Parameter as dropdown list.
        [InputParameter("Source price", 0, variants: new object[] {
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

        public int MinHistoryDepths => MIN_PERIOD;
        public override string ShortName => $"PVI ({this.SourcePrice})";
        public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorPositiveVolumeIndex.cs";

        // Keeps previous volume, price and PVI values.
        private double prevVolume = 0;
        private double prevPrice = 0;
        private double prevPVI = 1;

        /// <summary>
        /// Indicator's constructor. Contains general information: name, description, LineSeries etc. 
        /// </summary>
        public IndicatorPositiveVolumeIndex()
            : base()
        {
            // Defines indicator's group, name and description.            
            this.Name = "Positive Volume Index";
            this.Description = "Changes on the periods in which value of volume has increased in comparison with the previous period.";

            // Defines line on demand with particular parameters.
            this.AddLineSeries("PVI'Line", Color.Blue, 1, LineStyle.Solid);

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

            double volume = this.Volume();
            // If symbol type or chart aggregation doesn't provide volumes - use ticks value.
            double curVolume = (double.IsNaN(volume) || volume == 0) ? this.Ticks() : volume;

            double curPrice = this.GetPrice(this.SourcePrice, 0);
            double curPVI = (curVolume > this.prevVolume && this.prevPrice != 0) ? this.prevPVI + this.prevPVI * (curPrice - this.prevPrice) / this.prevPrice : this.prevPVI;

            this.SetValue(curPVI);

            // Save state.
            if (args.Reason != UpdateReason.NewTick)
            {
                this.prevVolume = curVolume;
                this.prevPrice = curPrice;
                this.prevPVI = curPVI;
            }
        }
    }
}