// Copyright QUANTOWER LLC. © 2017-2023. All rights reserved.

using TradingPlatform.BusinessLayer;
using System.Drawing;

namespace MovingAverages;

public class IndicatorFYL : Indicator, IWatchlistIndicator
{
    #region Parameters
    [InputParameter("Period of Linear Regression", 10, 2, 9999)]
    public int Period = 20;
    [InputParameter("Sources prices for the regression line", 20, variants: new object[] {
         "Close", PriceType.Close,
         "Open", PriceType.Open,
         "High", PriceType.High,
         "Low", PriceType.Low,
         "Typical", PriceType.Typical,
         "Medium", PriceType.Median,
         "Weighted", PriceType.Weighted,
         "Volume", PriceType.Volume,
         "Open interest", PriceType.OpenInterest
    })]
    public PriceType SourcePrice = PriceType.Close;
    public int MinHistoryDepths => this.Period;
    public override string ShortName => $"FYL ({this.Period})";

    private double sumX;
    private double divisor;

    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorFYL.cs";

    #endregion Parameters

    public IndicatorFYL()
        : base()
    {
        // Defines indicator's name and description.
        this.Name = "FYL";
        this.Description = "Regression line";

        // Defines line on demand with particular parameters.
        this.AddLineSeries("line1", Color.CadetBlue, 1, LineStyle.Solid);

        // By default indicator will be applied on main window of the chart
        this.SeparateWindow = false;
    }

    protected override void OnInit()
    {
        this.sumX = (double)Period * (Period - 1) * 0.5;
        this.divisor = sumX * sumX - (double)Period * Period * (Period - 1) * (2 * Period - 1) / 6;
    }
    protected override void OnUpdate(UpdateArgs args)
    {
        //Проверяем что истории достаточно
        if (Count < MinHistoryDepths)
        {
            double price = GetPrice(SourcePrice);
            SetValue(price);
            return;
        }

        double sumY = 0.0;
        double sumXY = 0.0;

        // Calculation of sum
        for (int i = 0; i < Period; i++)
        {
            double price = this.GetPrice(SourcePrice, i);
            sumY += price;
            sumXY += i * price;
        }

        // Calculation of coefficients
        double a = (Period * sumXY - sumX * sumY) / divisor;
        double b = (sumY - a * sumX) / Period;

        // Setting of current value
        this.SetValue(a * (Period - 1) + b);
    }
}