using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace Fractals;

public class IndicatorFractals : Indicator
{
    [InputParameter("Period", 10)]
    public int period = 3;

    [InputParameter("Local Maximum Color", 20)]
    public Color maximumColor = Color.Green;

    [InputParameter("Local Minimum Color", 30)]
    public Color minimumColor = Color.Red;

    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorFractals.cs";

    public IndicatorFractals()
        : base()
    {
        Name = "Fractals";
        Description = "My indicator's annotation";

        AddLineSeries("HighLine", Color.DarkOliveGreen, 1, LineStyle.Points);
        AddLineSeries("LowLine", Color.IndianRed, 1, LineStyle.Points);
        SeparateWindow = false;
    }

    protected override void OnInit()
    { }

    protected override void OnUpdate(UpdateArgs args)
    {
        if (Count < period * 2 + 1)
            return;

        double baseHigh = High(period);
        double baseLow = Low(period);

        double currentHigh;
        double currentLow;
        int minTrendValue = 0;
        int maxTrendValue = 0;

        SetValue(High(), 0);
        SetValue(Low(), 1);

        for (int i = 0; i <= period; i++)
        {
            currentHigh = High(period + i);
            currentLow = Low(period + i);

            if (baseHigh > currentHigh)
                maxTrendValue++;

            if (baseLow < currentLow)
                minTrendValue++;
        }
        for (int i = 0; i <= period; i++)
        {

            currentHigh = High(period - i);
            currentLow = Low(period - i);

            if (baseHigh > currentHigh)
                maxTrendValue++;

            if (baseLow < currentLow)
                minTrendValue++;
        }

        if (maxTrendValue == period * 2)
            LinesSeries[0].SetMarker(period, new IndicatorLineMarker(maximumColor, upperIcon: IndicatorLineMarkerIconType.UpArrow));
        if (minTrendValue == period * 2)
            LinesSeries[1].SetMarker(period, new IndicatorLineMarker(minimumColor, bottomIcon: IndicatorLineMarkerIconType.DownArrow));

        if (maxTrendValue != period * 2 && minTrendValue != period * 2)
        {
            LinesSeries[0].RemoveMarker(period);
            LinesSeries[1].RemoveMarker(period);
        }
    }
}