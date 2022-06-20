// Copyright QUANTOWER LLC. © 2017-2022. All rights reserved.

using System;
using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace Oscillators
{
    public sealed class IndicatorTrueStrengthIndex : Indicator, IWatchlistIndicator
    {
        // Displays Input Parameter as input field.
        [InputParameter("First MA period", 0, 1, 999, 1, 0)]
        public int FirstPeriod = 5;

        // Displays Input Parameter as input field.
        [InputParameter("Second MA period", 1, 1, 999, 1, 0)]
        public int SecondPeriod = 8;

        //
        [InputParameter("Calculation type", 10, variants: new object[]
        {
            "All available data", IndicatorCalculationType.AllAvailableData,
            "By period", IndicatorCalculationType.ByPeriod,
        })]
        public IndicatorCalculationType CalculationType = Indicator.DEFAULT_CALCULATION_TYPE;

        private Indicator firstEma;
        private Indicator firstEmaAbs;
        private Indicator secondEma;
        private Indicator secondEmaAbs;

        private HistoricalDataCustom firstEmaHD;
        private HistoricalDataCustom firstEmaAbsHD;
        private HistoricalDataCustom secondEmaHD;
        private HistoricalDataCustom secondEmaAbsHD;

        public int MinHistoryDepths => this.FirstPeriod + this.SecondPeriod;
        public override string ShortName => $"TSI ({this.FirstPeriod}:{this.SecondPeriod})";

        /// <summary>
        /// Indicator's constructor. Contains general information: name, description, LineSeries etc. 
        /// </summary>
        public IndicatorTrueStrengthIndex()
            : base()
        {
            // Defines indicator's name and description.
            this.Name = "True Strength Index";
            this.Description = "Is a variation of the Relative Strength Indicator which uses a doubly-smoothed exponential moving average of price momentum to eliminate choppy price changes and spot trend changes";

            // Defines lines on demand with particular parameters.
            this.AddLineLevel(50, "Upper", Color.Gray, 1, LineStyle.Solid);
            this.AddLineLevel(0, "Zero", Color.Gray, 1, LineStyle.Solid);
            this.AddLineLevel(-50, "Lower", Color.Gray, 1, LineStyle.Solid);
            this.AddLineSeries("TSI", Color.Orange, 1, LineStyle.Solid);

            this.SeparateWindow = true;
        }

        /// <summary>
        /// This function will be called after creating an indicator as well as after its input params reset or chart (symbol or timeframe) updates.
        /// </summary>
        protected override void OnInit()
        {
            // Get EMA indicators from built-in indicator collection.
            this.firstEma = Core.Indicators.BuiltIn.EMA(this.FirstPeriod, PriceType.Close, this.CalculationType);
            this.firstEmaAbs = Core.Indicators.BuiltIn.EMA(this.FirstPeriod, PriceType.Close, this.CalculationType);
            this.secondEma = Core.Indicators.BuiltIn.EMA(this.SecondPeriod, PriceType.Close, this.CalculationType);
            this.secondEmaAbs = Core.Indicators.BuiltIn.EMA(this.SecondPeriod, PriceType.Close, this.CalculationType);

            // Create custom HistoricalData objects for stored custom values and syncronize their with this(TSI) indicator.
            this.firstEmaHD = new HistoricalDataCustom(this);
            this.firstEmaAbsHD = new HistoricalDataCustom(this);
            this.secondEmaHD = new HistoricalDataCustom(this);
            this.secondEmaAbsHD = new HistoricalDataCustom(this);

            // Attach EMA indicators to custom HistoricalData.
            this.firstEmaHD.AddIndicator(this.firstEma);
            this.firstEmaAbsHD.AddIndicator(this.firstEmaAbs);
            this.secondEmaHD.AddIndicator(this.secondEma);
            this.secondEmaAbsHD.AddIndicator(this.secondEmaAbs);
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
            double mtm = this.GetMTM(0);

            // Store custom value as 'Close' price into custom HD,
            // because EMA indicator (which was attached to it) was initialized with PriceType.Close.
            this.firstEmaHD[PriceType.Close, 0] = mtm;
            this.firstEmaAbsHD[PriceType.Close, 0] = Math.Abs(mtm);

            // Skip some period for correct calculation.
            if (this.Count < this.FirstPeriod)
                return;

            this.secondEmaHD[PriceType.Close, 0] = this.firstEma.GetValue();
            this.secondEmaAbsHD[PriceType.Close, 0] = this.firstEmaAbs.GetValue();

            if (this.Count < this.MinHistoryDepths)
                return;

            // Set value to 'TSI' line buffer.
            this.SetValue(100 * this.secondEma.GetValue() / this.secondEmaAbs.GetValue());
        }

        /// <summary>
        /// Compute the momentum value.
        /// </summary>
        private double GetMTM(int offset)
        {
            if (this.Count < 2)
                return this.GetPrice(PriceType.Close, offset);

            return this.GetPrice(PriceType.Close, offset) - this.GetPrice(PriceType.Close, offset + 1);
        }
    }
}