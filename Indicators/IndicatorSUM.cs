// Copyright QUANTOWER LLC. © 2017-2023. All rights reserved.

using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace OscillatorsIndicators;

public class IndicatorSUM : Indicator
{
    #region Parameters
    [InputParameter("Period", 10, 1, 99999, 0, 0)]
    public int Period = 14;

    [InputParameter("Sources prices", 1, variants: new object[] {
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

    public override string ShortName => $"{this.Name} ({this.Period}: {this.SourcePrice})";

    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorSUM.cs";

    #endregion Parameters

    public IndicatorSUM()
    {
        this.Name = "Sum";
        this.AddLineSeries("Sum", Color.Orange, 1, LineStyle.Solid);
        this.SeparateWindow = true;
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        var prevValue = this.Count > 1 ? this.GetValue(1) : 0;
        var prevPrice = this.Count <= this.Period ? 0 : this.GetPrice(this.SourcePrice, this.Period);

        var value = this.GetPrice(this.SourcePrice) + (double.IsNaN(prevValue) ? 0 : prevValue) - prevPrice;

        this.SetValue(value);
    }
}