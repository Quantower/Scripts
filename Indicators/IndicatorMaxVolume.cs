// Copyright QUANTOWER LLC. © 2017-2024. All rights reserved.

using System.Collections.Generic;
using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace VolumeIndicators;

public class IndicatorMaxVolume : Indicator
{
    private Color DojiColor = Color.White;
    private Color UpBarColor = Color.Green;
    private Color DownBarColor = Color.Red;
    private int Length = 1;

    private List<int> maxVolumeIndexes = new List<int>();

    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorMaxVolume.cs";
    public IndicatorMaxVolume()
        : base()
    {
        Name = "Max Volume";
        SeparateWindow = false;
    }

    protected override void OnInit()
    {
        base.OnInit();
        this.maxVolumeIndexes.Clear();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        if (this.Count <= this.Length+1)
            return;
        double currentVolume = Volume();
        bool isMaximum = false;
        for (int i = 1; i<=this.Length; i++)
        {
            double previousVolume = Volume(i);
            if (currentVolume > previousVolume)
                isMaximum = true;
            else
            {
                isMaximum = false;
                break;
            }
        }
        if (isMaximum && (this.maxVolumeIndexes.Count == 0 || this.maxVolumeIndexes[^1] != this.Count-1))
            this.maxVolumeIndexes.Add(this.Count - 1);
        else if (this.maxVolumeIndexes.Contains(this.Count - 1))
            this.maxVolumeIndexes.Remove(this.Count-1);
    }
    public override void OnPaintChart(PaintChartEventArgs args)
    {
        Graphics graphics = args.Graphics;
        RectangleF originalClip = graphics.ClipBounds;
        graphics.SetClip(args.Rectangle);
        try
        {
            var currWindow = this.CurrentChart.MainWindow;
            float barWidth = this.CurrentChart.BarsWidth;
            int leftIndex = args.LeftVisibleBarIndex;
            int rightIndex = args.RightVisibleBarIndex;
            for (int i = 0; i < this.maxVolumeIndexes.Count; i++)
            {
                int index = this.maxVolumeIndexes[i];
                if (index < leftIndex || index > rightIndex)
                    continue;
                index = this.Count - 1 - index;
                float x = (float)currWindow.CoordinatesConverter.GetChartX(this.Time(index));
                float y = (float)currWindow.CoordinatesConverter.GetChartY(this.Open(index));
                double currentClose = this.Close(index);
                double currentOpen = this.Open(index);
                Pen markerPen = new Pen(Color.White, 3);
                if (currentClose > currentOpen)
                    markerPen.Color = UpBarColor;
                else if (currentClose < currentOpen)
                    markerPen.Color = DownBarColor;
                else if (currentClose == currentOpen)
                    markerPen.Color = DojiColor;
                graphics.DrawLine(markerPen, x + barWidth / 2, y, x + barWidth*2 - barWidth / 2, y);
            }
        }
        finally
        {
            graphics.SetClip(originalClip);
        }
    }
    public override IList<SettingItem> Settings
    {
        get
        {
            var settings = base.Settings;
            settings.Add(new SettingItemInteger("Length", this.Length)
            {
                Text = "Comparsion Length",
                Minimum = 1,
            });
            settings.Add(new SettingItemColor("DojiColor", this.DojiColor)
            {
                Text = "Doji Color"
            });
            settings.Add(new SettingItemColor("UpBarColor", this.UpBarColor)
            {
                Text = "Up color"
            });
            settings.Add(new SettingItemColor("DownBarColor", this.DownBarColor)
            {
                Text = "Down Color"
            });
            return settings;
        }
        set
        {
            base.Settings = value;
            if (value.TryGetValue("Length", out int Length))
                this.Length = Length;
            if (value.TryGetValue("DojiColor", out Color DojiColor))
                this.DojiColor = DojiColor;
            if (value.TryGetValue("UpBarColor", out Color UpBarColor))
                this.UpBarColor = UpBarColor;
            if (value.TryGetValue("DownBarColor", out Color DownBarColor))
                this.DownBarColor = DownBarColor;
            this.OnSettingsUpdated();
        }
    }
}