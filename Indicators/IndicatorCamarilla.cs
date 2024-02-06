// Copyright QUANTOWER LLC. © 2017-2024. All rights reserved.

using System;
using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace TrendIndicators;

public class IndicatorCamarilla : Indicator
{
    private HistoricalData dailyHistoricalData;

    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorCamarilla.cs";

    public IndicatorCamarilla()
        : base()
    {
        Name = "Camarilla";

        AddLineSeries("R1", Color.IndianRed, 1, LineStyle.Solid);
        AddLineSeries("S1", Color.IndianRed, 1, LineStyle.Solid);

        AddLineSeries("R2", Color.OrangeRed, 1, LineStyle.Solid);
        AddLineSeries("S2", Color.OrangeRed, 1, LineStyle.Solid);

        AddLineSeries("R3", Color.DarkOliveGreen, 1, LineStyle.Solid);
        AddLineSeries("S3", Color.DarkOliveGreen, 1, LineStyle.Solid);

        AddLineSeries("R4", Color.Green, 1, LineStyle.Solid);
        AddLineSeries("S4", Color.Green, 1, LineStyle.Solid);

        SeparateWindow = false;
    }

    protected override void OnInit()
    {
        // Download daily historical data with a ten-day margin
        this.dailyHistoricalData = this.Symbol.GetHistory(Period.DAY1, Time(Count - 1).Date.AddDays(-10));
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        int index = (int)dailyHistoricalData.GetIndexByTime(Time().Date.AddDays(-1).Ticks, SeekOriginHistory.End);

        // Get the bar value of the previous day
        HistoryItemBar previousDayBar = (HistoryItemBar)dailyHistoricalData[index];

        for (int i = 1; i <= 8; i++)
        {
            int number = (int)Math.Round((double)i / 2, MidpointRounding.AwayFromZero);
            if (i % 2 == 1)
                SetValue((previousDayBar.High - previousDayBar.Low) * (1.1 / Fibonacci(4 - number + 1)) + previousDayBar.Close, i - 1); //Set value for resistance
            else
                SetValue(previousDayBar.Close - (previousDayBar.High - previousDayBar.Low) * (1.1 / Fibonacci(4 - number + 1)), i - 1);//Set value to support
        }
    }

    private int Fibonacci(int n) //Get coefficient - 2,4,6,12 in this case
    {
        int a = 2;
        int b = 0;
        if (n == 1)
            return a;
        for (int i = 2; i <= n; i++)
        {
            if (i % 2 == 0)
                b = a * 2;
            else
                b = a * 3 / 2;
            a = b;
        }
        return b;
    }
}