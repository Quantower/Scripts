// Copyright QUANTOWER LLC. © 2017-2024. All rights reserved.

using BarsDataIndicators.Utils;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using TradingPlatform.BusinessLayer;
using TradingPlatform.BusinessLayer.Utils;
using TradingPlatform.BusinessLayer.Utils.IntervalGeneration;

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
    private const string RESET_TIME_NAME_SI = "ResetTime";

    private const string RESET_TYPE_NAME_SI = "Reset type";
    private const string BY_PERIOD_SESSION_TYPE = "By period";
    private const string FULL_HISTORY_SESSION_TYPE = "Full range";
    private const string SPECIFIED_SESSION_TYPE = "Specified session";
    private const string CUSTOM_RANGE_SESSION_TYPE = "Custom range";

    private const string LINE_COLORS_SI = "ColorLines";
    private const string CLOSE_LINE_COLOR_BY_SI = "CloseLineColorBy";
    private const string MA_LINE_COLORS_SI = "MAColorLines";
    private const string MA_LINE_COLOR_BY_SI = "MALineColorBy";

    private const string SHOW_WICKS_SI = "ShowWicks";
    #endregion Consts

    #region Parameters

    [InputParameter("Delta source", 1, variants: new object[]
    {
        BY_VOLUME_TYPE, CumulativeDeltaSourceType.Volume,
        BY_TRADES_TYPE, CumulativeDeltaSourceType.Trades
    })]
    public CumulativeDeltaSourceType DeltaSourceType;

    [InputParameter(RESET_TYPE_NAME_SI, 4, variants: new object[]
    {
        BY_PERIOD_SESSION_TYPE, CumulativeDeltaSessionMode.ByPeriod,
        FULL_HISTORY_SESSION_TYPE, CumulativeDeltaSessionMode.FullHistory,
        SPECIFIED_SESSION_TYPE, CumulativeDeltaSessionMode.SpecifiedSession,
        CUSTOM_RANGE_SESSION_TYPE, CumulativeDeltaSessionMode.CustomRange
    })]
    public CumulativeDeltaSessionMode SessionMode;

    [InputParameter("Average Type", 21, variants: new object[]{
            "Simple Moving Average", MaMode.SMA,
            "Exponential Moving Average", MaMode.EMA,
            "Smoothed Moving Average", MaMode.SMMA,
            "Linearly Weighted Moving Average", MaMode.LWMA,
        })]
    public MaMode MaType = MaMode.SMA;

    [InputParameter("Period of Moving Average", 22, 1, 9999, 1, 1)]
    public int MAPeriod = 20;

    private MALineColorOption maLineColorOption;

    private CloseLineColorOption closeLineСoloringOption;

    public ISessionsContainer SessionContainer
    {
        get
        {
            switch (this.SessionMode)
            {
                case CumulativeDeltaSessionMode.SpecifiedSession:
                    {
                        if (this.specifiedSessionContainerId == CHART_SESSION_CONTAINER_SELECT_ITEM)
                            return this.CurrentChart?.CurrentSessionContainer;
                        else
                            return this.selectedSessionContainer;
                    }

                case CumulativeDeltaSessionMode.CustomRange:
                    return this.customSessionContainer;

                case CumulativeDeltaSessionMode.ByPeriod:
                    return this.byPeriodSessionContainer ?? this.fullDaySessionContainer;

                case CumulativeDeltaSessionMode.FullHistory:
                default:
                    return this.fullDaySessionContainer;
            }
        }
    }
    private string specifiedSessionContainerId;
    private ISessionsContainer customSessionContainer;
    private ISessionsContainer fullDaySessionContainer;
    private ISessionsContainer selectedSessionContainer;
    private ISessionsContainer byPeriodSessionContainer;

    public DateTime CustomRangeStartTime
    {
        get
        {
            if (this.customRangeStartTime == default)
            {
                var session = this.GetFullDayTimeInterval(this.GetTimeZone());
                this.customRangeStartTime = session.From;
            }
            return DateTime.SpecifyKind(this.customRangeStartTime, DateTimeKind.Local);
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

            return DateTime.SpecifyKind(this.customRangeEndTime, DateTimeKind.Local);
        }
        set => this.customRangeEndTime = value;
    }
    private DateTime customRangeEndTime;

    public DateTime ByPeriodResetTime
    {
        get
        {
            if (this.byPeriodResetTime == default)
                this.byPeriodResetTime = DateTime.Today; // 00:00
            return DateTime.SpecifyKind(this.byPeriodResetTime, DateTimeKind.Local);
        }
        set => this.byPeriodResetTime = value;
    }
    private DateTime byPeriodResetTime;
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

    private bool showWicks = true;

    private AreaBuilder currentAreaBuider;

    private IntervalGenerator intervalGenerator;

    // Count observed on the previous realtime update. It lets us determine
    // how many Renko/Range bars were actually created by one market update.
    private int lastKnownCount;

    // Absolute chronological index of the EARLIEST CLOSED bar whose
    // VolumeAnalysisData.Total was unavailable when it was calculated.
    // -1 means there is no pending bar.
    private int earliestPendingBarIndex = -1;

    private Color upLineColor;
    private Color downLineColor;

    private Color maUpLineColor;
    private Color maDownLineColor;

    private Indicator ma;
    #endregion Parameters

    public IndicatorCumulativeDelta()
        : base()
    {
        this.LinesSeries[1].Style = LineStyle.Solid;
        this.AddLineSeries("MA", Color.Red, 2, LineStyle.Solid);
        this.LinesSeries[2].Visible = false;

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
        var timeZone = this.GetTimeZone();

        this.fullDaySessionContainer = new CustomSessionsContainer("FullDaySession", timeZone, new CustomSession[]
        {
        this.CreateCustomSession(TimeSpan.Zero, new TimeSpan(23, 59, 59), timeZone.TimeZoneInfo)
        });

        switch (this.SessionMode)
        {
            case CumulativeDeltaSessionMode.CustomRange:
                {
                    this.customSessionContainer = new CustomSessionsContainer("CustomSession", timeZone, new CustomSession[]
                    {
                this.CreateCustomSession(
                    this.CustomRangeStartTime.TimeOfDay,
                    this.CustomRangeEndTime.TimeOfDay,
                    timeZone.TimeZoneInfo)
                    });
                    break;
                }

            case CumulativeDeltaSessionMode.ByPeriod:
                {
                    var resetTime = this.ByPeriodResetTime.TimeOfDay;

                    this.byPeriodSessionContainer = new CustomSessionsContainer("ByPeriodSession", timeZone, new CustomSession[]
                    {
                this.CreateCustomSession(
                    resetTime,
                    this.GetByPeriodSessionCloseTime(resetTime),
                    timeZone.TimeZoneInfo)
                    });
                    break;
                }

            case CumulativeDeltaSessionMode.FullHistory:
            case CumulativeDeltaSessionMode.SpecifiedSession:
            default:
                break;
        }

        base.OnInit();

        this.lastKnownCount = 0;
        this.earliestPendingBarIndex = -1;
        this.intervalGenerator = null;

        this.ma = Core.Indicators.BuiltIn.MA(this.MAPeriod, PriceType.Close, this.MaType);
        this.CandleHistoricalData.AddIndicator(this.ma);
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        if (this.IsLoading || this.Count == 0)
            return;
        if (args.Reason == UpdateReason.HistoricalBar)
        {
            this.CalculateIndicatorByOffset(0, false);
            this.lastKnownCount = this.Count;
            return;
        }
        if (this.lastKnownCount <= 0 || this.lastKnownCount > this.Count)
        {
            this.lastKnownCount = this.Count;
            this.CalculateIndicatorByOffset(0, true);
            return;
        }

        int addedBars = this.Count - this.lastKnownCount;

        if (addedBars > 0 || args.Reason == UpdateReason.NewBar)
        {
            int maxOffset = addedBars > 0
                ? Math.Min(addedBars, this.Count - 1)
                : Math.Min(1, this.Count - 1);

            this.lastKnownCount = this.Count;
            if (this.TryResolveEarliestPendingBar())
                return;
            this.RecalculateBars(maxOffset, true);
            return;
        }
        this.CalculateIndicatorByOffset(0, true);
    }

    protected override void OnClear()
    {
        this.ResetRangeCalculationState();

        this.lastKnownCount = 0;
        this.earliestPendingBarIndex = -1;

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
            settings.Add(new SettingItemSelectorLocalized(CLOSE_LINE_COLOR_BY_SI, closeLineColorOptions.GetItemByValue(this.closeLineСoloringOption), closeLineColorOptions, 11)
            {
                Text = loc._("Coloring mode"),
                SeparatorGroup = separ,
                Relation = lineRelationVisibility
            });
            settings.Add(new SettingItemPairColor(LINE_COLORS_SI, new PairColor(this.upLineColor, this.downLineColor, loc._("Up"), loc._("Down")), 12)
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

            settings.Add(new SettingItemSelectorLocalized(SESSION_TEMPLATE_NAME_SI, selectedItem, items, 5)
            {
                Text = loc._("Sessions template"),
                SeparatorGroup = separ,
                Relation = new SettingItemRelationVisibility(RESET_TYPE_NAME_SI, new SelectItem("", (int)CumulativeDeltaSessionMode.SpecifiedSession))
            });

            //
            //
            //
            settings.Add(new SettingItemDateTime(CUSTOM_OPEN_SESSION_NAME_SI, this.CustomRangeStartTime, 6)
            {
                ValueChangingBehavior = SettingItemValueChangingBehavior.WithConfirmation,
                Format = DatePickerFormat.Time,
                SeparatorGroup = separ,
                Relation = new SettingItemRelationVisibility(RESET_TYPE_NAME_SI, new SelectItem("", (int)CumulativeDeltaSessionMode.CustomRange))
            });
            settings.Add(new SettingItemDateTime(CUSTOM_CLOSE_SESSION_NAME_SI, this.CustomRangeEndTime, 6)
            {
                ValueChangingBehavior = SettingItemValueChangingBehavior.WithConfirmation,
                Format = DatePickerFormat.Time,
                SeparatorGroup = separ,
                Relation = new SettingItemRelationVisibility(RESET_TYPE_NAME_SI, new SelectItem("", (int)CumulativeDeltaSessionMode.CustomRange))
            });
            settings.Add(new SettingItemDateTime(RESET_TIME_NAME_SI, this.ByPeriodResetTime, 8)
            {
                Text = loc._("Reset time"),
                ValueChangingBehavior = SettingItemValueChangingBehavior.WithConfirmation,
                Format = DatePickerFormat.Time,
                SeparatorGroup = separ,
                Relation = new SettingItemRelationVisibility(RESET_TYPE_NAME_SI, new SelectItem("", (int)CumulativeDeltaSessionMode.ByPeriod)),
            });
            //
            //
            //
            settings.Add(new SettingItemPeriod(RESET_PERIOD_NAME_SI, this.ResetPeriod, 7)
            {
                Text = loc._("Period"),
                ExcludedPeriods = new BasePeriod[] { BasePeriod.Tick, BasePeriod.Second, BasePeriod.Year },
                SeparatorGroup = separ,
                Relation = new SettingItemRelationVisibility(RESET_TYPE_NAME_SI, new SelectItem("", (int)CumulativeDeltaSessionMode.ByPeriod))
            });

            var lineColorOptions = new List<SelectItem>()
            {
                new SelectItem(loc._("Price Cross"), MALineColorOption.PriceCross),
                new SelectItem(loc._("Value Change (Up/Down)"), MALineColorOption.ValueChange),
                new SelectItem(loc._("Solid Color"), MALineColorOption.SolidColor)
            };
            settings.Add(new SettingItemSelectorLocalized(MA_LINE_COLOR_BY_SI, lineColorOptions.GetItemByValue(this.maLineColorOption), lineColorOptions, 23)
            {
                Text = loc._("Color by"),
                SeparatorGroup = separ
            });

            settings.Add(new SettingItemPairColor(MA_LINE_COLORS_SI, new PairColor(this.maUpLineColor, this.maDownLineColor, loc._("Up"), loc._("Down")), 24)
            {
                Text = loc._("Lines"),
                SeparatorGroup = separ,
                Relation = new SettingItemRelationVisibility(MA_LINE_COLOR_BY_SI, new object[] { lineColorOptions[0], lineColorOptions[1] })
            });
            var candlesRelationVisibility = new SettingItemRelationVisibility(
                "VisualStyle",
                new SelectItem("", (int)CandleDrawIndicatorVisualMode.Candles)
            );

            settings.Add(new SettingItemBoolean(SHOW_WICKS_SI, this.showWicks, 11)
            {
                Text = loc._("Show wick, if available"),
                SeparatorGroup = separ,
                Relation = candlesRelationVisibility
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
                var newValue = Core.Instance.TimeUtils.ConvertFromUTCToTimeZone(item.GetValue<DateTime>(), this.GetTimeZone());

                if (this.CustomRangeStartTime != newValue)
                {
                    this.CustomRangeStartTime = newValue;
                    needRefresh |= item.ValueChangingReason == SettingItemValueChangingReason.Manually;
                }
            }

            if (holder.TryGetValue(CUSTOM_CLOSE_SESSION_NAME_SI, out item))
            {
                var newValue = Core.Instance.TimeUtils.ConvertFromUTCToTimeZone(item.GetValue<DateTime>(), this.GetTimeZone());
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
                var newValue = (MALineColorOption)((SelectItem)item.Value).Value;

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

            if (holder.TryGetValue(SHOW_WICKS_SI, out item))
            {
                var newValue = item.GetValue<bool>();

                if (this.showWicks != newValue)
                {
                    this.showWicks = newValue;
                    needRefresh |= item.ValueChangingReason == SettingItemValueChangingReason.Manually;
                }
            }
            if (holder.TryGetValue(RESET_TIME_NAME_SI, out item))
            {
                var newValue = Core.Instance.TimeUtils.ConvertFromUTCToTimeZone(item.GetValue<DateTime>(), this.GetTimeZone());

                if (this.ByPeriodResetTime != newValue)
                {
                    this.ByPeriodResetTime = newValue;
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
    private void RecalculateBars(int maxOffset, bool trackPending)
    {
        if (this.Count == 0)
            return;

        maxOffset = Math.Min(maxOffset, this.Count - 1);
        this.ResetRangeCalculationState();

        for (int offset = maxOffset; offset >= 0; offset--)
            this.CalculateIndicatorByOffset(offset, trackPending);
    }
    private void CalculateIndicatorByOffset(int offset, bool trackPending)
    {
        if (offset < 0 || this.Count <= offset)
            return;
        DateTime time = this.Time(offset);
        var sessionContainer = this.SessionContainer;

        if (this.SessionMode == CumulativeDeltaSessionMode.SpecifiedSession ||
            this.SessionMode == CumulativeDeltaSessionMode.CustomRange)
        {
            if (sessionContainer == null || !sessionContainer.ContainsDate(time))
            {
                this.currentAreaBuider?.Reset();
                return;
            }
        }


        Interval<DateTime> range;
        if (this.currentAreaBuider != null &&
            this.IsAreaBuilderCompatible(this.currentAreaBuider) &&
            this.currentAreaBuider.Contains(time))
        {
            range = this.currentAreaBuider.Range;
        }
        else
        {
            range = this.GetAccumulationRange(time);

            if (range.IsEmpty)
                return;

            this.currentAreaBuider?.Dispose();
            this.currentAreaBuider = this.CreateAreaBuilder(range);
        }


        double previousClose = 0d;

        if (!this.IsStartOfAccumulationRange(offset, range) && offset + 1 < this.Count)
        {
            double value = this.LinesSeries[1].GetValue(offset + 1);

            if (!double.IsNaN(value) && !double.IsInfinity(value))
                previousClose = value;
        }

        var currentItem = this.GetVolumeAnalysisData(offset);
        var total = currentItem?.Total;
        if (trackPending && offset > 0 && total == null)
        {
            int barIndex = this.GetAbsoluteBarIndex(offset);

            if (this.earliestPendingBarIndex < 0 ||
                barIndex < this.earliestPendingBarIndex)
            {
                this.earliestPendingBarIndex = barIndex;
            }
        }


        this.currentAreaBuider.Calculate(previousClose, total);

        this.SetValues(
            this.currentAreaBuider.Bar.Open,
            this.currentAreaBuider.Bar.High,
            this.currentAreaBuider.Bar.Low,
            this.currentAreaBuider.Bar.Close,
            offset);

        if (total != null)
        {
            bool isUpColor = this.closeLineСoloringOption switch
            {
                CloseLineColorOption.Sign => this.currentAreaBuider.Bar.Close > 0,

                CloseLineColorOption.Delta =>
                    (this.DeltaSourceType == CumulativeDeltaSourceType.Volume && total.Delta > 0) ||
                    (this.DeltaSourceType == CumulativeDeltaSourceType.Trades &&
                     (total.BuyTrades - total.SellTrades) > 0),

                _ => true,
            };

            this.LinesSeries[1].SetMarker(
                offset,
                isUpColor ? this.upLineColor : this.downLineColor);
        }
        else
        {
            this.LinesSeries[1].RemoveMarker(offset);
        }

        if (this.Count > offset && this.Count > this.MAPeriod)
        {
            switch (this.maLineColorOption)
            {
                case MALineColorOption.PriceCross:
                    this.LinesSeries[2].SetMarker(
                        offset,
                        this.LinesSeries[1].GetValue(offset) > this.LinesSeries[2].GetValue(offset)
                            ? this.maUpLineColor
                            : this.maDownLineColor);
                    break;

                case MALineColorOption.ValueChange:
                    if (offset + 1 < this.Count)
                    {
                        this.LinesSeries[2].SetMarker(
                            offset,
                            this.LinesSeries[2].GetValue(offset) > this.LinesSeries[2].GetValue(offset + 1)
                                ? this.maUpLineColor
                                : this.maDownLineColor);
                    }
                    break;

                case MALineColorOption.SolidColor:
                    this.LinesSeries[2].RemoveMarker(offset);
                    break;
            }
        }
    }

    private int GetAbsoluteBarIndex(int offset)
    {
        return this.Count - 1 - offset;
    }

    private int GetOffsetByAbsoluteBarIndex(int barIndex)
    {
        return this.Count - 1 - barIndex;
    }

    private bool TryResolveEarliestPendingBar()
    {
        if (this.earliestPendingBarIndex < 0 || this.Count == 0)
            return false;

        int offset = this.GetOffsetByAbsoluteBarIndex(this.earliestPendingBarIndex);


        if (offset <= 0 || offset >= this.Count)
        {
            this.earliestPendingBarIndex = -1;
            return false;
        }

        var total = this.GetVolumeAnalysisData(offset)?.Total;

        if (total == null)
            return false;

        int recalcFromOffset = offset;

        this.earliestPendingBarIndex = -1;

        this.RecalculateBars(recalcFromOffset, true);

        return true;
    }

    private Interval<DateTime> GetAccumulationRange(DateTime time)
    {
        if (this.SessionMode == CumulativeDeltaSessionMode.FullHistory)
            return new Interval<DateTime>(time, DateTime.MaxValue);

        var sessionContainer = this.SessionContainer;

        if (sessionContainer == null)
            return default;

        if (this.intervalGenerator == null)
        {
            this.intervalGenerator = new IntervalGenerator(
                time,
                this.GetStepPeriod(),
                sessionContainer,
                this.GetTimeZone());
        }
        else if (!this.intervalGenerator.Current.Contains(time))
                    this.intervalGenerator.MoveUntil(time); 
        return this.intervalGenerator.Current;
    }
    private void ResetRangeCalculationState()
    {
        this.intervalGenerator = null;

        if (this.currentAreaBuider != null)
        {
            this.currentAreaBuider.Dispose();
            this.currentAreaBuider = null;
        }
    }

    private bool IsStartOfAccumulationRange(int offset, Interval<DateTime> currentRange)
    {
        if (offset + 1 >= this.Count)
            return true;

        if (this.SessionMode == CumulativeDeltaSessionMode.FullHistory)
            return false;

        DateTime previousTime = this.Time(offset + 1);

        return !currentRange.Contains(previousTime);
    }

    private bool IsAreaBuilderCompatible(AreaBuilder areaBuilder)
    {
        return this.DeltaSourceType switch
        {
            CumulativeDeltaSourceType.Trades => areaBuilder is AreaBuilderByTrades,
            _ => areaBuilder is AreaBuilderByVolume,
        };
    }

    protected override void SetValues(double open, double high, double low, double close, int offset)
    {
        if (!IsValidPrice(open) || !IsValidPrice(close))
            return;
        if (!this.showWicks)
        {
            high = Math.Max(open, close);
            low  = Math.Min(open, close);
        }
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
    private TimeSpan GetByPeriodSessionCloseTime(TimeSpan resetTime)
    {
        var close = resetTime - TimeSpan.FromSeconds(1);

        if (close < TimeSpan.Zero)
            close += TimeSpan.FromDays(1);

        return close;
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

        protected AreaBuilder(Interval<DateTime> range)
        {
            this.Range = range;
            this.Bar = new BarBuilder();
        }

        internal void Calculate(double previousClose, VolumeAnalysisItem total)
        {
            this.Bar.Clear();

            this.Bar.Open = previousClose;
            this.Bar.High = previousClose;
            this.Bar.Low = previousClose;
            this.Bar.Close = previousClose;

            if (total != null)
                this.Update(total);
        }
        internal abstract void Update(VolumeAnalysisItem total);
        internal bool Contains(DateTime dt) => this.Range.Contains(dt);
        internal void Reset()
        {
            this.Bar.Clear();
            this.Bar.Open = 0d;
            this.Bar.High = 0d;
            this.Bar.Low = 0d;
            this.Bar.Close = 0d;
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

            this.Bar.High =
                !double.IsNaN(total.MaxDelta) && total.MaxDelta != double.MinValue
                    ? this.Bar.Open + Math.Abs(total.MaxDelta)
                    : Math.Max(this.Bar.Close, this.Bar.Open);

            this.Bar.Low =
                !double.IsNaN(total.MinDelta) && total.MinDelta != double.MaxValue
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
            this.Bar.Close =
                this.Bar.Open +
                (total.BuyTrades - total.SellTrades);

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