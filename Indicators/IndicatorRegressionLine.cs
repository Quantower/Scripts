// Copyright QUANTOWER LLC. © 2017-2024. All rights reserved.

using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace MovingAverages;

public sealed class IndicatorRegressionLine : Indicator, IWatchlistIndicator
{
    // Displays Input Parameter as input field (or checkbox if value type is bolean).
    [InputParameter("Period of Linear Regression", 0, 1, 9999)]
    public int Period = 2;
    // Displays Input Parameter as dropdown list.
    [InputParameter("Sources prices for the regression line", 1, variants: new object[] {
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
    public override string ShortName => "REGRESSION " + this.Period;
    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorRegressionLine.cs";

    private int sumPeriod;

    /// <summary>
    /// Indicator's constructor. Contains general information: name, description, LineSeries etc. 
    /// </summary>
    public IndicatorRegressionLine()
        : base()
    {
        // Serves for an identification of related indicators with different parameters.
        this.Name = "Regression Line";
        this.Description = "Linear regression line used to measure trends";

        // Defines line on demand with particular parameters.
        this.AddLineSeries("Regression Line", Color.Blue, 1, LineStyle.Solid);
        this.SeparateWindow = false;
    }
    /// <summary>
    /// This function will be called after creating an indicator as well as after its input params reset or chart (symbol or timeframe) updates.
    /// </summary>
    protected override void OnInit()
    {
        this.sumPeriod = 0;
        for (int i = 0; i < this.Period; i++)
            this.sumPeriod += i;
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
        if (this.Count < this.MinHistoryDepths)
        {
            double price = this.GetPrice(this.SourcePrice);
            this.SetValue(price);
            return;
        }
        double sumPrices = 0.0;    // sum of prices
        double sumPeriod_Price = 0.0;  // sum of period*price  
        double sumSqr_Price = 0.0;   // sum of price sqr 

        // Calculation of sum
        for (int i = 0; i < this.Period; i++)
        {
            double price = this.GetPrice(this.SourcePrice, i);
            sumPrices += price;
            sumPeriod_Price += i * price;
            sumSqr_Price += price * price;
        }

        // Calculation of coefficients
        double p = (this.Period * sumPeriod_Price - this.sumPeriod * sumPrices) / (this.Period * sumSqr_Price - this.sumPeriod * this.sumPeriod);
        double b = (sumPrices * sumSqr_Price - this.sumPeriod * sumPeriod_Price) / (this.Period * sumSqr_Price - this.sumPeriod * this.sumPeriod);

        // Setting of current value
        this.SetValue(p * this.Period + b);
    }
}