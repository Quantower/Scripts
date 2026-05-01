// Copyright QUANTOWER LLC. © 2017-2024. All rights reserved.

using System;
using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace Oscillators;

public class IndicatorPriceMomentumOscillator : Indicator
{
    [InputParameter("Sources prices", 2, variants: new object[] {
            "Close", PriceType.Close,
            "Open", PriceType.Open,
            "High", PriceType.High,
            "Low", PriceType.Low,
            "Typical", PriceType.Typical,
            "Medium", PriceType.Median,
            "Weighted", PriceType.Weighted,
        })]
    public PriceType PriceType = PriceType.Close;
    [InputParameter("Smoothing period", 0, 1, 999)]
    public int SmoothingPeriod = 35;
    [InputParameter("PMO Period", 0, 1, 999)]
    public int PMOPeriod = 20;
    [InputParameter("Signal Line Period", 0, 1, 999)]
    public int SLPeriod = 10;

    private CustomSmoothingFunction MA1;
    private CustomSmoothingFunction MA2;
    private CustomSmoothingFunction MA3;

    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorPriceMomentumOscillator.cs";
    public IndicatorPriceMomentumOscillator()
        : base()
    {
        Name = "Price Momentum Oscillator";
        AddLineSeries("PMOLine", Color.CadetBlue, 1, LineStyle.Solid);
        AddLineSeries("Signal Lyne", Color.Orange, 1, LineStyle.Dash);
        SeparateWindow = true;
    }

    protected override void OnInit()
    {
        this.MA1 = new CustomSmoothingFunction(SmoothingPeriod);
        this.MA2 = new CustomSmoothingFunction(PMOPeriod);
        this.MA3 = new CustomSmoothingFunction(SLPeriod);
    }
    protected override void OnUpdate(UpdateArgs args)
    {
        if (this.Count < 2)
            return;
        if (args.Reason == UpdateReason.HistoricalBar || args.Reason == UpdateReason.NewBar)
        {
            this.MA1.newBar();
            this.MA2.newBar();
            this.MA3.newBar();
        }
        double rate = 100 * (this.GetPrice(this.PriceType, 0) - this.GetPrice(this.PriceType, 1)) / this.GetPrice(this.PriceType, 1);
        double smoothing = this.MA1.Calculate(rate);
        double PMO = this.MA2.Calculate(10 * smoothing);
        double SignalLine = this.MA3.Calculate(PMO);
        this.SetValue(PMO);
        this.SetValue(SignalLine, 1);
    }
}
internal sealed class CustomSmoothingFunction
{
    private readonly double Multiplier;
    private double previousValue;
    private bool previousValueCalculated;
    public CustomSmoothingFunction(int Period)
    {
        this.Multiplier = 2.0 / (Period);
        previousValue = 0;
        previousValueCalculated = false;
    }
    public double Calculate(double value)
    {
        if (previousValue == 0)
        {
            previousValue = value;
            return previousValue;
        }
        double currentValue = ((value - this.previousValue) * this.Multiplier) + this.previousValue;
        if (!previousValueCalculated)
        {
            previousValue = currentValue;
            previousValueCalculated = true;
        }
        return currentValue;
    }
    public void newBar()
    {
        previousValueCalculated = false;
    }
}
