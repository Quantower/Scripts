// Copyright QUANTOWER LLC. © 2017-2022. All rights reserved.

using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace BarsDataIndicators
{
    public class IndicatorOpenInterest : Indicator, IWatchlistIndicator
    {
        [InputParameter("Smooth period", 10, 1, int.MaxValue, 1, 0)]
        public int MaPeriod = 10;

        public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorOpenInterest.cs";

        public override string ShortName
        {
            get
            {
                string name = this.Name;
                if (this.LinesSeries[1].Visible)
                    name += $" ({this.MaPeriod})";

                return name;
            }
        }

        public int MinHistoryDepths => this.MaPeriod;

        private Indicator sma;

        public IndicatorOpenInterest()
            : base()
        {
            // Defines indicator's group, name and description.
            this.Name = "Open Interest";
            this.Description = "The total number of outstanding derivative contracts";

            // Defines line on demand with particular parameters.
            this.AddLineSeries("OpenInterest", Color.Green, 2, LineStyle.Solid);
            this.AddLineSeries("Smooth OI", Color.Orange, 2, LineStyle.Solid);
            this.SeparateWindow = true;
        }

        protected override void OnInit()
        {
            this.sma = Core.Indicators.BuiltIn.SMA(this.MaPeriod, PriceType.OpenInterest);
            this.AddIndicator(this.sma);
        }

        protected override void OnUpdate(UpdateArgs args)
        {
            base.OnUpdate(args);

            if (this.Count > 0 && this.HistoricalData.Count >= this.Count)
            {
                double openInterest = this.OpenInterest();
                this.SetValue(openInterest);

                if (this.Count > this.MaPeriod)
                    this.SetValue(this.sma.GetValue(), 1, 0);
            }
        }

        protected override void OnClear()
        {
            if (this.sma != null)
            {
                this.RemoveIndicator(this.sma);
                this.sma?.Dispose();
            }
        }
    }
}