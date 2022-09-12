// Copyright QUANTOWER LLC. © 2017-2022. All rights reserved.

using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace Channels
{
    public sealed class IndicatorPriceChannel : Indicator, IWatchlistIndicator
    {
        // Define 'Period' input parameter and set allowable range (from 1 to 999) 
        [InputParameter("Period of price channel", 0, 1, 999)]
        public int Period = 20;

        public int MinHistoryDepths => this.Period;
        public override string ShortName => $"Channel ({this.Period})";
        public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorPriceChannel.cs";

        /// <summary>
        /// Indicator's constructor. Contains general information: name, description, LineSeries etc. 
        /// </summary>
        public IndicatorPriceChannel()
            : base()
        {
            // Defines indicator's name and description.
            this.Name = "Price Channel";
            this.Description = "Based on measurement of min and max prices for the definite number of periods";

            // Define two lines (on main window) with particular parameters 
            this.AddLineSeries("Highest", Color.Red, 2, LineStyle.Solid);
            this.AddLineSeries("Lowest", Color.CadetBlue, 2, LineStyle.Solid);

            this.SeparateWindow = false;
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
            // Skip some period for correct calculation.
            if (this.Count < this.MinHistoryDepths)
                return;

            // Get the highest price on the interval
            int highestOffset = this.Highest(PriceType.High, 0, this.Period);
            double highestPrice = this.GetPrice(PriceType.High, highestOffset);

            // Get the lowest price on the interval
            int lowestOffset = this.Lowest(PriceType.Low, 0, this.Period);
            double lowestPrice = this.GetPrice(PriceType.Low, lowestOffset);

            // Set highestPrice to 'Highest' line buffer and lowestPrice to 'Lowest' line buffer.
            this.SetValue(highestPrice, 0);
            this.SetValue(lowestPrice, 1);
        }

        ///<summary>
        ///Get the highest price on an interval.
        ///</summary>
        ///<param name="priceType">Type of price</param>
        ///<param name="startOffset">Start position offset</param>
        ///<param name="count">The count of bars in an interval</param>
        ///<returns></returns>
        private int Highest(PriceType priceType, int startOffset, int count)
        {
            int maxValueOffset = startOffset;
            for (int i = 0; i < count; i++)
            {
                if (this.GetPrice(priceType, maxValueOffset) < this.GetPrice(priceType, startOffset + i))
                    maxValueOffset = startOffset + i;
            }
            return maxValueOffset;
        }

        ///<summary>
        ///Get the lowest price on an interval.
        ///</summary>
        ///<param name="priceType">Type of price</param>
        ///<param name="startOffset">Start position offset</param>
        ///<param name="count">The count of bars in an interval</param>
        ///<returns></returns>
        private int Lowest(PriceType priceType, int startOffset, int count)
        {
            int minValueOffset = startOffset;
            for (int i = 0; i < count; i++)
            {
                if (this.GetPrice(priceType, minValueOffset) > this.GetPrice(priceType, startOffset + i))
                    minValueOffset = startOffset + i;
            }
            return minValueOffset;
        }
    }
}