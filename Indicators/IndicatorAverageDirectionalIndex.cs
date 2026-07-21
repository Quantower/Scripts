using System;
using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace Trend;

public sealed class IndicatorAverageDirectionalIndex : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", 0, 1, 999, 1, 0)]
    public int Period = 14;

    public int MinHistoryDepths => (2 * this.Period - 1) * 2;
    public override string ShortName => $"ADX ({this.Period})";
    public override string HelpLink => "https://help.quantower.com/analytics-panels/chart/technical-indicators/trend/average-directional-movement-index-adx-indicator";
    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorAverageDirectionalIndex.cs";

    private HistoricalDataCustom customHDTrueRange;
    private HistoricalDataCustom customHDPlusDm;
    private HistoricalDataCustom customHDMinusDm;

    private Indicator maTrueRange;
    private Indicator maPlusDm;
    private Indicator maMinusDm;

    public IndicatorAverageDirectionalIndex()
        : base()
    {
        this.Name = "Average Directional Index";
        this.Description = "The ADX determines the strength of a prevailing trend.";

        this.AddLineSeries("ADX'Line", Color.Green, 1, LineStyle.Solid);
        this.AddLineSeries("+DI'Line", Color.Blue, 1, LineStyle.Solid);
        this.AddLineSeries("-DI'Line", Color.Red, 1, LineStyle.Solid);

        this.SeparateWindow = true;
    }

    protected override void OnInit()
    {
        this.customHDTrueRange = new HistoricalDataCustom(this);
        this.customHDPlusDm = new HistoricalDataCustom(this);
        this.customHDMinusDm = new HistoricalDataCustom(this);

        int wilderPeriod = 2 * this.Period - 1;

        this.maTrueRange = Core.Indicators.BuiltIn.MA(
            wilderPeriod,
            PriceType.Close,
            MaMode.EMA,
            IndicatorCalculationType.AllAvailableData);

        this.maPlusDm = Core.Indicators.BuiltIn.MA(
            wilderPeriod,
            PriceType.Close,
            MaMode.EMA,
            IndicatorCalculationType.AllAvailableData);

        this.maMinusDm = Core.Indicators.BuiltIn.MA(
            wilderPeriod,
            PriceType.Close,
            MaMode.EMA,
            IndicatorCalculationType.AllAvailableData);

        this.customHDTrueRange.AddIndicator(this.maTrueRange);
        this.customHDPlusDm.AddIndicator(this.maPlusDm);
        this.customHDMinusDm.AddIndicator(this.maMinusDm);
    }
    protected override void OnUpdate(UpdateArgs args)
    {
        if (this.Count < 2)
            return;

        double trueRange = this.CalculateTrueRange();
        double plusDm = this.CalculatePlusDm();
        double minusDm = this.CalculateMinusDm();

        this.customHDTrueRange[PriceType.Close] = trueRange;
        this.customHDPlusDm[PriceType.Close] = plusDm;
        this.customHDMinusDm[PriceType.Close] = minusDm;

        if (this.Count < this.Period + 1)
            return;

        double smoothedTrueRange = this.maTrueRange.GetValue();
        double smoothedPlusDm = this.maPlusDm.GetValue();
        double smoothedMinusDm = this.maMinusDm.GetValue();

        double plusDi = smoothedTrueRange != 0.0
            ? 100.0 * smoothedPlusDm / smoothedTrueRange
            : 0.0;

        double minusDi = smoothedTrueRange != 0.0
            ? 100.0 * smoothedMinusDm / smoothedTrueRange
            : 0.0;

        double dx = (plusDi + minusDi) != 0.0
            ? 100.0 * Math.Abs(plusDi - minusDi) / (plusDi + minusDi)
            : 0.0;

        double adx = this.Count > this.MinHistoryDepths
            ? (this.GetValue(1) * (this.Period - 1) + dx) / this.Period
            : dx;

        this.SetValue(adx);
        this.SetValue(plusDi, 1);
        this.SetValue(minusDi, 2);
    }

    private double CalculateTrueRange()
    {
        double hl = this.High() - this.Low();
        double hc = Math.Abs(this.High() - this.Close(1));
        double lc = Math.Abs(this.Low() - this.Close(1));

        return Math.Max(hl, Math.Max(hc, lc));
    }

    private double CalculatePlusDm()
    {
        double upMove = this.High() - this.High(1);
        double downMove = this.Low(1) - this.Low();

        return (upMove > downMove && upMove > 0.0) ? upMove : 0.0;
    }

    private double CalculateMinusDm()
    {
        double upMove = this.High() - this.High(1);
        double downMove = this.Low(1) - this.Low();

        return (downMove > upMove && downMove > 0.0) ? downMove : 0.0;
    }
}