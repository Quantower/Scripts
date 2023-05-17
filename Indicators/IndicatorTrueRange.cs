// Copyright QUANTOWER LLC. © 2017-2023. All rights reserved.

using System;
using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace VolatilityIndicators;

public class IndicatorTrueRange : Indicator
{
    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorTrueRange.cs";

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
        double tr = this.CalculateTrueRange();

        this.SetValue(tr);
    }

    public double CalculateTrueRange(int offset = 0)
    {
        double hi = this.GetPrice(PriceType.High, offset);
        double lo = this.GetPrice(PriceType.Low, offset);

        double prevClose = (Count <= offset + 1) ? this.Close(offset)
                                                 : this.Close(offset + 1);

        return Math.Max(hi - lo, Math.Max(Math.Abs(prevClose - hi), Math.Abs(prevClose - lo)));
    }
}