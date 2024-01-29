// Copyright QUANTOWER LLC. © 2017-2021. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace VolumeIndicators;

public sealed class IndicatorTimeSessions : Indicator
{
    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorTimeSessions.cs";

    private Period currentPeriod;

    private readonly Session[] sessions;

    public IndicatorTimeSessions()
        : base()
    {
        // Defines indicator's name and description.
        this.Name = "TimeSessions";

        // By default indicator will be applied on main window of the chart
        this.SeparateWindow = false;
        this.OnBackGround = true;

        this.sessions = new Session[]
        {
            new Session(Color.Green, "First Session", 1),
            new Session(Color.Red, "Second Session", 2),
            new Session(Color.GreenYellow, "Third Session", 3),
            new Session(Color.Blue, "Fourth Session", 4),
            new Session(Color.Cyan, "Fifth Session", 5)
        };
    }

    protected override void OnInit() => this.currentPeriod =this.HistoricalData.Aggregation.TryGetPeriod(out var period) ? period : Period.MIN1;

    protected override void OnUpdate(UpdateArgs args) { }


    public override void OnPaintChart(PaintChartEventArgs args)
    {
        base.OnPaintChart(args);

        if (this.CurrentChart == null)
            return;

        if (this.currentPeriod.Duration.Days >= 1)
            return;

        var graphics = args.Graphics;

        var mainWindow = this.CurrentChart.MainWindow;

        var leftBorderTime = this.Time(this.Count - 1);
        var rightBorderTime = this.Time(0);

        var bordersSpan = rightBorderTime - leftBorderTime;
        int daysSpan = (int)bordersSpan.TotalDays;
        int leftCoordinate;
        int rightCoordinate;

        DateTime startTime;
        DateTime endTime;

        for (int i = 0; i < this.sessions.Length; i++)
        {
            if (!this.sessions[i].SessionVisibility)
                continue;

            var leftTime = this.sessions[i].SessionFirstTime;
            var rightTime = this.sessions[i].SessionSecondTime;

            startTime = new DateTime(leftBorderTime.Year, leftBorderTime.Month, leftBorderTime.Day, leftTime.Hour, leftTime.Minute, leftTime.Second);
            endTime = new DateTime(leftBorderTime.Year, leftBorderTime.Month, leftBorderTime.Day, rightTime.Hour, rightTime.Minute, rightTime.Second);

            if (leftTime.Hour > rightTime.Hour)
                endTime = endTime.AddDays(1);

            for (int j = 0; j <= daysSpan+1; j++)
            {
                if (startTime < mainWindow.CoordinatesConverter.GetTime(mainWindow.ClientRectangle.Right) && endTime > mainWindow.CoordinatesConverter.GetTime((int)mainWindow.ClientRectangle.Left))
                {
                    if (startTime < mainWindow.CoordinatesConverter.GetTime(mainWindow.ClientRectangle.Left))
                        leftCoordinate = mainWindow.ClientRectangle.Left;
                    else
                        leftCoordinate = (int)mainWindow.CoordinatesConverter.GetChartX(startTime);
                    if (endTime > mainWindow.CoordinatesConverter.GetTime(mainWindow.ClientRectangle.Right))
                        rightCoordinate = mainWindow.ClientRectangle.Right;
                    else
                        rightCoordinate = (int)mainWindow.CoordinatesConverter.GetChartX(endTime);

                    graphics.FillRectangle(this.sessions[i].sessionBrush, leftCoordinate, 0, rightCoordinate - leftCoordinate, this.CurrentChart.MainWindow.ClientRectangle.Height);
                }
                startTime = startTime.AddDays(1);
                endTime = endTime.AddDays(1);
            }
        }
    }

    public override IList<SettingItem> Settings
    {
        get
        {
            var settings = base.Settings;
            for (int i = 0; i < this.sessions.Length; i++)
                settings.Add(new SettingItemGroup(this.sessions[i].SessionName, this.sessions[i].Settings));

            return settings;
        }
        set
        {
            base.Settings = value;
            for (int i = 0; i < this.sessions.Length; i++)
                this.sessions[i].Settings = value;
        }
    }
}

internal sealed class Session : ICustomizable
{
    public DateTime SessionFirstTime { get; set; }
    public DateTime SessionSecondTime { get; set; }
    public Color SessionColor { get; set; }
    public bool SessionVisibility { get; set; }
    public string SessionName { get; set; }
    private int sessionSortIndex { get; set; }
    public SolidBrush sessionBrush { get; set; }
    public Session(Color color, string name = "Session X", int sortingIndex = 20)
    {
        this.SessionName = name;
        this.sessionSortIndex = sortingIndex;
        this.SessionColor = Color.FromArgb(51, color);
        this.SessionFirstTime = new DateTime();
        this.SessionSecondTime = new DateTime();
        this.SessionVisibility = false;
        this.sessionBrush = new SolidBrush(this.SessionColor);
    }
    public IList<SettingItem> Settings
    {
        get
        {
            var settings = new List<SettingItem>();
            var separatorGroup1 = new SettingItemSeparatorGroup(this.SessionName, this.sessionSortIndex);

            string relationName = $"{this.SessionName}SessionVisibility";
            settings.Add(new SettingItemBoolean(relationName, this.SessionVisibility)
            {
                Text = "Visible",
                SortIndex = sessionSortIndex,
                SeparatorGroup = separatorGroup1,
            });

            var visibleRelation = new SettingItemRelationVisibility(relationName, true);

            settings.Add(new SettingItemDateTime("SessionFirstTime", this.SessionFirstTime)
            {
                Text = "Start Time",
                SortIndex = sessionSortIndex,
                Format = DatePickerFormat.Time,
                SeparatorGroup = separatorGroup1,
                Relation = visibleRelation
            });

            settings.Add(new SettingItemDateTime("SessionSecondTime", this.SessionSecondTime)
            {
                Text = "End Time",
                SortIndex = sessionSortIndex,
                Format = DatePickerFormat.Time,
                SeparatorGroup = separatorGroup1,
                Relation = visibleRelation
            });
            settings.Add(new SettingItemColor("SessionColor", this.SessionColor)
            {
                Text = "Color",
                SortIndex = sessionSortIndex,
                SeparatorGroup = separatorGroup1,
                Relation = visibleRelation
            });

            return settings;
        }
        set
        {
            var settings = new List<SettingItem>();

            if (value.TryGetValue(this.SessionName, out List<SettingItem> inputSettings))
                settings = inputSettings;
            if (settings.TryGetValue($"{this.SessionName}SessionVisibility", out bool SessionVisibility))
                this.SessionVisibility = SessionVisibility;
            if (settings.TryGetValue("SessionFirstTime", out DateTime SessionFirstTime))
                this.SessionFirstTime = SessionFirstTime;
            if (settings.TryGetValue("SessionSecondTime", out DateTime SessionSecondTime))
                this.SessionSecondTime = SessionSecondTime;
            if (settings.TryGetValue("SessionColor", out Color SessionColor))
                this.SessionColor = SessionColor;
            this.sessionBrush.Color = this.SessionColor;
        }
    }
}