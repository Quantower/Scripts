// Copyright QUANTOWER LLC. © 2017-2024. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace VolumeIndicators;

public class IndicatorDeltaFlow : Indicator, IVolumeAnalysisIndicator, IWatchlistIndicator
{
    #region Parameters
    public const string COLORING_SCHEME_SI = "Coloring scheme";

    public override string HelpLink => "https://help.quantower.com/analytics-panels/chart/technical-indicators/volume/delta-flow";

    [InputParameter(COLORING_SCHEME_SI, 10, variants: new object[]
    {
        "By delta", DeltaPaceColoringScheme.ByDelta,
        "By bar", DeltaPaceColoringScheme.ByBar
    })]
    internal DeltaPaceColoringScheme ColoringScheme = DeltaPaceColoringScheme.ByDelta;

    internal PairColor PairColor;

    private bool isVolumeAnalysisProgressSubscribed;
    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorDeltaFlow.cs";

    #endregion Parameters

    public IndicatorDeltaFlow()
    {
        this.Name = "Delta Flow";
        this.AddLineSeries("Histogram", Color.Gray, 3, LineStyle.Histogramm);
        this.IsUpdateTypesSupported = false;

        this.PairColor = new PairColor()
        {
            Color1 = Color.FromArgb(33, 150, 243),
            Color2 = Color.FromArgb(239, 83, 80),
            Text1 = loc._("Up"),
            Text2 = loc._("Down")
        };
        this.SeparateWindow = true;
    }

    #region Overrides
    protected override void OnInit()
    {
        this.isVolumeAnalysisProgressSubscribed = false;
    }
    protected override void OnUpdate(UpdateArgs args)
    {
        // 
        if (!this.isVolumeAnalysisProgressSubscribed)
        {
            if (this.HistoricalData?.VolumeAnalysisCalculationProgress != null)
            {
                this.HistoricalData.VolumeAnalysisCalculationProgress.ProgressChanged += VolumeAnalysisCalculationProgress_ProgressChanged;
                this.isVolumeAnalysisProgressSubscribed = true;         
            }
        }

        this.CalculateIndicatorByOffset(offset: 0);
    }
    public override IList<SettingItem> Settings 
    {
        get 
        { 
            var settings = base.Settings;

            var inputSepar = settings.GetItemByName(COLORING_SCHEME_SI)?.SeparatorGroup ?? new SettingItemSeparatorGroup();

            settings.Add(new SettingItemPairColor("HistogramColors", this.PairColor, 10)
            {
                SeparatorGroup = inputSepar,
                Text = loc._("Histogram colors")
            });

            return settings;
        }
        set
        {
            base.Settings = value;

            if (value.GetItemByName("HistogramColors") is SettingItemPairColor pairColorSI)
            {
                this.PairColor = (PairColor)pairColorSI.Value;
                this.Refresh();
            }
        }
    }
    protected override void OnClear()
    {
        if (this.HistoricalData?.VolumeAnalysisCalculationProgress != null)
            this.HistoricalData.VolumeAnalysisCalculationProgress.ProgressChanged -= VolumeAnalysisCalculationProgress_ProgressChanged;
    }
    #endregion Overrides

    #region Misc and Event handlers
    private void CalculateIndicatorByOffset(int offset)
    {
        var index = this.Count - 1 - offset;
        if (index < 0)
            return;

        var volumeAnalysis = this.HistoricalData[index, SeekOriginHistory.Begin].VolumeAnalysisData;

        if (volumeAnalysis != null)
        {
            var value = volumeAnalysis.Total.Delta * (this.High(offset) - this.Low(offset));
            this.SetValue(Math.Abs(value), 0, offset);

            var isUpColor =
                (ColoringScheme == DeltaPaceColoringScheme.ByDelta && volumeAnalysis.Total.Delta > 0) ||
                (ColoringScheme == DeltaPaceColoringScheme.ByBar && this.Close(offset) > this.Open(offset));

            if (isUpColor)
                this.LinesSeries[0].SetMarker(offset, this.PairColor.Color1);
            else
                this.LinesSeries[0].SetMarker(offset, this.PairColor.Color2);
        }
    }
    private void RecalculateAllIndicator()
    {
        for (int i = 0; i < this.Count - 1; i++)
            this.CalculateIndicatorByOffset(i);
    }

    private void VolumeAnalysisCalculationProgress_ProgressChanged(object sender, VolumeAnalysisTaskEventArgs e)
    {
        if (e.CalculationState == VolumeAnalysisCalculationState.Processing)
            this.RecalculateAllIndicator();
    }
    #endregion Misc and Event handlers

    #region IVolumeAnalysisIndicator
    public bool IsRequirePriceLevelsCalculation => false;
    public void VolumeAnalysisData_Loaded()
    {
        this.RecalculateAllIndicator();
    }
    #endregion IVolumeAnalysisIndicator

    #region IWatchlistIndicator
    public int MinHistoryDepths => 2;
    #endregion IWatchlistIndicator

    internal enum DeltaPaceColoringScheme
    {
        ByDelta = 0,
        ByBar = 10,
    }
}