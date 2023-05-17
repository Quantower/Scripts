// Copyright QUANTOWER LLC. © 2017-2023. All rights reserved.

using BarsDataIndicators.Utils;
using System;
using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace BarsDataIndicators;

public class IndicatorCumulativeDelta : CandleDrawIndicator, IVolumeAnalysisIndicator
{
    public override string ShortName => this.Name;

    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorCumulativeDelta.cs";

    public IndicatorCumulativeDelta()
        : base()
    {
        this.Name = "Cumulative delta";
        this.LinesSeries[0].Name = "Cumulative open";
        this.LinesSeries[1].Name = "Cumulative high";
        this.LinesSeries[2].Name = "Cumulative low";
        this.LinesSeries[3].Name = "Cumulative close";

        this.AddLineLevel(0d, "Zero line", Color.Gray, 1, LineStyle.DashDot);

        this.SeparateWindow = true;
    }

    protected override void OnInit()
    {
        this.IsLoading = true;
        base.OnInit();
    }
    protected override void OnUpdate(UpdateArgs args)
    {
        if (args.Reason == UpdateReason.HistoricalBar)
            return;

        this.CalculateIndicatorByOffset(offset: 0);
    }

    private void CalculateIndicatorByOffset(int offset)
    {
        int prevOffset = offset + 1;

        if (this.HistoricalData.Count <= prevOffset)
            return;

        var currentItem = this.HistoricalData[offset].VolumeAnalysisData;
        var prevItem = this.HistoricalData[prevOffset].VolumeAnalysisData;

        if (currentItem != null && prevItem != null)
        {
            double prevCumulativeDelta = !currentItem.Total.CumulativeDelta.Equals(currentItem.Total.Delta)
                ? prevItem.Total.CumulativeDelta
                : 0d;

            double high = currentItem.Total.MaxDelta != double.MinValue
                ? prevCumulativeDelta + Math.Abs(currentItem.Total.MaxDelta)
                : Math.Max(currentItem.Total.CumulativeDelta, prevCumulativeDelta);

            double low = currentItem.Total.MinDelta != double.MaxValue
                ? prevCumulativeDelta - Math.Abs(currentItem.Total.MinDelta)
                : Math.Min(currentItem.Total.CumulativeDelta, prevCumulativeDelta);

            this.SetValues(prevCumulativeDelta, high, low, currentItem.Total.CumulativeDelta, offset);
        }
        else
            this.SetHole(offset);
    }

    #region IVolumeAnalysisIndicator

    bool IVolumeAnalysisIndicator.IsRequirePriceLevelsCalculation => false;
    void IVolumeAnalysisIndicator.VolumeAnalysisData_Loaded()
    {
        for (int i = 0; i < this.Count; i++)
            this.CalculateIndicatorByOffset(i);

        this.IsLoading = false;
    }

    #endregion IVolumeAnalysisIndicator
}