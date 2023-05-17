// Copyright QUANTOWER LLC. © 2017-2023. All rights reserved.

using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace OscillatorsIndicators;

public class IndicatorRelativeStrengthIndexSmoothed : Indicator, IWatchlistIndicator
{
    #region Parameters
    [InputParameter("RSI period", 10, 1, 9999, 1, 0)]
    public int Period = 14;

    [InputParameter("Sources prices", 20, variants: new object[] {
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

    [InputParameter("Smooth period", 30, 1, 9999, 1, 0)]
    public int SmoothPeriod = 5;

    [InputParameter("Type of Moving Average", 40, variants: new object[] {
        "Simple", MaMode.SMA,
        "Exponential", MaMode.EMA,
        "Smoothed Modified", MaMode.SMMA,
        "Linear Weighted", MaMode.LWMA}
    )]
    public MaMode MaType = MaMode.SMA;

    [InputParameter("MA period", 50, 1, 9999, 1, 0)]
    public int MAPeriod = 9;

    [InputParameter("Calculation type", 60, variants: new object[]
    {
        "All available data", IndicatorCalculationType.AllAvailableData,
        "By period", IndicatorCalculationType.ByPeriod,
    })]
    public IndicatorCalculationType CalculationType = Indicator.DEFAULT_CALCULATION_TYPE;
    private Indicator ema;
    private HistoricalDataCustom customHD;
    private Indicator rsi;

    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorRelativeStrengthIndexSmoothed.cs";

    #endregion Parameters

    #region IWatchlistIndicator
    public int MinHistoryDepths
    {
        get
        {
            var rsiPeriod = this.Period + this.MAPeriod;

            if (this.CalculationType == IndicatorCalculationType.ByPeriod)
                rsiPeriod *= 2;

            return EmaPeriod + rsiPeriod;
        }
    }
    private int EmaPeriod => this.CalculationType == IndicatorCalculationType.ByPeriod ? this.SmoothPeriod : this.SmoothPeriod * 2;
    #endregion IWatchlistIndicator

    public IndicatorRelativeStrengthIndexSmoothed()
    {
        this.Name = "RSI Smoothed";

        this.AddLineSeries("RSI Smoothed", Color.DodgerBlue, 2, LineStyle.Solid);
        this.AddLineSeries("RSI Average", Color.Orange, 1, LineStyle.Solid);

        this.AddLineLevel(70d, "Up level", Color.Gray, 1, LineStyle.Dash);
        this.AddLineLevel(30d, "Down level", Color.Gray, 1, LineStyle.Dash);
        this.AddLineLevel(50d, "Middle level", Color.Gray, 1, LineStyle.Dash);

        this.SeparateWindow = true;
    }

    protected override void OnInit()
    {
        this.ema = Core.Instance.Indicators.BuiltIn.EMA(this.SmoothPeriod, this.SourcePrice, this.CalculationType);
        this.AddIndicator(this.ema);

        this.customHD = new HistoricalDataCustom();
        this.rsi = Core.Instance.Indicators.BuiltIn.RSI(this.Period, this.SourcePrice, RSIMode.Exponential, this.MaType, this.MAPeriod, this.CalculationType);
        this.customHD.AddIndicator(this.rsi);
    }
    protected override void OnUpdate(UpdateArgs args)
    {
        if (this.Count <= this.EmaPeriod)
            return;

        var emaValue = this.ema.GetValue();

        if (args.Reason != UpdateReason.NewTick)
            this.customHD.AddValue(emaValue, emaValue, emaValue, emaValue);
        else
            this.customHD.SetValue(emaValue, emaValue, emaValue, emaValue);

        if (this.Count < this.MinHistoryDepths)
            return;

        SetValue(this.rsi.GetValue(), 0);
        SetValue(this.rsi.GetValue(0, 1), 1);
    }
    protected override void OnClear()
    {
        this.RemoveIndicator(this.ema);
        this.ema?.Dispose();

        this.customHD.RemoveIndicator(this.rsi);
        this.rsi?.Dispose();
        this.customHD?.Dispose();
    }
}