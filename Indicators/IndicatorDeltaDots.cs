// Copyright QUANTOWER LLC. © 2017-2024. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using TradingPlatform.BusinessLayer;
using TradingPlatform.BusinessLayer.Chart;

namespace VolumeIndicators;
public class IndicatorDeltaDots : Indicator, IVolumeAnalysisIndicator
{
    public Color askDominantColor
    {
        get => this.askDominantBrush.Color;
        set => this.askDominantBrush.Color = value;
    }
    private readonly SolidBrush askDominantBrush;
    public Color bidDominantColor
    {
        get => this.bidDominantBrush.Color;
        set => this.bidDominantBrush.Color = value;
    }
    private readonly SolidBrush bidDominantBrush;
    public Color askMinColor
    {
        get => this.askMinBrush.Color;
        set => this.askMinBrush.Color = value;
    }
    private readonly SolidBrush askMinBrush;
    public Color askMaxColor
    {
        get => this.askMaxBrush.Color;
        set => this.askMaxBrush.Color = value;
    }
    private readonly SolidBrush askMaxBrush;
    public Color bidMinColor
    {
        get => this.bidMinBrush.Color;
        set => this.bidMinBrush.Color = value;
    }
    private readonly SolidBrush bidMinBrush;
    public Color bidMaxColor
    {
        get => this.bidMaxBrush.Color;
        set => this.bidMaxBrush.Color = value;
    }
    private readonly SolidBrush bidMaxBrush;
    public Color borderColor
    {
        get => this.borderPen.Color;
        set => this.borderPen.Color = value;
    }
    private readonly Pen borderPen;
    private double minimumVolume = 0;
    private double compareTreshold = 0;
    private int minDotSize = 10;
    private int maxDotSize = 100;
    private int customDotSize = 100;
    private int minColorIntensity = 20;
    private bool showSmallerVolume = true;
    private bool useMinSize = true;
    private bool diagonalComparsion = false;
    private bool useMaxSize = true;
    private bool allowZeroComparsion = true;
    private bool useCustomSize = false;
    private bool useVariableIntensity = false;
    private bool drawBorders = false;
    private DisplayMode displayMode = DisplayMode.DomSide;
    private DDMode ddMode = DDMode.PerBar;
    private PriceType perBarPriceType = PriceType.High;
    private ReferenceValue referenceValue = ReferenceValue.MinVolume;
    private DeltaReference deltaReference = DeltaReference.AllMax;
    private bool drawValues = false;
    private Color valueColor = Color.White;
    private Font valueFont;
    public bool IsRequirePriceLevelsCalculation => true;
    private bool volumeDataLoaded = false;

    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorDeltaDots.cs";
    public IndicatorDeltaDots()
        : base()
    {
        Name = "Delta Dots";
        SeparateWindow = false;

        this.askDominantBrush = new SolidBrush(Color.FromArgb(90, Color.Green));
        this.bidDominantBrush = new SolidBrush(Color.FromArgb(90, Color.Red));
        this.borderPen = new Pen(Color.White);
        this.askMaxBrush = new SolidBrush(Color.FromArgb(90, Color.Green));
        this.askMinBrush = new SolidBrush(Color.FromArgb(20, Color.Green));
        this.bidMaxBrush = new SolidBrush(Color.FromArgb(90, Color.Red));
        this.bidMinBrush = new SolidBrush(Color.FromArgb(20, Color.Red));
        this.valueFont = new Font("Tahoma", 12);
        this.volumeDataLoaded = false;
    }
    protected override void OnInit()
    {
    }
    protected override void OnUpdate(UpdateArgs args)
    {
    }
    public override void OnPaintChart(PaintChartEventArgs args)
    {
        base.OnPaintChart(args);
        if (this.CurrentChart == null || !volumeDataLoaded)
            return;
        var gr = args.Graphics;
        var currWindow = this.CurrentChart.Windows[args.WindowIndex];
        RectangleF prevClipRectangle = gr.ClipBounds;
        gr.SetClip(args.Rectangle);
        try
        {
            var t = this.HistoricalData[0, SeekOriginHistory.Begin].VolumeAnalysisData;
            var b = this.HistoricalData;
            int leftIndex = args.LeftVisibleBarIndex;
            int rightIndex = args.RightVisibleBarIndex;
            double referenceValue = 1;
            if (this.referenceValue != ReferenceValue.MinVolume)
            {
                int startIndex = this.referenceValue == ReferenceValue.VisibleAverage ? leftIndex : 0;
                int endIndex = this.referenceValue == ReferenceValue.VisibleAverage ? rightIndex : this.HistoricalData.Count - 1;
                double volumeSum = 0;
                double tradesCount = 1;
                for (int i = startIndex; i <= endIndex; i++)
                {
                    var volumeData = this.HistoricalData[i, SeekOriginHistory.Begin].VolumeAnalysisData;
                    if (volumeData == null)
                        continue;
                    volumeSum += volumeData.Total.Volume;
                    tradesCount += volumeData.Total.Trades;
                }
                referenceValue = volumeSum / (endIndex-startIndex+1);
            }
            else
                referenceValue = this.minimumVolume;
            double maxDelta = 0;
            if (this.useVariableIntensity && this.displayMode == DisplayMode.DomSide)
            {
                int startIndex = this.deltaReference == DeltaReference.VisibleMax ? leftIndex : 0;
                int endIndex = this.deltaReference == DeltaReference.VisibleMax ? rightIndex : this.HistoricalData.Count - 1;
                for (int i = startIndex; i <= endIndex; i++)
                {
                    var volumeData = this.HistoricalData[i, SeekOriginHistory.Begin].VolumeAnalysisData;
                    if (volumeData == null)
                        continue;
                    double currDelta = Math.Abs(volumeData.Total.Delta);
                    if (currDelta >= maxDelta)
                        maxDelta = currDelta;
                }
            }
            for (int i = leftIndex; i <= rightIndex; i++)
            {
                RectangleF currentClip = gr.ClipBounds;
                var currentData = (IHistoryItem)(this.HistoricalData[i, SeekOriginHistory.Begin].Clone());
                if(currentData.VolumeAnalysisData == null)
                    continue;
                Dictionary<double, VolumeAnalysisItem> volumeData = new Dictionary<double, VolumeAnalysisItem>();
                RectangleF dotRect = new RectangleF();
                dotRect.X = (float)(currWindow.CoordinatesConverter.GetChartX(this.HistoricalData[i, SeekOriginHistory.Begin].TimeLeft) + this.CurrentChart.BarsWidth / 2);
                Brush currentBrush;
                List<double> prices = new List<double>();
                if (this.ddMode == DDMode.PerBar)
                    volumeData.Add(this.HistoricalData.GetPrice(this.perBarPriceType, this.HistoricalData.Count - i - 1), currentData.VolumeAnalysisData.Total);
                else
                {
                    foreach (var item in currentData.VolumeAnalysisData.PriceLevels)
                    {
                        volumeData.Add(item.Key, item.Value);
                        prices.Add(item.Key);
                    }
                }
                foreach (var item in volumeData)
                {
                    double buyValue = item.Value.BuyVolume;
                    double sellValue = item.Value.SellVolume;
                    if (diagonalComparsion && this.ddMode == DDMode.PerPrice)
                    {
                        int priceIndex = prices.IndexOf(item.Key);
                        if (priceIndex != -1)
                        {
                            for (int j = -1; j <= 1; j++)
                            {
                                if (priceIndex + j >= prices.Count || priceIndex + j < 0)
                                    continue;
                                var currVolData = volumeData[prices[priceIndex + j]];
                                if (currVolData.SellVolume > sellValue)
                                {
                                    sellValue = volumeData[prices[priceIndex + j]].SellVolume;
                                }
                            }
                        }
                    }
                    if (buyValue == 0 && !allowZeroComparsion)
                        buyValue = 1;
                    if (sellValue == 0 && !allowZeroComparsion)
                        sellValue = 1;
                    double totalValue = buyValue + sellValue;

                    if (item.Value.Volume <= minimumVolume && !this.showSmallerVolume || (sellValue <= this.compareTreshold && buyValue <= this.compareTreshold))
                        continue;
                    dotRect.Y = (float)currWindow.CoordinatesConverter.GetChartY(item.Key);
                    dotRect = DotCalculation(totalValue, dotRect, referenceValue);
                    if (this.displayMode == DisplayMode.DomSide || this.displayMode == DisplayMode.Gradient)
                    {
                        currentBrush = GetBrush(maxDelta, buyValue, totalValue, dotRect);
                        gr.FillEllipse(currentBrush, dotRect);
                    }
                    if (this.displayMode == DisplayMode.Pie)
                    {
                        double ratio = 0;
                        float angle = 0;
                        currentBrush = this.askDominantBrush;
                        ratio = buyValue / totalValue;
                        angle = (float)(360 * ratio);
                        gr.FillPie(currentBrush, dotRect, 270 - angle / 2, angle);
                        currentBrush = this.bidDominantBrush;
                        gr.FillPie(currentBrush, dotRect, 270 + angle / 2, 360 - angle);
                    }
                    if (this.displayMode == DisplayMode.Split)
                    {
                        double ratio = 0;
                        float splitHeight = 0;

                        currentBrush = this.askDominantBrush;
                        ratio = buyValue / totalValue;
                        splitHeight = (float)(dotRect.Height * ratio);
                        gr.IntersectClip(new RectangleF(dotRect.X, dotRect.Y, dotRect.Width, splitHeight));
                        gr.FillEllipse(currentBrush, dotRect);
                        gr.SetClip(currentClip);
                        currentBrush = this.bidDominantBrush;
                        gr.IntersectClip(new RectangleF(dotRect.X, dotRect.Y + splitHeight, dotRect.Width, dotRect.Height - splitHeight));
                        gr.FillEllipse(currentBrush, dotRect);
                        gr.SetClip(currentClip);
                    }
                    if (drawBorders)
                        gr.DrawEllipse(borderPen, dotRect);
                    if(drawValues)
                    {
                        string volumeText = item.Value.Volume.ToString("N0");
                        SizeF textSize = gr.MeasureString(volumeText, this.valueFont);
                        PointF textPosition = new PointF(dotRect.X + (dotRect.Width - textSize.Width) / 2, dotRect.Y + (dotRect.Height - textSize.Height) / 2);
                        using (Brush textBrush = new SolidBrush(this.valueColor))
                        {
                            gr.DrawString(volumeText, this.valueFont, textBrush, textPosition);
                        }
                    }
                    gr.SetClip(currentClip);
                    dotRect.X += dotRect.Width / 2;
                }
                volumeData.Clear();
                prices.Clear();
            }
        }
        finally
        {
            gr.SetClip(prevClipRectangle);
        }
    }
    private RectangleF DotCalculation(double volume, RectangleF currRect, double referenceValue)
    {
        if (referenceValue == 0)
            referenceValue = (float)volume;
        float circleSize = 0;
        float minSize = this.useMinSize ? this.minDotSize : 1;
        circleSize = (float)(volume / referenceValue) * minSize;
        if (circleSize > maxDotSize && useMaxSize && !useCustomSize)
        {
            circleSize = maxDotSize;
        }
        if (circleSize < minDotSize && useMinSize && !useCustomSize)
        {
            circleSize = minDotSize;
        }
        if (useCustomSize)
            circleSize = customDotSize;
        currRect.Height = circleSize;
        currRect.Width = circleSize;
        currRect.X -= currRect.Width / 2;
        currRect.Y -= currRect.Height / 2;
        return currRect;
    }

    private Brush GetBrush(double maxDelta, double buyValue, double totalValue, RectangleF currRectangle)
    {
        Brush currentBrush = new SolidBrush(Color.White);
        if (this.displayMode == DisplayMode.Gradient)
        {
            PointF topPoint = new PointF(currRectangle.X + currRectangle.Width / 2, currRectangle.Y);
            PointF bottomPoint = new PointF(currRectangle.X + currRectangle.Width / 2, currRectangle.Y + currRectangle.Height);
            LinearGradientBrush linGrBrush;
            Blend blend = new Blend();
            float[] relativeIntensities = { 0.0f, 0.5f, 1.0f };
            blend.Factors = relativeIntensities;
            linGrBrush = new LinearGradientBrush(topPoint, bottomPoint, askDominantBrush.Color, bidDominantBrush.Color);
            float[] relativePositions = { 0f, (float)(buyValue / totalValue), 1.0f };
            blend.Positions = relativePositions;
            linGrBrush.Blend = blend;
            currentBrush = linGrBrush;
        }
        else
        {
            if (useVariableIntensity)
            {
                double currDelta = Math.Abs(buyValue-(totalValue-buyValue));
                Color minColor = Color.White;
                Color maxColor = Color.White;
                double colorRatio = this.deltaReference == DeltaReference.NoDelta ? buyValue / totalValue : currDelta/maxDelta;
                if (buyValue > (totalValue - buyValue))
                {
                    minColor = this.askMinColor;
                    maxColor = this.askMaxColor;
                }
                else
                {
                    minColor = this.bidMinColor;
                    maxColor = this.bidMaxColor;
                    if (this.deltaReference == DeltaReference.NoDelta)
                        colorRatio = 1-colorRatio;
                }
                double minAlpha = ((double)this.minColorIntensity / 100) * 255;
                int redDiff = maxColor.R - minColor.R;
                int greenDif = maxColor.G - minColor.G;
                int blueDif = maxColor.B - minColor.B;
                int alphaDif = maxColor.A - minColor.A;
                int newRed = (int)(redDiff * colorRatio + minColor.R);
                int newGreen = (int)(greenDif * colorRatio + minColor.G);
                int newBlue = (int)(blueDif * colorRatio + minColor.B);
                int newAlpha = (int)(alphaDif * colorRatio + minColor.A);
                if (newAlpha < minAlpha)
                    newAlpha = (int)minAlpha;
                currentBrush = new SolidBrush(Color.FromArgb(newAlpha, newRed, newGreen, newBlue));
            }
            else
            {
                if (buyValue > (totalValue - buyValue))
                    currentBrush = this.askDominantBrush;
                else
                    currentBrush = this.bidDominantBrush;
            }
        }
        return currentBrush;
    }
    public void VolumeAnalysisData_Loaded()
    {
        volumeDataLoaded = true;
        var t = this.HistoricalData[0].VolumeAnalysisData;
    }
    public override IList<SettingItem> Settings
    {
        get
        {
            var settings = base.Settings;
            SettingItemSeparatorGroup groupColor = new SettingItemSeparatorGroup("Color Settings", 0);
            SettingItemSeparatorGroup groupCalculation = new SettingItemSeparatorGroup("Calculation Settings", 1);
            SettingItemSeparatorGroup groupTreshold = new SettingItemSeparatorGroup("Treshold Settings", 2);
            SettingItemSeparatorGroup groupSize = new SettingItemSeparatorGroup("Circle Size Settings", 3);
            SettingItemSeparatorGroup valueSettings = new SettingItemSeparatorGroup("Volume display settings", 4);
            settings.Add(new SettingItemColor("askDominantColor", this.askDominantColor)
            {
                Text = "Ask Color",
                SortIndex = 1,
                SeparatorGroup = groupColor,
            });
            settings.Add(new SettingItemColor("bidDominantColor", this.bidDominantColor)
            {
                Text = "Bid Color",
                SortIndex = 1,
                SeparatorGroup = groupColor,
            });
            settings.Add(new SettingItemBoolean("drawBorders", this.drawBorders)
            {
                Text = "Draw Borders",
                SortIndex = 1,
                SeparatorGroup = groupColor,
            });
            SettingItemRelationVisibility drawBordersRelation = new SettingItemRelationVisibility("drawBorders", true);
            settings.Add(new SettingItemColor("borderColor", this.borderColor)
            {
                Text = "Border Color",
                SortIndex = 1,
                SeparatorGroup = groupColor,
                Relation = drawBordersRelation,
            });
            settings.Add(new SettingItemSelectorLocalized("ddMode", this.ddMode, new List<SelectItem> { new SelectItem("Per Bar", DDMode.PerBar), new SelectItem("Per Price", DDMode.PerPrice) })
            {
                Text = "Delta Dots mode",
                SortIndex = 1,
                SeparatorGroup = groupCalculation,
            });
            SettingItemRelationVisibility perBarRelation = new SettingItemRelationVisibility("ddMode", new SelectItem("Per Bar", DDMode.PerBar));
            SettingItemRelationVisibility perPriceRelation = new SettingItemRelationVisibility("ddMode", new SelectItem("Per Price", DDMode.PerPrice));
            settings.Add(new SettingItemSelectorLocalized("perBarPriceType", this.perBarPriceType, new List<SelectItem> { new SelectItem("High", PriceType.High), new SelectItem("Low", PriceType.Low), new SelectItem("Close", PriceType.Close), new SelectItem("Open", PriceType.Open) })
            {
                Text = "Placement Price",
                SortIndex = 1,
                Relation = perBarRelation,
                SeparatorGroup = groupCalculation,
            });
            settings.Add(new SettingItemSelectorLocalized("displayMode", this.displayMode, new List<SelectItem> { new SelectItem("Dominant Side", DisplayMode.DomSide), new SelectItem("Gradient", DisplayMode.Gradient), new SelectItem("Pie", DisplayMode.Pie), new SelectItem("Split", DisplayMode.Split) })
            {
                Text = "Display Mode",
                SortIndex = 1,
                SeparatorGroup = groupCalculation,
            });
            SettingItemRelationVisibility domColorRelation = new SettingItemRelationVisibility("displayMode", new SelectItem("Dominant Side", DisplayMode.DomSide));
            settings.Add(new SettingItemBoolean("useVariableIntensity", this.useVariableIntensity)
            {
                Text = "Use Variable Intensity",
                SortIndex = 1,
                SeparatorGroup = groupColor,
                Relation = domColorRelation,
            });
            SettingItemRelationVisibility useVariableIntensityRelation = new SettingItemRelationVisibility("useVariableIntensity", true);
            SettingItemMultipleRelation variableIntensiyColorRelation = new SettingItemMultipleRelation(new SettingItemRelationVisibility[] { useVariableIntensityRelation, domColorRelation });
            settings.Add(new SettingItemColor("askMaxColor", this.askMaxColor)
            {
                Text = "Ask Maximum Color",
                SortIndex = 1,
                SeparatorGroup = groupColor,
                Relation = variableIntensiyColorRelation,
            });
            settings.Add(new SettingItemColor("askMinColor", this.askMinColor)
            {
                Text = "Ask Minimum Color",
                SortIndex = 1,
                SeparatorGroup = groupColor,
                Relation = variableIntensiyColorRelation,
            });
            settings.Add(new SettingItemColor("bidMaxColor", this.bidMaxColor)
            {
                Text = "Bid Maximum Color",
                SortIndex = 1,
                SeparatorGroup = groupColor,
                Relation = variableIntensiyColorRelation,
            });
            settings.Add(new SettingItemColor("bidMinColor", this.bidMinColor)
            {
                Text = "Bid Minimum Color",
                SortIndex = 1,
                SeparatorGroup = groupColor,
                Relation = variableIntensiyColorRelation,
            });
            settings.Add(new SettingItemInteger("minColorIntensity", this.minColorIntensity)
            {
                Text = "Minimum Color Intensity",
                Dimension = "%",
                SortIndex = 1,
                Maximum = 100,
                Minimum = 0,
                Relation = useVariableIntensityRelation,
                SeparatorGroup = groupColor,
            });
            settings.Add(new SettingItemSelectorLocalized("deltaReference", this.deltaReference, new List<SelectItem> { new SelectItem("Current Bar Total Volume", DeltaReference.NoDelta), new SelectItem("Visible Chart Maximum Delta", DeltaReference.VisibleMax), new SelectItem("All Chart Maximum Delta", DeltaReference.AllMax) })
            {
                Text = "Intencity Precentage Reference Value",
                SortIndex = 1,
                SeparatorGroup = groupColor,
                Relation = useVariableIntensityRelation,
            });
            settings.Add(new SettingItemSelectorLocalized("referenceValue", this.referenceValue, new List<SelectItem> { new SelectItem("Visible Chart Average Size", ReferenceValue.VisibleAverage), new SelectItem("All Chart Average Size", ReferenceValue.AllAverage), new SelectItem("Minimum Volume", ReferenceValue.MinVolume) })
            {
                Text = "Trade Size Reference Value",
                SortIndex = 1,
                SeparatorGroup = groupCalculation,
            });
            settings.Add(new SettingItemDouble("minimumlVolume", this.minimumVolume)
            {
                Text = "Minimum Volume",
                SortIndex = 1,
                Minimum = 0,
                Maximum = 1000000000,
                DecimalPlaces = 2,
                Increment = 0.01,
                SeparatorGroup = groupTreshold
            });
            settings.Add(new SettingItemDouble("compareTreshold", this.compareTreshold)
            {
                Text = "Ask/Bid Minimum Volume Compare Threshold",
                SortIndex = 1,
                Minimum = 0,
                Maximum = 1000000000,
                DecimalPlaces = 2,
                Increment = 0.01,
                SeparatorGroup = groupTreshold
            });
            settings.Add(new SettingItemBoolean("allowZeroComparsion", this.allowZeroComparsion)
            {
                Text = "Allow Zero Ask/Bid Volume Comparsion",
                SortIndex = 1,
                SeparatorGroup = groupTreshold
            });
            settings.Add(new SettingItemBoolean("showSmallerVolume", this.showSmallerVolume)
            {
                Text = "Show Small Volume",
                SortIndex = 1,
                SeparatorGroup = groupTreshold
            });
            settings.Add(new SettingItemBoolean("diagonalComparsion", this.diagonalComparsion)
            {
                Text = "Use Diagonal Comparsion",
                SortIndex = 1,
                SeparatorGroup = groupCalculation,
                Relation = perPriceRelation,
            });
            settings.Add(new SettingItemBoolean("useCustomSize", this.useCustomSize)
            {
                Text = "Use Custom Size",
                SortIndex = 1,
                SeparatorGroup = groupSize,
            });
            SettingItemRelationVisibility customRelation = new SettingItemRelationVisibility("useCustomSize", true);
            settings.Add(new SettingItemInteger("customDotSize", this.customDotSize)
            {
                Text = "Dot Size",
                Dimension = "pt",
                SortIndex = 1,
                Maximum = 1000,
                Minimum = 1,
                Relation = customRelation,
                SeparatorGroup = groupSize,
            });
            SettingItemRelationVisibility notCustomRelation = new SettingItemRelationVisibility("useCustomSize", false);
            settings.Add(new SettingItemBoolean("useMinSize", this.useMinSize)
            {
                Text = "Use Minimum Size",
                SortIndex = 1,
                Relation = notCustomRelation,
                SeparatorGroup = groupSize,
            });
            SettingItemRelationVisibility minRelation = new SettingItemRelationVisibility("useMinSize", true);
            SettingItemMultipleRelation minimumNotCustom = new SettingItemMultipleRelation(new SettingItemRelationVisibility[] { notCustomRelation, minRelation });
            settings.Add(new SettingItemInteger("minDotSize", this.minDotSize)
            {
                Text = "Minimum Dot Size",
                Dimension = "pt",
                SortIndex = 1,
                Maximum = 1000,
                Minimum = 1,
                Relation = minimumNotCustom,
                SeparatorGroup = groupSize,
            });
            settings.Add(new SettingItemBoolean("useMaxSize", this.useMaxSize)
            {
                Text = "Use Maximum Size",
                SortIndex = 1,
                Relation = notCustomRelation,
                SeparatorGroup = groupSize,
            });
            SettingItemRelationVisibility maxRelation = new SettingItemRelationVisibility("useMaxSize", true);
            SettingItemMultipleRelation maximumNotCustom = new SettingItemMultipleRelation(new SettingItemRelationVisibility[] { notCustomRelation, maxRelation });
            settings.Add(new SettingItemInteger("maxDotSize", this.maxDotSize)
            {
                Text = "Maximum  Dot Size",
                Dimension = "pt",
                SortIndex = 1,
                Maximum = 1000000000,
                Minimum = 1,
                Relation = maximumNotCustom,
                SeparatorGroup = groupSize,
            });
            settings.Add(new SettingItemBoolean("drawValues", this.drawValues)
            {
                Text = "Draw Volume",
                SortIndex = 1,
                SeparatorGroup = valueSettings,
            });
            SettingItemRelationVisibility drawValuesRelation = new SettingItemRelationVisibility("drawValues", true);
            settings.Add(new SettingItemFont("valueFont", this.valueFont)
            {
                Text = "Value Font",
                SortIndex = 1,
                SeparatorGroup = valueSettings,
                Relation = drawValuesRelation
            });
            settings.Add(new SettingItemColor("valueColor", this.valueColor)
            {
                Text = "Value Color",
                SortIndex = 1,
                SeparatorGroup = valueSettings,
                Relation = drawValuesRelation
            });
            return settings;
        }
        set
        {
            if (value.TryGetValue("askDominantColor", out Color askDominantColor))
                this.askDominantColor = askDominantColor;
            if (value.TryGetValue("bidDominantColor", out Color bidDominantColor))
                this.bidDominantColor = bidDominantColor;
            if (value.TryGetValue("askMaxColor", out Color askMaxColor))
                this.askMaxColor = askMaxColor;
            if (value.TryGetValue("askMinColor", out Color askMinColor))
                this.askMinColor = askMinColor;
            if (value.TryGetValue("bidMaxColor", out Color bidMaxColor))
                this.bidMaxColor = bidMaxColor;
            if (value.TryGetValue("bidMinColor", out Color bidMinColor))
                this.bidMinColor = bidMinColor;
            if (value.TryGetValue("ddMode", out DDMode ddMode))
                this.ddMode = ddMode;
            if (value.TryGetValue("displayMode", out DisplayMode displayMode))
                this.displayMode = displayMode;
            if (value.TryGetValue("deltaReference", out DeltaReference deltaReference))
                this.deltaReference = deltaReference;
            if (value.TryGetValue("minimumlVolume", out double minimumlVolume))
                this.minimumVolume = minimumlVolume;
            if (value.TryGetValue("compareTreshold", out double compareTreshold))
                this.compareTreshold = compareTreshold;
            if (value.TryGetValue("allowZeroComparsion", out bool allowZeroComparsion))
                this.allowZeroComparsion = allowZeroComparsion;
            if (value.TryGetValue("diagonalComparsion", out bool diagonalComparsion))
                this.diagonalComparsion = diagonalComparsion;
            if (value.TryGetValue("useVariableIntensity", out bool useVariableIntensity))
                this.useVariableIntensity = useVariableIntensity;
            if (value.TryGetValue("drawBorders", out bool drawBorders))
                this.drawBorders = drawBorders;
            if (value.TryGetValue("borderColor", out Color borderColor))
                this.borderColor = borderColor;
            if (value.TryGetValue("minDotSize", out int minDotSize))
                this.minDotSize = minDotSize;
            if (value.TryGetValue("maxDotSize", out int maxDotSize))
                this.maxDotSize = maxDotSize;
            if (value.TryGetValue("minColorIntensity", out int minColorIntensity))
                this.minColorIntensity = minColorIntensity;
            if (value.TryGetValue("useMinSize", out bool useMinSize))
                this.useMinSize = useMinSize;
            if (value.TryGetValue("showSmallerVolume", out bool showSmallerVolume))
                this.showSmallerVolume = showSmallerVolume;
            if (value.TryGetValue("useMaxSize", out bool useMaxSize))
                this.useMaxSize = useMaxSize;
            if (value.TryGetValue("perBarPriceType", out PriceType perBarPriceType))
                this.perBarPriceType = perBarPriceType;
            if (value.TryGetValue("referenceValue", out ReferenceValue referenceValue))
                this.referenceValue = referenceValue;
            if (value.TryGetValue("useCustomSize", out bool useCustomSize))
                this.useCustomSize = useCustomSize;
            if (value.TryGetValue("customDotSize", out int customDotSize))
                this.customDotSize = customDotSize;
            if (value.TryGetValue("drawValues", out bool drawValues))
                this.drawValues = drawValues;
            if (value.TryGetValue("valueFont", out Font valueFont))
                this.valueFont = valueFont;
            if (value.TryGetValue("valueColor", out Color valueColor))
                this.valueColor = valueColor;
            this.OnSettingsUpdated();
        }
    }
}
public enum DDMode
{
    PerBar,
    PerPrice
}
public enum DisplayMode
{
    DomSide,
    Split,
    Pie,
    Gradient
}
public enum ReferenceValue
{
    MinVolume,
    VisibleAverage,
    AllAverage,
}
public enum DeltaReference
{
    NoDelta,
    VisibleMax,
    AllMax,
}