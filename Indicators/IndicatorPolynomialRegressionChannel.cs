// Copyright QUANTOWER LLC. © 2017-2024. All rights reserved.

using MathNet.Numerics;
using MathNet.Numerics.Statistics;
using System;
using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace Channels;

public sealed class IndicatorPolynomialRegressionChannel : Indicator
{
    [InputParameter("Period of Hull Moving Average", 10, 1, 9999, 1, 0)]
    public int MaPeriod;

    /// <summary>
    /// Price type of moving average. 
    /// </summary>
    [InputParameter("Sources prices for MA", 20, variants: new object[]
    {
        "Close", PriceType.Close,
        "Open", PriceType.Open,
        "High", PriceType.High,
        "Low", PriceType.Low,
        "Typical", PriceType.Typical,
        "Medium", PriceType.Median,
        "Weighted", PriceType.Weighted,
    })]
    public PriceType SourcePrice;

    [InputParameter("Type of moving average", 3, variants: new object[]{
        "Simple Moving Average", MaMode.SMA,
        "Exponential Moving Average", MaMode.EMA,
        "Smoothed Moving Average", MaMode.SMMA,
        "Linearly Weighted Moving Average", MaMode.LWMA,
    })]
    public MaMode MaType;

    [InputParameter("Polynomial degree", 10, 1, 9999, 1, 0)]
    public int polynomialDegree;

    private Indicator ma;
    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorPolynomialRegressionChannel.cs";
    public IndicatorPolynomialRegressionChannel()
        : base()
    {
        this.Name = "Polynomial Regression Channel";
        this.SeparateWindow = false;
        this.MaPeriod = 20;
        this.SourcePrice = PriceType.Close;
        this.MaType = MaMode.SMA;
        this.polynomialDegree = 2;

        this.AddLineSeries("Upper Band", Color.Green, 1, LineStyle.Solid);
        this.AddLineSeries("Lower Band", Color.Red, 1, LineStyle.Solid);
        this.AddLineSeries("Regression", Color.CadetBlue, 1, LineStyle.Solid);
    }
    protected override void OnInit()
    {
        this.ma = Core.Instance.Indicators.BuiltIn.MA(this.MaPeriod, this.SourcePrice, this.MaType);
        this.AddIndicator(this.ma);
    }
    protected override void OnUpdate(UpdateArgs args)
    {
        double[] xData = new double[this.MaPeriod];
        double[] yData = new double[this.MaPeriod];
        for (int i = 0; i < this.MaPeriod; i++)
        {
            xData[i] = i;
            yData[i] = this.ma.GetValue(i);
        }

        double[] coefficients = Fit.Polynomial(xData, yData, this.polynomialDegree);
        double[] predictedValues = new double[this.MaPeriod];
        for (int i = 0; i < this.MaPeriod; i++)
        {
            double predictedValue = 0;
            for (int j = 0; j <= this.polynomialDegree; j++)
                predictedValue += coefficients[j] * Math.Pow(i, j);

            predictedValues[i] = predictedValue;
        }

        double stdDev = Statistics.StandardDeviation(predictedValues);
        this.SetValue(predictedValues[0] + 2 * stdDev, 0);
        this.SetValue(predictedValues[0] - 2 * stdDev, 1);
        this.SetValue(predictedValues[0], 2);
    }
}