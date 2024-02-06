// Copyright QUANTOWER LLC. © 2017-2024. All rights reserved.

using System;
using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace Volatility;

/// <summary>
/// Shows the difference of the volatility value from the average one.
/// </summary>
public sealed class IndicatorStandardDeviation : Indicator, IWatchlistIndicator
{
    public override string HelpLink => "https://help.quantower.com/analytics-panels/chart/technical-indicators/volatility/standard-deviation";

    // Displays Input Parameter as dropdown list.
    [InputParameter("Sources prices for MA", 0, variants: new object[] {
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

    [InputParameter("Type of Moving Average", 1, variants: new object[] {
         "Simple", MaMode.SMA,
         "Exponential", MaMode.EMA,
         "Modified", MaMode.SMMA,
         "Linear Weighted", MaMode.LWMA}
    )]
    public MaMode MAType = MaMode.SMA;
    //
    [InputParameter("Calculation type", 5, variants: new object[]
    {
        "All available data", IndicatorCalculationType.AllAvailableData,
        "By period", IndicatorCalculationType.ByPeriod,
    })]
    public IndicatorCalculationType CalculationType = Indicator.DEFAULT_CALCULATION_TYPE;

    // Displays Input Parameter as input field (or checkbox if value type is boolean).
    [InputParameter("Period", 10, 1, 999, 1, 0)]
    public int Period = 20;

    public int MinHistoryDepths => this.Period;
    public override string ShortName => $"SD ({this.Period}: {this.SourcePrice}: {this.MAType})";

    // Holds moving average values.
    private Indicator ma;

    /// <summary>
    /// Indicator's constructor. Contains general information: name, description, LineSeries etc. 
    /// </summary>
    public IndicatorStandardDeviation()
        : base()
    {
        // Defines indicator's group, name and description.            
        this.Name = "Standard Deviation";
        this.Description = "Shows the difference of the volatility value from the average one.";

        // Defines line on demand with particular parameters.
        this.AddLineSeries("SD'Line", Color.Blue, 1, LineStyle.Solid);

        this.SeparateWindow = true;
    }

    /// <summary>
    /// This function will be called after creating an indicator as well as after its input params reset or chart (symbol or timeframe) updates.
    /// </summary>
    protected override void OnInit()
    {
        // Creates an instance of the proper indicator from the default indicators list.
        this.ma = Core.Indicators.BuiltIn.MA(this.Period, this.SourcePrice, this.MAType, this.CalculationType);
        // Adds an auxiliary (MA) indicator to the current one (SD). 
        // This will let inner indicator (MA) to be calculated in advance to the current one (SD).
        this.AddIndicator(this.ma);
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
        // Skip if count is smaller than period value.
        if (this.Count < this.MinHistoryDepths)
            return;

        // Processes calculating loop.
        double dAmount = 0;
        double movingAverage = this.ma.GetValue();
        for (int j = 0; j < this.Period; j++)
            dAmount += Math.Pow(this.GetPrice(this.SourcePrice, j) - movingAverage, 2);

        this.SetValue(Math.Sqrt(dAmount / this.Period));
    }
}