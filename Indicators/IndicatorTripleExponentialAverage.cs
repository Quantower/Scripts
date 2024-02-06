// Copyright QUANTOWER LLC. © 2017-2024. All rights reserved.

using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace MovingAverageIndicators;

public class IndicatorTripleExponentialAverage : Indicator, IWatchlistIndicator
{
    #region Parameters
    [InputParameter("Period of Exponential Moving Average", 0, 1, 9999, 1, 0)]
    public int MaPeriod = 9;

    [InputParameter("Sources prices for EMA", 1, variants: new object[]{
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
    //
    [InputParameter("Calculation type", 10, variants: new object[]
    {
        "All available data", IndicatorCalculationType.AllAvailableData,
        "By period", IndicatorCalculationType.ByPeriod,
    })]
    public IndicatorCalculationType CalculationType = Indicator.DEFAULT_CALCULATION_TYPE;

    public int MinHistoryDepths => this.MaPeriod * 3;
    public override string ShortName => $"TRIX ({this.MaPeriod}: {this.SourcePrice})";

    private Indicator emaIndicator;

    private HistoricalDataCustom doubleEmaHD;
    private Indicator doubleEmaIndicator;

    private HistoricalDataCustom trixEmaHD;
    private Indicator trixEma;

    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorTripleExponentialAverage.cs";

    #endregion Parameters

    public IndicatorTripleExponentialAverage()
    {
        this.Name = "Triple Exponential Average";

        this.AddLineSeries("TRIX line", Color.Orange, 2, LineStyle.Solid);
        this.AddLineLevel(0d, "Zero line", Color.Gray, 1, LineStyle.Dot);

        this.SeparateWindow = true;
    }

    protected override void OnInit()
    {
        base.OnInit();

        this.emaIndicator = Core.Indicators.BuiltIn.EMA(this.MaPeriod, PriceType.Close, this.CalculationType);
        this.AddIndicator(this.emaIndicator);

        this.doubleEmaHD = new HistoricalDataCustom(this);
        this.doubleEmaIndicator = Core.Indicators.BuiltIn.EMA(this.MaPeriod, PriceType.Close, this.CalculationType);
        this.doubleEmaHD.AddIndicator(this.doubleEmaIndicator);

        this.trixEmaHD = new HistoricalDataCustom(this);
        this.trixEma = Core.Indicators.BuiltIn.EMA(this.MaPeriod, PriceType.Close, this.CalculationType);
        this.trixEmaHD.AddIndicator(this.trixEma);
    }
    protected override void OnUpdate(UpdateArgs args)
    {
        base.OnUpdate(args);

        if (this.Count < this.MaPeriod)
            return;

        this.doubleEmaHD[PriceType.Close] = this.emaIndicator.GetValue();

        if (this.Count < this.MaPeriod * 2)
            return;

        this.trixEmaHD[PriceType.Close] = this.doubleEmaIndicator.GetValue();

        if (this.Count < this.MinHistoryDepths)
            return;

        var trix = (this.trixEma.GetValue() - this.trixEma.GetValue(1)) / this.trixEma.GetValue(1);
        this.SetValue(trix);
    }
    protected override void OnClear()
    {
        base.OnClear();

        this.emaIndicator?.Dispose();
        this.doubleEmaIndicator?.Dispose();
        this.trixEma?.Dispose();

        this.doubleEmaHD?.Dispose();
        this.trixEmaHD?.Dispose();
    }
}