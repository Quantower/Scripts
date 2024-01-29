// Copyright QUANTOWER LLC. © 2017-2021. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace VolumeIndicators;

public sealed class IndicatorTimeSessions : Indicator
{
    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorTimeSessions.cs";

    Session[] sessions = new Session[5] { new Session(Color.Green, "First Session", 1), new Session(Color.Red, "Second Session", 2), new Session(Color.GreenYellow, "Third Session", 3), new Session(Color.Blue, "Fourth Session", 4), new Session(Color.Cyan, "Fifth Session", 5) };

    public IndicatorTimeSessions()
        : base()
    {
        // Defines indicator's name and description.
        this.Name = "TimeSessions";
        this.SeparateWindow = false;
        this.OnBackGround = true;

    }


    protected override void OnInit()
    {
        Period currentPeriod = Period.MIN1;
        if (this.HistoricalData.Aggregation.TryGetPeriod(out Period period))
            currentPeriod = period;
    }


    protected override void OnUpdate(UpdateArgs args)
    {
    }

    Period currentPeriod;
    public override void OnPaintChart(PaintChartEventArgs args)
    {
        base.OnPaintChart(args);

        if (this.CurrentChart == null)
            return;

        Graphics graphics = args.Graphics;

        var mainWindow = this.CurrentChart.MainWindow;

        var leftBorderTime = Time(Count - 1);
        var rightBorderTime = Time(0);

        var bordersSpan = rightBorderTime - leftBorderTime;
        int daysSpan = (int)bordersSpan.TotalDays;
        int leftCoordinate;
        int rightCoordinate;

        DateTime startTime;
        DateTime endTime;
        if (currentPeriod.Duration.Days < 1)
            for (int i = 0; i < this.sessions.Length; i++)
            {
                if (sessions[i].SessionVisibility)
                {

                    var leftTime = this.sessions[i].SessionFirstTime;
                    var rightTime = this.sessions[i].SessionSecondTime;

                    startTime = new DateTime(leftBorderTime.Year, leftBorderTime.Month, leftBorderTime.Day, leftTime.Hour, leftTime.Minute, leftTime.Second);
                    endTime = new DateTime(leftBorderTime.Year, leftBorderTime.Month, leftBorderTime.Day, rightTime.Hour, rightTime.Minute, rightTime.Second);

                    if (leftTime.Hour > rightTime.Hour)
                        endTime = endTime.AddDays(1);

                    for (int j = 0; j <= daysSpan+1; j++)
                    {
                        if (startTime < (DateTime)mainWindow.CoordinatesConverter.GetTime((int)mainWindow.ClientRectangle.Right) && endTime > (DateTime)mainWindow.CoordinatesConverter.GetTime((int)mainWindow.ClientRectangle.Left))
                        {
                            if (startTime < (DateTime)mainWindow.CoordinatesConverter.GetTime((int)mainWindow.ClientRectangle.Left))
                                leftCoordinate = (int)mainWindow.ClientRectangle.Left;
                            else
                                leftCoordinate = (int)mainWindow.CoordinatesConverter.GetChartX(startTime);
                            if (endTime > (DateTime)mainWindow.CoordinatesConverter.GetTime((int)mainWindow.ClientRectangle.Right))
                                rightCoordinate = (int)mainWindow.ClientRectangle.Right;
                            else
                                rightCoordinate = (int)mainWindow.CoordinatesConverter.GetChartX(endTime);

                            graphics.FillRectangle(sessions[i].sessionBrush, leftCoordinate, 0, rightCoordinate - leftCoordinate, this.CurrentChart.MainWindow.ClientRectangle.Height);
                        }
                        startTime = startTime.AddDays(1);
                        endTime = endTime.AddDays(1);
                    }
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
                Text = "Session Visibility",
                SortIndex = sessionSortIndex,
                SeparatorGroup = separatorGroup1,
            });

            var visibleRelation = new SettingItemRelationVisibility(relationName, true);

            settings.Add(new SettingItemDateTime("SessionFirstTime", this.SessionFirstTime)
            {
                Text = "Start Session Time",
                SortIndex = sessionSortIndex,
                Format = DatePickerFormat.Time,
                SeparatorGroup = separatorGroup1,
                Relation = visibleRelation
            });

            settings.Add(new SettingItemDateTime("SessionSecondTime", this.SessionSecondTime)
            {
                Text = "End Session Time",
                SortIndex = sessionSortIndex,
                Format = DatePickerFormat.Time,
                SeparatorGroup = separatorGroup1,
                Relation = visibleRelation
            });
            settings.Add(new SettingItemColor("SessionColor", this.SessionColor)
            {
                Text = "Session Color",
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