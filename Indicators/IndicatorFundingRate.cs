// Copyright QUANTOWER LLC. © 2017-2024. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TradingPlatform.BusinessLayer;

namespace VolatilityIndicators;

internal class IndicatorFundingRate : Indicator
{
    #region Parameters

    [InputParameter("Base interest rate (Daily), %", 10, 0, 9999)]
    public double BaseInterestRate = 0.03;

    [InputParameter("Quote interest rate (Daily), %", 20, 0, 9999)]
    public double QuoteInterestRate = 0.06;

    [InputParameter("Funding interval", 30, 0, 9999, 1, 0)]
    public int FundingInterval = 3;

    [InputParameter("Premium index", 40)]
    public Symbol CurrenntSymbol;

    public double InterestRate;
    private CancellationTokenSource cts;

    private readonly Dictionary<int, decimal> fundindRates;

    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorFundingRate.cs";

    #endregion Parameters

    public IndicatorFundingRate()
    {
        this.Name = "Funding rate";
        this.AddLineSeries("line", Color.Orange, 2, LineStyle.Solid);
        this.fundindRates = new Dictionary<int, decimal>();
        this.SeparateWindow = true;
    }

    protected override void OnInit()
    {
        base.OnInit();

        // find symbol
        if (this.CurrenntSymbol == null)
        {
            var symbols = Core.SearchSymbols(new SearchSymbolsRequestParameters()
            {
                FilterName = ".XBTUSDPI8H",
                ExchangeIds = new List<string>(),
                SymbolTypes = new List<SymbolType>() { SymbolType.Indexes }
            });

            if (symbols != null && symbols.Count > 0)
                this.CurrenntSymbol = symbols[0];
        }

        // abort previous task
        this.cts?.Cancel();

        this.cts = new CancellationTokenSource();
        var token = this.cts.Token;

        // start 
        if (this.CurrenntSymbol != null)
        {
            this.InterestRate = (this.QuoteInterestRate - this.BaseInterestRate) / this.FundingInterval;

            Task.Factory.StartNew(() =>
            {
                try
                {
                    if (token.IsCancellationRequested)
                        return;

                    // load history
                    using var history = this.CurrenntSymbol.GetHistory(new HistoryRequestParameters()
                    {
                        Aggregation = new HistoryAggregationTime(Period.DAY1),
                        FromTime = this.HistoricalData.FromTime,
                        ToTime = this.HistoricalData.ToTime,
                        CancellationToken = token,
                        HistoryType = HistoryType.Last
                    });

                    if (token.IsCancellationRequested)
                        return;

                    if (history != null && history.Count > 0)
                    {
                        // calculation
                        for (int i = 0; i < history.Count; i++)
                        {
                            var prices = new List<double>()
                        {
                            history[i, SeekOriginHistory.Begin][PriceType.Open],
                            history[i, SeekOriginHistory.Begin][PriceType.High],
                            history[i, SeekOriginHistory.Begin][PriceType.Low],
                            history[i, SeekOriginHistory.Begin][PriceType.Close]
                        };

                            decimal f0 = this.CalculateF(prices.Max());
                            decimal f1 = this.CalculateF(prices.Min());
                            decimal f2 = this.CalculateF(history[i, SeekOriginHistory.Begin][PriceType.Close]);

                            decimal f = f0 + f1 + f2;

                            this.fundindRates[i] = f;
                        }

                        // delay
                        Task.Delay(1000, token).Wait(token);

                        if (token.IsCancellationRequested)
                            return;

                        // draw line
                        for (int i = 0; i < this.HistoricalData.Count; i++)
                        {
                            int offest = i == 0 ? this.Count - 1 : this.Count - i - 1;

                            int fundindIndex = (int)history.GetIndexByTime(this.HistoricalData[i].TicksLeft);

                            if (this.fundindRates.ContainsKey(fundindIndex))
                                this.SetValue((double)this.fundindRates[fundindIndex], 0, offest);
                        }
                    }
                    else
                    {
                        Core.Loggers.Log("Funding rate: No data", LoggingLevel.Error);
                    }
                }
                catch (Exception ex)
                {
                    Core.Loggers.Log(ex);
                }
            }, token);
        }
    }

    protected override void OnClear()
    {
        this.cts?.Cancel();
        this.fundindRates.Clear();
    }

    public override void Dispose()
    {
        // !!!
        this.Clear();
        base.Dispose();
    }

    private decimal CalculateF(double price)
    {
        double normalizePrice = price * 100;

        return (decimal)normalizePrice + Math.Min(Math.Max(-0.05M, (decimal)(this.InterestRate - normalizePrice)), 0.05M);
    }
}