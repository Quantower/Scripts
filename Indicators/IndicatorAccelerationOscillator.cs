// Copyright QUANTOWER LLC. © 2017-2022. All rights reserved.

using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace Oscillators
{
    /// <summary>
    /// Acceleration/Deceleration Oscillator measures the acceleration and deceleration of the current momentum.
    /// </summary>
    public sealed class IndicatorAccelerationOscillator : Indicator, IWatchlistIndicator
    {
        private const int AC_PERIOD = 5;
        private const int AO_MAX_PERIOD = 34;

        public override string HelpLink => "https://help.quantower.com/analytics-panels/chart/technical-indicators/oscillators/accelerator-oscillator";
        public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorAccelerationOscillator.cs";
        public int MinHistoryDepths => AC_PERIOD + AO_MAX_PERIOD;
        public override string ShortName => "AC";

        // Custom historical data to keep calculated values SMA(AO, 5).
        private HistoricalDataCustom customHistData;
        // Calculation indicators.
        private Indicator ao;
        private Indicator smaAO;

        /// <summary>
        /// Indicator's constructor. Contains general information: name, description, LineSeries etc. 
        /// </summary>
        public IndicatorAccelerationOscillator()
            : base()
        {
            // Defines indicator's group, name and description.            
            this.Name = "Acceleration Oscillator";
            this.Description = "Acceleration/Deceleration Oscillator (AC) measures the acceleration and deceleration of the current momentum.";

            // Defines line on demand with particular parameters.
            this.AddLineSeries("AC'Line", Color.Gray, 2, LineStyle.Histogramm);
            this.SeparateWindow = true;
        }

        /// <summary>
        /// This function will be called after creating an indicator as well as after its input params reset or chart (symbol or timeframe) updates.
        /// </summary>
        protected override void OnInit()
        {
            // Creates an instance of the custom historical data which will be syncronized by the current indicator instance.
            this.customHistData = new HistoricalDataCustom(this);

            // Creates a smoothing indicator which will keep smoothed custom data (for close prices).
            this.smaAO = Core.Indicators.BuiltIn.SMA(AC_PERIOD, PriceType.Close);

            // Adds the smoothing indicator to the custom historical data.
            this.customHistData.AddIndicator(this.smaAO);

            // Creates auxiliary indicator to calculate AO value with current historical data.
            this.ao = Core.Indicators.BuiltIn.AO();
            this.AddIndicator(this.ao);
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
            if (this.Count < AO_MAX_PERIOD)
                return;

            // Gets AO indicator value.
            double aoValue = this.ao.GetValue();

            // The calculated value must be set as close price against the custom HistoricalData (a respective price type argument), 
            // because the SMA indicator was initialized with the source price - PriceType.Close.
            this.customHistData.SetValue(0, 0, 0, aoValue);

            if (this.Count < this.MinHistoryDepths)
                return;

            // Calculates difference between AO's value and smoothed custom historical data: AC = AO - SMA(AO,5).
            double differ = aoValue - this.smaAO.GetValue();

            // Skips value setting if it's NaN.
            if (double.IsNaN(differ))
                return;

            double prevDiffer = (this.Count > 1) ? this.GetValue(1) : differ;

            // Sets value for displaying on the chart.
            this.SetValue(aoValue - this.smaAO.GetValue());

            if (prevDiffer < differ)
                this.LinesSeries[0].SetMarker(0, Color.Green);
            else if (prevDiffer > differ)
                this.LinesSeries[0].SetMarker(0, Color.Red);
        }
    }
}