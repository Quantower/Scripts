// Copyright QUANTOWER LLC. © 2017-2022. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using TradingPlatform.BusinessLayer;

namespace OscillatorsIndicators
{
    public class IndicatorSeparatedAverageTrueRange : Indicator
    {
        #region Parameters
        private const string HISTOGRAM_COLORS_SETTING_NAME = "HistogramColor";
        private const string PERIOD_SETTING_NAME = "Period of Moving Average";

        [InputParameter(PERIOD_SETTING_NAME, 0, 1, 999, 1, 0)]
        public int Period = 14;

        [InputParameter("Type of Moving Average", 1, variants: new object[] {
             "Simple", MaMode.SMA,
             "Exponential", MaMode.EMA,
             "Smoothed", MaMode.SMMA,
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

        private HistoricalDataCustom customHD1;
        private HistoricalDataCustom customHD2;
        private Indicator atr1;
        private Indicator atr2;
        private Color upHistogramColor;
        private Color downHistogramColor;

        public override string ShortName => $"SATR ({this.Period}: {this.MAType})";

        public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorSeparatedAverageTrueRange.cs";

        #endregion Parameters

        public IndicatorSeparatedAverageTrueRange()
        {
            this.Name = "Separated Average True Range";

            this.upHistogramColor = Color.FromArgb(0, 178, 89);
            this.downHistogramColor = Color.FromArgb(251, 87, 87);

            this.AddLineSeries("Buy ATR", this.upHistogramColor, 2, LineStyle.Solid);
            this.AddLineSeries("Sell ATR", this.downHistogramColor, 2, LineStyle.Solid);
            this.AddLineSeries("Delta", Color.DodgerBlue, 2, LineStyle.Histogramm);

            this.LinesSeries[0].Visible = this.LinesSeries[1].Visible = false;

            this.SeparateWindow = true;
        }

        protected override void OnInit()
        {
            base.OnInit();

            this.customHD1 = new HistoricalDataCustom();
            this.atr1 = Core.Indicators.BuiltIn.ATR(this.Period, this.MAType, this.CalculationType);
            this.customHD1.AddIndicator(this.atr1);

            this.customHD2 = new HistoricalDataCustom();
            this.atr2 = Core.Indicators.BuiltIn.ATR(this.Period, this.MAType, this.CalculationType);
            this.customHD2.AddIndicator(this.atr2);
        }

        public override IList<SettingItem> Settings
        {
            get
            {
                var settings = base.Settings;

                var separatorGroup = settings.GetItemByName(PERIOD_SETTING_NAME)?.SeparatorGroup ?? new SettingItemSeparatorGroup("");
                settings.Add(new SettingItemPairColor(HISTOGRAM_COLORS_SETTING_NAME, new PairColor(this.upHistogramColor, this.downHistogramColor, loc._("Up"), loc._("Down")), 10)
                {
                    Text = loc._("Histogram style"),
                    SeparatorGroup = separatorGroup
                });
                return settings;
            }
            set
            {
                if(value.GetItemByName(HISTOGRAM_COLORS_SETTING_NAME) is SettingItemPairColor pairColorSI && pairColorSI.Value is PairColor colors)
                {
                    this.upHistogramColor = colors.Color1;
                    this.downHistogramColor = colors.Color2;

                    this.Refresh();
                }

                base.Settings = value;
            }
        }

        protected override void OnUpdate(UpdateArgs args)
        {
            if(this.customHD1.Count == 0 || (!double.IsNaN(this.customHD1[PriceType.Close]) && args.Reason != UpdateReason.NewTick))
                this.customHD1.AddValue(double.NaN, double.NaN, double.NaN, double.NaN);

            if (this.customHD2.Count == 0 || (!double.IsNaN(this.customHD2[PriceType.Close]) && args.Reason != UpdateReason.NewTick))
                this.customHD2.AddValue(double.NaN, double.NaN, double.NaN, double.NaN);

            if (this.Open() < this.Close())
            {
                this.customHD1.SetValue(this.Open(), this.High(), this.Low(), this.Close());
                this.customHD2.SetValue(double.NaN, double.NaN, double.NaN, double.NaN);
            }
            else
            {
                this.customHD2.SetValue(this.Open(), this.High(), this.Low(), this.Close());
                this.customHD1.SetValue(double.NaN, double.NaN, double.NaN, double.NaN);
            }

            if (this.Count < this.Period)
                return;

            var v1 = double.IsNaN(this.atr1.GetValue()) ? this.atr1.GetValue(1) : this.atr1.GetValue();
            var v2 = double.IsNaN(this.atr2.GetValue()) ? this.atr2.GetValue(1) : this.atr2.GetValue();

            this.SetValue(v1, 0);
            this.SetValue(v2, 1);

            var delta = v1 - v2;
            this.SetValue(delta, 2);

            if (delta > 0)
                this.LinesSeries[2].SetMarker(0, this.upHistogramColor);
            else
                this.LinesSeries[2].SetMarker(0, this.downHistogramColor);
        }
    }
}

