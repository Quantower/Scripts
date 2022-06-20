// Copyright QUANTOWER LLC. © 2017-2022. All rights reserved.

using TradingPlatform.BusinessLayer;
using System.Drawing;
using System.Linq;

namespace MovingAverages
{
    public sealed class Indicator3MASignal : Indicator, IWatchlistIndicator
    {
        //Defines input parameters as input fields     
        [InputParameter("Short Moving Average Period", 0, 1, 999, 1, 0)]
        public int ShortMaPeriod = 5;

        [InputParameter("Middle Moving Average Period", 1, 1, 999, 1, 0)]
        public int MiddleMaPeriod = 10;

        [InputParameter("Long Moving Average Period", 2, 1, 999, 1, 0)]
        public int LongMaPeriod = 25;

        [InputParameter("Amount of bars passed before opening position", 3, 1, 999, 1, 0)]
        public int BarsInterval = 1;

        private Indicator shortMa;
        private Indicator middleMa;
        private Indicator longMa;
        private int currentTrend;

        private const int UP = 1; // Up trend
        private const int DOWN = -1; // Down trend
        private const int NONE = 0; // No trend

        public int MinHistoryDepths => Enumerable.Max(new int[] { this.ShortMaPeriod, this.MiddleMaPeriod, this.LongMaPeriod });
        public override string ShortName => $"MAS3 ({this.ShortMaPeriod}:{this.MiddleMaPeriod}:{this.LongMaPeriod}:{this.BarsInterval})";

        /// <summary>
        /// Indicator's constructor. Contains general information: name, description, LineSeries etc. 
        /// </summary>
        public Indicator3MASignal()
        {
            // Defines indicator's name and description.
            this.Name = "3MASignal";
            this.Description = "Offers buy and sell signals according to intersections of three moving averages";

            // Define one line with particular parameters 
            this.AddLineSeries("3MASignal", Color.Orange, 5, LineStyle.Histogramm);

            this.SeparateWindow = true;
        }

        /// <summary>
        /// This function will be called after creating an indicator as well as after its input params reset or chart (symbol or timeframe) updates.
        /// </summary>
        protected override void OnInit()
        {
            this.currentTrend = NONE;

            // Get SMA indicators from built-in indicator collection 
            this.shortMa = Core.Indicators.BuiltIn.MA(this.ShortMaPeriod, PriceType.Close, MaMode.SMA);
            this.middleMa = Core.Indicators.BuiltIn.MA(this.MiddleMaPeriod, PriceType.Close, MaMode.SMA);
            this.longMa = Core.Indicators.BuiltIn.MA(this.LongMaPeriod, PriceType.Close, MaMode.SMA);

            this.AddIndicator(this.shortMa);
            this.AddIndicator(this.middleMa);
            this.AddIndicator(this.longMa);
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
            // Skip largest period for correct calculation.  
            if (this.Count < this.MinHistoryDepths)
                return;

            // Define the trend on the interval
            for (int shift = 0; shift < this.BarsInterval; shift++)
            {
                if (shift == 0)
                {
                    // Calculate the initial state
                    this.currentTrend = this.CompareMA(this.shortMa.GetValue(shift, 0), this.middleMa.GetValue(shift, 0), this.longMa.GetValue(shift, 0));
                }
                else
                {
                    int trend = this.CompareMA(this.shortMa.GetValue(shift, 0), this.middleMa.GetValue(shift, 0), this.longMa.GetValue(shift, 0));

                    if (trend != this.currentTrend)
                    {
                        this.currentTrend = NONE;
                        break;
                    }
                }
            }
            // Set price to the line buffer (line index is 0) by offset 0;
            this.SetValue(this.currentTrend);
        }

        /// <summary>
        /// Compare SMA values for define current trend
        /// </summary>
        /// <param name="shMa">ShortMA value</param>
        /// <param name="midMA">MiddleMa value</param>
        /// <param name="lgMa">LongMa value</param>
        /// <returns>Current trend (as int)</returns>
        private int CompareMA(double shMa, double midMA, double lgMa)
        {
            if (midMA > lgMa && midMA < shMa)
                return UP;
            else if (midMA > shMa && midMA < lgMa)
                return DOWN;

            return NONE;
        }
    }
}