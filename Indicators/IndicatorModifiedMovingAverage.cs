// Copyright QUANTOWER LLC. © 2017-2024. All rights reserved.

using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace MovingAverages;

/// <summary>
/// Modified Moving Average comprises a sloping factor to help it overtake with the growing or declining value of the trading price of the currency.
/// </summary>
public sealed class IndicatorModifiedMovingAverage : Indicator, IWatchlistIndicator
{
    // Displays Input Parameter as input field (or checkbox if value type is bolean).
    [InputParameter("Period of Modified Moving Average", 0, 1, 9999)]
    public int Period = 20;

    // Displays Input Parameter as dropdown list.
    [InputParameter("Sources prices for MA", 1, variants: new object[] {
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
    public override string ShortName => $"MMA ({this.Period}: {this.SourcePrice})";
    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorModifiedMovingAverage.cs";

    // Calculation coefficient.
    private double coeff;

    /// <summary>
    /// Indicator's constructor. Contains general information: name, description, LineSeries etc. 
    /// </summary>
    public IndicatorModifiedMovingAverage()
        : base()
    {
        // Defines indicator's group, name and description.            
        this.Name = "Modified Moving Average";
        this.Description = "Modified Moving Average comprises a sloping factor to help it overtake with the growing or declining value of the trading price of the currency.";

        // Defines line on demand with particular parameters.
        this.AddLineSeries("MMA", Color.DodgerBlue, 1, LineStyle.Solid);

        this.SeparateWindow = false;
    }

    /// <summary>
    /// This function will be called after creating an indicator as well as after its input params reset or chart (symbol or timeframe) updates.
    /// </summary>
    protected override void OnInit()
    {
        //coefficient calculating
        this.coeff = 1.0d / this.Period;
    }

    /// <summary>
    /// Calculation entry point. This function is called when a price data updates. 
    /// Will be running under the HistoricalBar mode during history loading. 
    /// Under NewTick during real time. 
    /// Under NewBar if start of the new bar is required.
    /// </summary>
    /// <param name="args">Provides data of updating reason and incoming price.</param>
    protected override void OnUpdate(UpdateArgs args)
    {
        // Checking, if current amount of bars less, than period of moving average - calculation is impossible.
        if (this.Count < this.MinHistoryDepths)
            return;

        // Value calculation.
        double mma = this.GetPrice(this.SourcePrice);

        for (int i = 0; i < this.Period; i++)
        {
            double price = this.GetPrice(this.SourcePrice, i);
            mma = price * this.coeff + mma * (1.0 - this.coeff);
        }

        // Displaying value on the chart.
        this.SetValue(mma);
    }
}