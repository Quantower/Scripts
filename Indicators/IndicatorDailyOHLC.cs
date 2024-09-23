// Copyright QUANTOWER LLC. © 2017-2024. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using TradingPlatform.BusinessLayer;
using TradingPlatform.BusinessLayer.Chart;
using TradingPlatform.BusinessLayer.Utils;

namespace ChanneIsIndicators;

public class IndicatorDailyOHLC : Indicator
{
    #region Parameters   

    public DailyOHLCSessionType DailySessionType = DailyOHLCSessionType.AllDay;
    public string CustomSessionName = string.Empty;
    public DateTime CustomRangeStartTime
    {
        get
        {
            if (this.customRangeStartTime == default)
            {
                var session = this.CreateDefaultSession();
                this.customRangeStartTime = Core.Instance.TimeUtils.DateTimeUtcNow.Date.AddTicks(session.OpenTime.Ticks);
            }
            return this.customRangeStartTime;
        }
        set => this.customRangeStartTime = value;
    }
    private DateTime customRangeStartTime;
    public DateTime CustomRangeEndTime
    {
        get
        {
            if (this.customRangeEndTime == default)
                this.customRangeEndTime = this.CustomRangeStartTime;

            return this.customRangeEndTime;
        }
        set => this.customRangeEndTime = value;
    }
    private DateTime customRangeEndTime;
    public int DaysCount = 10;
    public int PreviousDataOffset = 0;
    public bool UseExtendLines = false;
    public bool AllowToDrawExtendLines => this.UseExtendLines && this.DailySessionType != DailyOHLCSessionType.AllDay;
    public DateTime ExtendRangeStartTime
    {
        get
        {
            if (this.extendRangeStartTime == default)
                this.extendRangeStartTime = this.CustomRangeStartTime;

            return this.extendRangeStartTime;
        }
        set => this.extendRangeStartTime = value;
    }
    private DateTime extendRangeStartTime;
    public DateTime ExtendRangeEndTime
    {
        get
        {
            if (this.extendRangeEndTime == default)
                this.extendRangeEndTime = this.CustomRangeEndTime;

            return this.extendRangeEndTime;
        }
        set => this.extendRangeEndTime = value;
    }
    private DateTime extendRangeEndTime;
    public NativeAlignment LabelAlignment { get; set; }
    public int labelFormat { get; set; }
    public int labelPosition { get; set; }
    public bool ShowLabel { get; private set; }
    public LineOptions OpenLineOptions
    {
        get => this.openLineOptions;
        private set
        {
            this.openLineOptions = value;
            this.openLinePen = ProcessPen(this.openLinePen, value);
        }
    }
    private LineOptions openLineOptions;
    private Pen openLinePen;
    public LineOptions OpenExtendLineOptions
    {
        get => this.openExtendLineOptions;
        private set
        {
            this.openExtendLineOptions = value;
            this.openExtendLinePen = ProcessPen(this.openExtendLinePen, value);
        }
    }
    private LineOptions openExtendLineOptions;
    private Pen openExtendLinePen;
    public bool ShowOpenLineLabel { get; private set; }

    public LineOptions HighLineOptions
    {
        get => this.highLineOptions;
        private set
        {
            this.highLineOptions = value;
            this.highLinePen = ProcessPen(this.highLinePen, value);
        }
    }
    private LineOptions highLineOptions;
    private Pen highLinePen;
    public LineOptions HighExtendLineOptions
    {
        get => this.highExtendLineOptions;
        private set
        {
            this.highExtendLineOptions = value;
            this.highExtendLinePen = ProcessPen(this.highExtendLinePen, value);
        }
    }
    private LineOptions highExtendLineOptions;
    private Pen highExtendLinePen;
    public bool ShowHighLineLabel { get; private set; }

    public LineOptions LowLineOptions
    {
        get => this.lowLineOptions;
        private set
        {
            this.lowLineOptions = value;
            this.lowLinePen = ProcessPen(this.lowLinePen, value);
        }
    }
    private LineOptions lowLineOptions;
    private Pen lowLinePen;
    public LineOptions LowExtendLineOptions
    {
        get => this.lowExtendLineOptions;
        private set
        {
            this.lowExtendLineOptions = value;
            this.lowExtendLinePen = ProcessPen(this.lowExtendLinePen, value);
        }
    }
    private LineOptions lowExtendLineOptions;
    private Pen lowExtendLinePen;
    public bool ShowLowLineLabel { get; private set; }

    public LineOptions CloseLineOptions
    {
        get => this.closeLineOptions;
        private set
        {
            this.closeLineOptions = value;
            this.closeLinePen = ProcessPen(this.closeLinePen, value);
        }
    }
    private LineOptions closeLineOptions;
    private Pen closeLinePen;
    public LineOptions CloseExtendLineOptions
    {
        get => this.closeExtendLineOptions;
        private set
        {
            this.closeExtendLineOptions = value;
            this.closeExtendLinePen = ProcessPen(this.closeExtendLinePen, value);
        }
    }
    private LineOptions closeExtendLineOptions;
    private Pen closeExtendLinePen;
    public bool ShowCloseLineLabel { get; private set; }

    public LineOptions MiddleLineOptions
    {
        get => this.middleLineOptions;
        private set
        {
            this.middleLineOptions = value;
            this.middleLinePen = ProcessPen(this.middleLinePen, value);
        }
    }
    private LineOptions middleLineOptions;
    private Pen middleLinePen;
    public LineOptions MiddleExtendLineOptions
    {
        get => this.middleExtendLineOptions;
        private set
        {
            this.middleExtendLineOptions = value;
            this.middleExtendLinePen = ProcessPen(this.middleExtendLinePen, value);
        }
    }
    private LineOptions middleExtendLineOptions;
    private Pen middleExtendLinePen;
    public bool ShowMiddleLineLabel { get; private set; }
    public Font CurrentFont { get; private set; }
    public string OpenCustomText = "O: ", HighCustomText = "H: ", LowCustomText = "L: ", CloseCustomText = "C: ", MiddleCustomText = "M: ";

    private readonly IList<DailyRangeItem> rangeCache;

    private DailyRangeItem currentRange;
    private ISession currentSession;
    private ISession extendSession;
    private ISessionsContainer chartSessionContainer;
    private DateTime currentSessionOpenDateTime;
    private DateTime currentSessionCloseDateTime;
    private readonly StringFormat centerNearSF;

    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorDailyOHLC.cs";

    #endregion Parameters

    public IndicatorDailyOHLC()
    {
        this.Name = "Daily OHLC";

        this.AllowFitAuto = true;
        this.SeparateWindow = false;
        this.LabelAlignment = NativeAlignment.Right;
        this.ShowLabel = true;
        this.labelFormat = 1;
        this.labelPosition = 1;
        this.rangeCache = new List<DailyRangeItem>();

        this.OpenLineOptions = new LineOptions()
        {
            Enabled = true,
            WithCheckBox = true,
            Color = Color.Orange,
            LineStyle = LineStyle.Solid,
            Width = 1
        };
        this.OpenExtendLineOptions = new LineOptions()
        {
            Enabled = true,
            WithCheckBox = true,
            Color = Color.Orange,
            LineStyle = LineStyle.Dash,
            Width = 1
        };
        this.ShowOpenLineLabel = true;

        this.HighLineOptions = new LineOptions()
        {
            Enabled = true,
            WithCheckBox = true,
            Color = Color.Red,
            LineStyle = LineStyle.Solid,
            Width = 1
        };
        this.HighExtendLineOptions = new LineOptions()
        {
            Enabled = true,
            WithCheckBox = true,
            Color = Color.Red,
            LineStyle = LineStyle.Dash,
            Width = 1
        };
        this.ShowHighLineLabel = true;

        this.LowLineOptions = new LineOptions()
        {
            Enabled = true,
            WithCheckBox = true,
            Color = Color.Green,
            LineStyle = LineStyle.Solid,
            Width = 1
        };
        this.LowExtendLineOptions = new LineOptions()
        {
            Enabled = true,
            WithCheckBox = true,
            Color = Color.Green,
            LineStyle = LineStyle.Dash,
            Width = 1
        };
        this.ShowLowLineLabel = true;

        this.CloseLineOptions = new LineOptions()
        {
            Enabled = true,
            WithCheckBox = true,
            Color = Color.FromArgb(33, 150, 243),
            LineStyle = LineStyle.Solid,
            Width = 1
        };
        this.CloseExtendLineOptions = new LineOptions()
        {
            Enabled = true,
            WithCheckBox = true,
            Color = Color.FromArgb(33, 150, 243),
            LineStyle = LineStyle.Dash,
            Width = 1
        };
        this.ShowCloseLineLabel = true;

        this.MiddleLineOptions = new LineOptions()
        {
            Enabled = true,
            WithCheckBox = true,
            Color = Color.Gray,
            LineStyle = LineStyle.DashDot,
            Width = 1
        };
        this.MiddleExtendLineOptions = new LineOptions()
        {
            Enabled = true,
            WithCheckBox = true,
            Color = Color.Gray,
            LineStyle = LineStyle.Dash,
            Width = 1
        };
        this.ShowMiddleLineLabel = true;

        this.CurrentFont = new Font("Verdana", 10, GraphicsUnit.Pixel);
        this.centerNearSF = new StringFormat()
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Near
        };
    }

    #region Base overrides

    protected override void OnInit()
    {
        this.chartSessionContainer = this.CurrentChart?.CurrentSessionContainer;

        if (this.CurrentChart != null)
            this.CurrentChart.SettingsChanged += this.CurrentChartOnSettingsChanged;

        if (this.DailySessionType == DailyOHLCSessionType.AllDay)
        {
            // default session
            this.currentSession = this.CreateDefaultSession();
        }
        else if (this.DailySessionType == DailyOHLCSessionType.SpecifiedSession)
        {
            if (!string.IsNullOrEmpty(this.CustomSessionName))
            {
                // selected chart session
                var sessions = this.GetAvailableCustomChartSessions().Concat(this.GetAvailableSymbolSessions()).ToList();
                if (sessions.Count > 0)
                    this.currentSession = sessions.FirstOrDefault(s => s.Name.Equals(this.CustomSessionName) && s.Type == SessionType.Main);
            }
        }
        else if (this.DailySessionType == DailyOHLCSessionType.CustomRange)
            this.currentSession = new Session("Custom session", this.CustomRangeStartTime.TimeOfDay, this.CustomRangeEndTime.TimeOfDay);

        if (this.UseExtendLines)
            this.extendSession = new Session("Extend session", this.ExtendRangeStartTime.TimeOfDay, this.ExtendRangeEndTime.TimeOfDay);
    }
    protected override void OnUpdate(UpdateArgs args)
    {
        if (this.currentSession == null)
            return;

        var currentBarTime = this.Time();
        var inSession = this.currentSession.ContainsDate(currentBarTime);

        // Main range
        if (inSession)
        {
            // create new 'main' range
            if (this.currentRange == null || currentBarTime >= this.currentSessionCloseDateTime)
            {
                this.currentSessionOpenDateTime = currentBarTime.Date.AddTicks(this.currentSession.OpenTime.Ticks);
                this.currentSessionCloseDateTime = currentBarTime.Date.AddTicks(this.currentSession.CloseTime.Ticks);

                if (currentBarTime < this.currentSessionOpenDateTime)
                    this.currentSessionOpenDateTime = this.currentSessionOpenDateTime.AddDays(-1);
                if (currentBarTime > this.currentSessionCloseDateTime)
                    this.currentSessionCloseDateTime = this.currentSessionOpenDateTime.AddDays(1);

                this.rangeCache.Insert(0, this.currentRange = new DailyRangeItem(currentBarTime, this.Open()));
            }

            // update High/Low/Close in history
            if (args.Reason == UpdateReason.HistoricalBar)
                this.currentRange.TryUpdate(this.High(), this.Low(), this.Close());
            else
                this.currentRange.TryUpdate(this.Close());

            this.currentRange.EndDateTime = currentBarTime;

            // recalcualte extend datetime positions
            if (this.UseExtendLines)
            {
                this.currentRange.ExtendStartDateTime = this.currentRange.EndDateTime.Date.AddTicks(this.extendSession.OpenTime.Ticks);
                this.currentRange.ExtendEndDateTime = this.currentRange.EndDateTime.Date.AddTicks(this.extendSession.CloseTime.Ticks);

                if (this.extendSession.OpenTime > this.extendSession.CloseTime)
                    this.currentRange.ExtendEndDateTime = this.currentRange.ExtendEndDateTime.AddDays(1);
            }
        }
    }
    protected override void OnClear()
    {
        this.currentRange = null;
        this.currentSession = null;
        this.currentSessionCloseDateTime = default;
        this.currentSessionOpenDateTime = default;

        if (this.CurrentChart != null)
            this.CurrentChart.SettingsChanged -= this.CurrentChartOnSettingsChanged;

        this.rangeCache?.Clear();
    }
    public override void Dispose()
    {
        if (this.CurrentChart != null)
            this.CurrentChart.SettingsChanged -= this.CurrentChartOnSettingsChanged;

        base.Dispose();
    }
    public override IList<SettingItem> Settings
    {
        get
        {
            var settings = base.Settings;

            var allDay = new SelectItem("All day", DailyOHLCSessionType.AllDay);
            var specifiedSession = new SelectItem("Specified session", DailyOHLCSessionType.SpecifiedSession);
            var customRange = new SelectItem("Custom range", DailyOHLCSessionType.CustomRange);

            var belowTL = new SelectItem("Below the line", 0);
            var aboveTL = new SelectItem("Above the line", 1);

            var formatPrice = new SelectItem("Price", 0);
            var formatTextPrice = new SelectItem("Text and Price", 1);
            var formatText = new SelectItem("Text", 2);

            //
            var defaultSeparator = settings.FirstOrDefault()?.SeparatorGroup;

            settings.Add(new SettingItemSelectorLocalized("Session type", new SelectItem("Session type", this.DailySessionType), new List<SelectItem>
                             {
                                 allDay,
                                 specifiedSession,
                                 customRange
                             })
            {
                SeparatorGroup = defaultSeparator,
                Text = "Session type",
                SortIndex = 10,
            });
            //
            settings.Add(new SettingItemString("CustomSessionName", this.CustomSessionName, 20)
            {
                SeparatorGroup = defaultSeparator,
                Text = loc._("Custom session name"),
                Relation = new SettingItemRelationVisibility("Session type", specifiedSession)
            });
            settings.Add(new SettingItemDateTime("StartTime", this.customRangeStartTime, 20)
            {
                SeparatorGroup = defaultSeparator,
                Text = loc._("Start time"),
                Format = DatePickerFormat.LongTime,
                ValueChangingBehavior = SettingItemValueChangingBehavior.WithConfirmation,
                Relation = new SettingItemRelationVisibility("Session type", customRange)
            });
            settings.Add(new SettingItemDateTime("EndTime", this.customRangeEndTime, 20)
            {
                SeparatorGroup = defaultSeparator,
                Text = loc._("End time"),
                Format = DatePickerFormat.LongTime,
                ValueChangingBehavior = SettingItemValueChangingBehavior.WithConfirmation,
                Relation = new SettingItemRelationVisibility("Session type", customRange)
            });
            settings.Add(new SettingItemInteger("DaysCount", this.DaysCount, 20)
            {
                SeparatorGroup = defaultSeparator,
                Text = loc._("Numbers of days to calculate"),
                Relation = new SettingItemRelationVisibility("Session type", allDay, specifiedSession, customRange)
            });
            settings.Add(new SettingItemInteger("PreviousDataOffset", this.PreviousDataOffset, 20)
            {
                SeparatorGroup = defaultSeparator,
                Text = loc._("Use previous data (offset in days)"),
                Relation = new SettingItemRelationVisibility("Session type", allDay, specifiedSession, customRange)
            });
            settings.Add(new SettingItemBoolean("ShowExtendLines", this.UseExtendLines, 40)
            {
                SeparatorGroup = defaultSeparator,
                Text = loc._("Show extend lines"),
                Relation = new SettingItemRelationVisibility("Session type", specifiedSession, customRange)
            });
            settings.Add(new SettingItemDateTime("StartExtendTime", this.extendRangeStartTime, 45)
            {
                SeparatorGroup = defaultSeparator,
                Text = loc._("Start extend time"),
                Format = DatePickerFormat.LongTime,
                ValueChangingBehavior = SettingItemValueChangingBehavior.WithConfirmation,
                Relation = new SettingItemRelationVisibility("ShowExtendLines", true)
            });
            settings.Add(new SettingItemDateTime("EndExtendTime", this.extendRangeEndTime, 50)
            {
                SeparatorGroup = defaultSeparator,
                Text = loc._("End extend time"),
                Format = DatePickerFormat.LongTime,
                ValueChangingBehavior = SettingItemValueChangingBehavior.WithConfirmation,
                Relation = new SettingItemRelationVisibility("ShowExtendLines", true)
            });
            //
            settings.Add(new SettingItemBoolean("ShowLabel", this.ShowLabel, 60)
            {
                SeparatorGroup = defaultSeparator,
                Text = loc._("Show label")
            });
            settings.Add(new SettingItemFont("Font", this.CurrentFont, 60)
            {
                SeparatorGroup = defaultSeparator,
                Text = loc._("Font"),
                Relation = new SettingItemRelationVisibility("ShowLabel", true)
            });
            settings.Add(new SettingItemAlignment("Label alignment", this.LabelAlignment, 60)
            {
                Text = loc._("Label alignment"),
                SeparatorGroup = defaultSeparator,
                Relation = new SettingItemRelationVisibility("ShowLabel", true)
            });
            settings.Add(new SettingItemSelectorLocalized("Label position", new SelectItem("Label position", this.labelPosition), new List<SelectItem>
                             {
                                 belowTL,
                                 aboveTL
                             })
            {
                SeparatorGroup = defaultSeparator,
                Text = "Label position",
                SortIndex = 60,
                Relation = new SettingItemRelationVisibility("ShowLabel", true)
            });
            settings.Add(new SettingItemSelectorLocalized("Format", new SelectItem("Format", this.labelFormat), new List<SelectItem>
                             {
                                 formatPrice,
                                 formatTextPrice,
                                 formatText
                             })
            {
                SeparatorGroup = defaultSeparator,
                Text = "Format",
                SortIndex = 60,
                Relation = new SettingItemRelationVisibility("ShowLabel", true)
            });
            //
            var openLineStyleSeparator = new SettingItemSeparatorGroup("Open line style", -999);
            settings.Add(new SettingItemLineOptions("OpenLineOptions", this.OpenLineOptions, 60)
            {
                SeparatorGroup = openLineStyleSeparator,
                Text = loc._("Main line style"),
                ExcludedStyles = new LineStyle[] { LineStyle.Points }
            });
            settings.Add(new SettingItemLineOptions("OpenExtendLineOptions", this.OpenExtendLineOptions, 60)
            {
                SeparatorGroup = openLineStyleSeparator,
                Text = loc._("Extend line style"),
                ExcludedStyles = new LineStyle[] { LineStyle.Points }
            });
            settings.Add(new SettingItemBoolean("ShowOpenLineLabel", this.ShowOpenLineLabel, 60)
            {
                SeparatorGroup = openLineStyleSeparator,
                Text = loc._("Show line label")
            });
            settings.Add(new SettingItemString("OpenCustomText", this.OpenCustomText, 60)
            {
                SeparatorGroup = openLineStyleSeparator,
                Text = loc._("Custom text"),
                Relation = new SettingItemRelationVisibility("Format", formatText, formatTextPrice)
            });
            //
            var highLineStyleSeparator = new SettingItemSeparatorGroup("High line style", -999);
            settings.Add(new SettingItemLineOptions("HighLineOptions", this.HighLineOptions, 60)
            {
                SeparatorGroup = highLineStyleSeparator,
                Text = loc._("Main line style"),
                ExcludedStyles = new LineStyle[] { LineStyle.Points }
            });
            settings.Add(new SettingItemLineOptions("HighExtendLineOptions", this.HighExtendLineOptions, 60)
            {
                SeparatorGroup = highLineStyleSeparator,
                Text = loc._("Extend line style"),
                ExcludedStyles = new LineStyle[] { LineStyle.Points }
            });
            settings.Add(new SettingItemBoolean("ShowHighLineLabel", this.ShowHighLineLabel, 60)
            {
                SeparatorGroup = highLineStyleSeparator,
                Text = loc._("Show line label")
            });
            settings.Add(new SettingItemString("HighCustomText", this.HighCustomText, 60)
            {
                SeparatorGroup = highLineStyleSeparator,
                Text = loc._("Custom text"),
                Relation = new SettingItemRelationVisibility("Format", formatText, formatTextPrice)
            });
            //
            var lowLineStyleSeparator = new SettingItemSeparatorGroup("Low line style", -999);
            settings.Add(new SettingItemLineOptions("LowLineOptions", this.LowLineOptions, 60)
            {
                SeparatorGroup = lowLineStyleSeparator,
                Text = loc._("Main line style"),
                ExcludedStyles = new LineStyle[] { LineStyle.Points }
            });
            settings.Add(new SettingItemLineOptions("LowExtendLineOptions", this.LowExtendLineOptions, 60)
            {
                SeparatorGroup = lowLineStyleSeparator,
                Text = loc._("Extend line style"),
                ExcludedStyles = new LineStyle[] { LineStyle.Points }
            });
            settings.Add(new SettingItemBoolean("ShowLowLineLabel", this.ShowLowLineLabel, 60)
            {
                SeparatorGroup = lowLineStyleSeparator,
                Text = loc._("Show line label")
            });
            settings.Add(new SettingItemString("LowCustomText", this.LowCustomText, 60)
            {
                SeparatorGroup = lowLineStyleSeparator,
                Text = loc._("Custom text"),
                Relation = new SettingItemRelationVisibility("Format", formatText, formatTextPrice)
            });
            //
            var closeLineStyleSeparator = new SettingItemSeparatorGroup("Close line style", -999);
            settings.Add(new SettingItemLineOptions("CloseLineOptions", this.CloseLineOptions, 60)
            {
                SeparatorGroup = closeLineStyleSeparator,
                Text = loc._("Main line style"),
                ExcludedStyles = new LineStyle[] { LineStyle.Points }
            });
            settings.Add(new SettingItemLineOptions("CloseExtendLineOptions", this.CloseExtendLineOptions, 60)
            {
                SeparatorGroup = closeLineStyleSeparator,
                Text = loc._("Extend line style"),
                ExcludedStyles = new LineStyle[] { LineStyle.Points }
            });
            settings.Add(new SettingItemBoolean("ShowCloseLineLabel", this.ShowCloseLineLabel, 60)
            {
                SeparatorGroup = closeLineStyleSeparator,
                Text = loc._("Show line label")
            });
            settings.Add(new SettingItemString("CloseCustomText", this.CloseCustomText, 60)
            {
                SeparatorGroup = closeLineStyleSeparator,
                Text = loc._("Custom text"),
                Relation = new SettingItemRelationVisibility("Format", formatText, formatTextPrice)
            });
            //
            var middleLineStyleSeparator = new SettingItemSeparatorGroup("Middle line style", -999);
            settings.Add(new SettingItemLineOptions("MiddleLineOptions", this.MiddleLineOptions, 60)
            {
                SeparatorGroup = middleLineStyleSeparator,
                Text = loc._("Main line style"),
                ExcludedStyles = new LineStyle[] { LineStyle.Points }
            });
            settings.Add(new SettingItemLineOptions("MiddleExtendLineOptions", this.MiddleExtendLineOptions, 60)
            {
                SeparatorGroup = middleLineStyleSeparator,
                Text = loc._("Extend line style"),
                ExcludedStyles = new LineStyle[] { LineStyle.Points }
            });
            settings.Add(new SettingItemBoolean("ShowMiddleLineLabel", this.ShowMiddleLineLabel, 60)
            {
                SeparatorGroup = middleLineStyleSeparator,
                Text = loc._("Show line label")
            });
            settings.Add(new SettingItemString("MiddleCustomText", this.MiddleCustomText, 60)
            {
                SeparatorGroup = middleLineStyleSeparator,
                Text = loc._("Custom text"),
                Relation = new SettingItemRelationVisibility("Format", formatText, formatTextPrice)
            });

            return settings;
        }
        set
        {
            var holder = new SettingsHolder(value);

            if (holder.TryGetValue("OpenLineOptions", out SettingItem item) && item.Value is LineOptions openOptions)
                this.OpenLineOptions = openOptions;
            if (holder.TryGetValue("OpenExtendLineOptions", out item) && item.Value is LineOptions openExtendOptions)
                this.OpenExtendLineOptions = openExtendOptions;
            if (holder.TryGetValue("ShowOpenLineLabel", out item) && item.Value is bool showOpenLabel)
                this.ShowOpenLineLabel = showOpenLabel;
            if (holder.TryGetValue("OpenCustomText", out item) && item.Value is string openCustomText)
                this.OpenCustomText = openCustomText;

            if (holder.TryGetValue("HighLineOptions", out item) && item.Value is LineOptions highOptions)
                this.HighLineOptions = highOptions;
            if (holder.TryGetValue("HighExtendLineOptions", out item) && item.Value is LineOptions highExtendOptions)
                this.HighExtendLineOptions = highExtendOptions;
            if (holder.TryGetValue("ShowHighLineLabel", out item) && item.Value is bool showHighLabel)
                this.ShowHighLineLabel = showHighLabel;
            if (holder.TryGetValue("HighCustomText", out item) && item.Value is string highCustomText)
                this.HighCustomText = highCustomText;

            if (holder.TryGetValue("LowLineOptions", out item) && item.Value is LineOptions lowOptions)
                this.LowLineOptions = lowOptions;
            if (holder.TryGetValue("LowExtendLineOptions", out item) && item.Value is LineOptions lowExtendOptions)
                this.LowExtendLineOptions = lowExtendOptions;
            if (holder.TryGetValue("ShowLowLineLabel", out item) && item.Value is bool showLowLabel)
                this.ShowLowLineLabel = showLowLabel;
            if (holder.TryGetValue("LowCustomText", out item) && item.Value is string lowCustomText)
                this.LowCustomText = lowCustomText;

            if (holder.TryGetValue("CloseLineOptions", out item) && item.Value is LineOptions closeOptions)
                this.CloseLineOptions = closeOptions;
            if (holder.TryGetValue("CloseExtendLineOptions", out item) && item.Value is LineOptions closeExtendOptions)
                this.CloseExtendLineOptions = closeExtendOptions;
            if (holder.TryGetValue("ShowCloseLineLabel", out item) && item.Value is bool showCloseLabel)
                this.ShowCloseLineLabel = showCloseLabel;
            if (holder.TryGetValue("CloseCustomText", out item) && item.Value is string closeCustomText)
                this.CloseCustomText = closeCustomText;

            if (holder.TryGetValue("MiddleLineOptions", out item) && item.Value is LineOptions middleOptions)
                this.MiddleLineOptions = middleOptions;
            if (holder.TryGetValue("MiddleExtendLineOptions", out item) && item.Value is LineOptions middleExtendOptions)
                this.MiddleExtendLineOptions = middleExtendOptions;
            if (holder.TryGetValue("ShowMiddleLineLabel", out item) && item.Value is bool showMiddleLabel)
                this.ShowMiddleLineLabel = showMiddleLabel;
            if (holder.TryGetValue("MiddleCustomText", out item) && item.Value is string middleCustomText)
                this.MiddleCustomText = middleCustomText;

            var needRefresh = false;
            if (holder.TryGetValue("Session type", out item) && item.GetValue<DailyOHLCSessionType>() != this.DailySessionType)
            {
                this.DailySessionType = item.GetValue<DailyOHLCSessionType>();
                needRefresh |= item.ValueChangingReason == SettingItemValueChangingReason.Manually;
            }
            if (holder.TryGetValue("CustomSessionName", out item) && item.Value is string customSessionName)
                this.CustomSessionName = customSessionName;
            if (holder.TryGetValue("StartTime", out item) && item.Value is DateTime dtStartTime)
            {
                this.customRangeStartTime = dtStartTime;
                needRefresh |= item.ValueChangingReason == SettingItemValueChangingReason.Manually;
            }
            if (holder.TryGetValue("EndTime", out item) && item.Value is DateTime dtEndTime)
            {
                this.customRangeEndTime = dtEndTime;
                needRefresh |= item.ValueChangingReason == SettingItemValueChangingReason.Manually;
            }
            if (holder.TryGetValue("DaysCount", out item) && item.Value is int daysCount)
                this.DaysCount = daysCount;
            if (holder.TryGetValue("PreviousDataOffset", out item) && item.Value is int previousDataOffset)
                this.PreviousDataOffset = previousDataOffset;
            if (holder.TryGetValue("ShowExtendLines", out item) && item.Value is bool useExtendLines)
                this.UseExtendLines = useExtendLines;
            if (holder.TryGetValue("StartExtendTime", out item) && item.Value is DateTime dtStartExtendTime)
            {
                this.extendRangeStartTime = dtStartExtendTime;
                needRefresh |= item.ValueChangingReason == SettingItemValueChangingReason.Manually;
            }
            if (holder.TryGetValue("EndExtendTime", out item) && item.Value is DateTime dtEndExtendTime)
            {
                this.extendRangeEndTime = dtEndExtendTime;
                needRefresh |= item.ValueChangingReason == SettingItemValueChangingReason.Manually;
            }
            if (holder.TryGetValue("Font", out item) && item.Value is Font font)
                this.CurrentFont = font;
            if (holder.TryGetValue("Label alignment", out item) && item.Value is NativeAlignment labelAlignment)
                this.LabelAlignment = labelAlignment;
            if (holder.TryGetValue("ShowLabel", out item) && item.Value is bool showLabel)
                this.ShowLabel = showLabel;
            if (holder.TryGetValue("Label position", out var lpitem)&& lpitem.GetValue<int>() != this.labelPosition)
                this.labelPosition = lpitem.GetValue<int>();
            if (holder.TryGetValue("Format", out var lfitem)&& lfitem.GetValue<int>() != this.labelFormat)
                this.labelFormat = lfitem.GetValue<int>();
            if (needRefresh)
                this.Refresh();
            base.Settings = value;
        }
    }

    public override void OnPaintChart(PaintChartEventArgs args)
    {
        var gr = args.Graphics;
        var restoredClip = gr.ClipBounds;

        gr.SetClip(args.Rectangle);

        try
        {
            var currentWindow = this.CurrentChart.Windows[args.WindowIndex];

            var leftTime = currentWindow.CoordinatesConverter.GetTime(args.Rectangle.Left);
            var rightTime = currentWindow.CoordinatesConverter.GetTime(args.Rectangle.Right);

            int halfBarWidth = this.CurrentChart.BarsWidth / 2;
            for (int i = 0; i < this.DaysCount; i++)
            {
                if (i >= this.rangeCache.Count)
                    break;

                var range = this.rangeCache[i];

                bool isMainRangeOuside = range.EndDateTime < leftTime || range.StartDateTime > rightTime;
                bool needDrawExtendLines = this.AllowToDrawExtendLines && range.ExtendStartDateTime != range.ExtendEndDateTime;

                if (isMainRangeOuside)
                {
                    if (!isMainRangeOuside || range.ExtendEndDateTime < leftTime || range.ExtendStartDateTime > rightTime)
                        continue;
                }

                int prevDailyRangeOffset = i + this.PreviousDataOffset;
                bool needUsePreviosRange = prevDailyRangeOffset != i;

                if (needUsePreviosRange && prevDailyRangeOffset >= this.rangeCache.Count)
                    break;

                float leftX = (float)currentWindow.CoordinatesConverter.GetChartX(range.StartDateTime) + halfBarWidth;
                if (leftX < args.Rectangle.Left)
                    leftX = args.Rectangle.Left;

                float rightX = (float)currentWindow.CoordinatesConverter.GetChartX(range.EndDateTime) + halfBarWidth;
                if (rightX > args.Rectangle.Right)
                    rightX = args.Rectangle.Right;

                float leftExtendX = default;
                float rightExtendX = default;

                if (needDrawExtendLines)
                {
                    leftExtendX = (float)currentWindow.CoordinatesConverter.GetChartX(range.ExtendStartDateTime) + halfBarWidth;
                    if (leftExtendX < args.Rectangle.Left)
                        leftExtendX = args.Rectangle.Left;

                    rightExtendX = (float)currentWindow.CoordinatesConverter.GetChartX(range.ExtendEndDateTime) + halfBarWidth;
                    if (rightExtendX > args.Rectangle.Right)
                        rightExtendX = args.Rectangle.Right;
                }

                int top = args.Rectangle.Top;
                int bottom = args.Rectangle.Bottom;

                // get previous date
                if (needUsePreviosRange)
                    range = this.rangeCache[prevDailyRangeOffset];

                if (this.HighLineOptions.Enabled || this.HighExtendLineOptions.Enabled)
                {
                    float highY = (float)currentWindow.CoordinatesConverter.GetChartY(range.High);
                    if (highY > top && highY < bottom)
                    {
                        if (!isMainRangeOuside && this.HighLineOptions.Enabled)
                        {
                            gr.DrawLine(this.highLinePen, leftX, highY, rightX, highY);

                            if (this.ShowHighLineLabel)
                                this.DrawBillet(gr, range.High, ref leftX, ref rightX, ref highY, this.CurrentFont, this.highLineOptions, this.highLinePen, this.centerNearSF, this.LabelAlignment, HighCustomText);
                        }

                        if (needDrawExtendLines && this.HighExtendLineOptions.Enabled)
                            gr.DrawLine(this.highExtendLinePen, leftExtendX, highY, rightExtendX, highY);
                    }
                }

                if (this.LowLineOptions.Enabled || this.LowExtendLineOptions.Enabled)
                {
                    float lowY = (float)currentWindow.CoordinatesConverter.GetChartY(range.Low);
                    if (lowY > top && lowY < bottom)
                    {
                        if (!isMainRangeOuside && this.LowLineOptions.Enabled)
                        {
                            gr.DrawLine(this.lowLinePen, leftX, lowY, rightX, lowY);

                            if (this.ShowLowLineLabel)
                                this.DrawBillet(gr, range.Low, ref leftX, ref rightX, ref lowY, this.CurrentFont, this.lowLineOptions, this.lowLinePen, this.centerNearSF, this.LabelAlignment, LowCustomText);
                        }

                        if (needDrawExtendLines && this.LowExtendLineOptions.Enabled)
                            gr.DrawLine(this.lowExtendLinePen, leftExtendX, lowY, rightExtendX, lowY);
                    }
                }

                if (this.OpenLineOptions.Enabled || this.OpenExtendLineOptions.Enabled)
                {
                    float openY = (float)currentWindow.CoordinatesConverter.GetChartY(range.Open);
                    if (openY > top && openY < bottom)
                    {
                        if (!isMainRangeOuside && this.OpenLineOptions.Enabled)
                        {
                            gr.DrawLine(this.openLinePen, leftX, openY, rightX, openY);

                            if (this.ShowOpenLineLabel)
                                this.DrawBillet(gr, range.Open, ref leftX, ref rightX, ref openY, this.CurrentFont, this.openLineOptions, this.openLinePen, this.centerNearSF, this.LabelAlignment, OpenCustomText);
                        }

                        if (needDrawExtendLines && this.OpenExtendLineOptions.Enabled)
                            gr.DrawLine(this.openExtendLinePen, leftExtendX, openY, rightExtendX, openY);
                    }
                }

                if (this.CloseLineOptions.Enabled || this.CloseExtendLineOptions.Enabled)
                {
                    float closeY = (float)currentWindow.CoordinatesConverter.GetChartY(range.Close);
                    if (closeY > top && closeY < bottom)
                    {
                        if (!isMainRangeOuside && this.CloseLineOptions.Enabled)
                        {
                            gr.DrawLine(this.closeLinePen, leftX, closeY, rightX, closeY);

                            if (this.ShowCloseLineLabel)
                                this.DrawBillet(gr, range.Close, ref leftX, ref rightX, ref closeY, this.CurrentFont, this.closeLineOptions, this.closeLinePen, this.centerNearSF, this.LabelAlignment, CloseCustomText);
                        }

                        if (needDrawExtendLines && this.CloseExtendLineOptions.Enabled)
                            gr.DrawLine(this.closeExtendLinePen, leftExtendX, closeY, rightExtendX, closeY);
                    }
                }

                if (this.MiddleLineOptions.Enabled || this.MiddleExtendLineOptions.Enabled)
                {
                    float middleY = (float)currentWindow.CoordinatesConverter.GetChartY(range.MiddlePrice);
                    if (middleY > top && middleY < bottom)
                    {
                        if (!isMainRangeOuside && this.MiddleLineOptions.Enabled)
                        {
                            gr.DrawLine(this.middleLinePen, leftX, middleY, rightX, middleY);

                            if (this.ShowMiddleLineLabel)
                                this.DrawBillet(gr, range.MiddlePrice, ref leftX, ref rightX, ref middleY, this.CurrentFont, this.middleLineOptions, this.middleLinePen, this.centerNearSF, this.LabelAlignment, MiddleCustomText);
                        }

                        if (needDrawExtendLines && this.MiddleExtendLineOptions.Enabled)
                            gr.DrawLine(this.middleExtendLinePen, leftExtendX, middleY, rightExtendX, middleY);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Core.Loggers.Log(ex);
        }
        finally
        {
            gr.SetClip(restoredClip);
        }
    }
    protected override bool OnTryGetMinMax(int fromOffset, int toOffset, out double min, out double max)
    {
        min = Const.DOUBLE_UNDEFINED;
        max = Const.DOUBLE_UNDEFINED;

        var fromTime = this.HistoricalData[toOffset].TicksLeft;
        var toTime = this.HistoricalData[fromOffset].TicksLeft;

        var minPrice = double.MaxValue;
        var maxPrice = double.MinValue;
        var hasMinPrice = false;
        var hasMaxPrice = false;

        for (int i = 0; i < this.DaysCount; i++)
        {
            if (i >= this.rangeCache.Count)
                break;

            var range = this.rangeCache[i];
            if (range.StartDateTime.Ticks > toTime || range.EndDateTime.Ticks < fromTime)
                continue;

            if (maxPrice < range.High)
            {
                hasMaxPrice = true;
                maxPrice = range.High;
            }

            if (minPrice > range.Low)
            {
                hasMinPrice = true;
                minPrice = range.Low;
            }
        }

        if (hasMinPrice)
            min = minPrice;

        if (hasMaxPrice)
            max = maxPrice;

        return hasMaxPrice || hasMinPrice;
    }

    #endregion Base overrides

    private void CurrentChartOnSettingsChanged(object sender, ChartEventArgs e)
    {
        if (this.DailySessionType == DailyOHLCSessionType.SpecifiedSession)
        {
            if (this.CurrentChart?.CurrentSessionContainer == null || this.chartSessionContainer == null)
                return;

            if (!this.chartSessionContainer.Equals(this.CurrentChart.CurrentSessionContainer))
                this.Refresh();
        }
    }

    private void DrawBillet(Graphics gr, double price, ref float leftX, ref float rightX, ref float priceY, Font font, LineOptions lineOptions, Pen pen, StringFormat stringFormat, NativeAlignment nativeAlignment, string prefix)
    {
        string label = "";
        if (ShowLabel==true)
            label = labelFormat==1 ? prefix + this.Symbol.FormatPrice(price) : labelFormat == 0 ? this.Symbol.FormatPrice(price) : prefix;
        var labelSize = gr.MeasureString(label, font);

        var rect = new RectangleF()
        {
            Height = labelSize.Height,
            Width = labelSize.Width + 5,
            Y = labelPosition == 1 ? priceY - labelSize.Height - lineOptions.Width : priceY - lineOptions.Width + 1
        };

        switch (nativeAlignment)
        {
            case NativeAlignment.Center:
                {
                    rect.X = (rightX - leftX) / 2f + leftX - rect.Width / 2f;
                    break;
                }
            case NativeAlignment.Right:
                {
                    rect.X = rightX - rect.Width;
                    break;
                }
            case NativeAlignment.Left:
            default:
                {
                    rect.X = leftX;
                    break;
                }
        }

        gr.FillRectangle(pen.Brush, rect);
        gr.DrawString(label, font, Brushes.White, rect, stringFormat);
    }
    private Session CreateDefaultSession()
    {
        // 00:00
        var startTime = new DateTime(Core.Instance.TimeUtils.DateTimeUtcNow.Date.Ticks, DateTimeKind.Unspecified);
        // 23:59:59
        var endTime = new DateTime(Core.Instance.TimeUtils.DateTimeUtcNow.Date.AddHours(23).AddMinutes(59).AddSeconds(59).Ticks, DateTimeKind.Unspecified);

        var timeZone = this.CurrentChart?.CurrentTimeZone ?? Core.Instance.TimeUtils.SelectedTimeZone;
        return new Session("Default",
            Core.Instance.TimeUtils.ConvertFromTimeZoneToUTC(startTime, timeZone).TimeOfDay,
            Core.Instance.TimeUtils.ConvertFromTimeZoneToUTC(endTime, timeZone).TimeOfDay);
    }
    private IList<ISession> GetAvailableCustomChartSessions() => this.chartSessionContainer?.ActiveSessions?.ToList() ?? new List<ISession>();
    private IList<ISession> GetAvailableSymbolSessions() => this.Symbol?.CurrentSessionsInfo?.ActiveSessions?.ToList() ?? new List<ISession>();

    private static Pen ProcessPen(Pen pen, LineOptions lineOptions)
    {
        if (pen == null)
            pen = new Pen(Color.Empty);

        pen.Color = lineOptions.Color;
        pen.Width = lineOptions.Width;

        try
        {
            switch (lineOptions.LineStyle)
            {
                case LineStyle.Solid:
                    {
                        pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Solid;
                        break;
                    }
                case LineStyle.Dot:
                    {
                        pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dot;
                        break;
                    }
                case LineStyle.Dash:
                    {
                        pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                        break;
                    }
                case LineStyle.DashDot:
                    {
                        pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Custom;
                        float[] dp = new float[] { 2, 4, 7, 4 };
                        pen.DashPattern = dp;
                        break;
                    }
                case LineStyle.Histogramm:
                    {
                        pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Custom;
                        float[] dp = new float[] { 0.25F, 1 };
                        pen.DashPattern = dp;
                        pen.Width = 4;
                        break;
                    }
            }
        }
        catch { }
        return pen;
    }
}

#region Nested

internal class DailyRangeItem
{
    public double High { get; private set; }
    public double Low { get; private set; }
    public double Open { get; private set; }
    public double Close { get; private set; }
    public double MiddlePrice => (this.High + this.Low) / 2;

    public DateTime StartDateTime { get; internal set; }
    public DateTime EndDateTime { get; internal set; }
    public DateTime ExtendStartDateTime { get; internal set; }
    public DateTime ExtendEndDateTime { get; internal set; }

    public DailyRangeItem(DateTime startDateTime, double openPrice)
    {
        this.StartDateTime = startDateTime;
        this.Open = openPrice;

        this.High = double.MinValue;
        this.Low = double.MaxValue;
    }

    public bool TryUpdate(double high, double low, double close)
    {
        bool updated = false;

        if (this.High < high)
        {
            this.High = high;
            updated = true;
        }

        if (this.Low > low)
        {
            this.Low = low;
            updated = true;
        }

        if (this.Close != close)
        {
            this.Close = close;
            updated = true;
        }

        return updated;
    }
    public bool TryUpdate(double close)
    {
        bool updated = false;

        if (this.High < close)
        {
            this.High = close;
            updated = true;
        }

        if (this.Low > close)
        {
            this.Low = close;
            updated = true;
        }

        if (this.Close != close)
        {
            this.Close = close;
            updated = true;
        }
        return updated;
    }
}

public enum DailyOHLCSessionType { AllDay, SpecifiedSession, CustomRange, }
public enum DailyOHLCLabelPosition { Left, Center, Right }

#endregion Nested