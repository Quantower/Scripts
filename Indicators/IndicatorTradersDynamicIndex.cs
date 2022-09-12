// Copyright QUANTOWER LLC. © 2017-2022. All rights reserved.

using System.Drawing;
using System.Linq;
using TradingPlatform.BusinessLayer;

namespace Trend
{
    public class IndicatorTradersDynamicIndex: Indicator, IWatchlistIndicator
    {
        #region Parameters
        [InputParameter("RSI period", 10, 1, 9999)]
        public int Period = 13;

        [InputParameter("Sources prices for the RSI line", 20, variants: new object[] {
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

        [InputParameter("Type of the RSI line", 30, variants: new object[] {
             "Simple", RSIMode.Simple,
             "Exponential", RSIMode.Exponential}
        )]
        public RSIMode RSIMode = RSIMode.Exponential;

        [InputParameter("Volatility Band", 40, 1, 999)]
        public int VolatilityBand = 34;
        [InputParameter("Standard Deviation", 50, 0.1, 100.0, 0.1, 2)]
        public double StandardDeviation = 1.6185;

        [InputParameter("Fast MA period on RSI", 60, 1, 9999)]
        public int FastMaOnRSIPeriod = 2;

        [InputParameter("Fast MA type on RSI", 60, variants: new object[] {
            "Simple", MaMode.SMA,
            "Exponential", MaMode.EMA,
            "Smoothed Modified", MaMode.SMMA,
            "Linear Weighted", MaMode.LWMA}
        )]
        public MaMode FastMAOnRSIMode = MaMode.SMA;

        [InputParameter("Slow MA period on RSI", 70, 1, 9999)]
        public int SlowMAOnRSIPeriod = 7;

        [InputParameter("Slow MA type on RSI", 80, variants: new object[] {
            "Simple", MaMode.SMA,
            "Exponential", MaMode.EMA,
            "Smoothed Modified", MaMode.SMMA,
            "Linear Weighted", MaMode.LWMA}
        )]
        public MaMode SlowMAOnRSIMode = MaMode.SMA;

        //
        [InputParameter("Calculation type", 90, variants: new object[]
        {
            "All available data", IndicatorCalculationType.AllAvailableData,
            "By period", IndicatorCalculationType.ByPeriod,
        })]
        public IndicatorCalculationType CalculationType = Indicator.DEFAULT_CALCULATION_TYPE;

        #endregion Parameters

        private Indicator rsi;
        private Indicator bb;
        private HistoricalDataCustom historicalDataBB;
        private Indicator priceMa;
        private HistoricalDataCustom historicalDataPrice;
        private Indicator signalMa;
        private HistoricalDataCustom historicalDataSignal;

        public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorTradersDynamicIndex.cs";

        /// <summary>
        /// Indicator's constructor. Contains general information: name, description, LineSeries etc. 
        /// </summary>
        public IndicatorTradersDynamicIndex()
            : base()
        {
            // Defines indicator's name and description.
            Name = "Traders Dynamic Index";
            Description = "";

            // Defines line on demand with particular parameters.
            AddLineSeries("BB Up", Color.Blue, 1, LineStyle.Solid);
            AddLineSeries("BB Middle", Color.Orange, 1, LineStyle.Solid);
            AddLineSeries("BB Down", Color.Blue, 1, LineStyle.Solid);
            AddLineSeries("Fast MA on RSI", Color.Green, 1, LineStyle.Solid);
            AddLineSeries("Slow MA on RSI", Color.Red, 1, LineStyle.Solid);

            AddLineLevel(68, "", Color.Gray, 1, LineStyle.Dash);
            AddLineLevel(50, "", Color.Gray, 1, LineStyle.Dash);
            AddLineLevel(32, "", Color.Gray, 1, LineStyle.Dash);

            SeparateWindow = true;
        }

        public int MinHistoryDepths => Enumerable.Max(new int[] { VolatilityBand, FastMaOnRSIPeriod, SlowMAOnRSIPeriod }) + Period;
        public override string ShortName => $"TDI ({Period}: {VolatilityBand}: {FastMaOnRSIPeriod}: {SlowMAOnRSIPeriod})";

        /// <summary>
        /// This function will be called after creating an indicator as well as after its input params reset or chart (symbol or timeframe) updates.
        /// </summary>
        protected override void OnInit()
        {
            rsi = Core.Indicators.BuiltIn.RSI(Period, SourcePrice, RSIMode, MaMode.SMA, 1);

            bb = Core.Indicators.BuiltIn.BB(VolatilityBand, StandardDeviation, PriceType.Close, MaMode.SMA);
            historicalDataBB = new HistoricalDataCustom(this);
            historicalDataBB.AddIndicator(bb);

            priceMa = Core.Indicators.BuiltIn.MA(FastMaOnRSIPeriod, PriceType.Close, FastMAOnRSIMode, this.CalculationType);
            historicalDataPrice = new HistoricalDataCustom(this);
            historicalDataPrice.AddIndicator(priceMa);

            signalMa = Core.Indicators.BuiltIn.MA(SlowMAOnRSIPeriod, PriceType.Close, SlowMAOnRSIMode, this.CalculationType);
            historicalDataSignal = new HistoricalDataCustom(this);
            historicalDataSignal.AddIndicator(signalMa);

            AddIndicator(rsi);
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
            if (this.Count < this.Period)
                return;

            historicalDataBB[PriceType.Close] = rsi.GetValue();
            historicalDataPrice[PriceType.Close] = rsi.GetValue();
            historicalDataSignal[PriceType.Close] = rsi.GetValue();

            if (this.Count < this.MinHistoryDepths)
                return;

            SetValue(bb.GetValue(0, 0), 0);
            SetValue(bb.GetValue(0, 1), 1);
            SetValue(bb.GetValue(0, 2), 2);
            SetValue(priceMa.GetValue(), 3);
            SetValue(signalMa.GetValue(), 4);
        }
    }

}

