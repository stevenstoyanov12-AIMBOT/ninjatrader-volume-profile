# VolumeProfileVP

A volume profile indicator for NinjaTrader 8, written in NinjaScript (C#). It draws a horizontal histogram of traded volume by price to the right of the chart and marks the Point of Control and the Value Area.

This is a port of the open-source TradingView script ["Volume Profile" by kv4coins](https://www.tradingview.com/script/r3VrWAO4-Volume-Profile/) (Pine v6), which builds on IldarAkhmetgaleev's "Poor man's volume profile". The binning, POC and Value Area logic follow the original; the NinjaScript implementation is mine.

## What it shows

- **Profile:** volume traded at each price level over the lookback window.
- **Point of Control (POC):** the price level with the most volume.
- **Value Area:** the band around the POC that holds a chosen share of total volume (68% by default).

## How it works

1. Take the highest high and lowest low of the last `LookbackBars` bars and split that range into `NumberOfRows` price levels.
2. For each bar, add its volume to every level that falls inside the bar's low-to-high range. Bars can be filtered to bullish only or bearish only.
3. The level with the most volume is the POC.
4. Starting from the POC, grow the Value Area one level at a time, always taking the neighbour with more volume, until it holds `ValueAreaPercent` of the total.
5. Draw one line per level to the right of the last bar. Line length is the level's volume relative to the POC.

The profile is recalculated only on the last bar during history and on each tick when live, so it does not rebuild the whole window for every historical bar.

## Parameters

| Parameter | Default | Range | Meaning |
|---|---|---|---|
| Lookback Bars | 200 | 10–3000 | Bars included in the profile |
| Number Of Rows | 100 | 10–490 | Price levels in the profile |
| Right Offset (bars) | 15 | 0–400 | Gap between the last bar and the profile's right edge |
| Max Profile Width (bars) | 50 | 1–200 | Length of the POC row |
| Volume Type | Both | Both, Bullish, Bearish | Which bars count |
| Show POC | on | | Highlight the POC row |
| Show Value Area | on | | Highlight the Value Area rows |
| Value Area % | 68 | 5–95 | Share of volume inside the Value Area |
| Profile / Value Area / POC Color | DimGray / RoyalBlue / Red | | Row colours |

## Install

1. In NinjaTrader 8, open **New > NinjaScript Editor**.
2. Right-click **Indicators > New Indicator** and name it `VolumeProfileVP`.
3. Replace the generated code with the contents of `VolumeProfileVP.cs`.
4. Compile with F5.
5. Add **VolumeProfileVP** to a chart from the Indicators window.

## Limitations

- Built for time-based charts (minute, second, day). The profile is positioned with time offsets, so it will not line up on tick, volume or range bars.
- A bar's full volume is added to every level it touches, as in the original script. It is an approximation from bar data, not a true tick-by-tick profile.

## Credits and license

Ported from "Volume Profile" by kv4coins, originally based on work by IldarAkhmetgaleev. The original is published under the [Mozilla Public License 2.0](https://mozilla.org/MPL/2.0/), and this port is shared under the same license.
