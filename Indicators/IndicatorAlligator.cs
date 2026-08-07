// Copyright QUANTOWER LLC. © 2017-2024. All rights reserved.

using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace MovingAverageIndicators;

public sealed class IndicatorAlligator : Indicator
{
    // Displays Input Parameter as dropdown list.
    [InputParameter("Period of Jaw Moving Average", 10, 1, 9999)]
    public int JawMAPeriod = 13;

    [InputParameter("Type of Jaw Moving Average", 11, variants: new object[]{
        "Simple", MaMode.SMA,
        "Exponential", MaMode.EMA,
        "Smoothed Simple", MaMode.SMMA,
           "Linear Weighted", MaMode.LWMA}
    )]
    public MaMode JawMAType = MaMode.SMMA;

    [InputParameter("Source price for Jaw Moving Average", 12, variants: new object[] {
         "Close", PriceType.Close,
         "Open", PriceType.Open,
         "High", PriceType.High,
         "Low", PriceType.Low,
         "Typical", PriceType.Typical,
         "Median", PriceType.Median,
         "Weighted", PriceType.Weighted}
    )]
    public PriceType JawSourcePrice = PriceType.Median;

    // Displays Input Parameter as input field (or checkbox if value type is bolean).

    [InputParameter("Period of Teeth Moving Average", 21, 1, 9999)]
    public int TeethMAPeriod = 8;

    [InputParameter("Type of Teeth Moving Average", 22, variants: new object[]{
        "Simple", MaMode.SMA,
        "Exponential", MaMode.EMA,
        "Smoothed Simple", MaMode.SMMA,
           "Linear Weighted", MaMode.LWMA}
     )]
    public MaMode TeethMAType = MaMode.SMMA;

    [InputParameter("Source price for Teeth Moving Average", 23, variants: new object[]{
         "Close", PriceType.Close,
         "Open", PriceType.Open,
         "High", PriceType.High,
         "Low", PriceType.Low,
         "Typical", PriceType.Typical,
         "Median", PriceType.Median,
         "Weighted", PriceType.Weighted}
    )]
    public PriceType TeethSourcePrice = PriceType.Median;

    [InputParameter("Period of Lips Moving Average", 31, 1, 9999)]
    public int LipsMAPeriod = 5;

    [InputParameter("Type of Lips Moving Average", 32, variants: new object[]{
        "Simple", MaMode.SMA,
        "Exponential", MaMode.EMA,
        "Smoothed Simple", MaMode.SMMA,
        "Linear Weighted", MaMode.LWMA}
     )]
    public MaMode LipsMAType = MaMode.SMMA;
    //

    [InputParameter("Source price for Lips Moving Average", 33, variants: new object[]{
         "Close", PriceType.Close,
         "Open", PriceType.Open,
         "High", PriceType.High,
         "Low", PriceType.Low,
         "Typical", PriceType.Typical,
         "Median", PriceType.Median,
         "Weighted", PriceType.Weighted}
    )]
    public PriceType LipsSourcePrice = PriceType.Median;

    [InputParameter("Calculation type", 40, variants: new object[]
{
        "All available data", IndicatorCalculationType.AllAvailableData,
        "By period", IndicatorCalculationType.ByPeriod,
})]
    public IndicatorCalculationType CalculationType = Indicator.DEFAULT_CALCULATION_TYPE;

    // Serves for an identification of related indicators with different parameters.
    public override string ShortName => $"Alligator ({this.JawMAPeriod}:{this.TeethMAPeriod}:{this.LipsMAPeriod})";
    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorAlligator.cs";

    private Indicator jawMa;
    private Indicator teethMa;
    private Indicator lipsMa;

    /// <summary>
    /// Indicator's constructor. Contains general information: name, description, LineSeries etc. 
    /// </summary>
    public IndicatorAlligator()
        : base()
    {
        // Defines indicator's name and description.
        this.Name = "Alligator";
        this.Description = "Three moving averages with different colors, periods and calculation methods";

        // Defines line on demand with particular parameters.
        this.AddLineSeries("JAW Line", Color.Blue, 1, LineStyle.Solid);
        this.AddLineSeries("TEETH Line", Color.Red, 1, LineStyle.Solid);
        this.AddLineSeries("LIPS Line", Color.Green, 1, LineStyle.Solid);
        this.LinesSeries[0].TimeShift = 8;
        this.LinesSeries[1].TimeShift = 5;
        this.LinesSeries[2].TimeShift = 3;

        this.SeparateWindow = false;
    }

    /// <summary>
    /// This function will be called after creating an indicator as well as after its input params reset or chart (symbol or timeframe) updates.
    /// </summary>
    protected override void OnInit()
    {
        this.jawMa = Core.Indicators.BuiltIn.MA(this.JawMAPeriod, this.JawSourcePrice, this.JawMAType, this.CalculationType);
        this.AddIndicator(this.jawMa);
        this.teethMa = Core.Indicators.BuiltIn.MA(this.TeethMAPeriod, this.TeethSourcePrice, this.TeethMAType, this.CalculationType);
        this.AddIndicator(this.teethMa);
        this.lipsMa = Core.Indicators.BuiltIn.MA(this.LipsMAPeriod, this.LipsSourcePrice, this.LipsMAType, this.CalculationType);
        this.AddIndicator(this.lipsMa);
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
        this.SetValue(this.jawMa.GetValue());
        this.SetValue(this.teethMa.GetValue(), 1);
        this.SetValue(this.lipsMa.GetValue(), 2);
    }
}