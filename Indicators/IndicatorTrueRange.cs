using System;
using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace VolatilityIndicators;

public class IndicatorTrueRange : Indicator, IWatchlistIndicator
{
    [InputParameter("Value mode", 0, variants: new object[]
    {
        "Absolute True Range", TrueRangeValueMode.AbsoluteTrueRange,
        "Percent Candle Range (Long)", TrueRangeValueMode.PercentCandleRangeLong
    })]
    public TrueRangeValueMode ValueMode = TrueRangeValueMode.AbsoluteTrueRange;

    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorTrueRange.cs";

    public int MinHistoryDepths => 2;

    public IndicatorTrueRange()
        : base()
    {
        // Defines indicator's name and description.
        Name = "True Range";

        // Defines line on demand with particular parameters.
        this.AddLineSeries("TR", Color.CadetBlue, 1, LineStyle.Solid);

        this.SeparateWindow = true;
    }

    protected override void OnUpdate(UpdateArgs args)
    {

        double value = this.ValueMode switch
        {
            TrueRangeValueMode.PercentCandleRangeLong => this.CalculatePercentCandleRangeLong(),
            _ => this.CalculateTrueRange()
        };

        this.SetValue(value);
    }

    public double CalculateTrueRange(int offset = 0)
    {
        double hi = this.GetPrice(PriceType.High, offset);
        double lo = this.GetPrice(PriceType.Low, offset);

        double prevClose = (this.Count <= offset + 1) ? this.Close(offset)
            : this.Close(offset + 1);

        return Math.Max(hi - lo, Math.Max(Math.Abs(prevClose - hi), Math.Abs(prevClose - lo)));
    }

    public double CalculatePercentCandleRangeLong(int offset = 0)
    {
        double hi = this.GetPrice(PriceType.High, offset);
        double lo = this.GetPrice(PriceType.Low, offset);

        if (lo <= 0)
            return 0;

        return (hi - lo) / lo * 100.0;
    }
}

public enum TrueRangeValueMode
{
    AbsoluteTrueRange,
    PercentCandleRangeLong
}
