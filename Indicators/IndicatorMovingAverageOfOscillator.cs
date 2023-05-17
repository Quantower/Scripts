// Copyright QUANTOWER LLC. © 2017-2023. All rights reserved.

using System;
using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace Oscillators;

public sealed class IndicatorMovingAverageOfOscillator : Indicator, IWatchlistIndicator
{
    // Displays Input Parameter as input field (or checkbox if value type is bolean).
    [InputParameter("Period of fast EMA", 0, 1, 999, 1, 0)]
    public int FastPeriod = 12;

    [InputParameter("Period of slow EMA", 1, 1, 999, 1, 0)]
    public int SlowPeriod = 26;

    [InputParameter("Period of signal EMA", 2, 1, 999, 1, 0)]
    public int SignalPeriod = 9;

    //
    [InputParameter("Calculation type", 10, variants: new object[]
    {
        "All available data", IndicatorCalculationType.AllAvailableData,
        "By period", IndicatorCalculationType.ByPeriod,
    })]
    public IndicatorCalculationType CalculationType = Indicator.DEFAULT_CALCULATION_TYPE;

    private int MaxEMAPeriod => Math.Max(this.FastPeriod, this.SlowPeriod);
    public int MinHistoryDepths => this.MaxEMAPeriod + this.SignalPeriod;
    public override string ShortName => $"OsMA ({this.FastPeriod}:{this.SlowPeriod}:{this.SignalPeriod})";
    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorMovingAverageOfOscillator.cs";

    private Indicator fastEMA;
    private Indicator slowEMA;
    private Indicator sma;
    private HistoricalDataCustom customHD;

    /// <summary>
    /// Indicator's constructor. Contains general information: name, description, LineSeries etc. 
    /// </summary>
    public IndicatorMovingAverageOfOscillator()
        : base()
    {
        // Defines indicator's name and description.
        this.Name = "Moving Average of Oscillator";
        this.Description = "Reflects the difference between an oscillator (MACD) and its moving average (signal line).";

        // Defines line on demand with particular parameters.
        this.AddLineSeries("OsMA", Color.Green, 4, LineStyle.Histogramm);

        this.SeparateWindow = true;
    }

    /// <summary>
    /// This function will be called after creating an indicator as well as after its input params reset or chart (symbol or timeframe) updates.
    /// </summary>
    protected override void OnInit()
    {
        // Get two EMA and one SMA indicators from built-in indicator collection 
        this.fastEMA = Core.Indicators.BuiltIn.EMA(this.FastPeriod, PriceType.Typical, this.CalculationType);
        this.slowEMA = Core.Indicators.BuiltIn.EMA(this.SlowPeriod, PriceType.Typical, this.CalculationType);
        this.sma = Core.Indicators.BuiltIn.SMA(this.SignalPeriod, PriceType.Close);

        // Create a custom HistoricalData and syncronize it with this(MACD) indicator.
        this.customHD = new HistoricalDataCustom(this);

        // Attach SMA indicator to custom HistoricalData. The SMA will calculate on the data, which will store in custom HD. 
        this.customHD.AddIndicator(this.sma);

        // Add auxiliary EMA indicators to the current one. 
        this.AddIndicator(this.fastEMA);
        this.AddIndicator(this.slowEMA);
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
        // Skip max period for correct calculation.  
        if (this.Count < this.MaxEMAPeriod)
            return;

        // Calculate a difference bettwen two EMA indicators.
        double differ = this.fastEMA.GetValue() - this.slowEMA.GetValue();

        // The calculated value must be set as close price against the custom HistoricalData,
        // because the SMA indicator was initialized with the source price - PriceType.Close. 
        this.customHD[PriceType.Close, 0] = differ;

        if (this.Count < this.MinHistoryDepths)
            return;

        // Get value from SMA indicator, which is calculated based on custom HistoricalData.
        double signal = this.sma.GetValue();
        if (double.IsNaN(signal))
            return;

        // Set value to the 'OsMA' line buffer.
        this.SetValue(differ - signal);
    }
}