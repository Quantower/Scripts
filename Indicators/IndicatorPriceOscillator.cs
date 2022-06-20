// Copyright QUANTOWER LLC. © 2017-2022. All rights reserved.

using System.Drawing;
using System.Linq;
using TradingPlatform.BusinessLayer;

namespace Oscillators
{
    /// <summary>
    /// Calculates the variation between price moving averages.
    /// </summary>
    public sealed class IndicatorPriceOscillator : Indicator, IWatchlistIndicator
    {
        // Displays Input Parameter as dropdown list.
        [InputParameter("Type of Moving Average", 0, variants: new object[] {
            "Simple", MaMode.SMA,
            "Exponential", MaMode.EMA,
            "Modified", MaMode.SMMA,
            "Linear Weighted", MaMode.LWMA}
        )]
        public MaMode MAType = MaMode.SMA;
        //
        [InputParameter("Calculation type", 1, variants: new object[]
        {
            "All available data", IndicatorCalculationType.AllAvailableData,
            "By period", IndicatorCalculationType.ByPeriod,
        })]
        public IndicatorCalculationType CalculationType = Indicator.DEFAULT_CALCULATION_TYPE;


        [InputParameter("Sources prices for MA", 2, variants: new object[] {
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

        // Displays Input Parameter as input field (or checkbox if value type is bolean).
        [InputParameter("Period of MA1", 3, 1, 999, 1, 0)]
        public int MAPeriod1 = 10;

        [InputParameter("Period of MA2", 4, 1, 999, 1, 0)]
        public int MAPeriod2 = 21;

        public int MinHistoryDepths => Enumerable.Max(new int[] { this.MAPeriod1, this.MAPeriod2 });
        public override string ShortName => $"PO ({this.MAPeriod1}: {this.MAPeriod2}: {this.SourcePrice}: {this.MAType})";

        // Holds moving averages values.
        private Indicator ma1, ma2;

        /// <summary>
        /// Indicator's constructor. Contains general information: name, description, LineSeries etc. 
        /// </summary>
        public IndicatorPriceOscillator()
            : base()
        {
            // Defines indicator's group, name and description.
            this.Name = "Price Oscillator";
            this.Description = "Calculates the variation between price moving averages.";

            // Defines line and level with particular parameters.
            this.AddLineSeries("PO'Line", Color.Blue, 1, LineStyle.Solid);
            this.AddLineLevel(0, "0'Line", Color.Gray, 1, LineStyle.Solid);
            this.SeparateWindow = true;
        }

        /// <summary>
        /// This function will be called after creating an indicator as well as after its input params reset or chart (symbol or timeframe) updates.
        /// </summary>
        protected override void OnInit()
        {
            // Creates an instances of the proper indicators (MA1, MA2) from the default indicators list.
            this.ma1 = Core.Indicators.BuiltIn.MA(this.MAPeriod1, this.SourcePrice, this.MAType, this.CalculationType);
            this.ma2 = Core.Indicators.BuiltIn.MA(this.MAPeriod2, this.SourcePrice, this.MAType, this.CalculationType);

            // Adds an auxiliary (MA1, MA2) indicator to the current one (PO). 
            // This will let inner indicator (MA1, MA2) to be calculated in advance to the current one (PO).
            this.AddIndicator(this.ma1);
            this.AddIndicator(this.ma2);
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
            // Skip if count is smaller than maximal period value.
            if (this.Count < this.MinHistoryDepths)
                return;

            // Sets value for displaying on the chart.
            this.SetValue(this.ma1.GetValue() - this.ma2.GetValue());
        }
    }
}