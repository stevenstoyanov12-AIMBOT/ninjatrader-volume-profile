// VolumeProfileVP.cs
// NinjaTrader 8 NinjaScript port of "Volume Profile" by kv4coins (TradingView, Pine v6, open-source).
// Original Pine idea credited to IldarAkhmetgaleev ("Poor man's volume profile"), extended by kv4coins.
// TradingView source: https://www.tradingview.com/script/r3VrWAO4-Volume-Profile/
// Original license: Mozilla Public License 2.0 (https://mozilla.org/MPL/2.0/)
// This port reimplements the same binning / POC / Value Area logic in NinjaScript for personal use.
//
// Compiled and run in NinjaTrader 8.
// Steps to install:
//   1. In NinjaTrader 8: New > NinjaScript Editor > Indicators > right-click > New Indicator,
//      name it VolumeProfileVP, then replace the generated code with everything below.
//   2. Compile (F5).
//   3. Add "VolumeProfileVP" to a chart like any other indicator.
//
// Known limitation: the profile is drawn to the right of the current bar using time-based
// offsets, so it's built for time-based bar types (minute/second/day charts). On tick/volume/
// range bars, the horizontal spacing math below won't line up correctly since bar timestamps
// aren't evenly spaced - flag this if you run non-time-based charts and it can be adapted.

#region Using declarations
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

// Declared at global scope (outside the Indicators namespace) on purpose: NinjaTrader
// auto-generates wrapper methods for this indicator in other namespaces (Strategies,
// MarketAnalyzerColumns), and those can only see this enum unqualified if it lives in
// the global namespace rather than nested inside NinjaTrader.NinjaScript.Indicators.
public enum VPVolumeType
{
    Both,
    Bullish,
    Bearish
}

namespace NinjaTrader.NinjaScript.Indicators
{
    public class VolumeProfileVP : Indicator
    {
        private double[] volumes;
        private double highestPrice;
        private double lowestPrice;
        private double priceInterval;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "Volume Profile with POC and Value Area, ported from kv4coins' TradingView 'Volume Profile' (Pine v6, MPL-2.0).";
                Name = "VolumeProfileVP";
                Calculate = Calculate.OnEachTick;
                IsOverlay = true;
                DisplayInDataBox = false;
                PaintPriceMarkers = false;
                IsSuspendedWhileInactive = true;

                LookbackBars = 200;
                NumberOfRows = 100;
                RightOffsetBars = 15;
                MaxProfileWidthBars = 50;
                VolumeTypeFilter = VPVolumeType.Both;
                ShowPOC = true;
                ShowValueArea = true;
                ValueAreaPercent = 68;
                ProfileColor = Brushes.DimGray;
                ValueAreaColor = Brushes.RoyalBlue;
                PocColor = Brushes.Red;
            }
            else if (State == State.DataLoaded)
            {
                volumes = new double[NumberOfRows];
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < LookbackBars)
                return;

            // Mirrors Pine's "barstate.islast" - only recompute/redraw on the most
            // recent bar (during history) or continuously once live, instead of
            // recalculating the whole lookback window on every historical bar.
            bool isLastBar = CurrentBar == Bars.Count - 1;
            if (State == State.Historical && !isLastBar)
                return;

            CalculateProfile();
            DrawProfile();
        }

        private void CalculateProfile()
        {
            highestPrice = MAX(High, LookbackBars)[0];
            lowestPrice = MIN(Low, LookbackBars)[0];
            priceInterval = (highestPrice - lowestPrice) / Math.Max(1, NumberOfRows - 1);

            Array.Clear(volumes, 0, volumes.Length);

            if (priceInterval <= 0)
                return;

            for (int i = 0; i < LookbackBars; i++)
            {
                double barHigh = High[i];
                double barLow = Low[i];
                double barOpen = Open[i];
                double barClose = Close[i];
                double barVol = Volume[i];

                bool isBullish = barClose >= barOpen;
                bool include = VolumeTypeFilter == VPVolumeType.Both
                    || (VolumeTypeFilter == VPVolumeType.Bullish && isBullish)
                    || (VolumeTypeFilter == VPVolumeType.Bearish && !isBullish);

                if (!include)
                    continue;

                for (int j = 0; j < NumberOfRows; j++)
                {
                    double priceLevel = lowestPrice + priceInterval * j;
                    if (priceLevel >= barLow && priceLevel < barHigh)
                        volumes[j] += barVol;
                }
            }
        }

        private void DrawProfile()
        {
            double maxVol = 0;
            int maxIdx = 0;
            for (int j = 0; j < NumberOfRows; j++)
            {
                if (volumes[j] > maxVol)
                {
                    maxVol = volumes[j];
                    maxIdx = j;
                }
            }

            if (maxVol <= 0)
                return;

            int vaLower = maxIdx;
            int vaUpper = maxIdx;

            if (ShowValueArea)
            {
                double totalVol = 0;
                for (int j = 0; j < NumberOfRows; j++)
                    totalVol += volumes[j];

                double targetVaVol = totalVol * ValueAreaPercent / 100.0;
                double vaSum = maxVol;

                while (vaSum < targetVaVol)
                {
                    double volUp = vaUpper < NumberOfRows - 1 ? volumes[vaUpper + 1] : 0;
                    double volDn = vaLower > 0 ? volumes[vaLower - 1] : 0;

                    if (volUp == 0 && volDn == 0)
                        break;

                    if (volUp >= volDn)
                    {
                        vaSum += volUp;
                        vaUpper++;
                    }
                    else
                    {
                        vaSum += volDn;
                        vaLower--;
                    }
                }
            }

            DateTime rightTime = GetTimeOffset(Time[0], RightOffsetBars);

            for (int j = 0; j < NumberOfRows; j++)
            {
                double price = lowestPrice + priceInterval * j;
                double lengthBars = volumes[j] / maxVol * MaxProfileWidthBars;
                DateTime leftTime = GetTimeOffset(rightTime, -(int)Math.Round(lengthBars));

                Brush color;
                if (j == maxIdx && ShowPOC)
                    color = PocColor;
                else if (ShowValueArea && j >= vaLower && j <= vaUpper)
                    color = ValueAreaColor;
                else
                    color = ProfileColor;

                Draw.Line(this, "VPRow" + j, false, leftTime, price, rightTime, price, color, DashStyleHelper.Solid, 2);
            }
        }

        // Approximates a future/past time offset using the chart's bar period so profile
        // rows can extend to the right of the last loaded bar. Time-based bar types only.
        private DateTime GetTimeOffset(DateTime from, int barsOffset)
        {
            TimeSpan barSpan;

            switch (BarsPeriod.BarsPeriodType)
            {
                case BarsPeriodType.Minute:
                    barSpan = TimeSpan.FromMinutes(BarsPeriod.Value);
                    break;
                case BarsPeriodType.Second:
                    barSpan = TimeSpan.FromSeconds(BarsPeriod.Value);
                    break;
                case BarsPeriodType.Day:
                    barSpan = TimeSpan.FromDays(BarsPeriod.Value);
                    break;
                default:
                    barSpan = TimeSpan.FromMinutes(1);
                    break;
            }

            return from.Add(TimeSpan.FromTicks(barSpan.Ticks * barsOffset));
        }

        #region Properties
        [NinjaScriptProperty]
        [Range(10, 3000)]
        [Display(Name = "Lookback Bars", Order = 1, GroupName = "Parameters")]
        public int LookbackBars { get; set; }

        [NinjaScriptProperty]
        [Range(10, 490)]
        [Display(Name = "Number Of Rows", Order = 2, GroupName = "Parameters")]
        public int NumberOfRows { get; set; }

        [NinjaScriptProperty]
        [Range(0, 400)]
        [Display(Name = "Right Offset (bars)", Order = 3, GroupName = "Parameters")]
        public int RightOffsetBars { get; set; }

        [NinjaScriptProperty]
        [Range(1, 200)]
        [Display(Name = "Max Profile Width (bars)", Order = 4, GroupName = "Parameters")]
        public int MaxProfileWidthBars { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Volume Type", Order = 5, GroupName = "Parameters")]
        public VPVolumeType VolumeTypeFilter { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Show POC", Order = 6, GroupName = "Point of Control")]
        public bool ShowPOC { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Show Value Area", Order = 7, GroupName = "Value Area")]
        public bool ShowValueArea { get; set; }

        [NinjaScriptProperty]
        [Range(5, 95)]
        [Display(Name = "Value Area %", Order = 8, GroupName = "Value Area")]
        public int ValueAreaPercent { get; set; }

        [XmlIgnore]
        [Display(Name = "Profile Color", Order = 9, GroupName = "Colors")]
        public Brush ProfileColor { get; set; }

        [Browsable(false)]
        public string ProfileColorSerializable
        {
            get { return Serialize.BrushToString(ProfileColor); }
            set { ProfileColor = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "Value Area Color", Order = 10, GroupName = "Colors")]
        public Brush ValueAreaColor { get; set; }

        [Browsable(false)]
        public string ValueAreaColorSerializable
        {
            get { return Serialize.BrushToString(ValueAreaColor); }
            set { ValueAreaColor = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "POC Color", Order = 11, GroupName = "Colors")]
        public Brush PocColor { get; set; }

        [Browsable(false)]
        public string PocColorSerializable
        {
            get { return Serialize.BrushToString(PocColor); }
            set { PocColor = Serialize.StringToBrush(value); }
        }
        #endregion
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private VolumeProfileVP[] cacheVolumeProfileVP;
		public VolumeProfileVP VolumeProfileVP(int lookbackBars, int numberOfRows, int rightOffsetBars, int maxProfileWidthBars, VPVolumeType volumeTypeFilter, bool showPOC, bool showValueArea, int valueAreaPercent)
		{
			return VolumeProfileVP(Input, lookbackBars, numberOfRows, rightOffsetBars, maxProfileWidthBars, volumeTypeFilter, showPOC, showValueArea, valueAreaPercent);
		}

		public VolumeProfileVP VolumeProfileVP(ISeries<double> input, int lookbackBars, int numberOfRows, int rightOffsetBars, int maxProfileWidthBars, VPVolumeType volumeTypeFilter, bool showPOC, bool showValueArea, int valueAreaPercent)
		{
			if (cacheVolumeProfileVP != null)
				for (int idx = 0; idx < cacheVolumeProfileVP.Length; idx++)
					if (cacheVolumeProfileVP[idx] != null && cacheVolumeProfileVP[idx].LookbackBars == lookbackBars && cacheVolumeProfileVP[idx].NumberOfRows == numberOfRows && cacheVolumeProfileVP[idx].RightOffsetBars == rightOffsetBars && cacheVolumeProfileVP[idx].MaxProfileWidthBars == maxProfileWidthBars && cacheVolumeProfileVP[idx].VolumeTypeFilter == volumeTypeFilter && cacheVolumeProfileVP[idx].ShowPOC == showPOC && cacheVolumeProfileVP[idx].ShowValueArea == showValueArea && cacheVolumeProfileVP[idx].ValueAreaPercent == valueAreaPercent && cacheVolumeProfileVP[idx].EqualsInput(input))
						return cacheVolumeProfileVP[idx];
			return CacheIndicator<VolumeProfileVP>(new VolumeProfileVP(){ LookbackBars = lookbackBars, NumberOfRows = numberOfRows, RightOffsetBars = rightOffsetBars, MaxProfileWidthBars = maxProfileWidthBars, VolumeTypeFilter = volumeTypeFilter, ShowPOC = showPOC, ShowValueArea = showValueArea, ValueAreaPercent = valueAreaPercent }, input, ref cacheVolumeProfileVP);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.VolumeProfileVP VolumeProfileVP(int lookbackBars, int numberOfRows, int rightOffsetBars, int maxProfileWidthBars, VPVolumeType volumeTypeFilter, bool showPOC, bool showValueArea, int valueAreaPercent)
		{
			return indicator.VolumeProfileVP(Input, lookbackBars, numberOfRows, rightOffsetBars, maxProfileWidthBars, volumeTypeFilter, showPOC, showValueArea, valueAreaPercent);
		}

		public Indicators.VolumeProfileVP VolumeProfileVP(ISeries<double> input , int lookbackBars, int numberOfRows, int rightOffsetBars, int maxProfileWidthBars, VPVolumeType volumeTypeFilter, bool showPOC, bool showValueArea, int valueAreaPercent)
		{
			return indicator.VolumeProfileVP(input, lookbackBars, numberOfRows, rightOffsetBars, maxProfileWidthBars, volumeTypeFilter, showPOC, showValueArea, valueAreaPercent);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.VolumeProfileVP VolumeProfileVP(int lookbackBars, int numberOfRows, int rightOffsetBars, int maxProfileWidthBars, VPVolumeType volumeTypeFilter, bool showPOC, bool showValueArea, int valueAreaPercent)
		{
			return indicator.VolumeProfileVP(Input, lookbackBars, numberOfRows, rightOffsetBars, maxProfileWidthBars, volumeTypeFilter, showPOC, showValueArea, valueAreaPercent);
		}

		public Indicators.VolumeProfileVP VolumeProfileVP(ISeries<double> input , int lookbackBars, int numberOfRows, int rightOffsetBars, int maxProfileWidthBars, VPVolumeType volumeTypeFilter, bool showPOC, bool showValueArea, int valueAreaPercent)
		{
			return indicator.VolumeProfileVP(input, lookbackBars, numberOfRows, rightOffsetBars, maxProfileWidthBars, volumeTypeFilter, showPOC, showValueArea, valueAreaPercent);
		}
	}
}

#endregion
