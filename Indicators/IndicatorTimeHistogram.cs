// Copyright QUANTOWER LLC. © 2017-2023. All rights reserved.

using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using TradingPlatform.BusinessLayer;

namespace VolumeIndicators;

internal class IndicatorTimeHistogram : Indicator, IVolumeAnalysisIndicator
{
    [InputParameter("Data type", variants: new object[]
    {
        "Trades", VolumeAnalysisField.Trades,
        "Buy trades", VolumeAnalysisField.BuyTrades,
        "Sell trades", VolumeAnalysisField.SellTrades,
        "Volume", VolumeAnalysisField.Volume,
        "Buy volume", VolumeAnalysisField.BuyVolume,
        "Buy volume, %", VolumeAnalysisField.BuyVolumePercent,
        "Sell volume", VolumeAnalysisField.SellVolume,
        "Sell volume, %", VolumeAnalysisField.SellVolumePercent,
        "Delta", VolumeAnalysisField.Delta,
        "Delta, %", VolumeAnalysisField.DeltaPercent,
        "Cumulative delta", VolumeAnalysisField.CumulativeDelta,
        "Open interest", VolumeAnalysisField.OpenInterest,
        "Average size", VolumeAnalysisField.AverageSize,
        "Average buy size", VolumeAnalysisField.AverageBuySize,
        "Average sell size", VolumeAnalysisField.AverageSellSize,
        "Max one trade Vol.", VolumeAnalysisField.MaxOneTradeVolume,
        "Max one trade Vol., %", VolumeAnalysisField.MaxOneTradeVolumePercent,
    })]
    public VolumeAnalysisField FieldType = VolumeAnalysisField.Volume;

    public override string ShortName => $"Time histogram ({this.FieldType})";

    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorTimeHistogram.cs";

    public IndicatorTimeHistogram()
    {
        this.Name = "Time histogram";

        this.AddLineSeries("Histogram", Color.DodgerBlue, 3, LineStyle.Histogramm);

        this.SeparateWindow = true;
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        if (this.HistoricalData.VolumeAnalysisCalculationProgress == null)
            return;

        if (this.HistoricalData.VolumeAnalysisCalculationProgress.State != VolumeAnalysisCalculationState.Finished)
            return;

        this.DrawIndicatorValue();
    }

    #region IVolumeAnalysisIndicator

    public bool IsRequirePriceLevelsCalculation => false;

    public void VolumeAnalysisData_Loaded()
    {
        Task.Factory.StartNew(() =>
        {
            // wait (треба дочекатись поки індикатор пройде всю історію)
            while (this.Count != this.HistoricalData.Count)
                Thread.Sleep(20);

            // draw history
            for (int offset = 0; offset < this.Count; offset++)
                this.DrawIndicatorValue(offset);
        });
    }

    #endregion IVolumeAnalysisIndicator

    private void DrawIndicatorValue(int offset = 0)
    {
        if (this.GetVolumeAnalysisData(offset) is VolumeAnalysisData analysisData)
        {
            double value = analysisData.Total.GetValue(this.FieldType);
            this.SetValue(value, 0, offset);

            // Coloring
            if (this.IsDeltaBased(this.FieldType))
            {
                int markerOffset = offset + 1;

                if (this.Count <= markerOffset)
                    return;

                if (value > 0)
                    this.LinesSeries[0].SetMarker(markerOffset, Color.Green);
                else if (value < 0)
                    this.LinesSeries[0].SetMarker(markerOffset, Color.Red);
            }
        }
    }

    private bool IsDeltaBased(VolumeAnalysisField fieldType) => fieldType == VolumeAnalysisField.Delta || fieldType == VolumeAnalysisField.DeltaPercent || fieldType == VolumeAnalysisField.CumulativeDelta;
}