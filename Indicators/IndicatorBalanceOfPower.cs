// Copyright QUANTOWER LLC. © 2017-2024. All rights reserved.

using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace OscillatorsIndicators;

public class IndicatorBalanceOfPower : Indicator
{
    private const int CURRENT_BAR_OFFSET = 0;
    private const PriceType INDICATOR_SOURCE_PRICE_TYPE = PriceType.Close;

    [InputParameter("Smoothing period", 0, 1, 999, 1, 2)]
    public int SmoothingPeriod = 5;

    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorBalanceOfPower.cs";

    [InputParameter("Smoothing Type", 3, variants: new object[]
    {
        "Simple Moving Average", MaMode.SMA,
        "Exponential Moving Average", MaMode.EMA,
        "Smoothed Moving Average", MaMode.SMMA,
        "Linearly Weighted Moving Average", MaMode.LWMA,
    })]
    public MaMode MaType = MaMode.SMA;

    public override string ShortName => $"BOP ({this.SmoothingPeriod})";

    private HistoricalDataCustom smoothingSource;
    private Indicator smoothing;

    public IndicatorBalanceOfPower()
        : base()
    {
        this.Name = "Balance Of Power";
        this.AddLineSeries("BoP Line", Color.CadetBlue, 1, LineStyle.Solid);
        this.AddLineSeries("Smoothed Line", Color.Blue, 1, LineStyle.Solid);
        this.SeparateWindow = true;
    }
    protected override void OnInit()
    {
        this.smoothing = Core.Instance.Indicators.BuiltIn.MA(this.SmoothingPeriod, INDICATOR_SOURCE_PRICE_TYPE, this.MaType);
        this.smoothingSource = new HistoricalDataCustom(this);
        this.smoothingSource.AddIndicator(this.smoothing);
    }
    protected override void OnUpdate(UpdateArgs args)
    {
        if (this.Count <= this.SmoothingPeriod + 1)
            return;

        var highLowDeltaPrice = this.High(CURRENT_BAR_OFFSET) - this.Low(CURRENT_BAR_OFFSET);
        var bop = highLowDeltaPrice != default
            ? (this.Close(CURRENT_BAR_OFFSET) - this.Open(CURRENT_BAR_OFFSET)) / highLowDeltaPrice
            : default;

        this.smoothingSource[INDICATOR_SOURCE_PRICE_TYPE] = bop;

        this.SetValue(bop);
        this.SetValue(this.smoothing.GetValue(), 1);
    }
}
