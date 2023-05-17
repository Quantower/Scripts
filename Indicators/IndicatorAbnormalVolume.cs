// Copyright QUANTOWER LLC. © 2017-2023. All rights reserved.

using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace VolumeIndicators;

public class IndicatorAbnormalVolume : Indicator, IWatchlistIndicator
{
    [InputParameter]
    public int Period = 60;

    public int MinHistoryDepths => Period;
    public override string ShortName => "AV";

    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorAbnormalVolume.cs";

    /// <summary>
    /// Indicator's constructor. Contains general information: name, description, LineSeries etc. 
    /// </summary>
    public IndicatorAbnormalVolume()
        : base()
    {
        // Defines indicator's name and description.
        this.Name = "Abnormal Volume";
        this.Description = "Shows the ratio of the volume of the current bar to the 'Period' of the previous ones";

        // Defines line on demand with particular parameters.
        this.AddLineSeries("line1", Color.CadetBlue, 1, LineStyle.Solid);

        // By default indicator will be applied on main window of the chart
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

        this.SetValue(this.Volume() / averageVolume);
    }
}