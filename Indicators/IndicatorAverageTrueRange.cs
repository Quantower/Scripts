// Copyright QUANTOWER LLC. © 2017-2024. All rights reserved.

using System.Drawing;
using TradingPlatform.BusinessLayer;
using VolatilityIndicators;

namespace Volatility;

public sealed class IndicatorAverageTrueRange : Indicator, IWatchlistIndicator
{
    // Displays Input Parameter as input field.
    [InputParameter("Period of Moving Average", 0, 1, 999, 1, 0)]
    public int Period = 13;

    // Displays Input Parameter as dropdown list.
    [InputParameter("Type of Moving Average", 1, variants: new object[] {
         "Simple", MaMode.SMA,
         "Exponential", MaMode.EMA,
         "Smoothed", MaMode.SMMA,
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
    public override string ShortName => $"ATR ({this.Period}: {this.MAType})";
    public override string HelpLink => "https://help.quantower.com/analytics-panels/chart/technical-indicators/volatility/average-true-range";
    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorAverageTrueRange.cs";

    private Indicator ma;
    private IndicatorTrueRange tr;
    private HistoricalDataCustom customHD;

    /// <summary>
    /// Indicator's constructor. Contains general information: name, description, LineSeries etc. 
    /// </summary>
    public IndicatorAverageTrueRange()
        : base()
    {
        // Defines indicator's name and description.
        this.Name = "Average True Range";
        this.Description = "Measures of market volatility.";
        this.IsUpdateTypesSupported = false;

        // Defines line on demand with particular parameters.
        this.AddLineSeries("ATR", Color.CadetBlue, 1, LineStyle.Solid);

        this.SeparateWindow = true;
    }

    /// <summary>
    /// This function will be called after creating an indicator as well as after its input params reset or chart (symbol or timeframe) updates.
    /// </summary>
    protected override void OnInit()
    {
        // Get MA indicator from built-in indicator collection (according to selected 'MaType').
        this.ma = Core.Indicators.BuiltIn.MA(this.Period, PriceType.Close, this.MAType, this.CalculationType);
        this.ma.UpdateType = IndicatorUpdateType.OnTick;
        this.AddIndicator(this.tr = new IndicatorTrueRange() { UpdateType = IndicatorUpdateType.OnTick });

        // Create a custom HistoricalData and syncronize it with this(ART) indicator.
        this.customHD = new HistoricalDataCustom(this);

        // Attach SMA indicator to custom HistoricalData. The MA will calculate on the data, which will store in custom HD. 
        this.customHD.AddIndicator(this.ma);
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
        // Get the TR value and store it to the custom HistoricalData.
        double tr = this.tr.GetValue();

        this.customHD[PriceType.Close, 0] = tr;

        // Skip some period for correct calculation.  
        if (this.Count < this.MinHistoryDepths)
            return;

        // Get MA value and set it to 'ATR' line buffer.
        double maValue = this.ma.GetValue();
        this.SetValue(maValue);
    }
}