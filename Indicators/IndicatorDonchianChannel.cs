// Copyright QUANTOWER LLC. © 2017-2023. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using TradingPlatform.BusinessLayer;

namespace ChanneIsIndicators;

public class IndicatorDonchianChannel : Indicator, IWatchlistIndicator
{
    #region Parameters

    [InputParameter("Period", 0, 1, 9999, 1, 0)]
    public int Period = 20;

    [InputParameter("Extremes", 1, variants: new object[] {
         "High-Low", ExtremType.HighLow,
         "High-Low-Open", ExtremType.HighLowOpen,
         "High-Low-Close", ExtremType.HighLowClose,
         "Open-High-Low", ExtremType.OpenHighLow,
         "Close-High-Low", ExtremType.CloseHighLow}
    )]
    public ExtremType Extremes = ExtremType.HighLow;

    [InputParameter("Margins", 2, increment: 1, decimalPlaces: 0)]
    public int Margins = -2;

    [InputParameter("Shift", 3, 0, 9999, increment: 1, decimalPlaces: 0)]
    public int Shift = 0;

    private MinMaxBuffer upperBuffer;
    private MinMaxBuffer lowerBuffer;

    public override string ShortName => $"Donchian ({this.Period}: {this.Extremes}: {this.Margins}: {this.Shift})";

    public int MinHistoryDepths => this.Period + this.Shift;

    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorDonchianChannel.cs";

    #endregion Parameters

    public IndicatorDonchianChannel()
    {
        this.Name = "Donchian Channel";
        this.AddLineSeries("Upper line", Color.Green, 2, LineStyle.Solid);
        this.AddLineSeries("Middle line", Color.Gray, 2, LineStyle.Solid);
        this.AddLineSeries("Lower line", Color.Red, 2, LineStyle.Solid);

        this.SeparateWindow = false;
    }

    protected override void OnInit()
    {
        base.OnInit();

        this.upperBuffer = new MinMaxBuffer(this.Period);
        this.lowerBuffer = new MinMaxBuffer(this.Period);
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        if (this.Count <= this.Shift)
            return;

        double smin, smax;

        var isNewBar = this.HistoricalData.Period == TradingPlatform.BusinessLayer.Period.TICK1
            ? args.Reason == UpdateReason.NewTick || args.Reason == UpdateReason.HistoricalBar
            : args.Reason == UpdateReason.NewBar || args.Reason == UpdateReason.HistoricalBar;

        switch (this.Extremes)
        {
            case ExtremType.HighLow:
                this.upperBuffer.UpdateValue(this.GetPrice(PriceType.High, this.Shift), isNewBar);
                this.lowerBuffer.UpdateValue(this.GetPrice(PriceType.Low, this.Shift), isNewBar);
                break;
            case ExtremType.HighLowOpen:
                this.upperBuffer.UpdateValue((this.GetPrice(PriceType.Open, this.Shift) + this.GetPrice(PriceType.High, this.Shift)) / 2, isNewBar);
                this.lowerBuffer.UpdateValue((this.GetPrice(PriceType.Open, this.Shift) + this.GetPrice(PriceType.Low, this.Shift)) / 2, isNewBar);
                break;
            case ExtremType.HighLowClose:
                this.upperBuffer.UpdateValue((this.GetPrice(PriceType.Close, this.Shift) + this.GetPrice(PriceType.High, this.Shift)) / 2, isNewBar);
                this.lowerBuffer.UpdateValue((this.GetPrice(PriceType.Close, this.Shift) + this.GetPrice(PriceType.Low, this.Shift)) / 2, isNewBar);
                break;
            case ExtremType.OpenHighLow:
                this.upperBuffer.UpdateValue(this.GetPrice(PriceType.Open, this.Shift), isNewBar);
                this.lowerBuffer.UpdateValue(this.GetPrice(PriceType.Open, this.Shift), isNewBar);
                break;
            case ExtremType.CloseHighLow:
                this.upperBuffer.UpdateValue(this.GetPrice(PriceType.Close, this.Shift), isNewBar);
                this.lowerBuffer.UpdateValue(this.GetPrice(PriceType.Close, this.Shift), isNewBar);
                break;
        }

        if (this.Count <= this.Period)
            return;

        smin = this.lowerBuffer.Min + (this.upperBuffer.Max - this.lowerBuffer.Min) * this.Margins / 100;
        smax = this.upperBuffer.Max - (this.upperBuffer.Max - this.lowerBuffer.Min) * this.Margins / 100;

        this.SetValue(smax, 0);
        this.SetValue((smax + smin) / 2, 1);
        this.SetValue(smin, 2);

    }

    protected override void OnClear()
    {
        base.OnClear();

        this.upperBuffer?.Clear();
        this.lowerBuffer?.Clear();
    }
}
public enum ExtremType
{
    HighLow,
    HighLowOpen,
    HighLowClose,
    OpenHighLow,
    CloseHighLow
};

public class MinMaxBuffer
{
    public readonly List<double> values;
    public int Range { get; private set; }

    public double Min { get; private set; }
    public double Max { get; private set; }

    public MinMaxBuffer(int range)
    {
        this.Range = range;
        this.Min = double.NaN;
        this.Max = double.NaN;
        this.values = Enumerable.Repeat(double.NaN, this.Range).ToList();
    }

    public void UpdateValue(double value, bool isNewbar)
    {
        if (isNewbar)
        {
            if (this.values.Count > 0)
                this.values.RemoveAt(this.values.Count - 1);

            if (this.values.Count > 0)
            {
                this.Max = this.values.Max();
                this.Min = this.values.Min();
            }
            else
            {
                this.Max = value;
                this.Min = value;
            }

            this.values.Insert(0, double.NaN);
        }

        this.values[0] = value;
        this.Max = Math.Max(value, this.Max);
        this.Min = Math.Min(value, this.Min);
    }

    internal void Clear() => this.values.Clear();
}