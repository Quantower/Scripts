// Copyright QUANTOWER LLC. © 2017-2023. All rights reserved.

using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using TradingPlatform.BusinessLayer;

namespace VolumeIndicators;

public class IndicatorDepthOfBidAsk : Indicator
{
    private const PriceType BID_PRICE_TYPE = PriceType.Open;
    private const PriceType ASK_PRICE_TYPE = PriceType.Close;

    [InputParameter("Number of levels", 10, 1, 99999, 1, 0)]
    public int Level2Count = 10;

    public override string ShortName => $"{this.Name} ({this.Level2Count}; {this.Format(this.CurrenctDataType)})";

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

    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorDepthOfBidAsk.cs";

    public IndicatorDepthOfBidAsk()
    {
        this.Name = "Depth of Bid/Ask";

        this.AddLineSeries("Asks depth", Color.FromArgb(235, 96, 47), 2, LineStyle.Solid);
        this.AddLineSeries("Bids depth", Color.FromArgb(55, 219, 186), 2, LineStyle.Solid);

        this.SeparateWindow = true;
    }

    public override IList<SettingItem> Settings
    {
        get
        {
            var settings = base.Settings;

            settings.Add(new SettingItemSelectorLocalized("DataType", new SelectItem("", (int)this.CurrenctDataType), new List<SelectItem>()
            {
                new SelectItem(this.Format(DataType.Cumulative), (int)DataType.Cumulative),
                new SelectItem(this.Format(DataType.ImbalancePerc), (int)DataType.ImbalancePerc),
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

    protected override void OnInit()
    {
        if (this.Symbol != null)
            this.Symbol.NewLevel2 += this.Symbol_NewLevel2;

        this.cumulativeCache = new HistoricalDataCustom(this);
        this.imbalanceCache = new HistoricalDataCustom(this);
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

        var (bid, ask) = this.GetCachedItems(0);
        this.SetValue(ask, 0, 0);
        this.SetValue(bid, 1, 0);
    }
    protected override void OnClear()
    {
        if (this.Symbol != null)
            this.Symbol.NewLevel2 -= this.Symbol_NewLevel2;
    }

    private void RedrawHistoryFromCache()
    {
        if (this.HistoricalData == null)
            return;

        for (int i = 0; i < this.HistoricalData.Count; i++)
        {
            int offset = this.HistoricalData.Count - i - 1;
            var (bid, ask) = this.GetCachedItems(offset);

            this.SetValue(ask, 0, offset);
            this.SetValue(bid, 1, offset);
        }
    }
    private (double bid, double ask) GetCachedItems(int offset)
    {
        if (this.CurrenctDataType == DataType.Cumulative)
            return (this.cumulativeCache[offset][BID_PRICE_TYPE], this.cumulativeCache[offset][ASK_PRICE_TYPE]);
        else
            return (this.imbalanceCache[offset][BID_PRICE_TYPE], this.imbalanceCache[offset][ASK_PRICE_TYPE]);
    }
    private string Format(DataType dataType) => dataType switch
    {
        DataType.Cumulative => loc._("Cumulative"),
        DataType.ImbalancePerc => loc._("Imbalance, %"),

        _ => string.Empty,
    };

    private void Symbol_NewLevel2(Symbol symbol, Level2Quote level2, DOMQuote dom) { }

    internal enum DataType { Cumulative, ImbalancePerc }
}