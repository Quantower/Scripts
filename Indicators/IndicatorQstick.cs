// Copyright QUANTOWER LLC. © 2017-2024. All rights reserved.

using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace Oscillators;

/// <summary>
/// Moving average that shows the difference between the prices at which an issue opens and closes.
/// </summary>
public sealed class IndicatorQstick : Indicator, IWatchlistIndicator
{
    // Displays Input Parameter as input field (or checkbox if value type is bolean).
    [InputParameter("Period", 0, 1, 999, 0, 0)]
    public int Period = 14;

    // Displays Input Parameter as dropdown list.
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

    public int MinHistoryDepths => this.Period;
    public override string ShortName => $"Qstick ({this.Period}: {this.MAType})";
    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorQstick.cs";

    private HistoricalDataCustom customHistData;
    private Indicator ma;

    /// <summary>
    /// Indicator's constructor. Contains general information: name, description, LineSeries etc. 
    /// </summary>
    public IndicatorQstick()
        : base()
    {
        // Defines indicator's group, name and description.
        this.Name = "Qstick";
        this.Description = "Moving average that shows the difference between the prices at which an issue opens and closes.";

        // Defines line on demand with particular parameters.
        this.AddLineSeries("Qstick'Line", Color.Blue, 1, LineStyle.Solid);
        this.AddLineLevel(0, "0'Line", Color.Gray, 1, LineStyle.Solid);

        this.SeparateWindow = true;
    }

    /// <summary>
    /// This function will be called after creating an indicator as well as after its input params reset or chart (symbol or timeframe) updates.
    /// </summary>
    protected override void OnInit()
    {
        // Creates an instance of the custom historical data which will be syncronized by the current indicator instance.
        this.customHistData = new HistoricalDataCustom(this);

        // Creates a smoothing indicator which will keep smoothed custom data.
        this.ma = Core.Indicators.BuiltIn.MA(this.Period, PriceType.Close, this.MAType, this.CalculationType);

        // Adds the smoothing indicator to the custom historical data.
        this.customHistData.AddIndicator(this.ma);
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
        // Populates custom HistoricalData price with the current price difference value.
        this.customHistData[PriceType.Close] = this.Close() - this.Open();

        // Skip if count is smaller than period value.
        if (this.Count < this.MinHistoryDepths)
            return;

        // Sets value (smoothing value on the custom historical data) for displaying on the chart.
        this.SetValue(this.ma.GetValue());
    }
}