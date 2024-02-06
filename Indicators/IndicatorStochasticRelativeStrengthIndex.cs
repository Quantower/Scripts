// Copyright QUANTOWER LLC. © 2017-2024. All rights reserved.

using System;
using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace Oscillators;

public sealed class IndicatorStochasticRelativeStrengthIndex : Indicator, IWatchlistIndicator
{
    [InputParameter("RSI Period", 10, 1, 999, 1, 0)]
    public int rsiPeriod = 14;

    [InputParameter("Stochastic period", 20, 1, 999, 1, 0)]
    public int StochPeriod = 14;

    [InputParameter("%K Period", 30, 1, 999, 1, 0)]
    public int kPeriod = 3;

    [InputParameter("%D Period", 40, 1, 999, 1, 0)]
    public int dPeriod = 3;

    [InputParameter("Calculation type", 50, variants: new object[]
    {
        "All available data", IndicatorCalculationType.AllAvailableData,
        "By period", IndicatorCalculationType.ByPeriod,
    })]
    public IndicatorCalculationType CalculationType = Indicator.DEFAULT_CALCULATION_TYPE;

    public int MinHistoryDepths => this.rsiPeriod + Math.Max(this.kPeriod, this.dPeriod);
    public override string ShortName => $"StochasticRSI ({this.rsiPeriod}: {this.StochPeriod}: {this.kPeriod}: {this.dPeriod})";
    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorStochasticRelativeStrengthIndex.cs";

    private Indicator rsi;
    private Indicator stochRSI;
    private HistoricalDataCustom hCustom;

    /// <summary>
    /// Indicator's constructor. Contains general information: name, description, LineSeries etc. 
    /// </summary>
    public IndicatorStochasticRelativeStrengthIndex()
        : base()
    {
        // Defines indicator's name and description.
        this.Name = "Stochastic x Relative Strength Index";
        this.Description = "StochRSI is an oscillator that measures the level of RSI relative to its range";

        // Defines line on demand with particular parameters.
        this.AddLineSeries("StochRSI", Color.CornflowerBlue, 1, LineStyle.Solid);
        this.AddLineSeries("Signal", Color.LightSkyBlue, 1, LineStyle.Solid);
        this.AddLineLevel(80, "up", Color.Gray, 1, LineStyle.Dot);
        this.AddLineLevel(20, "down", Color.Gray, 1, LineStyle.Dot);

        this.SeparateWindow = true;
    }

    /// <summary>
    /// This function will be called after creating an indicator as well as after its input params reset or chart (symbol or timeframe) updates.
    /// </summary>
    protected override void OnInit()
    {
        // Creates an instance of the custom historical data which will be synchronized with the current indicator instance.
        this.hCustom = new HistoricalDataCustom(this);
        this.rsi = Core.Indicators.BuiltIn.RSI(this.rsiPeriod, PriceType.Close, RSIMode.Exponential, MaMode.SMA, 5);
        this.AddIndicator(this.rsi);
        this.stochRSI = Core.Indicators.BuiltIn.Stochastic(this.StochPeriod, this.kPeriod, this.dPeriod, MaMode.SMA, this.CalculationType);
        this.hCustom.AddIndicator(this.stochRSI);
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
        if (this.Count < this.rsiPeriod)
            return;

        // Populates custom HistoricalData with 1 data layers: 
        this.hCustom.SetValue(0d, this.rsi.GetValue(), this.rsi.GetValue(), this.rsi.GetValue());

        if (this.Count < this.MinHistoryDepths)
            return;

        this.SetValue(this.stochRSI.GetValue());
        this.SetValue(this.stochRSI.GetValue(0, 1), 1);
    }
}