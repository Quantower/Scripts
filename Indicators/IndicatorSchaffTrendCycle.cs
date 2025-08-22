// Copyright QUANTOWER LLC. © 2017-2024. All rights reserved.

using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace TrendIndicators;

public class IndicatorSchaffTrendCycle : Indicator
{
    [InputParameter("Fast Period", 10, 1, 9999, 1, 0)]
    public int fastPeriod;

    [InputParameter("Slow Period", 10, 1, 9999, 1, 0)]
    public int slowPeriod;

    [InputParameter("k Period", 10, 1, 9999, 1, 0)]
    public int kPeriod;

    [InputParameter("d Period", 10, 1, 9999, 1, 0)]
    public int dPeriod;

    [InputParameter("High Line", 10, 0, 100)]
    public int HighLine;

    [InputParameter("Low Line", 10, 0, 100)]
    public int LowLine;

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

    [InputParameter("Smoothing type", 3, variants: new object[]{
            "Simple Moving Average", MaMode.SMA,
            "Exponential Moving Average", MaMode.EMA,
            "Smoothed Moving Average", MaMode.SMMA,
            "Linearly Weighted Moving Average", MaMode.LWMA,
        })]
    public MaMode MaType;

    private Indicator fastMA;
    private Indicator slowMA;
    private Indicator stochasticOscillator;
    private Indicator stochasticOscillatorPF;
    private Indicator stochasticOscillatorPFF;

    private HistoricalDataCustom macd;
    private HistoricalDataCustom pf;
    private HistoricalDataCustom pff;

    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorSchaffTrendCycle.cs";
    public IndicatorSchaffTrendCycle()
        : base()
    {
        this.Name = "Schaff Trend Cycle";
        this.SeparateWindow = true;

        this.MaType = MaMode.SMA;
        this.SourcePrice = PriceType.Close;

        this.fastPeriod = 23;
        this.slowPeriod = 50;
        this.kPeriod = 10;
        this.dPeriod = 10;
        this.HighLine = 80;
        this.LowLine = 20;

        this.AddLineSeries("Main Line", Color.CadetBlue, 1, LineStyle.Solid);
        this.AddLineSeries("High Line", Color.Green, 1, LineStyle.Solid);
        this.AddLineSeries("Low Line", Color.Red, 1, LineStyle.Solid);
    }


    protected override void OnInit()
    {
        this.fastMA = Core.Instance.Indicators.BuiltIn.EMA(this.fastPeriod, this.SourcePrice);
        this.slowMA = Core.Instance.Indicators.BuiltIn.EMA(this.slowPeriod, this.SourcePrice);

        this.stochasticOscillator = Core.Instance.Indicators.BuiltIn.Stochastic(this.kPeriod, this.dPeriod, this.dPeriod * 2, this.MaType);
        this.stochasticOscillatorPF = Core.Instance.Indicators.BuiltIn.Stochastic(this.kPeriod, this.dPeriod, this.dPeriod * 2, this.MaType);
        this.stochasticOscillatorPFF = Core.Instance.Indicators.BuiltIn.Stochastic(this.kPeriod, this.dPeriod, this.dPeriod * 2, this.MaType);

        this.macd = new HistoricalDataCustom(this);
        this.pf = new HistoricalDataCustom(this);
        this.pff = new HistoricalDataCustom(this);

        this.AddIndicator(this.fastMA);
        this.AddIndicator(this.slowMA);

        this.macd.AddIndicator(this.stochasticOscillator);
        this.pf.AddIndicator(this.stochasticOscillatorPF);
        this.pff.AddIndicator(this.stochasticOscillatorPFF);
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        if (this.Count < this.slowPeriod)
            return;

        double currentMACD = this.fastMA.GetValue() - this.slowMA.GetValue();
        this.macd.SetValue(currentMACD, currentMACD, currentMACD, currentMACD);

        double d = this.stochasticOscillator.GetValue(0, 1);
        this.pf.SetValue(d, d, d, d);

        double pf = this.stochasticOscillatorPF.GetValue();
        this.pff.SetValue(pf, pf, pf, pf);

        double pff = this.stochasticOscillatorPFF.GetValue(0, 1);
        this.SetValue(pff);
        this.SetValue(this.HighLine, 1);
        this.SetValue(this.LowLine, 2);
    }
}