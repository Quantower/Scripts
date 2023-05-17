// Copyright QUANTOWER LLC. © 2017-2023. All rights reserved.

using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace MovingAverages;

public sealed class IndicatorPivotPointMovingAverage : Indicator, IWatchlistIndicator
{
    // Define 'Period' input parameter and set allowable range (from 1 to 999) 
    [InputParameter("Period of PPMA", 0, 1, 9999, 1, 0)]
    public int Period = 5;

    public int MinHistoryDepths => this.Period;
    public override string ShortName => $"PPMA ({this.Period})";
    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorPivotPointMovingAverage.cs";

    /// <summary>
    /// Indicator's constructor. Contains general information: name, description, LineSeries etc. 
    /// </summary>
    public IndicatorPivotPointMovingAverage()
    {
        // Defines indicator's name and description.
        this.Name = "Pivot Point Moving Average";
        this.Description = "Uses the pivot point calculation as the input a simple moving average";

        // Define two lines with particular parameters 
        this.AddLineSeries("PP", Color.CadetBlue, 1, LineStyle.Solid);
        this.AddLineSeries("PPMA", Color.Yellow, 1, LineStyle.Solid);

        this.SeparateWindow = false;
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
        // Get pivot value for current bar
        double pivot = this.GetPivotByOffset(0);

        // Set "pivot" value to the 'PP' line buffer (line index is 0)
        this.SetValue(pivot);

        // Skip some period for correct calculation.  
        if (this.Count < this.MinHistoryDepths)
            return;

        // Calculate the sum of pivot values for the range (PPMAPeriod)
        double sum = pivot;
        for (int i = 1; i < this.Period; i++)
            sum += this.GetPivotByOffset(i);

        this.SetValue(sum / this.Period, 1);
    }

    /// <summary>
    /// Compute pivot point value
    /// </summary>
    /// <param name="offset">Historical offset</param>
    /// <returns>Pivot point value</returns>
    private double GetPivotByOffset(int offset) => (this.High(offset) + this.Low(offset) + this.Close(offset)) / 3;
}