// Copyright QUANTOWER LLC. © 2017-2023. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using TradingPlatform.BusinessLayer;
using TradingPlatform.BusinessLayer.Utils;

namespace VolumeIndicators;

public class IndicatorLevel2 : Indicator
{
    #region Parameters

    private const int MIN_BAR_HEIGHT_PX = 15;
    private const int MIN_BAR_OFFSET_PX = 5;

    [InputParameter("Pin to price scale", 10)]
    public bool PintToPriceScale { get; set; }

    [InputParameter("Levels count", 20, 1, 9999, 1, 0)]
    public int LevelsCount = 5;

    [InputParameter("Histogram width, %", 30, 1, 100, 1, 0)]
    public int HistogramWidthPercent = 15;

    //InputParameter("Stick to price scale")]
    public bool StickToPriceScale = false;

    public Color AskColor
    {
        get => this.askColor;
        set
        {
            if (this.askColor.Equals(value))
                return;

            this.askColor = value;
            this.askHistogramBrush = new SolidBrush(value);
        }
    }
    private Color askColor;
    private SolidBrush askHistogramBrush;

    public Color AskLabelColor
    {
        get => this.askLabelColor;
        set
        {
            if (this.askLabelColor.Equals(value))
                return;

            this.askLabelColor = value;
            this.askSizeFontBrush = new SolidBrush(value);
        }
    }
    private Color askLabelColor;
    private SolidBrush askSizeFontBrush;

    public Color BidColor
    {
        get => this.bidColor;
        set
        {
            if (this.bidColor.Equals(value))
                return;

            this.bidColor = value;
            this.bidHistogramBrush = new SolidBrush(value);
        }
    }
    private Color bidColor;
    private SolidBrush bidHistogramBrush;

    public Color BidLabelColor
    {
        get => this.bidLabelColor;
        set
        {
            if (this.bidLabelColor.Equals(value))
                return;

            this.bidLabelColor = value;
            this.bidSizeFontBrush = new SolidBrush(value);
        }
    }
    private Color bidLabelColor;
    private SolidBrush bidSizeFontBrush;

    [InputParameter("Price color", 60)]
    public Color PriceColor
    {
        get => this.priceColor;
        set
        {
            if (this.priceColor.Equals(value))
                return;

            this.priceColor = value;
            this.priceFontBrush = new SolidBrush(value);
        }
    }
    private Color priceColor;
    private SolidBrush priceFontBrush;

    public override string HelpLink => "https://help.quantower.com/analytics-panels/chart/technical-indicators/volume/level2-indicator";
    public override string ShortName => $"Lvl2 ({this.LevelsCount})";

    private IList<Lvl2LevelItem> asks;
    private IList<Lvl2LevelItem> bids;

    private readonly Lvl2FiltersCache filtersCache;

    private long lastQuoteTime;
    private long fRefreshTime;
    private double currentMaxLevelSize;

    private readonly Font font;
    private readonly StringFormat farCenter;

    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorLevel2.cs";

    #endregion Parameters

    public IndicatorLevel2()
    {
        this.Name = "Level2 indicator";

        this.AskColor = Color.FromArgb(64, 251, 87, 87);
        this.AskLabelColor = Color.FromArgb(255, this.AskColor);

        this.BidColor = Color.FromArgb(64, 0, 178, 89);
        this.BidLabelColor = Color.FromArgb(255, this.BidColor);
        this.PriceColor = Color.FromArgb(110, 119, 128);

        this.filtersCache = new Lvl2FiltersCache(this.Symbol);

        this.font = new Font("Verdana", 10, FontStyle.Regular, GraphicsUnit.Pixel);
        this.farCenter = new StringFormat() { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
    }

    #region Overrides

    protected override void OnInit()
    {
        base.OnInit();

        this.Symbol.NewLevel2 += this.Symbol_NewLevel2;
        this.fRefreshTime = TimeSpan.FromMilliseconds(333).Ticks;

        this.asks = new List<Lvl2LevelItem>();
        this.bids = new List<Lvl2LevelItem>();

        this.UpdateIndicatorData();
    }
    public override IList<SettingItem> Settings
    {
        get
        {
            var settings = base.Settings;

            var separator = settings.FirstOrDefault()?.SeparatorGroup;

            settings.Add(new SettingItemPairColor("AskStyle", new PairColor(this.AskLabelColor, this.AskColor, loc._("Text"), loc._("Back")), 40)
            {
                Text = loc._("Ask style"),
                SeparatorGroup = separator
            });
            settings.Add(new SettingItemPairColor("BidStyle", new PairColor(this.BidLabelColor, this.BidColor, loc._("Text"), loc._("Back")), 50)
            {
                Text = loc._("Bid style"),
                SeparatorGroup = separator
            });

            settings.Add(new SettingItemGroup("FiltersCache", this.filtersCache.Settings));

            return settings;
        }
        set
        {
            base.Settings = value;
            var holder = new SettingsHolder(value);

            #region Support old user settings

#warning не забути видалити цю хрінь (20.03.2023)
            if (holder.TryGetValue("Ask color", out var si) && si.Value is Color askBack)
                this.AskColor = askBack;
            if (holder.TryGetValue("Ask text color", out si) && si.Value is Color askFore)
                this.AskLabelColor = askFore;
            if (holder.TryGetValue("Bid color", out si) && si.Value is Color bidBack)
                this.BidColor = bidBack;
            if (holder.TryGetValue("Bid text color", out si) && si.Value is Color bidFore)
                this.BidLabelColor = bidFore;

            #endregion Support old user settings (30.12.2021)

            if (holder.TryGetValue("AskStyle", out si) && si.Value is PairColor askStyle)
            {
                this.AskLabelColor = askStyle.Color1;
                this.AskColor = askStyle.Color2;
            }

            if (holder.TryGetValue("BidStyle", out si) && si.Value is PairColor bidStyle)
            {
                this.BidLabelColor = bidStyle.Color1;
                this.BidColor = bidStyle.Color2;
            }

            if (holder.TryGetValue("FiltersCache", out si) && si?.Value is IList<SettingItem> filtersCacheSI)
                this.filtersCache.Settings = filtersCacheSI;
        }
    }
    public override void OnPaintChart(PaintChartEventArgs args)
    {
        if (this.Symbol == null)
            return;

        try
        {
            var gr = args.Graphics;
            gr.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            gr.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.GammaCorrected;
            gr.SetClip(args.Rectangle);

            int maxWidth = args.Rectangle.Width * this.HistogramWidthPercent / 100;

            if (!this.StickToPriceScale)
            {
                int tickSizeText = (int)gr.MeasureString(this.FormatPrice(this.Symbol.TickSize), this.font).Width;
                if (tickSizeText < maxWidth)
                    maxWidth -= tickSizeText;
            }

            this.DrawLevels(gr, this.Symbol, args.Rectangle, this.asks, maxWidth, DrawingLevelType.Ask);
            this.DrawLevels(gr, this.Symbol, args.Rectangle, this.bids, maxWidth, DrawingLevelType.Bid);
        }
        catch (Exception ex)
        {
            Core.Loggers.Log(ex);
        }
    }
    protected override void OnClear()
    {
        if (this.Symbol != null)
            this.Symbol.NewLevel2 -= this.Symbol_NewLevel2;

        this.asks?.Clear();
        this.bids?.Clear();

        base.OnClear();
    }
    public override void Dispose()
    {
        this.font?.Dispose();
        this.askHistogramBrush?.Dispose();
        this.askSizeFontBrush?.Dispose();

        this.bidHistogramBrush?.Dispose();
        this.bidSizeFontBrush?.Dispose();

        this.filtersCache?.Dispose();

        base.Dispose();
    }

    #endregion Overrides

    #region Update MD

    private void Symbol_NewLevel2(Symbol symbol, Level2Quote level2, DOMQuote dom) => this.TryMarketDepthRecalculate();
    internal void TryMarketDepthRecalculate()
    {
        if (this.IsItTimeToUpdate(out long now))
        {
            this.UpdateIndicatorData();
            this.lastQuoteTime = now;
        }
    }
    private bool IsItTimeToUpdate(out long now)
    {
        now = Core.TimeUtils.DateTimeUtcNow.Ticks;
        return now - this.lastQuoteTime > this.fRefreshTime;
    }
    private void UpdateIndicatorData()
    {
        if (this.Symbol == null || this.CurrentChart == null)
            return;

        var dom = this.Symbol.DepthOfMarket.GetDepthOfMarketAggregatedCollections(new GetDepthOfMarketParameters()
        {
            GetLevel2ItemsParameters = new GetLevel2ItemsParameters()
            {
                AggregateMethod = AggregateMethod.ByPriceLVL,
                CustomTickSize = this.CurrentChart.TickSize,
                LevelsCount = this.LevelsCount,
            }
        });

        // asks
        this.PopulateCollection(this.asks, dom.Asks, this.askHistogramBrush, this.askSizeFontBrush, out double maxAskSize);

        // bids
        this.PopulateCollection(this.bids, dom.Bids, this.bidHistogramBrush, this.bidSizeFontBrush, out double maxBidSize);

        this.currentMaxLevelSize = Math.Max(maxAskSize, maxBidSize);
    }

    #endregion Update MD

    #region Misc

    private void PopulateCollection(IList<Lvl2LevelItem> items, Level2Item[] level2Items, SolidBrush backBrush, SolidBrush foreBrush, out double maxSize)
    {
        maxSize = double.MinValue;
        int count = Math.Max(items.Count, level2Items.Length);

        for (int i = 0; i < count; i++)
        {
            if (items.Count <= i)
                items.Add(new Lvl2LevelItem());

            var cacheItem = items[i];

            cacheItem.IsValid = level2Items.Length > i;

            if (cacheItem.IsValid)
            {
                var newItem = level2Items[i];

                cacheItem.Price = newItem.Price;
                cacheItem.Size = newItem.Size;

                if (this.filtersCache.TryGetHighlightLevel(cacheItem.Size, out var level))
                {
                    cacheItem.BackBrush = level.BackBrush;
                    cacheItem.ForeBrush = level.ForeBrush;
                }
                else
                {
                    cacheItem.BackBrush = backBrush;
                    cacheItem.ForeBrush = foreBrush;
                }

                maxSize = Math.Max(cacheItem.Size, maxSize);
            }
        }


    }
    private void DrawLevels(Graphics gr, Symbol symbol, Rectangle windowRect, IList<Lvl2LevelItem> items, int maxWidth, DrawingLevelType type)
    {
        if (symbol == null)
            return;

        // Calculate bars height
        int barH = MIN_BAR_HEIGHT_PX - MIN_BAR_OFFSET_PX;
        if (this.PintToPriceScale)
        {
            barH = Math.Max(1, (int)Math.Round(this.CurrentChart.MainWindow.YScaleFactor * this.CurrentChart.TickSize));
            if (barH > 2)
                barH -= 1;
            if (barH > 5)
                barH -= 2;
        }

        // Skip drawing text in case of small scale
        bool drawText = true;
        if (barH < 10)
            drawText = false;

        double pointY = windowRect.Y + windowRect.Height / 2;
        for (int i = 0; i < items.Count; i++)
        {
            if (!items[i].IsValid)
                break;

            var item = items[i];

            item.Rectangle.Width = (float)(item.Size * maxWidth / this.currentMaxLevelSize);
            item.Rectangle.X = windowRect.Right - item.Rectangle.Width;
            item.Rectangle.Height = barH;

            if (this.PintToPriceScale)
            {
                item.Rectangle.Y = (int)Math.Round(this.CurrentChart.MainWindow.CoordinatesConverter.GetChartY(item.Price) - barH / 2);
            }
            else
            {
                if (type == DrawingLevelType.Ask)
                    item.Rectangle.Y = (float)pointY - MIN_BAR_HEIGHT_PX / 2;
                else if (type == DrawingLevelType.Bid)
                    item.Rectangle.Y = (float)pointY + MIN_BAR_HEIGHT_PX / 2;
            }

            // check if item is visible
            if (item.Rectangle.Top > windowRect.Top && item.Rectangle.Bottom < windowRect.Bottom)
            {
                //(Brush sizeFontBrush, Brush histoBrush) = this.GetBrushesFor(type, item);
                string sizeText = symbol.FormatQuantity(item.Size);
                string priceText = this.FormatPrice(item.Price);

                float sizeTextWidth = gr.MeasureString(sizeText, this.font).Width;
                float priceTextWidth = gr.MeasureString(priceText, this.font).Width;

                // draw price
                if (drawText && !this.PintToPriceScale && !this.StickToPriceScale)
                {
                    item.Rectangle.X -= priceTextWidth + 2;
                    gr.DrawString(priceText, this.font, this.priceFontBrush, windowRect.Right, item.Rectangle.Y + item.Rectangle.Height / 2, this.farCenter);
                }

                // draw size  
                if (drawText && windowRect.Width - item.Rectangle.Width >= sizeTextWidth)
                    gr.DrawString(sizeText, this.font, item.ForeBrush, item.Rectangle.X - 2, item.Rectangle.Y + item.Rectangle.Height / 2, this.farCenter);

                gr.FillRectangle(item.BackBrush, item.Rectangle);
            }

            if (type == DrawingLevelType.Ask)
                pointY -= MIN_BAR_HEIGHT_PX;
            else if (type == DrawingLevelType.Bid)
                pointY += MIN_BAR_HEIGHT_PX;
        }
    }

    #endregion Misc

    #region Nested

    private enum DrawingLevelType { Ask, Bid }

    private class Lvl2LevelItem
    {
        internal RectangleF Rectangle;
        public double Price { get; set; }
        public double Size { get; set; }
        public bool IsValid { get; set; }

        public Brush BackBrush { get; set; }
        public Brush ForeBrush { get; set; }

        public Lvl2LevelItem()
        {
            this.Rectangle = new Rectangle();
            this.IsValid = false;
        }

        public override string ToString()
        {
            if (this.IsValid)
                return $"Price:{this.Price}  Size:{this.Size}";
            else
                return this.IsValid.ToString();
        }
    }

    private class Lvl2HighlightLevel : ICustomizable, IDisposable
    {
        public Symbol Symbol { get; private set; }
        public int Index { get; private set; }

        private bool isEnabled;
        public bool IsEnabled
        {
            get => this.isEnabled;
            set
            {
                if (this.isEnabled == value)
                    return;

                this.isEnabled = value;
                this.OnEnabledChanged?.Invoke();
            }
        }

        private double level;
        public double Level
        {
            get => this.level;
            set
            {
                if (this.level == value)
                    return;

                this.level = value;
                this.OnLevelChanged?.Invoke();
            }
        }

        private Color? color;
        public Color Color
        {
            get => this.color.Value;
            set
            {
                if (this.color == value)
                    return;

                this.color = value;
                this.BackBrush = new SolidBrush(value);
                this.ForeBrush = new SolidBrush(Color.FromArgb(127, value));
            }
        }

        public Brush BackBrush { get; private set; }
        public Brush ForeBrush { get; private set; }

        public event Action OnLevelChanged;
        public event Action OnEnabledChanged;

        public Lvl2HighlightLevel(Symbol symbol, Color color, bool enable, int index = 0)
        {
            this.Symbol = symbol;
            this.Color = color;
            this.Index = index;
            this.IsEnabled = enable;
        }
        public void Dispose()
        {
            this.Symbol = null;

            this.BackBrush?.Dispose();
            this.BackBrush = null;

            this.ForeBrush?.Dispose();
            this.ForeBrush = null;
        }

        #region ICustomizable

        public IList<SettingItem> Settings
        {
            get
            {
                int lotStep = this.Symbol != null
                    ? CoreMath.GetValuePrecision((decimal)this.Symbol.LotStep)
                    : 0;

                var settings = new List<SettingItem>
                {
                    new SettingItemColor($"HighlightValueStyle_{this.Index}", this.Color)
                    {
                        SortIndex = 70,
                        Checked = this.IsEnabled,
                        //SeparatorGroup = separ,
                        WithCheckBox = true,
                        Text = loc._("Style"),
                        ColorText = loc._("Color")
                    },
                    new SettingItemDouble($"HighlightFilterValue_{this.Index}", this.Level)
                    {
                        SortIndex = 80,
                        Minimum = this.Symbol?.MinLot ?? 0d,
                        Maximum = this.Symbol?.MaxLot ?? double.MaxValue,
                        Increment = this.Symbol?.LotStep ?? 1d,
                        DecimalPlaces = lotStep,
                        Text = loc._("Value"),
                        Relation = new SettingItemRelation(new Dictionary<string, IEnumerable<object>>() { { $"HighlightValueStyle_{this.Index}", new object[0] } }, this.HighlightFilterValueRelationHandler)
                    }
                };

                return settings;
            }
            set
            {
                if (value.GetItemByName($"HighlightValueStyle_{this.Index}") is SettingItemColor item)
                {
                    this.IsEnabled = item.Checked;
                    this.Color = (Color)item.Value;
                }

                if (value.GetItemByName($"HighlightFilterValue_{this.Index}") is SettingItemDouble filterSI)
                    this.Level = (double)filterSI.Value;
            }
        }
        private bool HighlightFilterValueRelationHandler(SettingItemRelationParameters relationParameters)
        {
            bool hasChanged = false;

            try
            {
                if (relationParameters.ChangedItem is SettingItemColor si)
                {
                    hasChanged = relationParameters.DependentItem.Enabled != si.Checked;
                    relationParameters.DependentItem.Enabled = si.Checked;
                }
            }
            catch { }

            return hasChanged;
        }

        #endregion ICustomizable
    }

    private class Lvl2FiltersCache : ICustomizable, IDisposable
    {
        private readonly Color defaultColor;
        private readonly Lvl2HighlightLevel filter1;
        private readonly Lvl2HighlightLevel filter2;
        private readonly Lvl2HighlightLevel filter3;
        private readonly List<Lvl2HighlightLevel> filtersCache;

        private List<Lvl2HighlightLevel> enableSortedFiltersCache;

        public Lvl2FiltersCache(Symbol symbol)
        {
            this.filtersCache = new List<Lvl2HighlightLevel>();
            this.enableSortedFiltersCache = new List<Lvl2HighlightLevel>();

            this.defaultColor = Color.FromArgb(255, 234, 91);

            this.AddNewLevel(this.filter1 = new Lvl2HighlightLevel(symbol, this.defaultColor, false, 0));
            this.AddNewLevel(this.filter2 = new Lvl2HighlightLevel(symbol, this.defaultColor, false, 1));
            this.AddNewLevel(this.filter3 = new Lvl2HighlightLevel(symbol, this.defaultColor, false, 2));

            this.ResortCache();
        }

        public IList<SettingItem> Settings
        {
            get
            {
                var settings = new List<SettingItem>();

                //
                var filter1Separator = new SettingItemSeparatorGroup("Filter 1", -999);
                settings.Add(new SettingItemGroup("Filter1", this.ApplySeparatorGroup(this.filter1.Settings, filter1Separator)));

                //
                var filter2Separator = new SettingItemSeparatorGroup("Filter 2", -999);
                settings.Add(new SettingItemGroup("Filter2", this.ApplySeparatorGroup(this.filter2.Settings, filter2Separator)));

                //
                var filter3Separator = new SettingItemSeparatorGroup("Filter 3", -999);
                settings.Add(new SettingItemGroup("Filter3", this.ApplySeparatorGroup(this.filter3.Settings, filter3Separator)));

                return settings;
            }
            set
            {
                if (value.GetItemByName("Filter1")?.Value is IList<SettingItem> filter1SI)
                    this.filter1.Settings = filter1SI;

                if (value.GetItemByName("Filter2")?.Value is IList<SettingItem> filter2SI)
                    this.filter2.Settings = filter2SI;

                if (value.GetItemByName("Filter3")?.Value is IList<SettingItem> filter3SI)
                    this.filter3.Settings = filter3SI;
            }
        }

        internal bool TryGetHighlightLevel(double size, out Lvl2HighlightLevel level)
        {
            level = null;

            for (int i = this.enableSortedFiltersCache.Count - 1; i >= 0; i--)
            {
                var filter = this.enableSortedFiltersCache[i];

                if (!filter.IsEnabled)
                    continue;

                if (size >= filter.Level)
                {
                    level = filter;
                    break;
                }
            }

            return level != null;
        }
        internal void SetFilter1Setting(SettingItem si)
        {
            si.Name += "_0";
            this.filter1.Settings = new List<SettingItem> { si };
        }
        public void Dispose()
        {
            foreach (var level in this.filtersCache)
            {
                level.OnLevelChanged -= this.ResortCache;
                level.OnEnabledChanged -= this.ResortCache;
                level.Dispose();
            }

            this.filtersCache.Clear();
            this.enableSortedFiltersCache.Clear();
        }

        private void ResortCache()
        {
            var list = this.filtersCache.Where(l => l.IsEnabled).ToList();
            list.Sort((l, r) => l.Level.CompareTo(r.Level));
            this.enableSortedFiltersCache = list;
        }
        private void AddNewLevel(Lvl2HighlightLevel level)
        {
            level.OnLevelChanged += this.ResortCache;
            level.OnEnabledChanged += this.ResortCache;
            this.filtersCache.Add(level);
        }

        private IList<SettingItem> ApplySeparatorGroup(IList<SettingItem> settings, SettingItemSeparatorGroup separ)
        {
            foreach (var item in settings)
                item.SeparatorGroup = separ;

            return settings;
        }
    }

    #endregion Nested
}