// Copyright QUANTOWER LLC. © 2017-2023. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace TrendIndicators;

public class IndicatorNRMA : Indicator
{
    #region Parameters
    [InputParameter("K", 0, 0, 999, 0.1, 1)]
    public double K = 1;

    [InputParameter("Fast", 1, 0, 999, 0.1, 1)]
    public double Fast = 2;

    [InputParameter("Sharp", 2, 0, 999, 0.1, 1)]
    public double Sharp = 2;

    [InputParameter("Period", 3, 1, 9999, 1, 0)]
    public int Period = 3;

    [InputParameter("Sources prices", 4, variants: new object[]{
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

    [InputParameter("Type of moving average", 5, variants: new object[]{
        "Simple Moving Average", MaMode.SMA,
        "Exponential Moving Average", MaMode.EMA,
        "Smoothed Moving Average", MaMode.SMMA,
        "Linearly Weighted Moving Average", MaMode.LWMA,
    })]
    public MaMode MaType = MaMode.EMA;
    //
    [InputParameter("Calculation type", 10, variants: new object[]
    {
        "All available data", IndicatorCalculationType.AllAvailableData,
        "By period", IndicatorCalculationType.ByPeriod,
    })]
    public IndicatorCalculationType CalculationType = Indicator.DEFAULT_CALCULATION_TYPE;


    private Indicator ma;
    private double price;
    private int Trend;
    private double LPrice;
    private double HPrice;
    private int prevTrend;
    private double NRatio;

    public override string ShortName => $"NRMA ({this.Period}: {this.MaType}: {this.Fast}: {this.Sharp})";

    public double F { get; private set; }

    private HistoricalDataCustom customHD;
    private IndicatorLineMarker circleMarker;

    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorNRMA.cs";

    #endregion Parameters

    public IndicatorNRMA()
    {
        this.Name = "NRMA";

        this.AddLineSeries("line1", Color.Orange, 1, LineStyle.Solid);
        this.AddLineSeries("line2", Color.Gray, 1, LineStyle.Solid);
        this.circleMarker = new IndicatorLineMarker(this.LinesSeries[1].Color, IndicatorLineMarkerIconType.None, IndicatorLineMarkerIconType.FillCircle);

        this.SeparateWindow = false;
    }

    protected override void OnInit()
    {
        base.OnInit();

        this.F = 2.0 / (1.0 + this.Fast);
        //this.LinesSeries[1].Visible = false;

        this.LinesSeries[1].DrawBegin = this.Period;
        this.LinesSeries[0].DrawBegin = this.Period;

        this.customHD = new HistoricalDataCustom(this);
        this.ma = Core.Indicators.BuiltIn.MA(this.Period, PriceType.Close, this.MaType, CalculationType);
        this.customHD.AddIndicator(this.ma);
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        base.OnUpdate(args);

        if (args.Reason == UpdateReason.HistoricalBar || args.Reason == UpdateReason.NewBar)
        {
            if (this.Count <= this.Period)
                this.SetValue(this.price, 0);
            else
                this.SetValue(this.GetValue(1, 0) + this.NRatio * this.F * (this.price - this.GetValue(1, 0)));

            prevTrend = Trend;
        }

        this.price = this.GetPrice(this.SourcePrice);

        if (this.Count <= this.Period)
        {
            if (this.GetPrice(PriceType.Close) > this.GetPrice(PriceType.Open))
            {
                this.Trend = 1;
                this.LPrice = this.price * (1.0 - this.K * 0.01);
                this.HPrice = 0;
                this.SetMarker();
                this.SetValue(this.LPrice, 1);
            }
            else
            {
                this.Trend = -1;
                this.HPrice = this.price * (1.0 + this.K * 0.01);
                this.LPrice = 0;
                this.SetMarker();
                this.SetValue(this.HPrice, 1);
            }
            return;
        }

        this.Trend = this.prevTrend;

        if (this.Trend > 0)
        {
            if (this.price < this.GetValue(1, 1))
            {
                this.Trend = -1;
                this.HPrice = this.price * (1.0 + this.K * 0.01);
                this.LPrice = 0.0;
                this.SetMarker();
                this.SetValue(this.HPrice, 1);
            }
            else
            {
                this.Trend = 1;
                this.LPrice = price * (1.0 - K * 0.01);
                this.HPrice = 0.0;
                if (this.LPrice > this.GetValue(1, 1))
                {
                    this.SetMarker();
                    this.SetValue(this.LPrice, 1);
                }
                else
                {
                    this.SetMarker();
                    this.SetValue(this.GetValue(1, 1), 1);
                }
            }
        }
        else
        {
            if (price > GetValue(1, 1))
            {
                this.Trend = 1;
                this.LPrice = this.price * (1.0 - this.K * 0.01);
                this.HPrice = 0.0;
                this.SetMarker();
                this.SetValue(this.LPrice, 1);
            }
            else
            {
                this.Trend = -1;
                this.HPrice = this.price * (1.0 + this.K * 0.01);
                this.LPrice = 0.0;
                if (this.HPrice < this.GetValue(1, 1))
                {
                    this.SetMarker();
                    this.SetValue(this.HPrice, 1);
                }
                else
                {
                    this.SetMarker();
                    this.SetValue(this.GetValue(1, 1), 1);
                }
            }
        }

        this.customHD[PriceType.Close] = (100.0 * Math.Abs(price - GetValue(1, 1)) / price) / K;
        var maValue = this.ma.GetValue();

        if (!double.IsNaN(maValue))
            this.NRatio = Math.Pow(this.ma.GetValue(), this.Sharp);
    }

    public override IList<SettingItem> Settings
    {
        get => base.Settings;
        set
        {
            base.Settings = value;

            // update marker color.
            if (!this.circleMarker.Color.Equals(this.LinesSeries[1].Color))
                this.circleMarker.Color = this.LinesSeries[1].Color;
        }
    }

    private void SetMarker() => this.LinesSeries[1].SetMarker(0, this.circleMarker);
}