// Copyright QUANTOWER LLC. © 2017-2024. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using TradingPlatform.BusinessLayer;

namespace Trend;

/// <summary>
/// A trend following indicator that is used to predict when a given symbol's momentum is reversing. 
/// </summary>
public sealed class IndicatorZigZag : Indicator
{
    // Displays Input Parameter as input field (or checkbox if value type is bolean).
    [InputParameter("Percent Deviation", 0, 0.01, 100.0, 0.01, 2)]
    public double deviation = 0.1;

    private bool isLabelVisible;
    private Color upLabelColor;
    private Color downLabelColor;
    private Font labelFont;
    private LabelFormat labelFormat;

    // Defines ZigZag calculation variables.
    private int trendLineLength;
    private int retracementLength;
    private int direction;
    private double lastTurnPoint;
    private double lastPriceChangeDirection;

    public override string ShortName => $"ZZ ({this.deviation})";
    public override string HelpLink => "https://help.quantower.com/analytics-panels/chart/technical-indicators/trend/zigzag";
    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorZigZag.cs";

    /// <summary>
    /// Indicator's constructor. Contains general information: name, description, LineSeries etc. 
    /// </summary>
    public IndicatorZigZag()
        : base()
    {
        // Defines indicator's group, name and description.            
        this.Name = "ZigZag";
        this.Description = "A trend following indicator that is used to predict when a given security's momentum is reversing. ";

        // Defines line on demand with particular parameters.
        this.AddLineSeries("ZZ'Line", Color.Yellow, 2, LineStyle.Solid);

        this.SeparateWindow = false;

        this.isLabelVisible = true;
        this.upLabelColor = Color.Green;
        this.downLabelColor = Color.Red;
        this.labelFont = SystemFonts.DefaultFont;
        this.labelFormat = LabelFormat.PriceAndChange;
    }

    /// <summary>
    /// This function will be called after creating an indicator as well as after its input params reset or chart (symbol or timeframe) updates.
    /// </summary>
    protected override void OnInit()
    {
        // Initializes calculation parameters.
        this.trendLineLength = 0;
        this.retracementLength = 0;
        this.direction = 1;
        this.lastTurnPoint = 0;
        this.lastPriceChangeDirection = double.NaN;
    }

    /// <summary>
    /// Calculation entry point. This function is called when a price data updates. 
    /// Will be runing under the HistoricalBar mode during history loading. 
    /// Under NewTick during realtime. 
    /// Under NewBar if start of the new bar is required.
    /// </summary>
    /// <param name="args">Provides data of updating reason and incoming price.</param>
    protected override void OnUpdate(UpdateArgs args)
    {
        // Changes calculation parameters on each bar.
        if (args.Reason != UpdateReason.NewTick)
        {
            this.trendLineLength++;
            this.retracementLength++;
        }

        if (this.Count == 0)
            return;

        double high = this.High(0);
        double low = this.Low(0);

        // Detects uptrend moving.
        if (this.direction == 1)
        {
            // Trend continues.
            if (high >= this.lastTurnPoint)
            {
                this.lastTurnPoint = high;
                this.retracementLength = 0;
                this.DrawTrendLine(this.trendLineLength + 1);
                return;
            }
            // Sloping trend detection block.
            if (low <= this.lastTurnPoint - this.lastTurnPoint * this.deviation / 100)
            {
                this.lastTurnPoint = low;
                this.direction = -1;
                this.trendLineLength = this.retracementLength;
                this.retracementLength = 0;
                this.DrawTrendLine(this.trendLineLength + 1);
                this.DrawLabel(this.trendLineLength);
                return;
            }
        }
        // Detects downtrend moving.
        if (this.direction == -1)
        {
            // Trend continues.
            if (low <= this.lastTurnPoint)
            {
                this.lastTurnPoint = low;
                this.retracementLength = 0;
                this.DrawTrendLine(this.trendLineLength + 1);
                return;
            }
            // Sloping trend detection block.
            if (high >= this.lastTurnPoint + this.lastTurnPoint * this.deviation / 100)
            {
                this.lastTurnPoint = high;
                this.direction = 1;
                this.trendLineLength = this.retracementLength;
                this.retracementLength = 0;
                this.DrawTrendLine(this.trendLineLength + 1);
                this.DrawLabel(this.trendLineLength);
                return;
            }
        }
    }

    /// <summary>
    /// Trend line drawing
    /// </summary>
    private void DrawTrendLine(int x)
    {
        if (x > this.Count - 1)
            return;

        double high = this.High(0);
        double low = this.Low(0);

        for (int i = 0; i < x; i++)
        {
            double y = (this.direction > 0) ? high - i * (high - this.Low(x - 1)) / (x - 1) : low - i * (low - this.High(x - 1)) / (x - 1);
            this.SetValue(y, 0, i);
        }
    }

    private void DrawLabel(int x)
    {
        if (!this.isLabelVisible)
            return;

        var upperIcon = this.direction == -1 ? IndicatorLineMarkerIconType.Text : IndicatorLineMarkerIconType.None;
        var bottomIcon = this.direction == 1 ? IndicatorLineMarkerIconType.Text : IndicatorLineMarkerIconType.None;
        var marker = new IndicatorLineMarker(this.LinesSeries[0].Color, upperIcon, bottomIcon);

        var currentPrice = this.LinesSeries[0].GetValue(x);
        var priceText = this.Symbol.FormatPrice(currentPrice);
        var percent = (currentPrice - this.lastPriceChangeDirection) / this.lastPriceChangeDirection * 100;
        var percentText = double.IsNaN(percent) ? string.Empty : $"{Math.Round(percent, 2).ToString()}%";

        var labelText = this.labelFormat switch
        {
            LabelFormat.Price => priceText,
            LabelFormat.Change => percentText,
            LabelFormat.PriceAndChange => $"{priceText} ({percentText})",

            _ => priceText
        };

        var color = this.LinesSeries[0].Color;
        if (percent > 0)
            color = this.upLabelColor;
        else if (percent < 0)
            color = this.downLabelColor;

        marker.Color = color;
        marker.PaintLine = false;
        marker.TextSettings.Text = labelText;
        marker.TextSettings.Font = this.labelFont;
        this.LinesSeries[0].SetMarker(x, marker);

        this.lastPriceChangeDirection = this.LinesSeries[0].GetValue(x);
    }

    public override IList<SettingItem> Settings
    {
        get
        {
            var settings = base.Settings;

            var separatorGroup = settings.FirstOrDefault(s => s.Name == "Percent Deviation")?.SeparatorGroup;

            settings.Add(new SettingItemBoolean("IsLabelVisible", this.isLabelVisible)
            {
                Text = loc._("Is price label visible"),
                SeparatorGroup = separatorGroup
            });
            var labelVisibleRelation = new SettingItemRelationVisibility("IsLabelVisible", true);
            settings.Add(new SettingItemPairColor("LabelColors", new PairColor(this.upLabelColor, this.downLabelColor, loc._("Up"), loc._("Down")))
            {
                Text = loc._("Label colors"),
                Relation = labelVisibleRelation,
                SeparatorGroup = separatorGroup
            });
            settings.Add(new SettingItemFont("LabelFont", this.labelFont)
            {
                Text = loc._("Label font"),
                Relation = labelVisibleRelation,
                SeparatorGroup = separatorGroup
            });
            var labelFormats = new List<SelectItem>()
            {
                new SelectItem(loc._("Price"), LabelFormat.Price),
                new SelectItem(loc._("Change"), LabelFormat.Change),
                new SelectItem(loc._("Price and change"), LabelFormat.PriceAndChange),
            };
            settings.Add(new SettingItemSelectorLocalized("LabelFormat", labelFormats.GetItemByValue(this.labelFormat), labelFormats)
            {
                Text = loc._("Label format"),
                Relation = labelVisibleRelation,
                SeparatorGroup = separatorGroup
            });

            return settings;
        }
        set
        {
            base.Settings = value;
            var holder = new SettingsHolder(value);

            var settingsUpdated = false;
            if (holder.TryGetValue("IsLabelVisible", out var item))
            {
                settingsUpdated |= this.isLabelVisible != (bool)item.Value;
                this.isLabelVisible = (bool)item.Value;
            }

            if (holder.TryGetValue("LabelFormat", out item))
            {
                var selectItem = (SelectItem)item.Value;
                settingsUpdated |= (LabelFormat)selectItem.Value != this.labelFormat;
                this.labelFormat = (LabelFormat)selectItem.Value;
            }

            if (holder.TryGetValue("LabelFont", out item))
            {
                settingsUpdated |= this.labelFont != (Font)item.Value;
                this.labelFont = (Font)item.Value;
            }

            if (holder.TryGetValue("LabelColors", out item))
            {
                var pairColor = (PairColor)item.Value;

                settingsUpdated |= this.upLabelColor != pairColor.Color1 || this.downLabelColor != pairColor.Color2;
                this.upLabelColor = pairColor.Color1;
                this.downLabelColor = pairColor.Color2;
            }

            if (settingsUpdated)
                this.OnSettingsUpdated();
        }
    }

    public enum LabelFormat
    {
        Price,
        Change,
        PriceAndChange
    }
}