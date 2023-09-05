// Copyright QUANTOWER LLC. © 2017-2023. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using TradingPlatform.BusinessLayer;

namespace VolumeIndicators;

public class IndicatorDepthOfBidAsk : Indicator
{
    #region Parameters

    private const PriceType BID_PRICE_TYPE = PriceType.Open;
    private const PriceType ASK_PRICE_TYPE = PriceType.Close;

    [InputParameter("Number of levels", 10, 1, 99999, 1, 0)]
    public int Level2Count = 10;

    public override string ShortName => $"{this.Name} ({this.Level2Count}; {Format(this.CurrenctDataType)})";

    internal DataType CurrenctDataType
    {
        get => this.currenctDataType;
        private set
        {
            if (this.currenctDataType == value)
                return;

            this.currenctDataType = value;
            this.RedrawHistoryFromCache();
        }
    }
    private DataType currenctDataType;

    private HistoricalDataCustom cumulativeCache;
    private HistoricalDataCustom imbalanceCache;
    private LiquidityChangesCache liquidityChangesCache;

    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorDepthOfBidAsk.cs";

    #endregion Parameters

    public IndicatorDepthOfBidAsk()
    {
        this.Name = "Depth of Bid/Ask";

        this.AddLineSeries("Asks depth", Color.FromArgb(235, 96, 47), 2, LineStyle.Solid);
        this.AddLineSeries("Bids depth", Color.FromArgb(55, 219, 186), 2, LineStyle.Solid);

        this.SeparateWindow = true;
    }

    #region Base overrides

    protected override void OnInit()
    {
        if (this.Symbol != null)
            this.Symbol.NewLevel2 += this.Symbol_NewLevel2;

        this.cumulativeCache = new HistoricalDataCustom(this);
        this.imbalanceCache = new HistoricalDataCustom(this);
        this.liquidityChangesCache = new LiquidityChangesCache(this, this.Level2Count);
    }
    protected override void OnUpdate(UpdateArgs args)
    {
        if (args.Reason == UpdateReason.HistoricalBar)
            return;

        var dom = this.Symbol.DepthOfMarket.GetDepthOfMarketAggregatedCollections(new GetLevel2ItemsParameters()
        {
            AggregateMethod = AggregateMethod.ByPriceLVL,
            LevelsCount = this.Level2Count,
            CalculateCumulative = true
        });

        // get ask cumulative
        Level2Item askItem = null;
        if (dom.Asks.Length > 0)
        {
            askItem = dom.Asks.Last();
            this.cumulativeCache[ASK_PRICE_TYPE] = askItem.Cumulative;
        }

        // get bid cumulative
        Level2Item bidItem = null;
        if (dom.Bids.Length > 0)
        {
            bidItem = dom.Bids.Last();
            this.cumulativeCache[BID_PRICE_TYPE] = bidItem.Cumulative;
        }

        // calculate imbalance, %
        if (askItem != null && bidItem != null)
        {
            double total = askItem.Cumulative + bidItem.Cumulative;
            double bidImbalance = bidItem.Cumulative * 100 / total;

            this.imbalanceCache[BID_PRICE_TYPE] = bidImbalance;
            this.imbalanceCache[ASK_PRICE_TYPE] = 100 - bidImbalance;
        }

        // calculate total liquidity changes
        this.liquidityChangesCache.Update(dom.Asks, dom.Bids);

        var (bid, ask) = this.GetCachedValues(0);
        this.SetValue(ask, 0, 0);
        this.SetValue(bid, 1, 0);
    }
    protected override void OnClear()
    {
        if (this.Symbol != null)
            this.Symbol.NewLevel2 -= this.Symbol_NewLevel2;

        this.cumulativeCache?.Dispose();
        this.imbalanceCache?.Dispose();
        this.liquidityChangesCache?.Dispose();
    }
    public override IList<SettingItem> Settings
    {
        get
        {
            var settings = base.Settings;

            settings.Add(new SettingItemSelectorLocalized("DataType", new SelectItem("", (int)this.CurrenctDataType), new List<SelectItem>()
            {
                new SelectItem(Format(DataType.Cumulative), (int)DataType.Cumulative),
                new SelectItem(Format(DataType.ImbalancePerc), (int)DataType.ImbalancePerc),
                new SelectItem(Format(DataType.LiquidityChanges), (int)DataType.LiquidityChanges),
            })
            { Text = loc._("Data type"), SeparatorGroup = settings.FirstOrDefault()?.SeparatorGroup ?? new SettingItemSeparatorGroup() });

            return settings;
        }
        set
        {
            base.Settings = value;

            if (value.GetItemByName("DataType")?.Value is SelectItem si)
                this.CurrenctDataType = (DataType)si.Value;
        }
    }

    #endregion Base overrides

    private void Symbol_NewLevel2(Symbol symbol, Level2Quote level2, DOMQuote dom) { }

    #region Misc

    private void RedrawHistoryFromCache()
    {
        if (this.HistoricalData == null)
            return;

        for (int i = 0; i < this.HistoricalData.Count; i++)
        {
            int offset = this.HistoricalData.Count - i - 1;
            var (bid, ask) = this.GetCachedValues(offset);

            this.SetValue(ask, 0, offset);
            this.SetValue(bid, 1, offset);
        }
    }
    private (double bid, double ask) GetCachedValues(int offset)
    {
        switch (this.currenctDataType)
        {
            case DataType.Cumulative:
                {
                    var item = this.cumulativeCache[offset];
                    return (item[BID_PRICE_TYPE], item[ASK_PRICE_TYPE]);
                }
            case DataType.ImbalancePerc:
                {
                    var item = this.imbalanceCache[offset];
                    return (item[BID_PRICE_TYPE], item[ASK_PRICE_TYPE]);
                }
            case DataType.LiquidityChanges:
                {
                    return this.liquidityChangesCache.GetValues(offset);
                }

            default:
                return (default, default);
        }
    }
    private static string Format(DataType dataType) => dataType switch
    {
        DataType.Cumulative => loc._("Cumulative"),
        DataType.ImbalancePerc => loc._("Imbalance, %"),
        DataType.LiquidityChanges => loc._("Liquidity changes"),

        _ => string.Empty,
    };

    #endregion Misc

    #region Nested

    internal enum DataType { Cumulative, ImbalancePerc, LiquidityChanges }

    internal class LiquidityChangesCache : IDisposable
    {
        #region Parameters

        private const PriceType ASK_PRICE_TYPE = PriceType.Open;
        private const PriceType BID_PRICE_TYPE = PriceType.Close;

        private readonly int levelCount;
        private readonly HistoricalDataCustom historicalData;

        private IDictionary<double, LiquidityChangesLevel> prevAsksCache;
        private IDictionary<double, LiquidityChangesLevel> prevBidsCache;

        #endregion Parameters

        public LiquidityChangesCache(Indicator indicator, int count)
        {
            this.levelCount = count;
            this.historicalData = new HistoricalDataCustom(indicator);

            this.prevBidsCache = new Dictionary<double, LiquidityChangesLevel>();
            this.prevAsksCache = new Dictionary<double, LiquidityChangesLevel>();
        }

        internal void Update(Level2Item[] asks, Level2Item[] bids)
        {
            var asksDict = new Dictionary<double, LiquidityChangesLevel>();
            var bidsDict = new Dictionary<double, LiquidityChangesLevel>();

            var totalAsksLiquidityChanges = 0d;
            var totalBidsLiquidityChanges = 0d;

            for (var i = 0; i < this.levelCount; i++ )
            {
                //
                if (asks.Length > i)
                {
                    var ask = asks[i];

                    if (this.prevAsksCache.TryGetValue(ask.Price, out var level))
                    {
                        if (level.Size != ask.Size)
                            level.LiquidityChanges = ask.Size - level.Size;

                        totalAsksLiquidityChanges += level.LiquidityChanges;
                    }
                    else
                    {
                        level = new LiquidityChangesLevel
                        {
                            Size = ask.Size
                        };
                    }

                    asksDict[ask.Price] = level;
                }

                //
                if (bids.Length > i)
                {
                    var bid = bids[i];

                    if (this.prevBidsCache.TryGetValue(bid.Price, out var level))
                    {
                        if (level.Size != bid.Size)
                            level.LiquidityChanges = bid.Size - level.Size;

                        totalBidsLiquidityChanges += level.LiquidityChanges;
                    }
                    else
                    {
                        level = new LiquidityChangesLevel
                        {
                            Size = bid.Size
                        };
                    }

                    bidsDict[bid.Price] = level;
                }
            }

            this.prevAsksCache.Clear();
            this.prevAsksCache = asksDict;

            this.prevBidsCache.Clear();
            this.prevBidsCache = bidsDict;

            this.historicalData[ASK_PRICE_TYPE] = totalAsksLiquidityChanges;
            this.historicalData[BID_PRICE_TYPE] = totalBidsLiquidityChanges;
        }
        internal (double bid, double ask) GetValues(int offset)
        {
            var item = this.historicalData[offset];
            return (item[BID_PRICE_TYPE], item[ASK_PRICE_TYPE]);
        }

        public void Dispose()
        {
            this.historicalData?.Dispose();

            if (this.prevAsksCache != null)
            {
                this.prevAsksCache?.Clear();
                this.prevAsksCache = null;
            }

            if (this.prevBidsCache != null)
            {
                this.prevBidsCache.Clear();
                this.prevBidsCache = null;
            }
        }
    }
    struct LiquidityChangesLevel
    {
        public double Size { get; set; }
        public double LiquidityChanges { get; set; }
    }

    #endregion Nested
}