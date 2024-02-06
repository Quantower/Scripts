// Copyright QUANTOWER LLC. © 2017-2024. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace TrendIndicators;

public class IndicatorOmniTrend : Indicator, IWatchlistIndicator
{
    #region Parameters
    private const double RISK = 0.2;

    [InputParameter("Period of Moving Average", 0, 1, 999)]
    public int MaPeriod = 13;

    [InputParameter("Type of Moving Average", 1, variants: new object[] {
        "Simple", MaMode.SMA,
        "Exponential", MaMode.EMA,
        "Modified", MaMode.SMMA,
        "Linear Weighted", MaMode.LWMA})]
    public MaMode MAType = MaMode.EMA;
    //
    [InputParameter("Calculation type", 5, variants: new object[]
    {
        "All available data", IndicatorCalculationType.AllAvailableData,
        "By period", IndicatorCalculationType.ByPeriod,
    })]
    public IndicatorCalculationType CalculationType = Indicator.DEFAULT_CALCULATION_TYPE;

    [InputParameter("Source price", 10, variants: new object[]{
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

    [InputParameter("Period of ATR", 15, 1, 9999)]
    public int AtrPeriod = 11;

    [InputParameter("Volatility's factor", 20, 0.1, 10.0, 0.1, 1)]
    public double Kv = 1.5;

    public override string ShortName => $"OT ({this.MaPeriod}: {this.MAType}: {this.SourcePrice})";

    private double sMax;
    private double sMin;
    private double prevSMax;
    private double prevSMin;
    private OmniTrendDirection currentTrend;
    private OmniTrendDirection prevTrend;
    private double tUp;
    private double tDown;
    private double prevTDown;
    private double prevTUp;

    private readonly IndicatorLineMarker downMarker;
    private readonly IndicatorLineMarker upMarker;
    private Indicator atr;
    private Indicator ma;

    public int MaxPeriod => Math.Max(this.AtrPeriod, this.MaPeriod);
    public int MinHistoryDepths => this.MaxPeriod * 2;

    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorOmniTrend.cs";

    #endregion Parameters

    public IndicatorOmniTrend()
    {
        this.Name = "OmniTrend";

        this.AddLineSeries("Up", Color.Orange, 2, LineStyle.Solid);
        this.AddLineSeries("Down", Color.DodgerBlue, 2, LineStyle.Solid);

        this.upMarker = new IndicatorLineMarker(this.LinesSeries[0].Color, IndicatorLineMarkerIconType.None, IndicatorLineMarkerIconType.UpArrow);
        this.downMarker = new IndicatorLineMarker(this.LinesSeries[1].Color, IndicatorLineMarkerIconType.DownArrow, IndicatorLineMarkerIconType.None);

        this.SeparateWindow = false;
    }

    #region Overrides
    protected override void OnInit()
    {
        base.OnInit();

        this.atr = Core.Indicators.BuiltIn.ATR(this.AtrPeriod, this.MAType, this.CalculationType);
        this.ma = Core.Indicators.BuiltIn.MA(this.MaPeriod, this.SourcePrice, this.MAType, this.CalculationType);

        this.AddIndicator(this.atr);
        this.AddIndicator(this.ma);
    }
    protected override void OnUpdate(UpdateArgs args)
    {
        base.OnUpdate(args);

        if (this.Count < this.MaxPeriod)
            return;

        if (args.Reason == UpdateReason.HistoricalBar || args.Reason == UpdateReason.NewBar)
        {
            this.prevSMax = this.sMax;
            this.prevSMin = this.sMin;
            this.prevTDown = this.tDown;
            this.prevTUp = this.tUp;
            this.prevTrend = this.currentTrend;
        }

        this.sMax = this.ma.GetValue() + this.Kv * this.atr.GetValue();
        this.sMin = this.ma.GetValue() - this.Kv * this.atr.GetValue();

        if (this.GetPrice(PriceType.High) > this.prevSMax)
            this.currentTrend = OmniTrendDirection.Up;
        if (this.GetPrice(PriceType.Low) < this.prevSMin)
            this.currentTrend = OmniTrendDirection.Down;

        if (this.currentTrend == OmniTrendDirection.Up)
        {
            if (this.sMin < this.prevSMin)
                this.sMin = this.prevSMin;
            this.tUp = this.sMin - (RISK - 1.0) * this.atr.GetValue();

            this.SetValue(this.tUp, 0);

            if (this.prevTrend != this.currentTrend)
                this.SetMarker(OmniTrendDirection.Up);

            if (this.tUp < this.prevTUp)
                this.tUp = this.prevTUp;
        }
        else if (this.currentTrend == OmniTrendDirection.Down)
        {
            if (this.sMax > this.prevSMax)
                this.sMax = this.prevSMax;
            this.tDown = this.sMax + (RISK - 1.0) * this.atr.GetValue();

            this.SetValue(this.tDown, 1);

            if (this.prevTrend != this.currentTrend)
                this.SetMarker(OmniTrendDirection.Down);

            if (this.tDown > this.prevTDown && this.prevTDown != 0)
                this.tDown = this.prevTDown;
        }
    }
    public override IList<SettingItem> Settings
    {
        get => base.Settings;
        set
        {
            base.Settings = value;

            this.UpdateMarkerColors();
        }
    }
    #endregion Overrides

    #region Misc
    private void SetMarker(OmniTrendDirection direction)
    {
        switch (direction)
        {
            case OmniTrendDirection.Up:
                this.LinesSeries[0].SetMarker(1, this.upMarker);
                break;
            case OmniTrendDirection.Down:
                this.LinesSeries[1].SetMarker(1, this.downMarker);
                break;
            default:
                break;
        }
    }
    private void UpdateMarkerColors()
    {
        if (!this.LinesSeries[0].Color.Equals(this.upMarker.Color))
            this.upMarker.Color = this.LinesSeries[0].Color;

        if (!this.LinesSeries[1].Color.Equals(this.downMarker.Color))
            this.downMarker.Color = this.LinesSeries[1].Color;
    }
    #endregion Misc
}

internal enum OmniTrendDirection { Up, Down }