// Copyright QUANTOWER LLC. © 2017-2022. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace Trend
{
    public sealed class IndicatorSwingIndex : Indicator, IWatchlistIndicator
    {
        // Displays Input Parameter as input field.
        [InputParameter("Divider", 0, 0.1, 9999.0, 0.1, 1)]
        public double Divider = 300.0;

        [InputParameter("Calculation type", 10, variants: new object[]
        {
            "All available data", IndicatorCalculationType.AllAvailableData,
            "By period", IndicatorCalculationType.ByPeriod,
        })]
        public IndicatorCalculationType CalculationType = Indicator.DEFAULT_CALCULATION_TYPE;

        [InputParameter("Period", 20, 2, 9999, 1, 0)]
        public int Period = 10;

        public int MinHistoryDepths => this.CalculationType == IndicatorCalculationType.AllAvailableData ? 2 : this.Period * 2;
        public override string ShortName => $"SI ({this.Divider})";

        private Indicator atr;

        /// <summary>
        /// Indicator's constructor. Contains general information: name, description, LineSeries etc. 
        /// </summary>
        public IndicatorSwingIndex()
            : base()
        {
            // Defines indicator's name and description.
            this.Name = "Swing Index";
            this.Description = "Is used to confirm trend line breakouts on price charts.";

            // Defines line on demand with particular parameters.
            this.AddLineSeries("SI", Color.DodgerBlue, 1, LineStyle.Solid);

            this.SeparateWindow = true;
        }

        /// <summary>
        /// This function will be called after creating an indicator as well as after its input params reset or chart (symbol or timeframe) updates.
        /// </summary>
        protected override void OnInit()
        {
            // Get ATR indicator from built-in indicator collection.
            this.atr = Core.Indicators.BuiltIn.ATR(1, MaMode.SMA);

            // Add auxiliary ATR indicator to the current one. 
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
            // Skip the first bar.
            if (this.Count < this.MinHistoryDepths)
                return;

            if (this.CalculationType == IndicatorCalculationType.AllAvailableData)
                this.CalculateForAllData();
            else if (this.CalculationType == IndicatorCalculationType.ByPeriod)
                this.CalculateByPeriod();
        }
        public override IList<SettingItem> Settings
        {
            get
            {
                var settings = base.Settings;

                if (settings.GetItemByName("Period") is SettingItem periodSI && settings.GetItemByName("Calculation type") is SettingItem calcType)
                    periodSI.Relation = new SettingItemRelation(new Dictionary<string, IEnumerable<object>>() { ["Calculation type"] = new object[0] }, this.RelationHandler);

                return settings;
            }
            set => base.Settings = value;
        }

        private bool RelationHandler(SettingItemRelationParameters relationParameters)
        {
            bool hasChanged = false;

            try
            {
                bool isVisible = this.CalculationType == IndicatorCalculationType.ByPeriod;
                hasChanged = relationParameters.DependentItem.Visible != isVisible;

                relationParameters.DependentItem.Visible = isVisible;
            }
            catch (Exception ex)
            {
                Core.Loggers.Log(ex, "Swing Index: Relation");
            }

            return hasChanged;
        }

        private void CalculateByPeriod(int offset = 0)
        {
            int startOffset = offset + this.Period;

            if (this.Count <= startOffset + this.Period)
                return;

            double si = 0d;
            for (int i = startOffset - 1; i >= offset; i--)
                si += this.GetSI(i);

            this.SetValue(si, 0, offset);
        }
        private void CalculateForAllData()
        {
            double si = this.GetSI();
            double prevSI = double.IsNaN(this.GetValue(1)) ? 0 : this.GetValue(1);

            // Set value to the 'SI' line buffer.
            this.SetValue(prevSI + si);
        }

        private double GetSI(int offset = 0)
        {
            double prevClose = this.Close(offset + 1);
            double prevOpen = this.Open(offset + 1);
            double close = this.Close(offset);
            double high = this.High(offset);
            double low = this.Low(offset);

            // Calculate the si value.
            double ER = 0;
            if (prevClose >= low && prevClose <= high)
                ER = 0;
            else
            {
                if (prevClose > high)
                    ER = Math.Abs(high - prevClose);
                if (prevClose < low)
                    ER = Math.Abs(low - prevClose);
            }

            double K = Math.Max(Math.Abs(high - prevClose), Math.Abs(low - prevClose));
            double SH = Math.Abs(prevClose - prevOpen);
            double R = this.atr.GetValue(offset) - 0.5 * ER + 0.25 * SH;

            double si = 0;
            if (Math.Abs(R) >= 0.000001)
                si = 50 * (close - prevClose + 0.5 * (close - this.Open()) + 0.25 * (prevClose - prevOpen)) * K / (this.Divider * this.Symbol.GetTickSize(close)) / R;

            return si;
        }
    }
}