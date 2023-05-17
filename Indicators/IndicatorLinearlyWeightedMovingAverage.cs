// Copyright QUANTOWER LLC. © 2017-2023. All rights reserved.

using TradingPlatform.BusinessLayer;
using System.Drawing;

namespace MovingAverages;

public sealed class IndicatorLinearlyWeightedMovingAverage : Indicator, IWatchlistIndicator
{
    // Displays Input Parameter as dropdown list.
    [InputParameter("Period of LWMA", 0, 1, 9999, 1, 0)]
    public int Period = 20;

    // Displays Input Parameter as dropdown list.
    [InputParameter("Sources prices for LWMA", 1, variants: new object[] {
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

    public int MinHistoryDepths => this.Period;
    public override string ShortName => $"LWMA ({this.Period}: {this.SourcePrice})";
    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorLinearlyWeightedMovingAverage.cs";

    /// <summary>
    /// Indicator's constructor. Contains general information: name, description, LineSeries etc. 
    /// </summary>
    public IndicatorLinearlyWeightedMovingAverage()
        : base()
    {
        // Serves for an identification of related indicators with different parameters.
        this.Name = "Linearly Weighted Moving Average";
        this.Description = "The linear average price for the last N periods";
        // Defines line on demand with particular parameters.
        this.AddLineSeries("LWMA line", Color.Blue, 1, LineStyle.Solid);

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
        if (this.Count > this.MinHistoryDepths)
        {
            double numerator = 0.0;
            double denominator = 0.0;

            for (int i = 0; i < this.Period; i++)
            {
                int weight = this.Period - i;
                // Calculation of a coefficient
                numerator += weight * this.GetPrice(this.SourcePrice, i);
                // Calculation of a coefficient
                denominator += weight;
            }
            this.SetValue(denominator != 0D ? numerator / denominator : 0D);
        }
        else
        {
            double price = this.GetPrice(this.SourcePrice);
            this.SetValue(price);
        }
    }
}