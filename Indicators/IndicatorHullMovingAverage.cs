// Copyright QUANTOWER LLC. © 2017-2022. All rights reserved.

using System;
using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace MovingAverageIndicators
{
    public class IndicatorHullMovingAverage : Indicator, IWatchlistIndicator
    {
        #region Parameters

        // Period of moving average. 
        [InputParameter("Period of Hull Moving Average", 10, 1, 9999, 1, 0)]
        public int MaPeriod = 9;

        // Price type of moving average. 
        [InputParameter("Sources prices for MA", 20, variants: new object[]
        {
            "Close", PriceType.Close,
            "Open", PriceType.Open,
            "High", PriceType.High,
            "Low", PriceType.Low,
            "Typical", PriceType.Typical,
            "Median", PriceType.Median,
            "Weighted", PriceType.Weighted,
            "Volume", PriceType.Volume,
            "Open interest", PriceType.OpenInterest
        })]
        public PriceType SourcePrice = PriceType.Close;

        public override string ShortName => $"HMA ({this.MaPeriod}; {this.SourcePrice})";

        private HistoricalDataCustom hullWMASource;

        private Indicator fullWMA;
        private Indicator halfWMA;
        private Indicator wma;

        public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorHullMovingAverage.cs";

        #endregion Parameters

        #region IWatchlistIndicator

        public int MinHistoryDepths => this.MaPeriod;

        #endregion IWatchlistIndicator

        public IndicatorHullMovingAverage()
        {
            this.Name = "Hull Moving Average";
            this.AddLineSeries("HMA", Color.Orange, 2, LineStyle.Dash);
            this.SeparateWindow = false;
        }

        #region Overrides

        protected override void OnInit()
        {
            this.fullWMA = Core.Instance.Indicators.BuiltIn.LWMA(this.MaPeriod, this.SourcePrice);
            this.halfWMA = Core.Instance.Indicators.BuiltIn.LWMA(this.MaPeriod / 2, this.SourcePrice);
            this.wma = Core.Instance.Indicators.BuiltIn.LWMA((int)Math.Floor(Math.Sqrt(this.MaPeriod)), this.SourcePrice);

            this.hullWMASource = new HistoricalDataCustom(this);
            this.hullWMASource.AddIndicator(this.wma);

            this.AddIndicator(this.fullWMA);
            this.AddIndicator(this.halfWMA);
        }
        protected override void OnUpdate(UpdateArgs args)
        {
            if (this.Count < this.MaPeriod)
                return;

            this.hullWMASource.SetValue(0d, 0d, 0d, 2 * this.halfWMA.GetValue() - this.fullWMA.GetValue());
            this.SetValue(this.wma.GetValue());
        }
        protected override void OnClear()
        {
            if (this.hullWMASource != null)
            {
                this.hullWMASource.RemoveIndicator(this.wma);
                this.hullWMASource.Dispose();
            }

            if (this.fullWMA != null)
                this.RemoveIndicator(this.fullWMA);

            if (this.halfWMA != null)
                this.RemoveIndicator(this.halfWMA);
        }

        #endregion Overrides
    }
}
