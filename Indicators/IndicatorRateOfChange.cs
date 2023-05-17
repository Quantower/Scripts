// Copyright QUANTOWER LLC. © 2017-2023. All rights reserved.

using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace Oscillators;

public sealed class IndicatorRateOfChange : Indicator, IWatchlistIndicator
{
    // Displays 'Period' input parameter as input field.
    [InputParameter("Period of momentum", 0, 1, 999, 1, 0)]
    public int Period = 9;

    public int MinHistoryDepths => this.Period + 1;
    public override string ShortName => $"ROC ({this.Period})";
    public override string HelpLink => "https://help.quantower.com/analytics-panels/chart/technical-indicators/oscillators/rate-of-change";
    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorRateOfChange.cs";

    /// <summary>
    /// Indicator's constructor. Contains general information: name, description, LineSeries etc. 
    /// </summary>
    public IndicatorRateOfChange()
        : base()
    {
        // Defines indicator's name and description.
        this.Name = "Rate of Change";
        this.Description = "Shows the speed at which price is changing";

        // Defines line on demand with particular parameters.
        this.AddLineSeries("ROC", Color.Brown, 2, LineStyle.Solid);
        this.AddLineLevel(0d, "Zero", Color.Gray, 2, LineStyle.Solid);

        this.SeparateWindow = true;
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

        // Get close price.
        double price = this.Close();

        // Get close price by offset.
        double priceN = this.Close(this.Period);

        double roc = 100 * (price - priceN) / priceN;

        // Set value to 'ROC' line buffer.
        this.SetValue(roc);
    }
}