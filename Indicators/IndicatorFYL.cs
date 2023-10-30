// Copyright QUANTOWER LLC. © 2017-2023. All rights reserved.

using TradingPlatform.BusinessLayer;
using System.Drawing;

namespace MovingAverages;

#warning Не використовуйте абревіатури у назвах, особливо якщо якщо вони не загальноприйняті
public sealed class IndicatorFYL : Indicator, IWatchlistIndicator
{
    #region Parameters

    [InputParameter("Period of Linear Regression", 10, 2, 9999)]
    public int Period;

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
    public PriceType SourcePrice;

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

        this.Period = 20;
        this.SourcePrice = PriceType.Close;

        // By default indicator will be applied on main window of the chart
        this.SeparateWindow = false;

        // Defines line on demand with particular parameters.
        this.AddLineSeries("line1", Color.CadetBlue, 1, LineStyle.Solid);
    }

    protected override void OnInit()
    {
        this.sumX = (double)this.Period * (this.Period - 1) * 0.5;
        this.divisor = this.sumX * this.sumX - (double)this.Period * this.Period * (this.Period - 1) * (2 * this.Period - 1) / 6;
    }
    protected override void OnUpdate(UpdateArgs args)
    {
        //Проверяем что истории достаточно
        if (this.Count < this.MinHistoryDepths)
        {
            double price = this.GetPrice(this.SourcePrice);
            this.SetValue(price);
            return;
        }

        double sumY = 0.0;
        double sumXY = 0.0;

        // Calculation of sum
        for (int i = 0; i < this.Period; i++)
        {
            double price = this.GetPrice(this.SourcePrice, i);
            sumY += price;
            sumXY += i * price;
        }

        // Calculation of coefficients
        double a = (this.Period * sumXY - this.sumX * sumY) / this.divisor;
        double b = (sumY - a * this.sumX) / this.Period;

        // Setting of current value
        this.SetValue(a * (this.Period - 1) + b);
    }
}