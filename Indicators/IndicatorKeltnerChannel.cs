// Copyright QUANTOWER LLC. © 2017-2022. All rights reserved.

using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace Channels
{
    /// <summary>
    /// Keltner Channels are volatility-based envelopes set above and below an exponential moving average.
    /// </summary>
    public sealed class IndicatorKeltnerChannel : Indicator, IWatchlistIndicator
    {
        // Displays Input Parameter as input field (or checkbox if value type is bolean).
        [InputParameter("Sources prices for MA", 10, variants: new object[] {
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
        [InputParameter("Type of Moving Average", 20, variants: new object[] {
            "Simple", MaMode.SMA,
            "Exponential", MaMode.EMA,
            "Modified", MaMode.SMMA,
            "Linear Weighted", MaMode.LWMA}
         )]
        public MaMode MAType = MaMode.SMA;
        //
        [InputParameter("Calculation type", 30, variants: new object[]
        {
            "All available data", IndicatorCalculationType.AllAvailableData,
            "By period", IndicatorCalculationType.ByPeriod,
        })]
        public IndicatorCalculationType CalculationType = Indicator.DEFAULT_CALCULATION_TYPE;

        // Displays Input Parameter as dropdown list.
        [InputParameter("Period of MA for Keltner's Channel", 40, 1, 9999, 1)]
        public int Period = 20;

        // Displays Input Parameter as dropdown list.
        [InputParameter("Coefficient of channel's width", 50, 0.01, 100)]
        public double Offset = 2;

        public int MinHistoryDepths => this.Period;
        public override string ShortName => $"Keltner ({this.Period}: {this.Offset}: {this.SourcePrice}: {this.MAType})";

        private Indicator ma;
        private Indicator atr;

        /// <summary>
        /// Indicator's constructor. Contains general information: name, description, LineSeries etc. 
        /// </summary>
        public IndicatorKeltnerChannel()
            : base()
        {
            // Defines indicator's group, name and description.            
            this.Name = "Keltner Channel";
            this.Description = "Keltner Channels are volatility-based envelopes set above and below an exponential moving average";
            this.IsUpdateTypesSupported = false;

            // Defines line on demand with particular parameters.
            this.AddLineSeries("MA'Line", Color.Coral, 1, LineStyle.Solid);
            this.AddLineSeries("+ATR'Line", Color.Red, 1, LineStyle.Solid);
            this.AddLineSeries("-ATR'Line", Color.Purple, 1, LineStyle.Solid);
        }

        /// <summary>
        /// This function will be called after creating an indicator as well as after its input params reset or chart (symbol or timeframe) updates.
        /// </summary>
        protected override void OnInit()
        {
            // Creates an instances of the proper indicators from the default indicators list.
            this.ma = Core.Indicators.BuiltIn.MA(this.Period, this.SourcePrice, this.MAType, this.CalculationType);
            this.ma.UpdateType = IndicatorUpdateType.OnTick;

            this.atr = Core.Indicators.BuiltIn.ATR(this.Period, this.MAType, this.CalculationType);

            // Adds an auxiliary (MA, ATR ) indicators to the current one (Keltner). 
            // This will let inner indicator (MA, ATR) to be calculated in advance to the current one (Keltner).
            this.AddIndicator(this.ma);
            this.AddIndicator(this.atr);
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

            // Gets calculation values.
            double maValue = this.ma.GetValue();
            double atrValue = this.Offset * this.atr.GetValue();

            // Sets given values for the displaying on the chart. 
            this.SetValue(maValue, 0);
            this.SetValue(maValue + atrValue, 1);
            this.SetValue(maValue - atrValue, 2);
        }
    }
}