
// Copyright QUANTOWER LLC. © 2017-2021. All rights reserved.

using System.Collections.Generic;
using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace OscillatorsIndicators;

public class IndicatorDeltaDivergenceReversal : Indicator, IVolumeAnalysisIndicator
{
    private Color buyColor = Color.Green;
    private Color sellColor = Color.Red;
    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorDeltaDivergenceReversal.cs";

    public IndicatorDeltaDivergenceReversal()
        : base()
    {
        Name = "Delta Divergence Reversal";

        AddLineSeries("Low", Color.CadetBlue, 1, LineStyle.Points);
        AddLineSeries("High", Color.IndianRed, 1, LineStyle.Points);

        SeparateWindow = false;
    }
    public bool IsRequirePriceLevelsCalculation => false;

    public void VolumeAnalysisData_Loaded()
    {
        for (int i = 0; i < this.Count - 1; i++)
            DrawMarkers(i);
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        SetValue(High(), 1);
        SetValue(Low(), 0);
        if (this.HistoricalData.VolumeAnalysisCalculationProgress == null || this.HistoricalData.VolumeAnalysisCalculationProgress.State != VolumeAnalysisCalculationState.Finished)
            return;
        DrawMarkers();
    }

    private void DrawMarkers(int offset = 0)
    {
        var currentItem = this.HistoricalData[offset];
        if (currentItem.VolumeAnalysisData == null)
            return;

        var previousItem = this.HistoricalData[offset + 1];
        double delta = currentItem.VolumeAnalysisData.Total.Delta;
        double currentHigh = ((HistoryItemBar)currentItem).High;
        double previousHigh = ((HistoryItemBar)previousItem).High;
        double currentLow = ((HistoryItemBar)currentItem).Low;
        double previousLow = ((HistoryItemBar)previousItem).Low;

        if ((currentHigh > previousHigh && currentLow > previousLow) && delta < 0)
            LinesSeries[1].SetMarker(offset, new IndicatorLineMarker(sellColor, upperIcon: IndicatorLineMarkerIconType.DownArrow));
        else if ((currentHigh < previousHigh && currentLow < previousLow) && delta >= 0)
            LinesSeries[0].SetMarker(offset, new IndicatorLineMarker(buyColor, bottomIcon: IndicatorLineMarkerIconType.UpArrow));
        else
        {
            LinesSeries[0].RemoveMarker(offset);
            LinesSeries[1].RemoveMarker(offset);
        }
    }

    public override IList<SettingItem> Settings
    {
        get
        {
            var settings = base.Settings;
            settings.Add(new SettingItemColor("BuyColor", this.buyColor)
            {
                Text = "Buy Color"
            });
            settings.Add(new SettingItemColor("SellColor", this.sellColor)
            {
                Text = "Sell Color"
            });
            return settings;
        }
        set
        {
            base.Settings = value;
            if (value.TryGetValue("BuyColor", out Color buyColor))
                this.buyColor = buyColor;
            if (value.TryGetValue("SellColor", out Color sellColor))
                this.sellColor = sellColor;
            OnSettingsUpdated();
        }
    }
    protected override void OnSettingsUpdated()
    {
        base.OnSettingsUpdated();
        for (int i = 0; i < this.Count - 1; i++)
            DrawMarkers(i);
    }
}