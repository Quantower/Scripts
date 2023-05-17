// Copyright QUANTOWER LLC. © 2017-2023. All rights reserved.

using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace Trend;

public sealed class IndicatorDirectionalMovementIndex : Indicator, IWatchlistIndicator
{
    // Displays Input Parameter as input field (or checkbox if value type is boolean).
    [InputParameter("Period of Moving Average", 0, 1, 999, 1, 0)]
    public int Period = 14;

    // Displays Input Parameter as drop down list.
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

    public int MinHistoryDepths => this.Period * 2;
    public override string ShortName => $"DMI ({this.Period}:{this.MAType})";
    public override string HelpLink => "https://help.quantower.com/analytics-panels/chart/technical-indicators/trend/directional-movement-index-dmi-indicator";
    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorDirectionalMovementIndex.cs";

    private Indicator atr;
    private Indicator firstMA;
    private Indicator secondMA;

    private HistoricalDataCustom firstMaHD;
    private HistoricalDataCustom secondMaHD;

    private double plusDM;
    private double minusDM;

    /// <summary>
    /// Indicator's constructor. Contains general information: name, description, LineSeries etc. 
    /// </summary>
    public IndicatorDirectionalMovementIndex()
        : base()
    {
        // Defines indicator's name and description.
        this.Name = "Directional Movement Index";
        this.Description = "Identifies whether there is a definable trend in the market.";

        // Defines line on demand with particular parameters.
        this.AddLineSeries("Plus", Color.DodgerBlue, 1, LineStyle.Solid);
        this.AddLineSeries("Minus", Color.Red, 1, LineStyle.Solid);
        this.SeparateWindow = true;
    }

    /// <summary>
    /// This function will be called after creating an indicator as well as after its input params reset or chart (symbol or timeframe) updates.
    /// </summary>
    protected override void OnInit()
    {
        // Get ATR and two MA indicators from built-in indicator collection.
        this.atr = Core.Indicators.BuiltIn.ATR(this.Period, this.MAType, this.CalculationType);
        this.firstMA = Core.Indicators.BuiltIn.MA(this.Period, PriceType.Close, this.MAType, this.CalculationType);
        this.secondMA = Core.Indicators.BuiltIn.MA(this.Period, PriceType.Close, this.MAType, this.CalculationType);

        // Create a custom HistoricalData and synchronize it with 'this' (DMI) indicator.
        this.firstMaHD = new HistoricalDataCustom(this);
        this.secondMaHD = new HistoricalDataCustom(this);

        // Add auxiliary ATR indicator to the current one.
        this.AddIndicator(this.atr);

        // Attach MA indicators to custom HistoricalData.
        this.firstMaHD.AddIndicator(this.firstMA);
        this.secondMaHD.AddIndicator(this.secondMA);
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
        if (this.Count < this.Period)
            return;

        // Get an ATR value.
        double smoothedTR = this.atr.GetValue();

        if (double.IsNaN(smoothedTR) || smoothedTR == 0d)
        {
            this.plusDM = 0D;
            this.minusDM = 0D;
        }
        else
        {
            this.plusDM = this.GetPrice(PriceType.High) - this.GetPrice(PriceType.High, 1);
            if (this.plusDM < 0.0)
                this.plusDM = 0.0;
            else
                this.plusDM *= 100D / smoothedTR;

            this.minusDM = this.GetPrice(PriceType.Low, 1) - this.GetPrice(PriceType.Low);
            if (this.minusDM < 0.0)
                this.minusDM = 0.0;
            else
                this.minusDM *= 100D / smoothedTR;

            if (this.plusDM > this.minusDM)
                this.minusDM = 0.0;
            else
                this.plusDM = 0.0;
        }

        // The calculated value must be set as close price against the custom HistoricalData,
        // because the MA indicator was initialized with the source price - PriceType.Close. 
        this.firstMaHD[PriceType.Close, 0] = this.plusDM;
        this.secondMaHD[PriceType.Close, 0] = this.minusDM;

        // Skip some period for correct calculation.
        if (this.Count < this.MinHistoryDepths)
            return;

        // Get values from MA indicators.
        double plus = this.firstMA.GetValue();
        double minus = this.secondMA.GetValue();

        // Set values to "Plus" and "Minus" line buffers.
        this.SetValue(plus, 0);
        this.SetValue(minus, 1);
    }
}