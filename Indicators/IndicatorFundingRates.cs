// Copyright QUANTOWER LLC. © 2017-2021. All rights reserved.

using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace BarsDataIndicators;

public sealed class IndicatorFundingRates : Indicator
{
    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorFundingRates.cs";

    public IndicatorFundingRates()
    {
        this.Name = "Funding Rates";
        this.Description = "";

        this.AddLineSeries("FundingRate", Color.Green, 2);
        this.SeparateWindow = true;
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        double fundingRate = this.FundingRate();

        if (!double.IsNaN(fundingRate))
            fundingRate *= 100;

        this.SetValue(fundingRate);
    }
}