// Copyright QUANTOWER LLC. © 2017-2024. All rights reserved.

using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace MovingAverages;

/// <summary>
/// The SMMA gives recent prices an equal weighting to historic prices.
/// </summary>
public sealed class IndicatorSmoothedMovingAverage : Indicator, IWatchlistIndicator
{
    // Period of moving average. 
    [InputParameter("Period of Smoothed Moving Average", 0, 1, 999, 1, 0)]
    public int MaPeriod = 7;

    // Price type of moving average. 
    [InputParameter("Sources prices for MA", 1, variants: new object[]
    {
        "Close", PriceType.Close,
        "Open", PriceType.Open,
        "High", PriceType.High,
        "Low", PriceType.Low,
        "Typical", PriceType.Typical,
        "Median", PriceType.Median,
        "Weighted", PriceType.Weighted,
        "Volume", PriceType.Volume,
        "Open interest", PriceType.OpenInterest
    })]
    public PriceType SourcePrice = PriceType.Close;

    [InputParameter("Calculation type", 10, variants: new object[]
    {
        "All available data", IndicatorCalculationType.AllAvailableData,
        "By period", IndicatorCalculationType.ByPeriod,
    })]
    public IndicatorCalculationType CalculationType = Indicator.DEFAULT_CALCULATION_TYPE;

    /// <summary>
    /// Indicator's constructor. Contains general information: name, description, LineSeries etc. 
    /// </summary>
    public IndicatorSmoothedMovingAverage()
        : base()
    {
        // Defines indicator's group, name and description.            
        this.Name = "Smoothed Moving Average";
        this.Description = "The SMMA gives recent prices an equal weighting to historic prices.";

        // Defines line on demand with particular parameters.
        this.AddLineSeries("SMMA", Color.DodgerBlue, 1, LineStyle.Solid);

        this.SeparateWindow = false;
    }

    public int MinHistoryDepths => this.MaPeriod * 2;
    public override string ShortName => $"SMMA ({this.MaPeriod}: {this.SourcePrice})";
    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorSmoothedMovingAverage.cs";

    /// <summary>
    /// Calculation entry point. This function is called when a price data updates. 
    /// Will be runing under the HistoricalBar mode during history loading. 
    /// Under NewTick during realtime. 
    /// Under NewBar if start of the new bar is required.
    /// </summary>
    /// <param name="args">Provides data of updating reason and incoming price.</param>
    protected override void OnUpdate(UpdateArgs args)
    {
        // Checking, if current amount of bars less, than period of moving average - calculation is impossible.
        if (this.Count < this.MinHistoryDepths)
            return;

        if (this.CalculationType == IndicatorCalculationType.ByPeriod)
            this.CalculateByPeriod();
        else
            this.CalculateForAllData();
    }

    private void CalculateByPeriod(int offset = 0)
    {
        int startOffset = offset + this.MaPeriod;

        if (this.Count <= startOffset + this.MaPeriod)
            return;

        // calcualte start value
        double smma = this.CalculateSMA(startOffset);

        for (int i = startOffset - 1; i >= offset; i--)
            smma = this.CalculateSMMA(i, smma);

        this.SetValue(smma, offset);
    }

    private double CalculateSMMA(int offset, double prevSMMA) => (prevSMMA * (this.MaPeriod - 1) + this.GetPrice(this.SourcePrice, offset)) / this.MaPeriod;

    private void CalculateForAllData()
    {
        // Calculating the current value of the indicator.
        double value;
        double prevSMMA = this.GetValue(1);

        if (double.IsNaN(prevSMMA))
        {
            // Calculates initial value as Simple Moving Average.
            value = this.CalculateSMA(0);
        }
        else
        {
            value = this.CalculateSMMA(0, prevSMMA);
        }

        // Sets value for displaying on the chart.
        this.SetValue(value);
    }

    private double CalculateSMA(int offset)
    {
        double sum = 0d;
        for (int i = 0; i < this.MaPeriod; i++)
            sum += this.GetPrice(this.SourcePrice, offset + i);

        return sum / this.MaPeriod;
    }
}