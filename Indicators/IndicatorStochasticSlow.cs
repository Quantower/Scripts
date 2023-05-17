// Copyright QUANTOWER LLC. © 2017-2023. All rights reserved.

using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace Oscillators;

public sealed class IndicatorStochasticSlow : Indicator, IWatchlistIndicator
{
    // Custom historical data layers mapping.
    private const PriceType SSD_SERIES = PriceType.Close;
    private const PriceType SSD_SMOOTHED_SERIES = PriceType.Open;

    // Displays Input Parameter as input field (or checkbox if value type is bolean).
    [InputParameter("Period", 0, 1, 999, 1, 0)]
    public int Period = 14;

    [InputParameter("Smoothing", 1, 0, 999)]
    public int Smooth = 3;

    [InputParameter("Double smoothing", 2, 1, 999)]
    public int DoubleSmooth = 3;

    [InputParameter("Type of smoothing Stochastic", 3, variants: new object[] {
        "Simple", MaMode.SMA,
        "Exponential", MaMode.EMA,
        "Smoothed Modified", MaMode.SMMA,
        "Linear Weighted", MaMode.LWMA}
    )]
    public MaMode MaType = MaMode.SMA;
    //
    [InputParameter("Calculation type", 4, variants: new object[]
    {
        "All available data", IndicatorCalculationType.AllAvailableData,
        "By period", IndicatorCalculationType.ByPeriod,
    })]
    public IndicatorCalculationType CalculationType = Indicator.DEFAULT_CALCULATION_TYPE;

    public int MinHistoryDepths => this.Smooth + this.DoubleSmooth + this.Period;
    public override string ShortName => $"SSD ({this.Period})";
    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorStochasticSlow.cs";

    private Indicator ssdMa;
    private Indicator smoothedMa;
    private HistoricalDataCustom smoothedHD;
    private HistoricalDataCustom doubleSmoothedHD;

    /// <summary>
    /// Indicator's constructor. Contains general information: name, description, LineSeries etc. 
    /// </summary>
    public IndicatorStochasticSlow()
        : base()
    {
        // Defines indicator's name and description.
        this.Name = "Stochastic Slow";
        this.Description = "Shows the location of the current close relative to the high/low range over a set number of periods (Slow)";

        // Defines line on demand with particular parameters.
        this.AddLineSeries("Stochastic", Color.Green, 1, LineStyle.Solid);
        this.AddLineSeries("Stochastic Smoothed", Color.LightSkyBlue, 1, LineStyle.Solid);
        this.AddLineLevel(80, "up", Color.Blue, 1, LineStyle.Dash);
        this.AddLineLevel(20, "down", Color.Yellow, 1, LineStyle.Dash);

        this.SeparateWindow = true;
    }

    /// <summary>
    /// This function will be called after creating an indicator as well as after its input params reset or chart (symbol or timeframe) updates.
    /// </summary>
    protected override void OnInit()
    {
        // Creates an instance of the custom historical data which will be synchronized with the current indicator instance.
        this.smoothedHD = new HistoricalDataCustom(this);
        this.doubleSmoothedHD = new HistoricalDataCustom(this);
        // Creates a smoothing indicator which will keep smoothed custom data.
        this.ssdMa = Core.Indicators.BuiltIn.MA(this.Smooth, SSD_SERIES, this.MaType, this.CalculationType);
        // Attaches the smoothing indicator to the custom historical data.
        this.smoothedHD.AddIndicator(this.ssdMa);
        this.smoothedMa = Core.Indicators.BuiltIn.MA(this.DoubleSmooth, SSD_SMOOTHED_SERIES, this.MaType, this.CalculationType);
        this.doubleSmoothedHD.AddIndicator(this.smoothedMa);
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

        // Populates custom HistoricalData with 2 data layers: 
        // For SSD is getSSD();
        this.smoothedHD[SSD_SERIES, 0] = this.getSSD();

        if (this.Count < this.Period + this.Smooth)
            return;

        // For SSD_Smoothed is GetValue();
        this.doubleSmoothedHD[SSD_SMOOTHED_SERIES, 0] = this.ssdMa.GetValue();

        if (this.Count < this.MinHistoryDepths)
            return;

        // Calculation of smoothed curve
        this.SetValue(this.ssdMa.GetValue());
        // Calculation of double smoothed curve
        this.SetValue(this.smoothedMa.GetValue(), 1);
    }

    private double getSSD()
    {
        double high = double.MinValue;
        double low = double.MaxValue;

        for (int i = 0; i < this.Count && i < this.Period; i++)
        {
            double price = this.GetPrice(PriceType.High, i);
            if (price > high)
                high = price;
            price = this.GetPrice(PriceType.Low, i);
            if (price < low)
                low = price;
        }
        double denominator = high - low;
        double close = this.GetPrice(PriceType.Close);
        return denominator > 0.0 ? 100 * (close - low) / denominator : 0.0;
    }
}