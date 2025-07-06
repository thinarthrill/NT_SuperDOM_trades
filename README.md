# 📊 ivTrade — NinjaTrader SuperDOM Volume Flow Visualizer

This custom SuperDOM column for NinjaTrader 8 visually tracks aggressive bid/ask trades in real time using tick-level volume and price data.

---

## 🔍 Features

* Highlights aggressive trades hitting bid/ask with adjustable delay filter
* Displays volume bars based on dynamic thresholds (customizable with `koeff`)
* Differentiates `Ask`, `Bid`, and `Delta` trade types
* Aggregates and renders recent trade volume per price level
* Dynamically updates visual column with color-coded rectangles
* Uses concurrent dictionaries for real-time performance

---

## ⚙️ Customizable Parameters (via Properties Panel)

* `TradeType`: `Delta`, `Ask`, or `Bid`
* `Delay`: time (in seconds) to ignore repeat trades at same price
* `Filter`: minimum volume to register a trade
* `koeff`: multiplier for average volume threshold
* `DisplayText`: show/hide numerical volume in column
* `AskColor`, `BidColor`, `BarColor`, `ForeColor`, etc.

---

## 📦 Installation

1. Open NinjaTrader 8
2. Go to **Tools > NinjaScript Editor**
3. Add a new SuperDOM Column: `ivTrade.cs`
4. Paste code from this file into the editor
5. Compile and add the column to your SuperDOM from the UI

---

## 🖥 Use Case

This tool is useful for:

* Volume-based scalping strategies
* Identifying liquidity clusters or spoofing
* Visualizing buyer/seller aggression near key price levels

---

## 📌 Notes

* Relies on `SuperDom.MarketDepth` and `BarsRequest` for tick data
* Requires NinjaTrader 8 and appropriate SuperDOM license
* Designed for performance — avoids unnecessary rendering and locks

---

## 👤 Author

Developed by **Igor Volnukhin** — combining tick data microstructure with intuitive visual trading tools.
