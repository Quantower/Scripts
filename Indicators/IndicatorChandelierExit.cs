// Copyright QUANTOWER LLC. © 2017-2024. All rights reserved.

using System;
using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace IndicatorChandelierExit
{
    public class IndicatorChandelierExit : Indicator
    {
        [InputParameter("ATR Period", 0, 1, 9999)]
        public int Period = 22;
        [InputParameter("ATR Multiplier", 0, 1, 9999)]
        public double multipl = 3;
        [InputParameter("Mode selection", 1, variants: new object[]{
            "Trailing line", 1,
            "ATR lines", 2 })]
        public int mods = 1;

        public override string ShortName => $"CE ({this.Period}; {this.multipl})";

        private double longStop, longStopPrev, shortStop, shortStopPrev;
        private int dir;

        private Indicator atr;

        public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorChandelierExit.cs";
        public IndicatorChandelierExit()
            : base()
        {
            Name = "Chandelier Exit";
            Description = "The Chandelier Exit is a popular tool among traders used to help determine appropriate stop loss levels.";

            AddLineSeries("Sell", Color.Red, 2, LineStyle.Solid);
            AddLineSeries("Buy", Color.Green, 2, LineStyle.Solid);

            SeparateWindow = false;
        }

        protected override void OnInit()
        {
            this.atr = Core.Indicators.BuiltIn.ATR(this.Period, MaMode.SMA, Indicator.DEFAULT_CALCULATION_TYPE);
            this.AddIndicator(this.atr);
            dir = 1;
            longStop = High() - atr.GetValue() * multipl;
            shortStop = Low() + atr.GetValue() * multipl;
        }

        protected override void OnUpdate(UpdateArgs args)
        {
            longStopPrev = longStop;
            longStop = High() - atr.GetValue() * multipl;

            shortStopPrev = shortStop;
            shortStop = Low() + atr.GetValue() * multipl;
            if (mods==1&&GetPrice(PriceType.Close) > longStopPrev)
                longStop = Math.Max(longStop, longStopPrev);
            if (mods == 1&&GetPrice(PriceType.Close) < shortStopPrev)
                shortStop = Math.Min(shortStop, shortStopPrev);
            if (GetPrice(PriceType.Close) > shortStopPrev)
                dir = 1;
            else if (GetPrice(PriceType.Close) < longStopPrev)
                dir = -1;
            if (mods == 2)
            {
                SetValue(longStop, 1);
                SetValue(shortStop, 0);
            }
            else
            {
                if (dir == 1)
                    SetValue(longStop, 1);
                else
                    SetValue(shortStop, 0);
            }
        }
    }
}