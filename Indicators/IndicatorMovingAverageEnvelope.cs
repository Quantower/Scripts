// Copyright QUANTOWER LLC. © 2017-2024. All rights reserved.

using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace Channels;

public sealed class IndicatorMovingAverageEnvelope : Indicator, IWatchlistIndicator
{
    // Defines the 'Period' parameter as input field (where 'min' is 1 and 'max' is 999).
    [InputParameter("Period of MA for envelopes", 0, 1, 999, 1, 0)]
    public int Period = 20;

    // Defines the 'SourcePrice' parameter as dropdown list
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

    // Defines the 'MaType' parameter as dropdown list
    [InputParameter("Type of moving average", 2, variants: new object[]{
        "Simple Moving Average", MaMode.SMA,
        "Exponential Moving Average", MaMode.EMA,
        "Smoothed Moving Average", MaMode.SMMA,
        "Linearly Weighted Moving Average", MaMode.LWMA,
    })]
    public MaMode MaType = MaMode.SMA;

    [InputParameter("Calculation type", 3, variants: new object[]
    {
        "All available data", IndicatorCalculationType.AllAvailableData,
        "By period", IndicatorCalculationType.ByPeriod,
    })]
    public IndicatorCalculationType CalculationType = Indicator.DEFAULT_CALCULATION_TYPE;

    // Defines the 'UpShift' parameter as input field (where 'min' is 0.1 and 'increment' is 0.1).
    [InputParameter("Upband deviation in %", 4, 0.01, int.MaxValue, 0.01, 2)]
    public double UpShift = 0.1;

    // Defines the 'DownShift' parameter as input field (where 'min' is 0.1 and 'increment' is 0.1).
    [InputParameter("Downband deviation in %", 5, 0.01, int.MaxValue, 0.01, 2)]
    public double DownShift = 0.1;

    public int MinHistoryDepths => this.Period;
    public override string ShortName => $"MAE ({this.Period}:{this.UpShift}:{this.DownShift})";
    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorMovingAverageEnvelope.cs";

    private Indicator ma;

    /// <summary>
    /// Indicator's constructor. Contains general information: name, description, LineSeries etc. 
    /// </summary>
    public IndicatorMovingAverageEnvelope()
        : base()
    {
        // Defines indicator's name and description.
        this.Name = "Moving Average Envelope";
        this.Description = "Demonstrates a range of the prices discrepancy from a Moving Average";

        // Defines two lines with particular parameters.
        this.AddLineSeries("Lower Band", Color.Purple, 1, LineStyle.Solid);
        this.AddLineSeries("Upper Band", Color.LightSeaGreen, 1, LineStyle.Solid);

        this.SeparateWindow = false;
    }

    /// <summary>
    /// This function will be called after creating an indicator as well as after its input params reset or chart (symbol or timeframe) updates.
    /// </summary>
    protected override void OnInit()
    {
        // Get MA indicator from built-in indicator collection (according to selected 'MaType').
        this.ma = Core.Indicators.BuiltIn.MA(this.Period, this.SourcePrice, this.MaType, this.CalculationType);
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
        // Skip some period for correct calculation. 
        if (this.Count < this.MinHistoryDepths)
            return;

        // Get current close price (0 offset by default) 
        double maValue = this.ma.GetValue();

        // Set values to 'Lower Band' and 'Upper Band' line buffers.
        this.SetValue((1.0 - this.DownShift * 0.01) * maValue, 0);
        this.SetValue((1.0 + this.UpShift * 0.01) * maValue, 1);
    }
}