// Copyright QUANTOWER LLC. © 2017-2022. All rights reserved.

using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace TrendIndicators
{
    public class IndicatorSuperTrend: Indicator, IWatchlistIndicator
    {
        #region Parameters
        [InputParameter("ATR period", 0, 1, 999, 1, 0)]
        public int AtrPeriod = 10;
        [InputParameter("Digit", 1, 0.01, 10, 0.01, 2)]
        public double Digit = 3;
        [InputParameter("Up trend color", 3)]
        public Color UpTrendColor = Color.Green;
        [InputParameter("Down trend color", 3)]
        public Color DownTrendColor = Color.Red;

        public override string ShortName => $"ST ({this.AtrPeriod}: {this.Digit})";

        public int MinHistoryDepths => this.AtrPeriod;

        private Indicator atrIndicator;
        private int prevTrend;
        private double prevDown;
        private double prevUP;
        private double down;
        private double up;
        private int currTrend;

        public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorSuperTrend.cs";

        #endregion Parameters

        public IndicatorSuperTrend()
        {
            this.Name = "SuperTrend";
            this.AddLineSeries("ST line", Color.DodgerBlue, 2, LineStyle.Solid);
            this.SeparateWindow = false;           
        }

        protected override void OnInit()
        {
            base.OnInit();
            this.atrIndicator = Core.Indicators.BuiltIn.ATR(this.AtrPeriod, MaMode.SMA);
            this.AddIndicator(this.atrIndicator);
        }

        protected override void OnUpdate(UpdateArgs args)
        {
            base.OnUpdate(args);

            if (this.Count < this.AtrPeriod)
                return;

            var isNewBar = this.HistoricalData.Period == Period.TICK1
                ? args.Reason == UpdateReason.NewTick || args.Reason == UpdateReason.HistoricalBar
                : args.Reason == UpdateReason.NewBar || args.Reason == UpdateReason.HistoricalBar;

            if (isNewBar)
            {
                this.prevDown = this.up;
                this.prevTrend = this.currTrend;
                this.prevUP = this.down;
            }

            var middle = (this.GetPrice(PriceType.High) + this.GetPrice(PriceType.Low)) / 2;
            this.down = middle + (this.Digit * this.atrIndicator.GetValue());
            this.up = middle - (this.Digit * this.atrIndicator.GetValue());

            if (this.GetPrice(PriceType.Close) > this.prevUP)
                this.currTrend = 1;
            else if (this.GetPrice(PriceType.Close) < this.prevDown)
                this.currTrend = -1;
            else
                this.currTrend = this.prevTrend;

            if (this.currTrend > 0 && up < this.prevDown && this.currTrend <= this.prevTrend)
                this.up = this.prevDown;
            if (this.currTrend < 0 && this.down > this.prevUP && this.currTrend >= this.prevTrend)
                this.down = this.prevUP;

            if (this.currTrend == 1)
            {
                this.SetValue(this.up);
                this.LinesSeries[0].SetMarker(0, this.UpTrendColor);
            }
            else
            {
                this.SetValue(this.down);
                this.LinesSeries[0].SetMarker(0, this.DownTrendColor);
            }
        }

        protected override void OnClear()
        {
            base.OnClear();
            if (this.atrIndicator != null)
            {
                this.RemoveIndicator(this.atrIndicator);
                this.atrIndicator.Dispose();
            }
        }
    }
}
