// Copyright QUANTOWER LLC. © 2017-2024. All rights reserved.

using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace Oscillators;

public sealed class IndicatorAwesomeOscillator : Indicator, IWatchlistIndicator
{
    public int MinHistoryDepths => SLOW_PERIOD;
    public override string ShortName => "AO";
    public override string HelpLink => "https://help.quantower.com/analytics-panels/chart/technical-indicators/oscillators/awesome-oscillator";
    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorAwesomeOscillator.cs";

    private Indicator fastMA;
    private Indicator slowMA;

    // Fixed periods for SMA indicators.
    private const int FAST_PERIOD = 5;
    private const int SLOW_PERIOD = 34;

    /// <summary>
    /// Indicator's constructor. Contains general information: name, description, LineSeries etc. 
    /// </summary>
    public IndicatorAwesomeOscillator()
        : base()
    {
        // Defines indicator's name and description.
        this.Name = "Awesome Oscillator";
        this.Description = "Awesome Oscillator determines market momentum";

        // Defines line on demand with particular parameters.
        this.AddLineSeries("AO", Color.Gray, 2, LineStyle.Histogramm);
        this.SeparateWindow = true;
    }

    /// <summary>
    /// This function will be called after creating an indicator as well as after its input params reset or chart (symbol or timeframe) updates.
    /// </summary>
    protected override void OnInit()
    {
        // Get two SMA indicators from built-in indicator collection.
        this.fastMA = Core.Indicators.BuiltIn.SMA(FAST_PERIOD, PriceType.Median);
        this.slowMA = Core.Indicators.BuiltIn.SMA(SLOW_PERIOD, PriceType.Median);

        this.AddIndicator(this.fastMA);
        this.AddIndicator(this.slowMA);
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
        if (this.Count < this.MinHistoryDepths)
            return;

        // Calculate the AO value
        double ao = this.fastMA.GetValue() - this.slowMA.GetValue();
        double prevAO = double.IsNaN(this.GetValue(1))
            ? this.GetPrice(PriceType.Close, 0)
            : this.GetValue(1);

        // Set values to 'AO' line buffer.
        this.SetValue(ao);

        var indicatorColor = (prevAO > ao) ? Color.Red : Color.Green;

        this.LinesSeries[0].SetMarker(0, indicatorColor);
    }
}