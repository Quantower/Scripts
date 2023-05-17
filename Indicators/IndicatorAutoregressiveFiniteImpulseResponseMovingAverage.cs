// Copyright QUANTOWER LLC. © 2017-2023. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using TradingPlatform.BusinessLayer;

namespace MovingAverages;

public sealed class IndicatorAutoregressiveFiniteImpulseResponseMovingAverage : Indicator
{
    // Displays Input Parameter as input field (or checkbox if value type is bolean).
    [InputParameter("Window Period", 0, 1, 9999)]
    public int Period = 20;

    // Displays Input Parameter as dropdown list.
    [InputParameter("Sources prices for the Afirma line", 1, variants: new object[] {
         "Close", PriceType.Close,
         "Open", PriceType.Open,
         "High", PriceType.High,
         "Low", PriceType.Low,
         "Typical", PriceType.Typical,
         "Medium", PriceType.Median,
         "Weighted", PriceType.Weighted}
    )]
    // Displays Input Parameter as dropdown list.
    public PriceType SourcePrice = PriceType.Close;

    [InputParameter("Windowing function", 2, variants: new object[]{
         "Hanning", AfirmaMode.Hanning,
         "Hamming", AfirmaMode.Hamming,
         "Blackman", AfirmaMode.Blackman,
         "Blackman - Harris", AfirmaMode.BlackmanHarris}
    )]
    public AfirmaMode win = AfirmaMode.Hanning;
    // Displays Input Parameter as input field (or checkbox if value type is bolean).
    [InputParameter("Least-squares method", 3)]
    public bool least_squares_method = true;

    public override string ShortName => $"AFIRMA by {this.win} Windowing";
    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorAutoregressiveFiniteImpulseResponseMovingAverage.cs";

    private double den;
    private double sx2;
    private double sx3;
    private double sx4;
    private double sx5;
    private double sx6;
    private int n;
    private readonly List<double> koef;
    private readonly List<double> val;
    private readonly List<double> firma;

    /// <summary>
    /// Indicator's constructor. Contains general information: name, description, LineSeries etc. 
    /// </summary>
    public IndicatorAutoregressiveFiniteImpulseResponseMovingAverage()
        : base()
    {
        this.Name = "Autoregressive Finite Impulse Response Moving Average";
        this.Description = "Multiple digital filters (Windows of Hanning, Hamming, Blackman, Blackman-Harris) smoothed by a cubic spline fitting with using the least squares method";

        // Defines line on demand with particular parameters.
        this.AddLineSeries("AFIRMA Line", Color.Blue, 1, LineStyle.Solid);
        this.SeparateWindow = false;

        this.koef = new List<double>();
        this.val = new List<double>();
        this.firma = new List<double>();
    }

    /// <summary>
    /// This function will be called after creating an indicator as well as after its input params reset or chart (symbol or timeframe) updates.
    /// </summary>
    protected override void OnInit()
    {
        // http://en.wikipedia.org/wiki/Window_function
        for (int k = 0; k < this.Period; k++)
        {
            switch (this.win)
            {
                case AfirmaMode.Hanning:
                    this.koef.Insert(0, 0.50 - 0.50 * Math.Cos(2.0 * Math.PI * k / this.Period));
                    break;
                case AfirmaMode.Hamming:
                    this.koef.Insert(0, 0.54 - 0.46 * Math.Cos(2.0 * Math.PI * k / this.Period));
                    break;
                case AfirmaMode.Blackman:
                    this.koef.Insert(0, 0.42 - 0.50 * Math.Cos(2.0 * Math.PI * k / this.Period) + 0.08 * Math.Cos(4.0 * Math.PI * k / this.Period));
                    break;
                case AfirmaMode.BlackmanHarris:
                    this.koef.Insert(0, 0.35875 - 0.48829 * Math.Cos(2.0 * Math.PI * k / this.Period) +
                       0.14128 * Math.Cos(4.0 * Math.PI * k / this.Period) -
                       0.01168 * Math.Cos(6.0 * Math.PI * k / this.Period));
                    break;
            }

        }
        //Calculate sums for the least-squares method
        if (this.least_squares_method)
        {
            this.n = (this.Period - 1) / 2;
            this.sx2 = (2 * this.n + 1) / 3.0;
            this.sx3 = this.n * (this.n + 1) / 2.0;
            this.sx4 = this.sx2 * (3 * this.n * this.n + 3 * this.n - 1) / 5.0;
            this.sx5 = this.sx3 * (2 * this.n * this.n + 2 * this.n - 1) / 3.0;
            this.sx6 = this.sx2 * (3 * this.n * this.n * this.n * (this.n + 2) - 3 * this.n + 1) / 7.0;
            this.den = this.sx6 * this.sx4 / this.sx5 - this.sx5;
        }
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
        if (args.Reason != UpdateReason.NewTick)
        {
            this.val.Insert(0, 0);

            if (this.Count > this.Period)
            {
                for (int k = 0; k < this.Period; k++)
                {
                    this.val[0] += this.GetPrice(this.SourcePrice, k) * this.koef[k] / this.koef.Sum();
                }
                this.SetValue(this.val[0]);
            }
            if (this.least_squares_method && this.val.Count - 1 > this.n)
            {
                double a0 = this.val[this.n];
                double a1 = this.val[this.n] - this.val[this.n + 1];
                double sx2y = 0.0;
                double sx3y = 0.0;
                for (int i = 0; i <= this.n; i++)
                {
                    sx2y += i * i * this.GetPrice(this.SourcePrice, i);
                    sx3y += i * i * i * this.GetPrice(this.SourcePrice, i);
                }
                sx2y = 2.0 * sx2y / this.n / (this.n + 1);
                sx3y = 2.0 * sx3y / this.n / (this.n + 1);
                double p = sx2y - a0 * this.sx2 - a1 * this.sx3;
                double q = sx3y - a0 * this.sx3 - a1 * this.sx4;
                double a2 = (p * this.sx6 / this.sx5 - q) / this.den;
                double a3 = (q * this.sx4 / this.sx5 - p) / this.den;
                this.firma.Clear();
                for (int i = 0; i <= this.n; i++)
                    this.firma.Insert(0, a0 + i * a1 + i * i * a2 + i * i * i * a3);
            }
        }
        // Least square method to minimise time lag
        //if (args.Reason != UpdateReason.HistoricalBar) {
        if (this.least_squares_method && this.Count > this.Period && this.firma.Count >= this.n)
        {
            // update last n values to adapt the market
            for (int i = 0; i <= this.n; i++)
                this.SetValue(this.firma[i], i);
        }
        //}
    }
}