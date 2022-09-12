// Copyright QUANTOWER LLC. © 2017-2022. All rights reserved.

using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace Oscillators
{
    public sealed class IndicatorRelativeStrengthIndex : Indicator, IWatchlistIndicator
    {
        // RSI period
        [InputParameter("RSI period", 0, 1, 9999)]
        public int Period = 14;
        // Default will be performed on Close prices
        [InputParameter("Sources prices for the RSI line", 1, variants: new object[] {
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
        // Default will be performed on Simple mode
        [InputParameter("Mode for the RSI line", 2, variants: new object[] {
             "Simple", RSIMode.Simple,
             "Exponential", RSIMode.Exponential}
        )]
        public RSIMode SourceRSI = RSIMode.Exponential;

        [InputParameter("Type of Moving Average", 3, variants: new object[] {
            "Simple", MaMode.SMA,
            "Exponential", MaMode.EMA,
            "Smoothed Modified", MaMode.SMMA,
            "Linear Weighted", MaMode.LWMA}
        )]
        public MaMode MaType = MaMode.SMA;
        //
        [InputParameter("Calculation type", 4, variants: new object[]
        {
            "All available data", IndicatorCalculationType.AllAvailableData,
            "By period", IndicatorCalculationType.ByPeriod,
        })]
        public IndicatorCalculationType CalculationType = Indicator.DEFAULT_CALCULATION_TYPE;

        // Smoothing period
        [InputParameter("Smoothing period", 5, 1, 9999)]
        public int MAPeriod = 5;

        public int MinHistoryDepths
        {
            get
            {
                int minHistoryDepths = this.Period + this.MAPeriod;
                if (this.CalculationType == IndicatorCalculationType.ByPeriod)
                    minHistoryDepths += CALCULATION_PERIOD;

                return minHistoryDepths;
            }
        }
        public override string ShortName => $"RSI ({this.Period}: {this.SourcePrice})";

        public override string HelpLink => "https://help.quantower.com/analytics-panels/chart/technical-indicators/oscillators/relative-strength-index-rsi-indicator";
        public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorRelativeStrengthIndex.cs";

        private Indicator ma;
        private HistoricalDataCustom histCustom;

        private double prevV = 0.0;
        private double prevP = 0.0;
        private double sumV = 0.0;
        private double sumP = 0.0;
        private const int CALCULATION_PERIOD = 500;

        /// <summary>
        /// Indicator's constructor. Contains general information: name, description, LineSeries etc. 
        /// </summary>
        public IndicatorRelativeStrengthIndex()
            : base()
        {
            this.Name = "Relative Strength Index";
            this.Description = "RSI is classified as a momentum oscillator, measuring the velocity and magnitude of directional price movements";

            //Defines line on demand with particular parameters 
            this.AddLineSeries("RSI Line", Color.Green, 1, LineStyle.Solid);
            this.AddLineSeries("MA Line", Color.PowderBlue, 1, LineStyle.Solid);
            this.AddLineLevel(70, "Upper Limit", Color.Red, 1, LineStyle.Solid);
            this.AddLineLevel(30, "Lower Limit", Color.Blue, 1, LineStyle.Solid);
            this.AddLineLevel(50, "Middle Limit", Color.Gray, 1, LineStyle.Solid);

            this.SeparateWindow = true;
        }

        /// <summary>
        /// This function will be called after creating an indicator as well as after its input params reset or chart (symbol or timeframe) update
        /// </summary>
        protected override void OnInit()
        {
            // Creates an instance of the custom historical data which will be synchronized with the current indicator instance.
            this.histCustom = new HistoricalDataCustom(this);
            // Creates a smoothing indicator which will keep smoothed custom data.
            this.ma = Core.Indicators.BuiltIn.MA(this.MAPeriod, this.SourcePrice, this.MaType, this.CalculationType);
            // Attaches the smoothing indicator to the custom historical data.
            this.histCustom.AddIndicator(this.ma);
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
            if (args.Reason != UpdateReason.NewTick)
            {
                this.prevV = this.sumV;
                this.prevP = this.sumP;
            }
            if (this.Count < this.Period || this.Period == 0)
                return;

            if (this.SourceRSI == RSIMode.Simple)
                this.CalcSimple();
            else
            {
                if (this.CalculationType == IndicatorCalculationType.ByPeriod && this.Count > CALCULATION_PERIOD + this.Period)
                {
                    // (+ Period). Тому що, для самого крайнього лівого бара треба розрахувати попереднє значення. 
                    // Воно рахується по формулі SMA, для якого і потрібен цей період.
                    this.CalcExponByPeriod();
                }
                else
                    this.CalcExpon();
            }

            // The calculated value must be set as close price against the custom HistoricalData (a respective price type argument), 
            // because the SMA indicator was initialized with the source price - PriceType.Close. 
            this.histCustom.SetValue(0d, 0d, 0d, this.GetValue());

            // Being able to obtain data against HistCustom, MA finaly can smooth it
            this.SetValue(this.ma.GetValue(), 1);
        }

        protected override void OnClear()
        {
            this.prevP = default;
            this.prevV = default;
            this.sumP = default;
            this.sumV = default;
        }

        // Simple RSI method
        private void CalcSimple(int offset = 0, bool setValue = true)
        {
            this.sumV = 0D;
            this.sumP = 0D;

            for (int i = 0; i < this.Period; i++)
            {
                double diff = this.GetPrice(this.SourcePrice, i + offset) - this.GetPrice(this.SourcePrice, i + 1 + offset);

                if (double.IsNaN(diff))
                    continue;

                if (diff > 0D)
                    this.sumV += diff;
                else
                    this.sumP -= diff;
            }

            if (setValue)
            {
                double value = (this.sumP != 0D) ? 100D * (1.0 - 1.0 / (1.0 + this.sumV / this.sumP)) : 100D;
                this.SetValue(value);
            }
        }
        // Exponential RSI method
        private void CalcExpon()
        {
            if (this.Count == this.Period + 1)
            {
                this.CalcSimple();
                this.prevV = this.sumV = this.sumV / this.Period;
                this.prevP = this.sumP = this.sumP / this.Period;
            }
            else
            {
                double diff = this.GetPrice(this.SourcePrice) - this.GetPrice(this.SourcePrice, 1);
                if (diff > 0D)
                {
                    this.sumV = (this.prevV * (this.Period - 1) + diff) / this.Period;
                    this.sumP = this.prevP * (this.Period - 1) / this.Period;
                }
                else
                {
                    this.sumV = this.prevV * (this.Period - 1) / this.Period;
                    this.sumP = (this.prevP * (this.Period - 1) - diff) / this.Period;
                }
            }
            double rsi = (this.sumP != 0D) ? 100D - 100D / (1.0 + this.sumV / this.sumP) : 0D;
            this.SetValue(rsi);
        }
        private void CalcExponByPeriod(int offset = 0)
        {
            int startOffset = offset + CALCULATION_PERIOD;

            if (this.Count <= startOffset + this.Period)
                return;

            this.CalcSimple(startOffset, setValue: false);
            this.prevV = this.sumV = this.sumV / this.Period;
            this.prevP = this.sumP = this.sumP / this.Period;

            for (int i = startOffset - 1; i >= offset; i--)
            {
                double diff = this.GetPrice(this.SourcePrice, i) - this.GetPrice(this.SourcePrice, i + 1);
                if (diff > 0D)
                {
                    this.sumV = (this.prevV * (this.Period - 1) + diff) / this.Period;
                    this.sumP = this.prevP * (this.Period - 1) / this.Period;
                }
                else
                {
                    this.sumV = this.prevV * (this.Period - 1) / this.Period;
                    this.sumP = (this.prevP * (this.Period - 1) - diff) / this.Period;
                }
            }

            double rsi = (this.sumP != 0D) ? 100D - 100D / (1.0 + this.sumV / this.sumP) : 0D;
            this.SetValue(rsi);
        }
    }
}