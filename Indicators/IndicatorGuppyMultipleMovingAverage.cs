// Copyright QUANTOWER LLC. © 2017-2023. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using TradingPlatform.BusinessLayer;

namespace MovingAverageIndicators;

public sealed class IndicatorGuppyMultipleMovingAverage : Indicator, IWatchlistIndicator
{
    [InputParameter("Fast MA period 1", 0, 1, 9999, 1, 0)]
    public int FastPeriod1 = 3;
    [InputParameter("Fast MA period 2", 1, 1, 9999, 1, 0)]
    public int FastPeriod2 = 5;
    [InputParameter("Fast MA period 3", 2, 1, 9999, 1, 0)]
    public int FastPeriod3 = 8;
    [InputParameter("Fast MA period 4", 3, 1, 9999, 1, 0)]
    public int FastPeriod4 = 10;
    [InputParameter("Fast MA period 5", 4, 1, 9999, 1, 0)]
    public int FastPeriod5 = 12;
    [InputParameter("Fast MA period 6", 5, 1, 9999, 1, 0)]
    public int FastPeriod6 = 15;

    [InputParameter("Slow MA period 1", 10, 1, 9999, 1, 0)]
    public int SlowPeriod1 = 30;
    [InputParameter("Slow MA period 2", 11, 1, 9999, 1, 0)]
    public int SlowPeriod2 = 35;
    [InputParameter("Slow MA period 3", 12, 1, 9999, 1, 0)]
    public int SlowPeriod3 = 40;
    [InputParameter("Slow MA period 4", 13, 1, 9999, 1, 0)]
    public int SlowPeriod4 = 45;
    [InputParameter("Slow MA period 5", 14, 1, 9999, 1, 0)]
    public int SlowPeriod5 = 50;
    [InputParameter("Slow MA period 6", 15, 1, 9999, 1, 0)]
    public int SlowPeriod6 = 60;

    [InputParameter("Sources prices for MA", 20, variants: new object[]{
         "Close", PriceType.Close,
         "Open", PriceType.Open,
         "High", PriceType.High,
         "Low", PriceType.Low,
         "Typical", PriceType.Typical,
         "Medium", PriceType.Median,
         "Weighted", PriceType.Weighted,
         "Volume", PriceType.Volume,
         "Open interest", PriceType.OpenInterest
    })]
    public PriceType SourcePrice = PriceType.Close;
    [InputParameter("Type of moving average", 21, variants: new object[]{
        "Simple Moving Average", MaMode.SMA,
        "Exponential Moving Average", MaMode.EMA,
        "Smoothed Moving Average", MaMode.SMMA,
        "Linearly Weighted Moving Average", MaMode.LWMA,
    })]
    public MaMode MaType = MaMode.EMA;

    //
    [InputParameter("Calculation type", 25, variants: new object[]
    {
        "All available data", IndicatorCalculationType.AllAvailableData,
        "By period", IndicatorCalculationType.ByPeriod,
    })]
    public IndicatorCalculationType CalculationType = Indicator.DEFAULT_CALCULATION_TYPE;

    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorGuppyMultipleMovingAverage.cs";
    public int MinHistoryDepths => this.periodsArray.Max();

    private readonly Dictionary<int, Indicator> maIndicators;
    private int[] periodsArray;

    public IndicatorGuppyMultipleMovingAverage()
    {
        this.maIndicators = new Dictionary<int, Indicator>();

        this.Name = "Guppy Multiple Moving Averages";

        this.AddLineSeries("Fast MA 1", Color.MediumVioletRed, 1, LineStyle.Solid);
        this.AddLineSeries("Fast MA 2", Color.MediumVioletRed, 1, LineStyle.Solid);
        this.AddLineSeries("Fast MA 3", Color.MediumVioletRed, 1, LineStyle.Solid);
        this.AddLineSeries("Fast MA 4", Color.MediumVioletRed, 1, LineStyle.Solid);
        this.AddLineSeries("Fast MA 5", Color.MediumVioletRed, 1, LineStyle.Solid);
        this.AddLineSeries("Fast MA 6", Color.MediumVioletRed, 1, LineStyle.Solid);

        this.AddLineSeries("Slow MA 1", Color.DodgerBlue, 1, LineStyle.Solid);
        this.AddLineSeries("Slow MA 2", Color.DodgerBlue, 1, LineStyle.Solid);
        this.AddLineSeries("Slow MA 3", Color.DodgerBlue, 1, LineStyle.Solid);
        this.AddLineSeries("Slow MA 4", Color.DodgerBlue, 1, LineStyle.Solid);
        this.AddLineSeries("Slow MA 5", Color.DodgerBlue, 1, LineStyle.Solid);
        this.AddLineSeries("Slow MA 6", Color.DodgerBlue, 1, LineStyle.Solid);

        this.SeparateWindow = false;
        this.periodsArray = this.GetPeriodsArray();
    }

    protected override void OnInit()
    {
        base.OnInit();
        // update 
        this.periodsArray = this.GetPeriodsArray();

        for (int i = 0; i < this.periodsArray.Length; i++)
        {
            try
            {
                var ma = Core.Indicators.BuiltIn.MA(this.periodsArray[i], this.SourcePrice, this.MaType, this.CalculationType);
                this.maIndicators[i] = ma;
                this.AddIndicator(ma);
            }
            catch (Exception ex)
            {
                Core.Loggers.Log(ex);
            }
        }
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        base.OnUpdate(args);

        foreach (var item in this.maIndicators)
        {
            if (this.Count < this.periodsArray[item.Key])
                continue;

            this.SetValue(item.Value.GetValue(), item.Key);
        }
    }

    protected override void OnClear()
    {
        base.OnClear();

        foreach (var indicator in this.maIndicators.Values)
            indicator?.Dispose();

        this.maIndicators.Clear();
    }

    private int[] GetPeriodsArray() => new int[]
    {
        this.FastPeriod1,
        this.FastPeriod2,
        this.FastPeriod3,
        this.FastPeriod4,
        this.FastPeriod5,
        this.FastPeriod6,
        this.SlowPeriod1,
        this.SlowPeriod2,
        this.SlowPeriod3,
        this.SlowPeriod4,
        this.SlowPeriod5,
        this.SlowPeriod6
    };
}