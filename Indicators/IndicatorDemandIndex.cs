// Copyright QUANTOWER LLC. © 2017-2024. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using TradingPlatform.BusinessLayer;
using TradingPlatform.BusinessLayer.Utils;

namespace IndicatorDemandIndex;

public class IndicatorDemandIndex : Indicator
{
    private int Period = 20;
    private int maPeriod = 20;
    private int diMaPeriod = 20;
    private double controlLine = 0;

    private Indicator rangeEMA;
    private Indicator volumeEMA;
    private Indicator bpEMA;
    private Indicator spEMA;
    private Indicator diSMA;
    private HistoricalDataCustom SmoothingSource;

    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorDemandIndex.cs";

    public IndicatorDemandIndex()
        : base()
    {
        this.Name = "Demand Index";
        this.AddLineSeries("Main Line", Color.CadetBlue, 1, LineStyle.Solid);
        this.AddLineSeries("Smoothed Line", Color.IndianRed, 1, LineStyle.Solid);
        this.AddLineSeries("Control Line", Color.White, 1, LineStyle.Solid);
        this.SeparateWindow = true;
    }

    protected override void OnInit()
    {
        PriceType volumeEMASourcePrice = PriceType.Volume;
        if (this.Symbol.VolumeType == SymbolVolumeType.Volume)
            volumeEMASourcePrice = PriceType.Volume;
        else
            volumeEMASourcePrice = PriceType.Ticks;
        this.volumeEMA = Core.Instance.Indicators.BuiltIn.EMA(Period, volumeEMASourcePrice);
        this.rangeEMA = Core.Instance.Indicators.BuiltIn.EMA(Period, PriceType.Close);
        this.bpEMA = Core.Instance.Indicators.BuiltIn.EMA(maPeriod, PriceType.High);
        this.spEMA = Core.Instance.Indicators.BuiltIn.EMA(maPeriod, PriceType.Low);
        this.diSMA = Core.Instance.Indicators.BuiltIn.SMA(diMaPeriod, PriceType.Open);
        this.SmoothingSource = new HistoricalDataCustom(this);
        this.SmoothingSource.AddIndicator(rangeEMA);
        this.SmoothingSource.AddIndicator(bpEMA);
        this.SmoothingSource.AddIndicator(spEMA);
        this.SmoothingSource.AddIndicator(diSMA);
        this.AddIndicator(volumeEMA);
    }
    protected override void OnUpdate(UpdateArgs args)
    {
        if (this.Count < 2)
            return;
        double currentHigh = this.High();
        double currentLow = this.Low();
        double currentClose = this.Close();
        double currentVolume = this.Volume();
        if (this.Symbol.VolumeType == SymbolVolumeType.Volume)
            currentVolume = this.Volume();
        else
            currentVolume = this.Ticks();
        double previousHigh = this.High(1);
        double previousLow = this.Low(1);
        double previousClose = this.Close(1);
        double currentP = currentHigh + currentLow + 2 * currentClose;
        double previousP = previousHigh + previousLow + 2 * previousClose;
        double range = Math.Max(currentHigh, previousHigh) - Math.Min(currentLow, previousLow);
        this.SmoothingSource.SetValue(0d, 0d, 0d, range);
        double BP = 0;
        if (currentP < previousP)
            BP = (currentVolume / this.volumeEMA.GetValue()) / Math.Exp(0.375 * ((currentP + previousP) / (currentHigh - currentLow)) * ((previousP - currentP) / (currentP)));
        else
            BP = (currentVolume / this.volumeEMA.GetValue());
        double SP = 0;
        if (currentP <= previousP)
            SP = (currentVolume / this.volumeEMA.GetValue());
        else
            SP = (currentVolume / this.volumeEMA.GetValue()) / Math.Exp(0.375 * ((currentP + previousP) / (currentHigh - currentLow)) * ((currentP - previousP) / (previousP)));
        this.SmoothingSource.SetValue(0d, BP, SP, range);
        double Q = 0;
        double currentSPema = spEMA.GetValue();
        double currentBPema = bpEMA.GetValue();
        if (currentBPema > currentSPema)
            Q = currentSPema / currentBPema;
        else if (currentBPema < currentSPema)
            Q = currentBPema / currentSPema;
        else
            Q = 1;
        double DI = 0;
        if (currentSPema <= currentBPema)
            DI = 100 * (1 - Q);
        else
            DI = 100*(Q - 1);
        this.SmoothingSource.SetValue(DI, BP, SP, range);
        this.SetValue(DI);
        this.SetValue(this.diSMA.GetValue(), 1);
        this.SetValue(this.controlLine, 2);
    }
    public override IList<SettingItem> Settings
    {
        get
        {
            var settings = base.Settings;
            settings.Add(new SettingItemInteger("Period", this.Period)
            {
                Text = "Period",
                SortIndex = 1,
            });
            settings.Add(new SettingItemInteger("maPeriod", this.maPeriod)
            {
                Text = "Moving Average Period",
                SortIndex = 1,
            });
            settings.Add(new SettingItemInteger("diMaPeriod", this.diMaPeriod)
            {
                Text = "Demand Index MA Period",
                SortIndex = 1,
            });

            settings.Add(new SettingItemDouble("controlLine", this.controlLine)
            {
                Text = "Control Line Position",
                SortIndex = 2,
                DecimalPlaces = 2,
                Increment = 0.01,
            });
            return settings;
        }
        set
        {
            base.Settings = value;
            if (value.TryGetValue("Period", out int Period))
                this.Period = Period;
            if (value.TryGetValue("maPeriod", out int maPeriod))
                this.maPeriod = maPeriod;
            if (value.TryGetValue("diMaPeriod", out int diMaPeriod))
                this.diMaPeriod = diMaPeriod;
            if (value.TryGetValue("controlLine", out double controlLine))
                this.controlLine = controlLine;
            this.OnSettingsUpdated();
        }
    }
}
