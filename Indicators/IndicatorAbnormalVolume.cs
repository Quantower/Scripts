// Copyright QUANTOWER LLC. © 2017-2023. All rights reserved.

using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace VolumeIndicators;

public class IndicatorAbnormalVolume : Indicator, IWatchlistIndicator
{
    [InputParameter]
    public int Period = 60;

    [InputParameter]
    public double SignalLine = 2.0;

    [InputParameter]
    public Color MarkerColor = Color.Green;

    public int MinHistoryDepths => Period;
    public override string ShortName => "AV";

    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorAbnormalVolume.cs";

    public IndicatorAbnormalVolume()
        : base()
    {
        this.Name = "Abnormal Volume";
        this.Description = "Shows the ratio of the volume of the current bar to the 'Period' of the previous ones";

        this.AddLineSeries("Main Line", Color.CadetBlue, 1, LineStyle.Histogramm);
        this.AddLineSeries("Signal Line", Color.CadetBlue, 1, LineStyle.Solid);
        this.SeparateWindow = true;
    }

    protected override void OnInit()
    {
        // Add your initialization code here
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        if (this.Count <= this.Period)
            return;

        double sum = 0.0; // Sum of prices
        for (int i = 1; i <= this.Period; i++)
            sum += this.Volume(i);

        double averageVolume = sum / this.Period;
        double value = this.Volume() / averageVolume;
        this.SetValue(value);
        this.SetValue(this.SignalLine, 1);
        LinesSeries[0].RemoveMarker(0);
        if (GetValue() >= GetValue(0, 1))
        {
            LinesSeries[0].SetMarker(0, new IndicatorLineMarker(MarkerColor, IndicatorLineMarkerIconType.DownArrow));
        }
    }
}