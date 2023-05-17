// Copyright QUANTOWER LLC. © 2017-2023. All rights reserved.

using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using TradingPlatform.BusinessLayer;

namespace Volume;

public sealed class IndicatorMoneyFlowIndex : Indicator, IWatchlistIndicator
{
    // Displays Input Parameter as input field.
    [InputParameter("MFI Period", 0, 2, 999, 1, 0)]
    public int Period = 14;

    private readonly List<double> fpmf;
    private readonly List<double> fnmf;

    public int MinHistoryDepths => this.Period;
    public override string ShortName => $"MFI ({this.Period})";
    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorMoneyFlowIndex.cs";

    /// <summary>
    /// Indicator's constructor. Contains general information: name, description, LineSeries etc. 
    /// </summary>
    public IndicatorMoneyFlowIndex()
        : base()
    {
        this.fpmf = new List<double>();
        this.fnmf = new List<double>();

        // Defines indicator's name and description.
        this.Name = "Money Flow Index";
        this.Description = "The Money Flow Index (MFI) is an oscillator that uses both price and volume to measure buying and selling pressure";

        // Defines lines on demand with particular parameters.
        this.AddLineSeries("MFI", Color.Orange, 1, LineStyle.Solid);
        this.AddLineLevel(80d, "Up", Color.Gray, 1, LineStyle.Dot);
        this.AddLineLevel(20d, "Down", Color.Gray, 1, LineStyle.Dot);

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
        // Insert new item to collections only on HistoricalBar of NewBar.
        if (args.Reason != UpdateReason.NewTick)
        {
            this.fnmf.Insert(0, 0);
            this.fpmf.Insert(0, 0);
        }
        // Skip the bar at the beginning of the story.
        if (this.Count == 1)
            return;

        // Get current and previous typical prices.
        double curPrice = this.GetPrice(PriceType.Typical, 0);
        double prevPrice = this.GetPrice(PriceType.Typical, 1);

        // Try to get volume value. If it's '0' or 'NaN' then get ticks value.
        double vol = (this.Volume() == 0 || double.IsNaN(this.Volume())) ? this.Ticks() : this.Volume();
        if (double.IsNaN(vol) || vol == 0)
            return;

        // Populate collections.
        if (curPrice > prevPrice)
            this.fpmf[0] = curPrice * vol;
        else if (curPrice < prevPrice)
            this.fnmf[0] = curPrice * vol;

        // Skip some period for correct calculation.
        if (this.Count < this.MinHistoryDepths)
            return;

        // Get the sum of values for the specific interval.
        double pmf = this.fpmf.Take(this.Period).Sum();
        double nmf = this.fnmf.Take(this.Period).Sum();

        // Set value to the "MFI" line buffer.
        if (nmf != 0.0 && pmf != -nmf)
            this.SetValue(100.0 - 100.0 / (1.0 + pmf / nmf));
        else
            this.SetValue(100.0);
    }
}