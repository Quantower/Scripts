// Copyright QUANTOWER LLC. © 2017-2024. All rights reserved.

using BarsDataIndicators.Utils;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using TradingPlatform.BusinessLayer;
using TradingPlatform.BusinessLayer.Utils;

namespace BarsDataIndicators;

public class IndicatorCumulativeDelta : IndicatorCandleDrawBase, IVolumeAnalysisIndicator, ISessionObserverIndicator
{
    #region Consts

    private const string BY_VOLUME_TYPE = "By volume";
    private const string BY_TRADES_TYPE = "By trades";

    private const string CHART_SESSION_CONTAINER_SELECT_ITEM = "Chart session";

    private const string CUSTOM_OPEN_SESSION_NAME_SI = "Open time";
    private const string CUSTOM_CLOSE_SESSION_NAME_SI = "Close time";
    private const string RESET_PERIOD_NAME_SI = "ResetPeriod";
    private const string SESSION_TEMPLATE_NAME_SI = "sessionsTemplate";

    private const string RESET_TYPE_NAME_SI = "Reset type";
    private const string BY_PERIOD_SESSION_TYPE = "By period";
    private const string FULL_HISTORY_SESSION_TYPE = "Full range";
    private const string SPECIFIED_SESSION_TYPE = "Specified session";
    private const string CUSTOM_RANGE_SESSION_TYPE = "Custom range";

    private const string LINE_COLORS_SI = "ColorLines";
    private const string CLOSE_LINE_COLOR_BY_SI = "CloseLineColorBy";
    private const string MA_LINE_COLORS_SI = "MAColorLines";
    private const string MA_LINE_COLOR_BY_SI = "MALineColorBy";

    #endregion Consts

    #region Parameters

    [InputParameter("Delta source", 9, variants: new object[]
    {
        BY_VOLUME_TYPE, CumulativeDeltaSourceType.Volume,
        BY_TRADES_TYPE, CumulativeDeltaSourceType.Trades
    })]
    public CumulativeDeltaSourceType DeltaSourceType;

    [InputParameter(RESET_TYPE_NAME_SI, 30, variants: new object[]
    {
        BY_PERIOD_SESSION_TYPE, CumulativeDeltaSessionMode.ByPeriod,
        FULL_HISTORY_SESSION_TYPE, CumulativeDeltaSessionMode.FullHistory,
        SPECIFIED_SESSION_TYPE, CumulativeDeltaSessionMode.SpecifiedSession,
        CUSTOM_RANGE_SESSION_TYPE, CumulativeDeltaSessionMode.CustomRange
    })]
    public CumulativeDeltaSessionMode SessionMode;

    [InputParameter("Period of Moving Average", 31, 1, 9999, 1, 1)]
    public int MAPeriod = 20;

    [InputParameter("Average Type", 32, variants: new object[]{
            "Simple Moving Average", MaMode.SMA,
            "Exponential Moving Average", MaMode.EMA,
            "Smoothed Moving Average", MaMode.SMMA,
            "Linearly Weighted Moving Average", MaMode.LWMA,
        })]
    public MaMode MaType = MaMode.SMA;

    private MALineColorOption maLineColorOption;

    private CloseLineColorOption closeLineСoloringOption;

    public ISessionsContainer SessionContainer
    {
        get
        {
            switch (this.SessionMode)
            {
                //
                case CumulativeDeltaSessionMode.SpecifiedSession:
                    {
                        if (this.specifiedSessionContainerId == CHART_SESSION_CONTAINER_SELECT_ITEM)
                            return this.CurrentChart?.CurrentSessionContainer;
                        else
                            return this.selectedSessionContainer;
                    }
                //
                case CumulativeDeltaSessionMode.CustomRange:
                    return this.customSessionContainer;

                //
                default:
                case CumulativeDeltaSessionMode.ByPeriod:
                case CumulativeDeltaSessionMode.FullHistory:
                    return this.fullDaySessionContainer;
            }
        }
    }
    private string specifiedSessionContainerId;
    private ISessionsContainer customSessionContainer;
    private ISessionsContainer fullDaySessionContainer;
    private ISessionsContainer selectedSessionContainer;

    public DateTime CustomRangeStartTime
    {
        get
        {
            if (this.customRangeStartTime == default)
            {
                var session = this.GetFullDayTimeInterval(this.GetTimeZone());
                this.customRangeStartTime = session.From;
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
            {
                var session = this.GetFullDayTimeInterval(this.GetTimeZone());
                this.customRangeEndTime = session.To;
            }

            return this.customRangeEndTime;
        }
        set => this.customRangeEndTime = value;
    }
    private DateTime customRangeEndTime;

    public Period ResetPeriod { get; private set; }

    public override string ShortName
    {
        get
        {
            return this.DeltaSourceType switch
            {
                CumulativeDeltaSourceType.Volume => $"{this.Name} ({BY_VOLUME_TYPE})",
                CumulativeDeltaSourceType.Trades => $"{this.Name} ({BY_TRADES_TYPE})",

                _ => this.Name,
            };
        }
    }

    private AreaBuilder currentAreaBuider;

    private Color upLineColor;
    private Color downLineColor;

    private Color maUpLineColor;
    private Color maDownLineColor;

    private Indicator ma;
    #endregion Parameters

    public IndicatorCumulativeDelta()
        : base()
    {
        this.AddLineSeries("MA", Color.Red, 2, LineStyle.Solid);

        this.upLineColor = Color.FromArgb(0, 178, 89);
        this.downLineColor = Color.FromArgb(251, 87, 87);

        this.maUpLineColor = Color.Green;
        this.maDownLineColor = Color.Red;
        this.maLineColorOption = MALineColorOption.PriceCross;
        this.closeLineСoloringOption = CloseLineColorOption.Delta;

        this.Name = "Cumulative delta";

        this.AddLineLevel(0d, "Zero line", Color.Gray, 1, LineStyle.DashDot);

        this.DeltaSourceType = CumulativeDeltaSourceType.Volume;
        this.SessionMode = CumulativeDeltaSessionMode.ByPeriod;
        this.ResetPeriod = Period.DAY1;

        this.SeparateWindow = true;
    }

    #region Overrides

    protected override void OnInit()
    {
        switch (this.SessionMode)
        {
            case CumulativeDeltaSessionMode.CustomRange:
                {
                    this.customSessionContainer = new CustomSessionsContainer("CustomSession", this.GetTimeZone(), new CustomSession[]
                    {
                        this.CreateCustomSession(this.CustomRangeStartTime.TimeOfDay,this.CustomRangeEndTime.TimeOfDay, this.GetTimeZone().TimeZoneInfo)
                    });
                    break;
                }
            case CumulativeDeltaSessionMode.FullHistory:
            case CumulativeDeltaSessionMode.ByPeriod:
                {
                    var timeZone = this.GetTimeZone();
                    var session = this.GetFullDayTimeInterval(timeZone);
                    this.fullDaySessionContainer = new CustomSessionsContainer("FullDaySession", timeZone, new CustomSession[]
                    {
                        this.CreateCustomSession(session.From.TimeOfDay, session.To.TimeOfDay, this.GetTimeZone().TimeZoneInfo)
                    });
                    break;
                }
        }
        base.OnInit();

        this.ma = Core.Indicators.BuiltIn.MA(this.MAPeriod, PriceType.Close, this.MaType);
        this.CandleHistoricalData.AddIndicator(this.ma);
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        if (this.IsLoading)
            return;

        var currentVolumeData = this.GetVolumeAnalysisData(0);

        //
        // Try to recalculate prev items
        //
        if (currentVolumeData?.Total == null)
        {
            var currentIndex = Math.Max(0, this.Count - 1);

            if (this.currentAreaBuider.BarIndex != currentIndex)
            {
                for (int i = this.currentAreaBuider.BarIndex; i < this.Count; i++)
                {
                    var offset = Math.Max(0, this.Count - i - 1);

                    var prevVolumeDataItem = this.GetVolumeAnalysisData(offset);
                    if (prevVolumeDataItem == null)
                        continue;

                    this.CalculateIndicatorByOffset(offset, true, true);
                }
            }
        }
        //
        // Try to calculate current item
        //
        else
        {
            var isNewBar = args.Reason == UpdateReason.NewBar || args.Reason == UpdateReason.HistoricalBar;
            this.CalculateIndicatorByOffset(0, isNewBar, false);
        }

    }
    protected override void OnClear()
    {
        if (this.currentAreaBuider != null)
        {
            this.currentAreaBuider.Dispose();
            this.currentAreaBuider = null;
        }

        base.OnClear();
    }
    public override IList<SettingItem> Settings
    {
        get
        {
            var settings = base.Settings;

            var separ = settings.FirstOrDefault()?.SeparatorGroup;

            var lineRelationVisibility = new SettingItemRelationVisibility("VisualStyle", new SelectItem("", (int)CandleDrawIndicatorVisualMode.Lines));
            var closeLineColorOptions = new List<SelectItem>()
            {
                new SelectItem(loc._("By Delta"), CloseLineColorOption.Delta),
                new SelectItem(loc._("By Sign"), CloseLineColorOption.Sign),
            };
            settings.Add(new SettingItemSelectorLocalized(CLOSE_LINE_COLOR_BY_SI, closeLineColorOptions.GetItemByValue(this.closeLineСoloringOption), closeLineColorOptions, 20)
            {
                Text = loc._("Coloring mode"),
                SeparatorGroup = separ,
                Relation = lineRelationVisibility
            });
            settings.Add(new SettingItemPairColor(LINE_COLORS_SI, new PairColor(this.upLineColor, this.downLineColor, loc._("Up"), loc._("Down")), 20)
            {
                Text = loc._("Lines"),
                SeparatorGroup = separ,
                Relation = lineRelationVisibility
            });

            //
            //
            //
            var defaultItem = new SelectItem(CHART_SESSION_CONTAINER_SELECT_ITEM);
            var items = new List<SelectItem> { defaultItem };
            items.AddRange(Core.Instance.CustomSessions.Select(s => new SelectItem(s.Name, s.Id)));

            var selectedItem = items.FirstOrDefault(i => i.Value.Equals(this.specifiedSessionContainerId)) ?? items.First();

            settings.Add(new SettingItemSelectorLocalized(SESSION_TEMPLATE_NAME_SI, selectedItem, items, 20)
            {
                Text = loc._("Sessions template"),
                SeparatorGroup = separ,
                Relation = new SettingItemRelationVisibility(RESET_TYPE_NAME_SI, new SelectItem("", (int)CumulativeDeltaSessionMode.SpecifiedSession))
            });

            //
            //
            //
            settings.Add(new SettingItemDateTime(CUSTOM_OPEN_SESSION_NAME_SI, this.CustomRangeStartTime, 30)
            {
                ValueChangingBehavior = SettingItemValueChangingBehavior.WithConfirmation,
                Format = DatePickerFormat.Time,
                SeparatorGroup = separ,
                Relation = new SettingItemRelationVisibility(RESET_TYPE_NAME_SI, new SelectItem("", (int)CumulativeDeltaSessionMode.CustomRange))
            });
            settings.Add(new SettingItemDateTime(CUSTOM_CLOSE_SESSION_NAME_SI, this.CustomRangeEndTime, 30)
            {
                ValueChangingBehavior = SettingItemValueChangingBehavior.WithConfirmation,
                Format = DatePickerFormat.Time,
                SeparatorGroup = separ,
                Relation = new SettingItemRelationVisibility(RESET_TYPE_NAME_SI, new SelectItem("", (int)CumulativeDeltaSessionMode.CustomRange))
            });

            //
            //
            //
            settings.Add(new SettingItemPeriod(RESET_PERIOD_NAME_SI, this.ResetPeriod, 30)
            {
                Text = loc._("Period"),
                ExcludedPeriods = new BasePeriod[] { BasePeriod.Tick, BasePeriod.Second, BasePeriod.Minute, BasePeriod.Hour, BasePeriod.Year },
                SeparatorGroup = separ,
                Relation = new SettingItemRelationVisibility(RESET_TYPE_NAME_SI, new SelectItem("", (int)CumulativeDeltaSessionMode.ByPeriod))
            });

            var lineColorOptions = new List<SelectItem>()
            {
                new SelectItem(loc._("Price Cross"), MALineColorOption.PriceCross),
                new SelectItem(loc._("Value Change (Up/Down)"), MALineColorOption.ValueChange),
                new SelectItem(loc._("Solid Color"), MALineColorOption.SolidColor)
            };
            settings.Add(new SettingItemSelectorLocalized(MA_LINE_COLOR_BY_SI, lineColorOptions.GetItemByValue(this.maLineColorOption), lineColorOptions, 40)
            {
                Text = loc._("Color by"),
                SeparatorGroup = separ
            });

            settings.Add(new SettingItemPairColor(MA_LINE_COLORS_SI, new PairColor(this.maUpLineColor, this.maDownLineColor, loc._("Up"), loc._("Down")), 40)
            {
                Text = loc._("Lines"),
                SeparatorGroup = separ,
                Relation = new SettingItemRelationVisibility(MA_LINE_COLOR_BY_SI, new object[] { lineColorOptions[0], lineColorOptions[1] })
            });

            return settings;
        }
        set
        {
            var holder = new SettingsHolder(value);
            base.Settings = value;

            var needRefresh = false;

            if (holder.TryGetValue(SESSION_TEMPLATE_NAME_SI, out var item))
            {
                var newContainerId = item.GetValue<string>();

                if (newContainerId != this.specifiedSessionContainerId)
                {
                    this.specifiedSessionContainerId = newContainerId;
                    this.selectedSessionContainer = Core.Instance.CustomSessions[this.specifiedSessionContainerId];
                    needRefresh |= item.ValueChangingReason == SettingItemValueChangingReason.Manually;
                }
            }

            if (holder.TryGetValue(RESET_PERIOD_NAME_SI, out item))
            {
                var newValue = item.GetValue<Period>();

                if (this.ResetPeriod != newValue)
                {
                    this.ResetPeriod = newValue;
                    needRefresh |= item.ValueChangingReason == SettingItemValueChangingReason.Manually;
                }
            }

            if (holder.TryGetValue(CUSTOM_OPEN_SESSION_NAME_SI, out item))
            {
                var newValue = item.GetValue<DateTime>();

                if (this.CustomRangeStartTime != newValue)
                {
                    this.CustomRangeStartTime = newValue;
                    needRefresh |= item.ValueChangingReason == SettingItemValueChangingReason.Manually;
                }
            }

            if (holder.TryGetValue(CUSTOM_CLOSE_SESSION_NAME_SI, out item))
            {
                var newValue = item.GetValue<DateTime>();

                if (this.CustomRangeEndTime != newValue)
                {
                    this.CustomRangeEndTime = newValue;
                    needRefresh |= item.ValueChangingReason == SettingItemValueChangingReason.Manually;
                }
            }

            if (holder.TryGetValue(LINE_COLORS_SI, out item))
            {
                var newValue = item.GetValue<PairColor>();

                if (this.upLineColor != newValue.Color1 || this.downLineColor != newValue.Color2)
                {
                    this.upLineColor = newValue.Color1;
                    this.downLineColor = newValue.Color2;
                    needRefresh |= item.ValueChangingReason == SettingItemValueChangingReason.Manually;
                }
            }

            if (holder.TryGetValue(MA_LINE_COLOR_BY_SI, out item))
            {
                var newValue =  (MALineColorOption)((SelectItem)item.Value).Value;

                if (this.maLineColorOption != newValue)
                {
                    this.maLineColorOption = newValue;
                    needRefresh |= item.ValueChangingReason == SettingItemValueChangingReason.Manually;
                }
            }

            if (holder.TryGetValue(MA_LINE_COLORS_SI, out item))
            {
                var newValue = item.GetValue<PairColor>();

                if (this.upLineColor != newValue.Color1 || this.downLineColor != newValue.Color2)
                {
                    this.maUpLineColor = newValue.Color1;
                    this.maDownLineColor = newValue.Color2;
                    needRefresh |= item.ValueChangingReason == SettingItemValueChangingReason.Manually;
                }
            }

            if (holder.TryGetValue(CLOSE_LINE_COLOR_BY_SI, out item))
            {
                var newValue = (CloseLineColorOption)((SelectItem)item.Value).Value;

                if (this.closeLineСoloringOption != newValue)
                {
                    this.closeLineСoloringOption = newValue;
                    needRefresh |= item.ValueChangingReason == SettingItemValueChangingReason.Manually;
                }
            }

            //
            if (needRefresh)
                this.Refresh();
        }
    }

    #endregion Overrides

    #region Misc

    private void CalculateIndicatorByOffset(int offset, bool isNewBar, bool createAfterUpdate = false)
    {
        if (this.Count <= offset)
            return;

        var time = this.Time(offset);

        var index = Math.Max(this.Count - offset - 1, 0);

        //
        // Check session
        //
        if (this.SessionMode == CumulativeDeltaSessionMode.SpecifiedSession || this.SessionMode == CumulativeDeltaSessionMode.CustomRange)
        {
            if (!this.SessionContainer.ContainsDate(time))
            {
                this.currentAreaBuider?.Reset(index);
                return;
            }
        }

        //
        //
        //
        if (this.currentAreaBuider == null)
        {
            var period = this.GetStepPeriod();
            var range = this.SessionMode != CumulativeDeltaSessionMode.FullHistory
                ? HistoryStepsCalculator.GetSteps(time, time.AddTicks(period.Ticks), period.BasePeriod, period.PeriodMultiplier, this.SessionContainer, this.GetTimeZone()).FirstOrDefault()
                : new Interval<DateTime>(time, DateTime.MaxValue);

            if (range.IsEmpty)
                return;

            this.currentAreaBuider = this.CreateAreaBuilder(range);
        }
        else if (!this.currentAreaBuider.Contains(time))
        {
            var period = this.GetStepPeriod();
            var range = HistoryStepsCalculator.GetNextStep(this.currentAreaBuider.Range.To, period.BasePeriod, period.PeriodMultiplier, this.SessionContainer?.TimeZone);

            if (range.IsEmpty)
                return;

            this.currentAreaBuider = this.CreateAreaBuilder(range);
        }

        //
        var currentItem = this.GetVolumeAnalysisData(offset);
        if (currentItem == null || currentItem.Total == null)
            return;

        //


        if (isNewBar && !createAfterUpdate)
            this.currentAreaBuider.StartNew(index);

        this.currentAreaBuider.Update(currentItem.Total);

        this.SetValues(this.currentAreaBuider.Bar.Open, this.currentAreaBuider.Bar.High, this.currentAreaBuider.Bar.Low, this.currentAreaBuider.Bar.Close, offset);

        bool isUpColor = this.closeLineСoloringOption switch
        {
            CloseLineColorOption.Sign => this.LinesSeries[1].GetValue(offset) > 0,
            CloseLineColorOption.Delta => (this.DeltaSourceType == CumulativeDeltaSourceType.Volume && currentItem.Total.Delta > 0) ||
                                            (this.DeltaSourceType == CumulativeDeltaSourceType.Trades && (currentItem.Total.BuyTrades - currentItem.Total.SellTrades) > 0),

            _ => true,
        };
        this.LinesSeries[1].SetMarker(offset, isUpColor ? this.upLineColor : this.downLineColor);

        if (this.Count > offset && this.Count > this.MAPeriod)
        {
            switch (this.maLineColorOption)
            {
                case MALineColorOption.PriceCross:
                    this.LinesSeries[2].SetMarker(offset, this.LinesSeries[1].GetValue(offset) > this.LinesSeries[2].GetValue(offset) ? this.maUpLineColor : this.maDownLineColor);
                    break;
                case MALineColorOption.ValueChange:
                    this.LinesSeries[2].SetMarker(offset, this.LinesSeries[2].GetValue(offset) > this.LinesSeries[2].GetValue(offset + 1) ? this.maUpLineColor : this.maDownLineColor);
                    break;
            }
        }

        if (isNewBar && createAfterUpdate)
            this.currentAreaBuider.StartNew(++index);
    }

    protected override void SetValues(double open, double high, double low, double close, int offset)
    {
        if (!IsValidPrice(open) || !IsValidPrice(close))
            return;

        base.SetValues(open, high, low, close, offset);

        if (this.Count > offset && this.Count > this.MAPeriod)
            this.SetValue(this.ma.GetValue(offset), 2, offset);
    }

    private Interval<DateTime> GetFullDayTimeInterval(TradingPlatform.BusinessLayer.TimeZone timeZone)
    {
        var openTime = new DateTime(DateTime.UtcNow.Date.Ticks, DateTimeKind.Unspecified);
        var closeTime = new DateTime(DateTime.UtcNow.Date.AddDays(-1).Ticks, DateTimeKind.Unspecified);

        return new Interval<DateTime>(Core.Instance.TimeUtils.ConvertFromTimeZoneToUTC(openTime, timeZone), Core.Instance.TimeUtils.ConvertFromTimeZoneToUTC(closeTime, timeZone));
    }
    private TradingPlatform.BusinessLayer.TimeZone GetTimeZone()
    {
        return this.CurrentChart?.CurrentTimeZone ?? Core.Instance.TimeUtils.SelectedTimeZone;
    }
    private Period GetStepPeriod()
    {
        if (this.SessionMode == CumulativeDeltaSessionMode.ByPeriod)
            return this.ResetPeriod;
        else
            return Period.DAY1;
    }
    private CustomSession CreateCustomSession(TimeSpan open, TimeSpan close, TimeZoneInfo info)
    {
        var session = new CustomSession
        {
            OpenOffset = open,
            CloseOffset = close,
            IsActive = true,
            Name = "Main",
            Days = Enum.GetValues(typeof(DayOfWeek)).Cast<DayOfWeek>().ToArray(),
            Type = SessionType.Main
        };
        session.RecalculateOpenCloseTime(info);
        return session;
    }
    private AreaBuilder CreateAreaBuilder(Interval<DateTime> range)
    {
        return this.DeltaSourceType switch
        {
            CumulativeDeltaSourceType.Trades => new AreaBuilderByTrades(range),

            _ => new AreaBuilderByVolume(range),
        };
    }

    #endregion Misc

    #region IVolumeAnalysisIndicator

    bool IVolumeAnalysisIndicator.IsRequirePriceLevelsCalculation => false;
    public void VolumeAnalysisData_Loaded()
    {
        this.Refresh();

        this.IsLoading = false;
    }

    #endregion IVolumeAnalysisIndicator

    #region Nested

    public enum CumulativeDeltaSessionMode
    {
        ByPeriod,
        FullHistory,
        SpecifiedSession,
        CustomRange
    }
    public enum CumulativeDeltaSourceType
    {
        Volume,
        Trades
    }

    public enum MALineColorOption
    {
        PriceCross,
        ValueChange,
        SolidColor
    }

    public enum CloseLineColorOption
    {
        Delta,
        Sign
    }

    abstract class AreaBuilder : IDisposable
    {
        internal Interval<DateTime> Range { get; }
        internal BarBuilder Bar { get; private set; }
        internal int BarIndex { get; private set; }

        public AreaBuilder(Interval<DateTime> range)
        {
            this.Range = range;

            this.Bar = new BarBuilder();
            this.StartNew(0);
        }

        internal abstract void Update(VolumeAnalysisItem total);

        internal void StartNew(int barIndex)
        {
            var prevClose = !double.IsNaN(this.Bar.Close)
                ? this.Bar.Close
                : 0d;

            this.Bar.Clear();
            this.Bar.Open = prevClose;
            this.BarIndex = barIndex;
        }
        internal bool Contains(DateTime dt)
        {
            if (dt.CompareTo(this.Range.Min) < 0)
                return false;

            if (dt.CompareTo(this.Range.Max) >= 0)
                return false;

            return true;
        }
        internal void Reset(int barIndex)
        {
            this.Bar.Clear();
            this.Bar.Open = 0;
            this.BarIndex = barIndex;
        }

        public void Dispose()
        {
            this.Bar = null;
        }
    }
    sealed class AreaBuilderByVolume : AreaBuilder
    {
        public AreaBuilderByVolume(Interval<DateTime> range)
            : base(range)
        { }

        internal override void Update(VolumeAnalysisItem total)
        {
            this.Bar.Close = this.Bar.Open + total.Delta;

            this.Bar.High = !double.IsNaN(total.MaxDelta) && total.MaxDelta != double.MinValue
                ? this.Bar.Open + Math.Abs(total.MaxDelta)
                : Math.Max(this.Bar.Close, this.Bar.Open);

            this.Bar.Low = !double.IsNaN(total.MinDelta) && total.MinDelta != double.MaxValue
                ? this.Bar.Open - Math.Abs(total.MinDelta)
                : Math.Min(this.Bar.Close, this.Bar.Open);
        }
    }
    sealed class AreaBuilderByTrades : AreaBuilder
    {
        public AreaBuilderByTrades(Interval<DateTime> range)
            : base(range)
        { }

        internal override void Update(VolumeAnalysisItem total)
        {
            this.Bar.Close = this.Bar.Open + (total.BuyTrades - total.SellTrades);
            this.Bar.High = Math.Max(this.Bar.Close, this.Bar.Open);
            this.Bar.Low = Math.Min(this.Bar.Close, this.Bar.Open);
        }
    }

    internal class BarBuilder
    {
        public double Open { get; internal set; }
        public double High { get; internal set; }
        public double Low { get; internal set; }
        public double Close { get; internal set; }

        public BarBuilder()
        {
            this.Clear();
        }

        internal void Clear()
        {
            this.Open = double.NaN;
            this.High = double.NaN;
            this.Low = double.NaN;
            this.Close = double.NaN;
        }
    }

    #endregion Nested
}