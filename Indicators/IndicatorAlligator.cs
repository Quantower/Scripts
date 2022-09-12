// Copyright QUANTOWER LLC. © 2017-2022. All rights reserved.

using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace Trend
{
    public sealed class IndicatorAlligator : Indicator
    {
        // Displays Input Parameter as dropdown list.
        [InputParameter("Type of Jaw Moving Average", 0, variants: new object[]{
            "Simple", MaMode.SMA,
            "Exponential", MaMode.EMA,
            "Modified", MaMode.SMMA,
               "Linear Weighted", MaMode.LWMA}
        )]
        public MaMode JawMAType = MaMode.SMA;

        [InputParameter("Source price for Jaw Moving Average", 1, variants: new object[] {
             "Close", PriceType.Close,
             "Open", PriceType.Open,
             "High", PriceType.High,
             "Low", PriceType.Low,
             "Typical", PriceType.Typical,
             "Medium", PriceType.Median,
             "Weighted", PriceType.Weighted}
        )]
        public PriceType JawSourcePrice = PriceType.Close;

        // Displays Input Parameter as input field (or checkbox if value type is bolean).
        [InputParameter("Period of Jaw Moving Average", 2, 1, 999)]
        public int JawMAPeiod = 13;

        [InputParameter("Shift of Jaw Moving Average", 3, 1, 999)]
        public int JawMAShift = 8;

        [InputParameter("Type of Teeth Moving Average", 4, variants: new object[]{
            "Simple", MaMode.SMA,
            "Exponential", MaMode.EMA,
            "Modified", MaMode.SMMA,
               "Linear Weighted", MaMode.LWMA}
         )]
        public MaMode TeethMAType = MaMode.SMA;

        [InputParameter("Source price for Teeth Moving Average", 5, variants: new object[]{
             "Close", PriceType.Close,
             "Open", PriceType.Open,
             "High", PriceType.High,
             "Low", PriceType.Low,
             "Typical", PriceType.Typical,
             "Medium", PriceType.Median,
             "Weighted", PriceType.Weighted}
        )]
        public PriceType TeethSourcePrice = PriceType.Close;

        [InputParameter("Period of Teeth MovingAverage", 6, 1, 999)]
        public int TeethMAPeiod = 8;

        [InputParameter("Shift of Teeth Moving Average", 7, 1, 999)]
        public int TeethMAShift = 5;

        [InputParameter("Type of Lips Moving Average", 8, variants: new object[]{
            "Simple", MaMode.SMA,
            "Exponential", MaMode.EMA,
            "Modified", MaMode.SMMA,
               "Linear Weighted", MaMode.LWMA}
         )]
        public MaMode LipsMAType = MaMode.SMA;
        //
        [InputParameter("Calculation type", 9, variants: new object[]
        {
            "All available data", IndicatorCalculationType.AllAvailableData,
            "By period", IndicatorCalculationType.ByPeriod,
        })]
        public IndicatorCalculationType CalculationType = Indicator.DEFAULT_CALCULATION_TYPE;

        [InputParameter("Source price for Lips Moving Average", 10, variants: new object[]{
             "Close", PriceType.Close,
             "Open", PriceType.Open,
             "High", PriceType.High,
             "Low", PriceType.Low,
             "Typical", PriceType.Typical,
             "Medium", PriceType.Median,
             "Weighted", PriceType.Weighted}
        )]
        public PriceType LipsSourcePrice = PriceType.Close;

        [InputParameter("Period of Lips Moving Average", 11, 1, 9999)]
        public int LipsMAPeiod = 5;

        [InputParameter("Shift of Lips Moving Average", 12)]
        public int LipsMAShift = 3;

        // Serves for an identification of related indicators with different parameters.
        public override string ShortName => $"ALLIGATOR ({this.JawMAPeiod}:{this.TeethMAPeiod}:{this.LipsMAPeiod})";
        public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorAlligator.cs";

        private Indicator jawMa;
        private Indicator teethMa;
        private Indicator lipsMa;

        /// <summary>
        /// Indicator's constructor. Contains general information: name, description, LineSeries etc. 
        /// </summary>
        public IndicatorAlligator()
            : base()
        {
            // Defines indicator's name and description.
            this.Name = "Alligator";
            this.Description = "Three moving averages with different colors, periods and calculation methods";

            // Defines line on demand with particular parameters.
            this.AddLineSeries("JAW Line", Color.Green, 1, LineStyle.Solid);
            this.AddLineSeries("TEETH Line", Color.Red, 1, LineStyle.Solid);
            this.AddLineSeries("LIPS Line", Color.Blue, 1, LineStyle.Solid);

            this.SeparateWindow = false;
        }

        /// <summary>
        /// This function will be called after creating an indicator as well as after its input params reset or chart (symbol or timeframe) updates.
        /// </summary>
        protected override void OnInit()
        {
#warning Реалізувати "LineTimeShift".
            //LineTimeShift[0] = JawMAShift;
            //LineTimeShift[1] = TeethMAShift;
            //LineTimeShift[2] = LipsMAShift;
            this.jawMa = Core.Indicators.BuiltIn.MA(this.JawMAPeiod, this.JawSourcePrice, this.JawMAType, this.CalculationType);
            this.AddIndicator(this.jawMa);
            this.teethMa = Core.Indicators.BuiltIn.MA(this.TeethMAPeiod, this.TeethSourcePrice, this.TeethMAType, this.CalculationType);
            this.AddIndicator(this.teethMa);
            this.lipsMa = Core.Indicators.BuiltIn.MA(this.LipsMAPeiod, this.LipsSourcePrice, this.LipsMAType, this.CalculationType);
            this.AddIndicator(this.lipsMa);
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
            this.SetValue(this.jawMa.GetValue());
            this.SetValue(this.teethMa.GetValue(), 1);
            this.SetValue(this.lipsMa.GetValue(), 2);
        }
    }
}