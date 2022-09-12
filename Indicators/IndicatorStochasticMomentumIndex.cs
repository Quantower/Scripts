// Copyright QUANTOWER LLC. © 2017-2022. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using TradingPlatform.BusinessLayer;

namespace OscillatorsIndicators
{
    public class IndicatorStochasticMomentumIndex: Indicator, IWatchlistIndicator
    {
        #region Parameters

        [InputParameter("SMI period", 0, 1, 99999, 1,0)]
        public int SmiPeriod = 14;
        [InputParameter("Smoothing period", 1, 1, 99999, 1, 0)]
        public int SmoothPeriod1 = 3;
        [InputParameter("Double smoothing period", 2, 1, 99999, 1, 0)]
        public int SmoothPeriod2 = 2;
        [InputParameter("Signal period", 3, 1, 99999, 1, 0)]
        public int SignalPeriod = 3;
        [InputParameter("Source price", 4, variants: new object[]
        {
            "Close", PriceType.Close,
            "Open", PriceType.Open,
            "High", PriceType.High,
            "Low", PriceType.Low,
            "Typical", PriceType.Typical,
            "Median", PriceType.Median,
            "Weighted", PriceType.Weighted
        })]
        public PriceType SourcePrice = PriceType.Close;
        [InputParameter("Up trend color", 5)]
        public Color UpTrendColor = Color.DodgerBlue;
        [InputParameter("Down trend color", 6)]
        public Color DownTrendColor = Color.Orange;
        //
        [InputParameter("Calculation type", 10, variants: new object[]
        {
            "All available data", IndicatorCalculationType.AllAvailableData,
            "By period", IndicatorCalculationType.ByPeriod,
        })]
        public IndicatorCalculationType CalculationType = Indicator.DEFAULT_CALCULATION_TYPE;

        private MinMaxBuffer maxHighPriceService;
        private MinMaxBuffer minLowPriceService;

        private HistoricalDataCustom hd11;
        private Indicator ema11;

        private HistoricalDataCustom hd12;
        private Indicator ema12;

        private HistoricalDataCustom hd21;
        private Indicator ema21;

        private HistoricalDataCustom hd22;
        private Indicator ema22;

        private HistoricalDataCustom signalHd;
        private Indicator signalEma;

        public override string ShortName => $"SMI ({this.SmiPeriod}: {this.SmoothPeriod1}: {this.SmoothPeriod2}: {this.SignalPeriod}: {this.SourcePrice})";

        public int MinHistoryDepths => this.SmiPeriod + this.SmoothPeriod1 + this.SmoothPeriod2 + this.SignalPeriod;

        public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorStochasticMomentumIndex.cs";

        #endregion Parameters

        public IndicatorStochasticMomentumIndex()
        {
            this.Name = "Stochastic Momentum Index";

            this.AddLineSeries("SMI", Color.Gray, 2, LineStyle.Solid);
            this.AddLineSeries("SMI signal", Color.FromArgb(236,64, 122), 2, LineStyle.Dash);

            this.AddLineLevel(40d, "Up level", Color.DimGray, 1, LineStyle.DashDot);
            this.AddLineLevel(0d, "Zero level", Color.DimGray, 1, LineStyle.DashDot);
            this.AddLineLevel(-40d, "Down level", Color.DimGray, 1, LineStyle.DashDot);

            this.SeparateWindow = true;
        }

        protected override void OnInit()
        {
            base.OnInit();
            this.maxHighPriceService = new MinMaxBuffer(this.SmiPeriod);
            this.minLowPriceService = new MinMaxBuffer(this.SmiPeriod);

            this.hd11 = new HistoricalDataCustom(this);
            this.ema11 = Core.Indicators.BuiltIn.EMA(this.SmoothPeriod1, PriceType.Close, this.CalculationType);
            this.hd11.AddIndicator(this.ema11);

            this.hd12 = new HistoricalDataCustom(this);
            this.ema12 = Core.Indicators.BuiltIn.EMA(this.SmoothPeriod2, PriceType.Close, this.CalculationType);
            this.hd12.AddIndicator(this.ema12);

            this.hd21 = new HistoricalDataCustom(this);
            this.ema21 = Core.Indicators.BuiltIn.EMA(this.SmoothPeriod1, PriceType.Close, this.CalculationType);
            this.hd21.AddIndicator(this.ema21);

            this.hd22 = new HistoricalDataCustom(this);
            this.ema22 = Core.Indicators.BuiltIn.EMA(this.SmoothPeriod2, PriceType.Close, this.CalculationType);
            this.hd22.AddIndicator(this.ema22);

            this.signalHd = new HistoricalDataCustom(this);
            this.signalEma = Core.Indicators.BuiltIn.EMA(this.SignalPeriod, PriceType.Close, this.CalculationType);
            this.signalHd.AddIndicator(this.signalEma);
        }

        protected override void OnUpdate(UpdateArgs args)
        {
            base.OnUpdate(args);

            var isNewBar = this.HistoricalData.Period == Period.TICK1
                ? args.Reason == UpdateReason.NewTick || args.Reason == UpdateReason.HistoricalBar
                : args.Reason == UpdateReason.NewBar || args.Reason == UpdateReason.HistoricalBar;

            this.maxHighPriceService.UpdateValue(this.GetPrice(PriceType.High), isNewBar);
            this.minLowPriceService.UpdateValue(this.GetPrice(PriceType.Low), isNewBar);

            if (this.Count < this.SmiPeriod)
                return;

            var price = this.GetPrice(this.SourcePrice);

            this.hd11[PriceType.Close] = price - 0.5 * (this.maxHighPriceService.Max + this.minLowPriceService.Min);
            this.hd21[PriceType.Close] = this.maxHighPriceService.Max - this.minLowPriceService.Min;

            if (this.Count < this.SmiPeriod + this.SmoothPeriod1)
                return;

            this.hd12[PriceType.Close] = this.ema11.GetValue();
            this.hd22[PriceType.Close] = this.ema21.GetValue();

            if (this.Count < this.SmiPeriod + this.SmoothPeriod1 + this.SmoothPeriod2)
                return;

            var ema22Value = this.ema22.GetValue();

            if (ema22Value == 0 || double.IsNaN(ema22Value))
                return;

            var value = 100 * this.ema12.GetValue() / (0.5 * ema22Value);
            this.signalHd[PriceType.Close] = value;
            this.SetValue(value);

            if (this.Count < this.MinHistoryDepths)
                return;

            var signalValue = this.signalEma.GetValue();
            this.SetValue(signalValue, 1);

            if (value > signalValue)
                this.LinesSeries[0].SetMarker(0, this.UpTrendColor);
            else if (value < signalValue)
                this.LinesSeries[0].SetMarker(0, this.DownTrendColor);
        }
    }
    
    #region Utils
    public class MinMaxBuffer
    {
        public readonly List<double> values;
        public int Range { get; private set; }

        public double Min { get; private set; }
        public double Max { get; private set; }

        public MinMaxBuffer(int range)
        {
            this.Range = range;
            this.Min = double.NaN;
            this.Max = double.NaN;
            this.values = new List<double>();
        }

        public void UpdateValue(double value, bool isNewbar)
        {
            if (isNewbar)
            {
                if (this.values.Count == this.Range)
                    this.values.RemoveAt(this.values.Count - 1);

                if (this.values.Count > 0)
                {
                    this.Max = this.values.Max();
                    this.Min = this.values.Min();
                }
                else
                {
                    this.Max = value;
                    this.Min = value;
                }

                this.values.Insert(0, double.NaN);
            }

            this.values[0] = value;
            this.Max = Math.Max(value, this.Max);
            this.Min = Math.Min(value, this.Min);
        }

        internal void Clear() => this.values.Clear();
    }
    #endregion Utils
}
