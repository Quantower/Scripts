// Copyright QUANTOWER LLC. © 2017-2023. All rights reserved.

using System;
using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace MovingAverages;

/// <summary>
/// McGinley Dynamic avoids of most whipsaws and it rapidly moves up or down according to a quickly changing market. It needs no adjusting because it is dynamic and it adjusts itself.
/// </summary>
public sealed class IndicatorMcGinleyDynamic : Indicator, IWatchlistIndicator
{
    // Period of McGinley Dynamic. 
    [InputParameter("Period", 0, 2, 999, 1, 0)]
    public int Period = 14;

    // Dynamic tracking factor of McGinley Dynamic. 
    [InputParameter("Dynamic tracking factor", 1, 1, 999, 1, 0)]
    public int TrackingFactor = 2;

    // Price type of McGinley Dynamic. 
    [InputParameter("Source price", 2, variants: new object[]
    {
        "Close", PriceType.Close,
        "Open", PriceType.Open,
        "High", PriceType.High,
        "Low", PriceType.Low,
        "Typical", PriceType.Typical,
        "Median", PriceType.Median,
        "Weighted", PriceType.Weighted,
        "Volume", PriceType.Volume,
        "Open interest", PriceType.OpenInterest
    })]
    public PriceType SourcePrice = PriceType.Close;

    [InputParameter("Calculation type", 5, variants: new object[]
    {
        "All available data", IndicatorCalculationType.AllAvailableData,
        "By period", IndicatorCalculationType.ByPeriod,
    })]
    public IndicatorCalculationType CalculationType = Indicator.DEFAULT_CALCULATION_TYPE;

    public int MinHistoryDepths => this.Period + 1;
    public override string ShortName => $"MD ({this.Period}: {this.TrackingFactor}: {this.SourcePrice})";
    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorMcGinleyDynamic.cs";

    // Holds EMA's smoothing values.
    private Indicator ema;

    /// <summary>
    /// Indicator's constructor. Contains general information: name, description, LineSeries etc. 
    /// </summary>
    public IndicatorMcGinleyDynamic()
        : base()
    {
        // Defines indicator's group, name and description.            
        this.Name = "McGinley Dynamic";
        this.Description = "McGinley Dynamic avoids of most whipsaws and it rapidly moves up or down according to a quickly changing market. It needs no adjusting because it is dynamic and it adjusts itself.";

        // Defines line on demand with particular parameters.
        this.AddLineSeries("MD", Color.DodgerBlue, 1, LineStyle.Solid);
    }

    /// <summary>
    /// This function will be called after creating an indicator as well as after its input params reset or chart (symbol or timeframe) updates.
    /// </summary>
    protected override void OnInit()
    {
        // Creates an instance of the proper indicator from the default indicators list.
        this.ema = Core.Indicators.BuiltIn.EMA(this.Period, this.SourcePrice, this.CalculationType);
        // Adds an auxiliary (MA) indicator to the current one (MD). 
        // This will let inner indicator (MA) to be calculated in advance to the current one (MD).
        this.AddIndicator(this.ema);
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
        // Checking, if current amount of bars less, than period of moving average (+ 1) - calculation is impossible.
        if (this.Count < this.MinHistoryDepths)
            return;

        // Gets calculated value.
        double md = this.GetPrice(this.SourcePrice);
        double value = this.ema.GetValue(1);
        md = value + (md - value) / (this.TrackingFactor * Math.Pow(md / value, 4));

        // Sets value for displaying on the chart.
        this.SetValue(md);
    }
}