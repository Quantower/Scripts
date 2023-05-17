
// Copyright QUANTOWER LLC. © 2017-2021. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace PinBar;

public class IndicatorPinBar : Indicator
{
    private double coefficient = 0.2;
    private Color BearishColor = Color.Red;
    private Color BullishColor = Color.Green;
    private bool excludeDoji = true;

    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorPinBar.cs";

    public IndicatorPinBar()
        : base()
    {
        Name = "Pin Bar";

        AddLineSeries("HighLine", Color.DarkOliveGreen, 1, LineStyle.Points).ShowLineMarker = false;
        AddLineSeries("LowLine", Color.IndianRed, 1, LineStyle.Points).ShowLineMarker = false;

        SeparateWindow = false;
        UpdateType = IndicatorUpdateType.OnBarClose;
    }

    protected override void OnInit()
    {

    }

    protected override void OnUpdate(UpdateArgs args)
    {
        SetValue(High(), 0);
        SetValue(Low(), 1);

        BarType currentBarType = this.CurrentBarType();
        if (currentBarType == BarType.BearishBar)
            LinesSeries[0].SetMarker(0, new IndicatorLineMarker(BearishColor, upperIcon: IndicatorLineMarkerIconType.DownArrow));
        else if (currentBarType == BarType.BullishBar)
            LinesSeries[1].SetMarker(0, new IndicatorLineMarker(BullishColor, bottomIcon: IndicatorLineMarkerIconType.UpArrow));
        else
        {
            LinesSeries[0].RemoveMarker(0);
            LinesSeries[1].RemoveMarker(0);
        }
    }

    public override IList<SettingItem> Settings
    {
        get
        {
            var settings = base.Settings;

            settings.Add(new SettingItemDouble("Ratio", this.coefficient)
            {
                Text = "Ratio",
                SortIndex = 1,
                Minimum = 0,
                Maximum = 1,
                DecimalPlaces = 3,
                Increment = 0.001
            });
            settings.Add(new SettingItemColor("BearishColor", this.BearishColor)
            {
                Text = "Top mark color",
                SortIndex = 2,
            });
            settings.Add(new SettingItemColor("BullishColor", this.BullishColor)
            {
                Text = "Bottom mark color",
                SortIndex = 3,
            });
            settings.Add(new SettingItemBoolean("ExcludeDoji", this.excludeDoji)
            {
                Text = "Exclude Doji",
                SortIndex = 4,
            });
            return settings;
        }
        set
        {
            base.Settings = value;

            if (value.TryGetValue("Ratio", out double coefficient))
                this.coefficient = coefficient;

            if (value.TryGetValue("BearishColor", out Color bearishColor))
                this.BearishColor = bearishColor;

            if (value.TryGetValue("BullishColor", out Color bullishColor))
                this.BullishColor = bullishColor;

            if (value.TryGetValue("ExcludeDoji", out bool newExcludeDoji))
                this.excludeDoji = newExcludeDoji;

            this.OnSettingsUpdated();
        }
    }

    private BarType CurrentBarType()
    {
        BarType currentBarType = BarType.CommonBar;
        double currentClose = Close();
        double currentOpen = Open();
        double currentLow = Low();
        double currentHigh = High();
        double hightTailSize = 0;
        double lowTailSize = 0;
        bool excludeDoji = this.excludeDoji;
        if (!(excludeDoji && (currentClose == currentOpen)))
        {
            double sizeToBody = Math.Abs(currentClose - currentOpen) / (currentHigh - currentLow);
            if (sizeToBody < coefficient)
            {
                if (currentClose > currentOpen)
                {
                    if (currentLow < currentOpen)
                    {
                        lowTailSize = currentOpen - currentLow;
                    }
                    else
                        lowTailSize = 0;
                    if (currentHigh > currentClose)
                    {
                        hightTailSize = currentHigh - currentClose;
                    }
                    else
                        hightTailSize = 0;
                }
                else
                {
                    if (currentLow < currentClose)
                    {
                        lowTailSize = currentClose - currentLow;
                    }
                    else
                        lowTailSize = 0;
                    if (currentHigh > currentOpen)
                    {
                        hightTailSize = currentHigh - currentOpen;
                    }
                    else
                        hightTailSize = 0;
                }
            }
            if (hightTailSize < lowTailSize)
            {
                currentBarType = BarType.BullishBar;
            }
            else
                if (lowTailSize < hightTailSize)
            {
                currentBarType = BarType.BearishBar;
            }
        }
        return currentBarType;
    }
    public enum BarType
    {
        BearishBar,
        BullishBar,
        CommonBar
    }
}

public class PinBarRatio : Indicator, IWatchlistIndicator
{
    public int MinHistoryDepths => 2;
    public PinBarRatio()
        : base()
    {
        Name = "Pin Bar Ratio";

        AddLineSeries("Ratio", Color.DarkOliveGreen, 1, LineStyle.Solid);
        SeparateWindow = true;
    }

    protected override void OnInit()
    {

    }
    protected override void OnUpdate(UpdateArgs args)
    {
        SetValue(Math.Abs(this.Close() - this.Open()) / (this.High() - this.Low()), 0);
    }
}