// Copyright QUANTOWER LLC. © 2017-2024. All rights reserved.

using System;
using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace Oscillators;

public sealed class IndicatorCommodityChannelIndex : Indicator, IWatchlistIndicator
{
    // Displays Input Parameter as input field (or checkbox if value type is bolean).
    [InputParameter("Period", 10, 1, 999, 1, 0)]
    public int Period = 14;

    // Displays Input Parameter as dropdown list.
    [InputParameter("Sources prices for MA", 20, variants: new object[] {
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

    // Displays Input Parameter as dropdown list.
    [InputParameter("Type of Moving Average", 30, variants: new object[] {
         "Simple", MaMode.SMA,
         "Exponential", MaMode.EMA,
         "Modified", MaMode.SMMA,
         "Linear Weighted", MaMode.LWMA}
    )]
    public MaMode MAType = MaMode.SMA;

    [InputParameter("Calculation type", 40, variants: new object[]
    {
        "All available data", IndicatorCalculationType.AllAvailableData,
        "By period", IndicatorCalculationType.ByPeriod,
    })]
    public IndicatorCalculationType CalculationType = Indicator.DEFAULT_CALCULATION_TYPE;

    public int MinHistoryDepths => this.Period;
    public override string ShortName => $"CCI ({this.Period}: {this.SourcePrice})";
    public override string HelpLink => "https://help.quantower.com/analytics-panels/chart/technical-indicators/oscillators/commodity-channel-index";
    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorCommodityChannelIndex.cs";

    private Indicator MA;

    /// <summary>
    /// Indicator's constructor. Contains general information: name, description, LineSeries etc. 
    /// </summary>
    public IndicatorCommodityChannelIndex()
        : base()
    {
        // Serves for an identification of related indicators with different parameters.
        this.Name = "Commodity Channel Index";
        this.Description = "Measures the position of price in relation to its moving average";

        // Defines line on demand with particular parameters.
        this.AddLineSeries("CCI Line", Color.Red, 1, LineStyle.Solid);
        this.AddLineLevel(150, "150", Color.Gray, 1, LineStyle.Solid);
        this.AddLineLevel(100, "100", Color.Gray, 1, LineStyle.Solid);
        this.AddLineLevel(0, "0", Color.Gray, 1, LineStyle.Solid);
        this.AddLineLevel(-100, "-100", Color.Gray, 1, LineStyle.Solid);
        this.AddLineLevel(-150, "-150", Color.Gray, 1, LineStyle.Solid);
        this.SeparateWindow = true;
    }

    /// <summary>
    /// This function will be called after creating an indicator as well as after its input params reset or chart (symbol or timeframe) updates.
    /// </summary>
    protected override void OnInit()
    {
        // Creates an instance of the proper indicator from the default indicators list.
        this.MA = Core.Indicators.BuiltIn.MA(this.Period, this.SourcePrice, this.MAType, this.CalculationType);
        // Adds an auxiliary (MA) indicator to the current one (CCI). 
        // This will let inner indicator (MA) to be calculated in advance to the current one (CCI).
        this.AddIndicator(this.MA);
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

        double meanDeviation = 0;
        for (int i = 0; i < this.Period; i++)
        {
            double tp = this.GetPrice(this.SourcePrice, i);
            double sma = this.MA.GetValue(i);
            meanDeviation += Math.Abs(tp - sma);
        }

        meanDeviation = 0.015 * (meanDeviation / this.Period);

        double currentTP = this.GetPrice(this.SourcePrice);
        double currentSMA = this.MA.GetValue();

        this.SetValue((currentTP - currentSMA) / meanDeviation);
    }
}