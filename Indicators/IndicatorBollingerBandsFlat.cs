// Copyright QUANTOWER LLC. © 2017-2024. All rights reserved.

using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace Channels;

/// <summary>
/// The Bollinger Bands Flat (BBF) indicator provides the same data as BB, but drawn in separate field and easier to recognize whether price is in or out of the band.
/// </summary>
public sealed class IndicatorBollingerBandsFlat : Indicator, IWatchlistIndicator
{
    #region Parameters

    // Displays Input Parameter as input field (or checkbox if value type is bolean).
    [InputParameter("Period", 0, 1, 999, 1, 0)]
    public int Period = 9;

    [InputParameter("Type of Moving Average", 1, variants: new object[] {
         "Simple", MaMode.SMA,
         "Exponential", MaMode.EMA,
         "Modified", MaMode.SMMA,
         "Linear Weighted", MaMode.LWMA}
    )]
    public MaMode MAType = MaMode.SMA;

    // Displays Input Parameter as dropdown list.
    [InputParameter("Sources prices for MA", 2, variants: new object[] {
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

    [InputParameter("Deviation", 3, 0.01, 3, 0.01, 2)]
    public double Deviation = 1.5;
    //
    [InputParameter("Calculation type", 4, variants: new object[]
    {
        "All available data", IndicatorCalculationType.AllAvailableData,
        "By period", IndicatorCalculationType.ByPeriod,
    })]
    public IndicatorCalculationType CalculationType = Indicator.DEFAULT_CALCULATION_TYPE;

    #endregion

    // Holds additional indicators values.
    private Indicator ma;
    private Indicator sd;
    public int MinHistoryDepths => this.Period;
    public override string ShortName => $"BBF ({this.Period}: {this.Deviation})";
    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorBollingerBandsFlat.cs";

    /// <summary>
    /// Indicator's constructor. Contains general information: name, description, LineSeries etc. 
    /// </summary>
    public IndicatorBollingerBandsFlat()
        : base()
    {
        // Defines indicator's group, name and description.            
        this.Name = "Bollinger Bands Flat";
        this.Description = "The Bollinger Bands Flat (BBF) indicator provides the same data as BB, but drawn in separate field and easier to recognize whether price is in or out of the band.";

        // Defines line on demand with particular parameters.
        this.AddLineSeries("+SD", Color.Red, 1, LineStyle.Solid);
        this.AddLineSeries("-SD", Color.Red, 1, LineStyle.Solid);
        this.AddLineSeries("BBF'Line", Color.FromArgb(0, 51, 252), 1, LineStyle.Solid);
        this.AddLineLevel(0, "0'Line", Color.Aqua, 1, LineStyle.Solid);

        this.SeparateWindow = true;
    }

    /// <summary>
    /// This function will be called after creating an indicator as well as after its input params reset or chart (symbol or timeframe) updates.
    /// </summary>
    protected override void OnInit()
    {
        // Creates an instances of the proper indicators (MA, SD) from the default indicators list.
        this.ma = Core.Indicators.BuiltIn.MA(this.Period, this.SourcePrice, this.MAType, this.CalculationType);
        this.sd = Core.Indicators.BuiltIn.SD(this.Period, this.SourcePrice, this.MAType, this.CalculationType);
        // Adds auxiliary (MA, SD) indicators to the current one (BBF). 
        // This will let inner indicators (MA, SD) to be calculated in advance to the current one (BBF).
        this.AddIndicator(this.ma);
        this.AddIndicator(this.sd);
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
            return;

        // Sets value for displaying on the chart.
        double std = this.Deviation * this.sd.GetValue();

        this.SetValue(std);
        this.SetValue(-std, 1);
        this.SetValue(this.GetPrice(PriceType.Close) - this.ma.GetValue(), 2);
    }
}