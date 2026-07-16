// Copyright QUANTOWER LLC. © 2017-2025. All rights reserved.
using System;
using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace TrendIndicators
{
    public class IndicatorMAMA : Indicator
    {
        [InputParameter("Source price", 0, variants: new object[] {
            "Close",   PriceType.Close,
            "Open",    PriceType.Open,
            "High",    PriceType.High,
            "Low",     PriceType.Low,
            "Typical", PriceType.Typical,
            "Median",  PriceType.Median,
            "Weighted",PriceType.Weighted
        })]
        public PriceType SourcePrice = PriceType.Close;

        [InputParameter("Fast Limit", 1, 0.01, 1.0, 0.01, 2)]
        public double FastLimit = 0.5;

        [InputParameter("Slow Limit", 2, 0.001, 0.5, 0.001, 3)]
        public double SlowLimit = 0.05;

        private double previousPeriod = 6.0;
        private double previousSmoothPeriod = 6.0;

        public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorMAMA.cs";

        public IndicatorMAMA()
        {
            this.Name = "MESA Adaptive Moving Average";
            this.SeparateWindow = false;

            this.AddLineSeries("MAMA", Color.Red, 1, LineStyle.Solid);
            this.AddLineSeries("FAMA", Color.CadetBlue, 1, LineStyle.Solid);
        }
        public override string ShortName => $"MAMA ({this.FastLimit} : {this.SlowLimit})";
        protected override void OnUpdate(UpdateArgs args)
        {
            if (this.Count < 25)
                return;

            double ReDiscSm = 0.2 * this.ReDisc(0) + 0.8 * this.ReDisc(1);
            double ImDiscSm = 0.2 * this.ImDisc(0) + 0.8 * this.ImDisc(1);

            double currentPeriod = this.previousPeriod;
            if (ImDiscSm != 0.0 || ReDiscSm != 0.0)
            {
                double phaseDeg = Math.Atan2(ImDiscSm, ReDiscSm) * 180.0 / Math.PI;
                if (phaseDeg != 0.0)
                    currentPeriod = 360.0 / phaseDeg;
            }

            if (currentPeriod > 1.5 * this.previousPeriod) currentPeriod = 1.5 * this.previousPeriod;
            if (currentPeriod < 0.67 * this.previousPeriod) currentPeriod = 0.67 * this.previousPeriod;
            if (currentPeriod < 6.0) currentPeriod = 6.0;
            if (currentPeriod > 50.0) currentPeriod = 50.0;
            currentPeriod = 0.2 * currentPeriod + 0.8 * this.previousPeriod;

            double smoothPeriod = 0.33 * this.previousPeriod + 0.67 * this.previousSmoothPeriod;

            double phase0 = this.Phase(0);
            double phase1 = this.Phase(1);
            double deltaPhase = phase1 - phase0;
            if (deltaPhase < 1.0) deltaPhase = 1.0;

            double alpha = this.FastLimit / deltaPhase;
            if (alpha < this.SlowLimit) alpha = this.SlowLimit;

            double price = this.GetPrice(this.SourcePrice);

            double prevMAMA = this.GetValue(1, 0);
            if (double.IsNaN(prevMAMA)) prevMAMA = price;

            double MAMA = alpha * price + (1.0 - alpha) * prevMAMA;
            this.SetValue(MAMA, 0); 

            double prevFAMA = this.GetValue(1, 1);
            if (double.IsNaN(prevFAMA)) prevFAMA = price;

            double FAMA = 0.5 * alpha * MAMA + (1.0 - 0.5 * alpha) * prevFAMA;
            this.SetValue(FAMA, 1);

            this.previousPeriod = currentPeriod;
            this.previousSmoothPeriod = smoothPeriod;
        }

        private double Smooth(int index = 0)
            => (4.0 * this.GetPrice(this.SourcePrice, index)
              + 3.0 * this.GetPrice(this.SourcePrice, index + 1)
              + 2.0 * this.GetPrice(this.SourcePrice, index + 2)
              + 1.0 * this.GetPrice(this.SourcePrice, index + 3)) / 10.0;

        private double Detrender(int index = 0)
        {
            double k = 0.075 * this.previousPeriod + 0.54;
            return (0.0962 * this.Smooth(index)
                  + 0.5769 * this.Smooth(index + 2)
                  - 0.5769 * this.Smooth(index + 4)
                  - 0.0962 * this.Smooth(index + 6)) * k;
        }

        private double Quadrature(int index = 0)
        {
            double k = 0.075 * this.previousPeriod + 0.54;
            return (0.0962 * this.Detrender(index)
                  + 0.5769 * this.Detrender(index + 2)
                  - 0.5769 * this.Detrender(index + 4)
                  - 0.0962 * this.Detrender(index + 6)) * k;
        }

        private double InPhase(int index = 0) => this.Detrender(index + 3);

        private double HilbertOfInPhase(int index = 0)
        {
            double k = 0.075 * this.previousPeriod + 0.54;
            return (0.0962 * this.InPhase(index)
                  + 0.5769 * this.InPhase(index + 2)
                  - 0.5769 * this.InPhase(index + 4)
                  - 0.0962 * this.InPhase(index + 6)) * k;
        }

        private double HilbertOfQuadrature(int index = 0)
        {
            double k = 0.075 * this.previousPeriod + 0.54;
            return (0.0962 * this.Quadrature(index)
                  + 0.5769 * this.Quadrature(index + 2)
                  - 0.5769 * this.Quadrature(index + 4)
                  - 0.0962 * this.Quadrature(index + 6)) * k;
        }

        private double I2(int index = 0) => this.InPhase(index) - this.HilbertOfQuadrature(index);
        private double Q2(int index = 0) => this.Quadrature(index) + this.HilbertOfInPhase(index);

        private double SmoothedI2(int index = 0) => 0.2 * this.I2(index) + 0.8 * this.I2(index + 1);
        private double SmoothedQ2(int index = 0) => 0.2 * this.Q2(index) + 0.8 * this.Q2(index + 1);

        private double ReDisc(int index = 0)
            => this.SmoothedI2(index) * this.SmoothedI2(index + 1) + this.SmoothedQ2(index) * this.SmoothedQ2(index + 1);

        private double ImDisc(int index = 0)
            => this.SmoothedI2(index) * this.SmoothedQ2(index + 1) - this.SmoothedQ2(index) * this.SmoothedI2(index + 1);

        private double Phase(int index = 0)
        {
            double i = this.InPhase(index);
            double q = this.Quadrature(index);
            if (i == 0.0 && q == 0.0)
                return 0.0;
            return Math.Atan2(q, i) * 180.0 / Math.PI;
        }
    }

}
