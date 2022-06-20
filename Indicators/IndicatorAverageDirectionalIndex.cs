// Copyright QUANTOWER LLC. © 2017-2022. All rights reserved.

using System;
using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace Trend
{
    /// <summary>
    /// The Average Directional Index (ADX) determines the strength of a prevailing trend.
    /// </summary>
    public sealed class IndicatorAverageDirectionalIndex : Indicator, IWatchlistIndicator
    {
        // Displays Input Parameter as input field (or checkbox if value type is bolean).
        [InputParameter("Period", 0, 1, 999, 1, 0)]
        public int Period = 14;

        // Displays Input Parameter as dropdown list.
        [InputParameter("Type of Moving Average", 1, variants: new object[] {
            "Simple", MaMode.SMA,
            "Exponential", MaMode.EMA,
            "Modified", MaMode.SMMA,
            "Linear Weighted", MaMode.LWMA}
        )]
        public MaMode MAType = MaMode.SMA;
        //
        [InputParameter("Calculation type", 5, variants: new object[]
        {
            "All available data", IndicatorCalculationType.AllAvailableData,
            "By period", IndicatorCalculationType.ByPeriod,
        })]
        public IndicatorCalculationType CalculationType = Indicator.DEFAULT_CALCULATION_TYPE;

        public int MinHistoryDepths => this.Period * 2;
        public override string ShortName => $"ADX ({this.Period}: {this.MAType})";
        public override string HelpLink => "https://help.quantower.com/analytics-panels/chart/technical-indicators/trend/average-directional-movement-index-adx-indicator";

        private HistoricalDataCustom customHDadx;
        private HistoricalDataCustom customHDplusDm;
        private HistoricalDataCustom customHDminusDm;

        private Indicator rawAtr;
        private Indicator adxMa;
        private Indicator plusMa;
        private Indicator minusMa;

        /// <summary>
        /// Indicator's constructor. Contains general information: name, description, LineSeries etc. 
        /// </summary>
        public IndicatorAverageDirectionalIndex()
            : base()
        {
            // Defines indicator's group, name and description.            
            this.Name = "Average Directional Index";
            this.Description = "The ADX determines the strength of a prevailing trend.";

            // Defines line on demand with particular parameters.
            this.AddLineSeries("ADX'Line", Color.Green, 1, LineStyle.Solid);
            this.AddLineSeries("+DI'Line", Color.Blue, 1, LineStyle.Solid);
            this.AddLineSeries("-DI'Line", Color.Red, 1, LineStyle.Solid);

            this.SeparateWindow = true;
        }

        /// <summary>
        /// This function will be called after creating an indicator as well as after its input params reset or chart (symbol or timeframe) updates.
        /// </summary>
        protected override void OnInit()
        {
            // Creates an instances of the custom historical data which will be syncronized by the current indicator instance.
            this.customHDadx = new HistoricalDataCustom(this);
            this.customHDplusDm = new HistoricalDataCustom(this);
            this.customHDminusDm = new HistoricalDataCustom(this);

            // Creates a indicators which will keep custom data.
            this.adxMa = Core.Indicators.BuiltIn.MA(this.Period, PriceType.Close, this.MAType, this.CalculationType);
            this.plusMa = Core.Indicators.BuiltIn.MA(this.Period, PriceType.Close, this.MAType, this.CalculationType);
            this.minusMa = Core.Indicators.BuiltIn.MA(this.Period, PriceType.Close, this.MAType, this.CalculationType);

            // Adds indicators to the custom historical data.
            this.customHDadx.AddIndicator(this.adxMa);
            this.customHDplusDm.AddIndicator(this.plusMa);
            this.customHDminusDm.AddIndicator(this.minusMa);

            // Creates an instance of the proper indicator from the default indicators list.
            this.rawAtr = Core.Indicators.BuiltIn.ATR(1, this.MAType, this.CalculationType);

            // Adds an auxiliary (ATR) indicator to the current one (ADX). 
            // This will let inner indicator (ATR) to be calculated in advance to the current one (ADX).
            this.AddIndicator(this.rawAtr);
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
            // Gets directional movement values.
            this.GetPlusMinus(out double plusDM, out double minusDM);

            // Populates custom HistoricalData prices with the respective directional movement values.
            this.customHDplusDm[PriceType.Close] = plusDM;
            this.customHDminusDm[PriceType.Close] = minusDM;

            // Skip if count is smaller than period value.
            if (this.Count < this.Period)
                return;

            // Gets smoothed directional movement values.
            double plusDI = this.plusMa.GetValue();
            double minusDI = this.minusMa.GetValue();

            // Calculates ADX.
            double adx = (plusDI != -minusDI) ? 100 * Math.Abs(plusDI - minusDI) / (plusDI + minusDI) : 0D;

            // Populates custom HistoricalData close price with the ADX value.
            this.customHDadx[PriceType.Close] = adx;

            if (this.Count < this.MinHistoryDepths)
                return;

            // Sets values for displaying on the chart.
            this.SetValue(this.adxMa.GetValue());
            this.SetValue(plusDI, 1);
            this.SetValue(minusDI, 2);
        }

        /// <summary>
        /// Calculates directional movement of the same momentum.
        /// </summary>
        /// <param name="plusDM">positive directional movement</param>
        /// <param name="minusDM">negative directional movement</param>
        private void GetPlusMinus(out double plusDM, out double minusDM)
        {
            double rawATR = this.rawAtr.GetValue();

            if (this.Count < 2 || rawATR == 0)
            {
                plusDM = 0D;
                minusDM = 0D;
                return;
            }

            // Calculation of directional movement (DMs)
            plusDM = this.High() - this.High(1);
            if (plusDM < 0.0)
                plusDM = 0.0;
            else
                plusDM *= 100D / rawATR;
            minusDM = this.Low(1) - this.Low();
            if (minusDM < 0.0)
                minusDM = 0.0;
            else
                minusDM *= 100D / rawATR;

            if (plusDM > minusDM)
                minusDM = 0.0;
            else
                plusDM = 0.0;
        }
    }
}