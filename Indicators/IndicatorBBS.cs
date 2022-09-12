// Copyright QUANTOWER LLC. © 2017-2022. All rights reserved.

using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace ChanneIsIndicators
{
    public class IndicatorBBS : Indicator
    {
        [InputParameter("Fast period", 10, 1, 99999, 1, 0)]
        public int FastPeriod = 12;

        [InputParameter("Slow period", 20, 1, 99999, 1, 0)]
        public int SlowPeriod = 26;

        [InputParameter("Smooth period", 30, 1, 99999, 1, 0)]
        public int SmoothPeriod = 5;

        [InputParameter("Period", 10, 1, 99999, 1, 0)]
        public int Period = 10;

        [InputParameter("STD num", 10, 1, 99999, 1, 0)]
        public int STDNum = 1;

        public override string ShortName => $"{this.Name} ({this.FastPeriod}: {this.SlowPeriod}: {this.SmoothPeriod}: {this.Period}: {this.STDNum})";

        private double c1;
        private double c2;
        private double c3;
        private double c4;

        private double fastValue;
        private double slowValue;
        private double prevFastValue;
        private double prevSlowValue;

        public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorBBS.cs";

        public IndicatorBBS()
        {
            this.Name = "BBS";

            this.AddLineSeries("BBSNUM", Color.Orange, 1, LineStyle.Points);

            this.AddLineLevel(0.3, "Up level", Color.Gray, 1, LineStyle.Dot);
            this.AddLineLevel(0, "Middle level", Color.Gray, 1, LineStyle.Dot);
            this.AddLineLevel(-0.3, "Down level", Color.Gray, 1, LineStyle.Dot);

            this.SeparateWindow = true;
        }

        protected override void OnInit()
        {
            this.c1 = 2.0 / (1 + this.FastPeriod);
            this.c2 = 1 - this.c1;
            this.c3 = 2.0 / (1 + this.SlowPeriod);
            this.c4 = 1 - this.c3;
        }
        protected override void OnUpdate(UpdateArgs args)
        {
            double price = this.Close();

            if (this.Count == 1)
            {
                this.fastValue = price;
                this.slowValue = price;

                this.SetValue(0);
            }
            else
            {
                if (args.Reason == UpdateReason.HistoricalBar || args.Reason == UpdateReason.NewBar)
                {
                    this.prevFastValue = this.fastValue;
                    this.prevSlowValue = this.slowValue;
                }

                this.fastValue = this.c1 * price + this.c2 * this.prevFastValue;
                this.slowValue = this.c3 * price + this.c4 * this.prevSlowValue;

                double bbsnum = this.fastValue - this.slowValue;

                this.SetValue(bbsnum);
            }
        }
        protected override void OnClear()
        {
            this.prevFastValue = 0;
            this.prevSlowValue = 0;
        }
    }
}
