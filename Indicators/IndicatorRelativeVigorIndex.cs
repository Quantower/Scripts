// Copyright QUANTOWER LLC. © 2017-2023. All rights reserved.

using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace OscillatorsIndicators;

public class IndicatorRelativeVigorIndex : Indicator
{
    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorRelativeVigorIndex.cs";

    public IndicatorRelativeVigorIndex()
        : base()
    {
        // Defines indicator's name and description.
        Name = "Relative Vigor Index";
        Description = "Indicator measures the strength of a trend by comparing a security's closing price to its trading range while smoothing the results using a simple moving average.";

        // Defines line on demand with particular parameters.
        AddLineSeries("Signal line", Color.Red, 1, LineStyle.Solid);
        AddLineSeries("RVI line", Color.Green, 1, LineStyle.Solid);

        // By default indicator will be applied on main window of the chart
        SeparateWindow = true;
    }

    protected override void OnInit()
    {
        // Add your initialization code here
    }

    [InputParameter]
    public int Period = 2;
    protected override void OnUpdate(UpdateArgs args)
    {
        if (Count <= Period)
            return;
        SetValue(GetRVI(), 1);
        SetValue(GetSignalLine(), 0);
    }

    private double CloseMinusOpen(int index = 0)
    {
        return Close(index) - Open(index);
    }
    private double HightMinusLow(int index = 0)
    {
        return High(index) - Low(index);
    }
    private double Numerator(int index = 0)
    {
        double numerator = CloseMinusOpen(index) + 2 * CloseMinusOpen(index + 1) + 2 * CloseMinusOpen(index + 2) + CloseMinusOpen(index + 3);
        return numerator / 6;
    }
    private double Denominator(int index = 0)
    {
        double denominator = HightMinusLow(index) + 2 * HightMinusLow(index + 1) + 2 * HightMinusLow(index + 2) + HightMinusLow(index + 3);
        return denominator / 6;
    }
    private double GetRVI(int index = 0)
    {
        double numeratorSMA = 0;
        double denominatorSMA = 0;

        for (int i = 0; i < Period; i++)
        {
            numeratorSMA = numeratorSMA + Numerator(i + index);
            denominatorSMA = denominatorSMA + Denominator(i + index);
        }
        numeratorSMA = numeratorSMA / Period;
        denominatorSMA = denominatorSMA / Period;
        return numeratorSMA / denominatorSMA;
    }
    private double GetSignalLine()
    {
        double signalLine = 0;
        signalLine = GetRVI() + 2 * GetRVI(1) + 2 * GetRVI(2) + GetRVI(3);


        return signalLine / 6;
    }
}