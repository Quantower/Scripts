// Copyright QUANTOWER LLC. © 2017-2024. All rights reserved.

using System;
using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace Oscillators;

/// <summary>
/// Percentage Price Oscillator is a momentum indicator. Signal line is EMA of PPO. Formula: (FastEMA-SlowEMA)/SlowEMA
/// </summary>
public sealed class IndicatorPercentagePriceOscillator : Indicator, IWatchlistIndicator
{
    // Displays Input Parameter as input field (or checkbox if value type is bolean).
    [InputParameter("Fast EMA Period", 0, 1, 999, 1, 0)]
    public int fastEmaPeriod = 12;

    [InputParameter("Slow EMA Period", 1, 1, 999, 1, 0)]
    public int slowEmaPeriod = 26;

    [InputParameter("Signal EMA Period", 2, 1, 999, 1, 0)]
    public int signalEmaPeriod = 9;
    //
    [InputParameter("Calculation type", 5, variants: new object[]
    {
        "All available data", IndicatorCalculationType.AllAvailableData,
        "By period", IndicatorCalculationType.ByPeriod,
    })]
    public IndicatorCalculationType CalculationType = Indicator.DEFAULT_CALCULATION_TYPE;

    private int MaxEMAPeriod => Math.Max(this.fastEmaPeriod, this.slowEmaPeriod);
    public int MinHistoryDepths => this.MaxEMAPeriod + this.signalEmaPeriod;
    public override string ShortName => $"PPO ({this.fastEmaPeriod}: {this.slowEmaPeriod}: {this.signalEmaPeriod})";
    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorPercentagePriceOscillator.cs";

    private Indicator fastEma;
    private Indicator slowEma;
    private Indicator signalEma;
    private HistoricalDataCustom customHDsignal;

    /// <summary>
    /// Indicator's constructor. Contains general information: name, description, LineSeries etc. 
    /// </summary>
    public IndicatorPercentagePriceOscillator()
        : base()
    {
        // Defines indicator's group, name and description.            
        this.Name = "Percentage Price Oscillator";
        this.Description = "Percentage Price Oscillator is a momentum indicator. Signal line is EMA of PPO. Formula: (FastEMA-SlowEMA)/SlowEMA";

        // Defines line on demand with particular parameters.
        this.AddLineSeries("PPO'Line", Color.SkyBlue, 2, LineStyle.Solid);
        this.AddLineSeries("Signal'Line", Color.Red, 1, LineStyle.Solid);
        this.AddLineLevel(0, "0'Line", Color.Gray, 1, LineStyle.Solid);

        this.SeparateWindow = true;
    }

    /// <summary>
    /// This function will be called after creating an indicator as well as after its input params reset or chart (symbol or timeframe) updates.
    /// </summary>
    protected override void OnInit()
    {
        // Creates an instance of the custom historical data which will be syncronized by the current indicator instance.
        this.customHDsignal = new HistoricalDataCustom(this);

        // Creates a smoothing indicator which will keep smoothed custom data (for close prices).
        this.signalEma = Core.Indicators.BuiltIn.EMA(this.signalEmaPeriod, PriceType.Close, this.CalculationType);

        // Adds the smoothing indicator to the custom historical data.
        this.customHDsignal.AddIndicator(this.signalEma);

        // Creates an instances of the proper indicators from the default indicators list.
        this.fastEma = Core.Indicators.BuiltIn.EMA(this.fastEmaPeriod, PriceType.Close, this.CalculationType);
        this.slowEma = Core.Indicators.BuiltIn.EMA(this.slowEmaPeriod, PriceType.Close, this.CalculationType);

        // Adds an auxiliary (fastEMA, slowEMA) indicators to the current one (PPO). 
        // This will let inner indicators (fastEMA, slowEMA) to be calculated in advance to the current one (PPO).
        this.AddIndicator(this.fastEma);
        this.AddIndicator(this.slowEma);
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
        if (this.Count < this.MaxEMAPeriod)
            return;

        // Gets calculation values.
        double fastEMA = this.fastEma.GetValue();
        double slowEMA = this.slowEma.GetValue();

        // Gets PPO value.
        double ppo = (fastEMA - slowEMA) / slowEMA;

        // Populates custom HistoricalData price with the PPO value.
        this.customHDsignal[PriceType.Close] = ppo;

        // Skip if count is smaller than period value.
        if (this.Count < this.MinHistoryDepths)
            return;

        // Sets given values for the displaying on the chart. 
        this.SetValue(ppo, 0);
        this.SetValue(this.signalEma.GetValue(), 1);
    }
}