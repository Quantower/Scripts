// Copyright QUANTOWER LLC. © 2017-2022. All rights reserved.

using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace OscillatorsIndicators
{
    public class IndicatorBandas : Indicator
    {
        #region Parameters
        [InputParameter("Period", 10, 1, 99999, 1, 0)]
        public int Period = 52;

        [InputParameter("Distance", 20, 0.1, 99999, 0.1, 1)]
        public double Distance = 3.5;

        [InputParameter("Sources prices", 30, variants: new object[]{
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

        [InputParameter("Type of middle moving average", 40, variants: new object[]{
            "Simple Moving Average", MaMode.SMA,
            "Exponential Moving Average", MaMode.EMA,
            "Smoothed Moving Average", MaMode.SMMA,
            "Linearly Weighted Moving Average", MaMode.LWMA,
        })]
        public MaMode MaType = MaMode.EMA;

        public override string ShortName => $"{this.Name} ({this.Period}: {this.Distance})";

        private HistoricalDataCustom customHD;
        private Indicator diffMA;
        private Indicator middleMA;

        public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorBandas.cs";

        #endregion Parameters

        public IndicatorBandas()
        {
            this.Name = "Bandas";

            this.AddLineSeries("Middle", Color.Orange, 1, LineStyle.Solid);
            this.AddLineSeries("Upper", Color.DodgerBlue, 1, LineStyle.Solid);
            this.AddLineSeries("Lower", Color.DodgerBlue, 1, LineStyle.Solid);

            this.SeparateWindow = false;
        }

        #region Overrides
        protected override void OnInit()
        {
            this.customHD = new HistoricalDataCustom(this);
            this.diffMA = Core.Indicators.BuiltIn.EMA(this.Period, this.SourcePrice);
            this.customHD.AddIndicator(this.diffMA);

            this.middleMA = Core.Indicators.BuiltIn.MA(this.Period, PriceType.Typical, this.MaType);
            this.AddIndicator(this.middleMA);
        }
        protected override void OnUpdate(UpdateArgs args)
        {
            var diff = this.High() - this.Low();
            this.customHD.SetValue(0, 0, 0, diff);

            if (this.Count < this.Period)
                return;

            var middle = this.middleMA.GetValue();
            var offset = this.diffMA.GetValue() * this.Distance;

            this.SetValue(middle, 0);
            this.SetValue(middle + offset, 1);
            this.SetValue(middle - offset, 2);
        }
        protected override void OnClear()
        {
            this.customHD.RemoveIndicator(this.diffMA);
            this.customHD.Dispose();
            this.diffMA.Dispose();

            this.RemoveIndicator(this.middleMA);
            this.middleMA.Dispose();
        }
        #endregion Overrides

    }
}
