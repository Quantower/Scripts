// Copyright QUANTOWER LLC. © 2017-2022. All rights reserved.

using System;
using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace Channels
{
    public sealed class IndicaotrBollingerBands : Indicator, IWatchlistIndicator
    {
        //Defines input parameters as input fields
        [InputParameter("Period of MA for envelopes", 0, 1, 999)]
        public int Period = 20;
        [InputParameter("Value of confidence interval", 1, 0.01, 100.0, 0.01, 2)]
        public double D = 2.0;

        //Defines input parameters as dropdown lists
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
        public PriceType SourcePrices = PriceType.Close;
        [InputParameter("Type of moving average", 3, variants: new object[]{
            "Simple Moving Average", MaMode.SMA,
            "Exponential Moving Average", MaMode.EMA,
            "Smoothed Moving Average", MaMode.SMMA,
            "Linearly Weighted Moving Average", MaMode.LWMA,
        })]
        public MaMode MaType = MaMode.SMA;

        [InputParameter("Calculation type", 4, variants: new object[]
        {
            "All available data", IndicatorCalculationType.AllAvailableData,
            "By period", IndicatorCalculationType.ByPeriod,
        })]
        public IndicatorCalculationType CalculationType = Indicator.DEFAULT_CALCULATION_TYPE;

        public int MinHistoryDepths => this.Period;
        public override string ShortName => $"BB ({this.Period}:{this.D})";

        private Indicator ma;

        /// <summary>
        /// Indicator's constructor. Contains general information: name, description, LineSeries etc. 
        /// </summary>
        public IndicaotrBollingerBands()
            : base()
        {
            // Defines indicator's name and description.
            this.Name = "Bollinger Bands";
            this.Description = "Provides a relative definition of high and low based on standard deviation and a simple moving average";

            // Defines three lines with particular parameters.
            this.AddLineSeries("Upper Band", Color.Red, 2, LineStyle.Solid);
            this.AddLineSeries("Middle Band", Color.Gray, 2, LineStyle.Solid);
            this.AddLineSeries("Lower Band", Color.Green, 2, LineStyle.Solid);

            this.SeparateWindow = false;
        }

        /// <summary>
        /// This function will be called after creating an indicator as well as after its input params reset or chart (symbol or timeframe) updates.
        /// </summary>
        protected override void OnInit()
        {
            // Get MA indicator from built-in indicator collection (according to selected 'MaType') 
            this.ma = Core.Indicators.BuiltIn.MA(this.Period, this.SourcePrices, this.MaType, this.CalculationType);
            this.AddIndicator(this.ma);
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
            // Checking, if current amount of bars 
            // more, than period of moving average. If it is
            // then the calculation is possible
            if (this.Count < this.MinHistoryDepths)
                return;

            // Get MA value of current bar (0 offset by default)
            double maValue = this.ma.GetValue();

            // Calulation of the sum
            double sum = 0.0;
            for (int i = 0; i < this.Period; i++)
                sum += Math.Pow(this.GetPrice(this.SourcePrices, i) - maValue, 2);

            // Calculation of deviation value
            sum = this.D * Math.Sqrt(sum / this.Period);

            // set values to line buffers 
            this.SetValue(maValue + sum);
            this.SetValue(maValue, 1);
            this.SetValue(maValue - sum, 2);
        }
    }
}