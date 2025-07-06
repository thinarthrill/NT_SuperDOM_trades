// 
// Copyright (C) 2015, NinjaTrader LLC <www.ninjatrader.com>.
// NinjaTrader reserves the right to modify or overwrite this NinjaScript component with each release.
//
#region Using declarations
using NinjaTrader.Data;
using NinjaTrader.Gui.SuperDom;
using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
#endregion

namespace NinjaTrader.NinjaScript.SuperDomColumns
{
	public class ivTrade : SuperDomColumn
	{
		const int DepthCount = 9;
		
		private readonly	object			barsSync				= new object();
		private				bool			clearLoadingSent;
		private				FontFamily		fontFamily;
		private				FontStyle		fontStyle;
		private				FontWeight		fontWeight;
		private				Pen				gridPen;			
		private				double			halfPenWidth;
		private				bool			heightUpdateNeeded;
		private				int				lastMaxIndex			= -1;
		private				long			maxVolume;
		private				bool			mouseEventsSubscribed;
		private				double			textHeight;
		private				Point			textPosition			= new Point(4, 0);
		private				string			tradingHoursData		= TradingHours.UseInstrumentSettings;
		private				long			totalAskVolume;
		private				long			totalLastVolume;
		private				long			totalBidVolume;
		private				Typeface		typeFace;
		private 			double 			oldClose;
		private 			double			lastPrice;
		private				bool			isAsk=false;
		private				bool			isBid=false;

		double AVG = 0;
		double close=0;
		
		private void OnBarsUpdate(object sender, BarsUpdateEventArgs e)
		{
			if (State == State.Active && SuperDom != null && SuperDom.IsConnected)
			{
				if (SuperDom.IsReloading)
				{
					OnPropertyChanged();
					return;
				}

				BarsUpdateEventArgs barsUpdate = e;
				
				lock (barsSync)
				{
					int currentMaxIndex = barsUpdate.MaxIndex;
					
					AVG = 0;
					try
					{
						AVG += ((SuperDom.MarketDepth.Instrument.MarketDepth.Asks.Take(10).Sum(s=>s.Volume) + SuperDom.MarketDepth.Instrument.MarketDepth.Bids.Take(10).Sum(s=>s.Volume))/20);
					}
					catch 
					{
						AVG=1000;
					};
				
					for (int i = lastMaxIndex + 1; i <= currentMaxIndex; i++)
					{
						double	ask		= barsUpdate.BarsSeries.GetAsk(i);
						double	bid		= barsUpdate.BarsSeries.GetBid(i);
						close	= barsUpdate.BarsSeries.GetClose(i);
						DateTime tradeTime =barsUpdate.BarsSeries.GetTime(i);
						long	volume	= barsUpdate.BarsSeries.GetVolume(i);
						Print("long="+volume);
						
						if (barsUpdate.BarsSeries.GetIsFirstBarOfSession(i))
						{
							// If a new session starts, clear out the old values and start fresh
							maxVolume		= 0;
							totalAskVolume	= 0;
							totalLastVolume = 0;
							totalBidVolume = 0;
							Bids.Clear();
							Asks.Clear();
							LastBids.Clear();
							LastAsks.Clear();
							LastVolumes.Clear();
						}
						
						if (close!=oldClose)
						{
							DateTime lastAsk;
							DateTime lastBid;
							
							if (ask != double.MinValue && close >= ask)
							{
								LastAsks.TryGetValue(close, out lastAsk);
								if ((tradeTime.TimeOfDay.TotalSeconds-lastAsk.TimeOfDay.TotalSeconds)>Delay)
								{
									Asks.AddOrUpdate(close, volume, (price, oldVolume) => 0);
									Bids.AddOrUpdate(close, volume, (price, oldVolume) => 0);
								}
							}
							
							if (bid != double.MinValue && close <= bid)
							{
								LastBids.TryGetValue(close, out lastBid);
								if ((tradeTime.TimeOfDay.TotalSeconds-lastBid.TimeOfDay.TotalSeconds)>Delay)
								{
									Asks.AddOrUpdate(close, volume, (price, oldVolume) => 0);
									Bids.AddOrUpdate(close, volume, (price, oldVolume) => 0);
								}
							}
						}

						if (ask != double.MinValue && close >= ask)
						{
							LastAsks.AddOrUpdate(close, tradeTime, (price, lastTime) => tradeTime);
							if (volume >= Filter) {
								Asks.AddOrUpdate(close, volume, (price, oldVolume) => oldVolume + volume);
								totalAskVolume += volume;
								lastPrice = close;
								isAsk = true;
								isBid = false;
							}
						}
						if (bid != double.MinValue && close <= bid)
						{
							LastBids.AddOrUpdate(close, tradeTime, (price, lastTime) => tradeTime);
							if (volume >= Filter) {
								Bids.AddOrUpdate(close, volume, (price, oldVolume) => oldVolume + volume);
								totalBidVolume += volume;
								lastPrice=close;
								isAsk = false;
								isBid = true;
							}
						}
				
						long newVolume;
						LastVolumes.AddOrUpdate(close, newVolume = volume, (price, oldVolume) => newVolume = oldVolume + volume);
						totalLastVolume += volume;

						if (newVolume > maxVolume)
							maxVolume = newVolume;
						
						oldClose=close;
					}
					
					lastMaxIndex = barsUpdate.MaxIndex;
					if (!clearLoadingSent)
					{
						SuperDom.Dispatcher.InvokeAsync(() => SuperDom.ClearLoadingString());
						clearLoadingSent = true;
					}
				}
			}
		}

		private void OnMouseLeave(object sender, MouseEventArgs e)
		{
			OnPropertyChanged();
		}

		private void OnMouseEnter(object sender, MouseEventArgs e)
		{
			OnPropertyChanged();
		}

		private void OnMouseMove(object sender, MouseEventArgs e)
		{
			OnPropertyChanged();
		}

		protected override void OnRender(DrawingContext dc, double renderWidth)
		{
			// This may be true if the UI for a column hasn't been loaded yet (e.g., restoring multiple tabs from workspace won't load each tab until it's clicked by the user)
			if (gridPen == null)
			{
				if (UiWrapper != null && PresentationSource.FromVisual(UiWrapper) != null)
				{
					Matrix m			= PresentationSource.FromVisual(UiWrapper).CompositionTarget.TransformToDevice;
					double dpiFactor	= 1 / m.M11;
					gridPen				= new Pen(Application.Current.TryFindResource("BorderThinBrush") as Brush, 1 * dpiFactor);
					halfPenWidth		= gridPen.Thickness * 0.5;
				}
			}

			if (fontFamily != SuperDom.Font.Family
				|| (SuperDom.Font.Italic && fontStyle != FontStyles.Italic)
				|| (!SuperDom.Font.Italic && fontStyle == FontStyles.Italic)
				|| (SuperDom.Font.Bold && fontWeight != FontWeights.Bold)
				|| (!SuperDom.Font.Bold && fontWeight == FontWeights.Bold))
			{
				// Only update this if something has changed
				fontFamily	= SuperDom.Font.Family;
				fontStyle	= SuperDom.Font.Italic ? FontStyles.Italic : FontStyles.Normal;
				fontWeight	= SuperDom.Font.Bold ? FontWeights.Bold : FontWeights.Normal;
				typeFace	= new Typeface(fontFamily, fontStyle, fontWeight, FontStretches.Normal);
				heightUpdateNeeded = true;
			}

			double	verticalOffset	= -gridPen.Thickness;

			lock (SuperDom.Rows)
				foreach (PriceRow row in SuperDom.Rows)
				{
					if (renderWidth - halfPenWidth >= 0)
					{
						// Draw cell
						Rect rect = new Rect(-halfPenWidth, verticalOffset, renderWidth - halfPenWidth, SuperDom.ActualRowHeight);

						// Create a guidelines set
						GuidelineSet guidelines = new GuidelineSet();
						guidelines.GuidelinesX.Add(rect.Left	+ halfPenWidth);
						guidelines.GuidelinesX.Add(rect.Right	+ halfPenWidth);
						guidelines.GuidelinesY.Add(rect.Top		+ halfPenWidth);
						guidelines.GuidelinesY.Add(rect.Bottom	+ halfPenWidth);

						dc.PushGuidelineSet(guidelines);
						dc.DrawRectangle(BackColor, null, rect);
						dc.DrawLine(gridPen, new Point(-gridPen.Thickness, rect.Bottom), new Point(renderWidth - halfPenWidth, rect.Bottom));
						dc.DrawLine(gridPen, new Point(rect.Right, verticalOffset), new Point(rect.Right, rect.Bottom));
						
						if (row.Price==lastPrice && ((isAsk && TradeType == TradeType.Ask)||(isBid && TradeType == TradeType.Bid)))
							dc.DrawRectangle(null, new Pen(BarColor, 3), rect);
	
						if (SuperDom.IsConnected 
							&& !SuperDom.IsReloading
							&& State == NinjaTrader.NinjaScript.State.Active)
						{
							// Draw proportional volume bar
							long	askVolume		= 0;
							long	bidVolume		= 0;
							long	totalRowVolume	= 0;
							long	totalVolume		= 0;

							if (LastVolumes.TryGetValue(row.Price, out totalRowVolume))
								totalVolume = totalLastVolume;
							else
							{
								verticalOffset += SuperDom.ActualRowHeight;
								continue;
							}
								
							bool gotAsk		= Asks.TryGetValue(row.Price, out askVolume);
							bool gotBid	= Bids.TryGetValue(row.Price, out bidVolume);
							if (gotAsk || gotBid)
							{
								totalRowVolume	= bidVolume + askVolume;
								totalVolume		= totalAskVolume + totalBidVolume;
							}
							else
							{
								verticalOffset += SuperDom.ActualRowHeight;
								continue;
							}
							
							// Print volume value - remember to set MaxTextWidth so text doesn't spill into another column
							if (totalRowVolume > 0)
							{
								string volumeAskString = string.Empty;
								string volumeBidString = string.Empty;
								string tradeString = string.Empty;
								
								volumeAskString = askVolume.ToString(Core.Globals.GeneralOptions.CurrentCulture);
								volumeBidString = bidVolume.ToString(Core.Globals.GeneralOptions.CurrentCulture);

								if (renderWidth - 6 > 0)
								{
									if (DisplayText || rect.Contains(Mouse.GetPosition(UiWrapper)))
									{
										if (TradeType == TradeType.Delta)
											tradeString = (askVolume-bidVolume).ToString(Core.Globals.GeneralOptions.CurrentCulture);
										if (TradeType == TradeType.Ask)
											tradeString = volumeAskString;
										if (TradeType == TradeType.Bid)
											tradeString = volumeBidString;
										
										FormattedText tradeText = new FormattedText(tradeString, Core.Globals.GeneralOptions.CurrentCulture, FlowDirection.LeftToRight, typeFace, SuperDom.Font.Size, ForeColor) { MaxLineCount = 1, MaxTextWidth = renderWidth - 6, Trimming = TextTrimming.CharacterEllipsis };

										// Getting the text height is expensive, so only update it if something's changed
										if (heightUpdateNeeded)
										{
											textHeight = tradeText.Height;
											heightUpdateNeeded = false;
										}

										textPosition.Y = verticalOffset + (SuperDom.ActualRowHeight - textHeight) / 2;
										//Print("bid="+totalBidVolume+" ask="+totalAskVolume);
										if (bidVolume>AVG*koeff && TradeType!=TradeType.Ask)
										{
											dc.DrawRectangle(BidColor, null, rect);
										}
										
										if (askVolume>AVG*koeff && TradeType!=TradeType.Bid)
										{
											dc.DrawRectangle(AskColor, null, rect);
										}
										if (row.AskVolume>AVG*3 && TradeType!=TradeType.Ask && !row.IsCumulativeAskDepth)
											dc.DrawRectangle(BarColor, null, rect);
										if (row.BidVolume>AVG*3 && TradeType!=TradeType.Bid && !row.IsCumulativeBidDepth)
											dc.DrawRectangle(BarColor, null, rect);
										
										dc.DrawText(tradeText, textPosition);
									}
								}
							}
							verticalOffset += SuperDom.ActualRowHeight;
						}
						else
							verticalOffset += SuperDom.ActualRowHeight;

						dc.Pop();
					}
				}
		}

		public override void OnRestoreValues()
		{
			// Forecolor and standard bar color
			bool restored = false;

			SolidColorBrush defaultForeColor = Application.Current.FindResource("immutableBrushVolumeColumnForeground") as SolidColorBrush;
			if (	(ForeColor			as SolidColorBrush).Color == (ImmutableForeColor as SolidColorBrush).Color
				&&	(ImmutableForeColor as SolidColorBrush).Color != defaultForeColor.Color)
			{
				ForeColor			= defaultForeColor;
				ImmutableForeColor	= defaultForeColor;
				restored			= true;
			}

			SolidColorBrush defaultBarColor = Application.Current.FindResource("immutableBrushVolumeColumnBackground") as SolidColorBrush;
			if ((BarColor as SolidColorBrush).Color == (ImmutableBarColor as SolidColorBrush).Color
				&& (ImmutableBarColor as SolidColorBrush).Color != defaultBarColor.Color)
			{
				BarColor			= defaultBarColor;
				ImmutableBarColor	= defaultBarColor;
				restored			= true;
			}

			if (restored) OnPropertyChanged();
		}

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Name					= "ivTrade";//NinjaTrader.Custom.Resource.NinjaScriptSuperDomColumnVolume;
				Asks					= new ConcurrentDictionary<double, long>();
				LastAsks				= new ConcurrentDictionary<double,DateTime>();
				BackColor				= Brushes.Transparent;
				BarColor				= Application.Current.TryFindResource("brushVolumeColumnBackground") as Brush;
				AskColor				= Brushes.Green;
				DefaultWidth			= 160;
				Delay 					= 5;
				Filter					= 1;
				PreviousWidth			= -1;
				DisplayText				= false;
				ForeColor				= Application.Current.TryFindResource("brushVolumeColumnForeground") as Brush;
				ImmutableBarColor		= Application.Current.TryFindResource("immutableBrushVolumeColumnBackground") as Brush;
				ImmutableForeColor		= Application.Current.TryFindResource("immutableBrushVolumeColumnForeground") as Brush;
				IsDataSeriesRequired	= true;
				LastVolumes				= new ConcurrentDictionary<double,long>();
				BidColor				= Brushes.Red;
				Bids					= new ConcurrentDictionary<double,long>();
				LastBids				= new ConcurrentDictionary<double,DateTime>();
				TradeType				= TradeType.Delta;
				isAsk					= false;
				isBid					= false;
				koeff					= 3;
			}
			else if (State == State.Configure)
			{
				if (UiWrapper != null && PresentationSource.FromVisual(UiWrapper) != null)
				{ 
					Matrix m			= PresentationSource.FromVisual(UiWrapper).CompositionTarget.TransformToDevice;
					double dpiFactor	= 1 / m.M11;
					gridPen				= new Pen(Application.Current.TryFindResource("BorderThinBrush") as Brush,  1 * dpiFactor);
					halfPenWidth		= gridPen.Thickness * 0.5;
				}

				if (SuperDom.Instrument != null && SuperDom.IsConnected)
				{
					BarsPeriod bp		= new BarsPeriod
					{
						MarketDataType = MarketDataType.Last, 
						BarsPeriodType = BarsPeriodType.Tick, 
						Value = 1
					};

					SuperDom.Dispatcher.InvokeAsync(() => SuperDom.SetLoadingString());
					clearLoadingSent = false;

					if (BarsRequest != null)
					{
						BarsRequest.Update -= OnBarsUpdate;
						BarsRequest = null;
					}

					BarsRequest = new BarsRequest(SuperDom.Instrument,
						Cbi.Connection.PlaybackConnection != null ? Cbi.Connection.PlaybackConnection.Now : Core.Globals.Now,
						Cbi.Connection.PlaybackConnection != null ? Cbi.Connection.PlaybackConnection.Now : Core.Globals.Now);

					BarsRequest.BarsPeriod		= bp;
					BarsRequest.TradingHours	= (TradingHoursData == TradingHours.UseInstrumentSettings || TradingHours.Get(TradingHoursData) == null) ? SuperDom.Instrument.MasterInstrument.TradingHours : TradingHours.Get(TradingHoursData);
					BarsRequest.Update			+= OnBarsUpdate;

					BarsRequest.Request((request, errorCode, errorMessage) =>
						{
							// Make sure this isn't a bars callback from another column instance
							if (request != BarsRequest)
								return;

							lastMaxIndex	= 0;
							maxVolume		= 0;
							totalAskVolume	= 0;
							totalLastVolume = 0;
							totalBidVolume = 0;
							Bids.Clear();
							Asks.Clear();
							LastBids.Clear();
							LastAsks.Clear();
							LastVolumes.Clear();

							if (State >= NinjaTrader.NinjaScript.State.Terminated)
								return;

							if (errorCode == Cbi.ErrorCode.UserAbort)
							{
								if (State <= NinjaTrader.NinjaScript.State.Terminated)
									if (SuperDom != null && !clearLoadingSent)
									{
										SuperDom.Dispatcher.InvokeAsync(() => SuperDom.ClearLoadingString());
										clearLoadingSent = true;
									}
										
								request.Update -= OnBarsUpdate;
								request.Dispose();
								request = null;
								return;
							}
							
							if (errorCode != Cbi.ErrorCode.NoError)
							{
								request.Update -= OnBarsUpdate;
								request.Dispose();
								request = null;
								if (SuperDom != null && !clearLoadingSent)
								{
									SuperDom.Dispatcher.InvokeAsync(() => SuperDom.ClearLoadingString());
									clearLoadingSent = true;
								}
							}
							else if (errorCode == Cbi.ErrorCode.NoError)
							{
								SessionIterator	superDomSessionIt	= new SessionIterator(request.Bars);
								bool			isInclude60			= request.Bars.BarsType.IncludesEndTimeStamp(false);
								if (superDomSessionIt.IsInSession(Core.Globals.Now, isInclude60, request.Bars.BarsType.IsIntraday))
								{
									for (int i = 0; i < request.Bars.Count; i++)
									{
										DateTime time = request.Bars.BarsSeries.GetTime(i);
										if ((isInclude60 && time <= superDomSessionIt.ActualSessionBegin) || (!isInclude60 && time < superDomSessionIt.ActualSessionBegin))
											continue;
										
										double	ask		= request.Bars.BarsSeries.GetAsk(i);
										double	bid		= request.Bars.BarsSeries.GetBid(i);
										double	close	= request.Bars.BarsSeries.GetClose(i);
										long	volume=0;//	= request.Bars.BarsSeries.GetVolume(i);

										if (ask != double.MinValue && close >= ask)
										{
											Asks.AddOrUpdate(close, volume, (price, oldVolume) => oldVolume + volume);
											totalAskVolume += volume;
										}
										else if (bid != double.MinValue && close <= bid)
										{
											Bids.AddOrUpdate(close, volume, (price, oldVolume) => oldVolume + volume);
											totalBidVolume += volume;
										}

										long newVolume;
										LastVolumes.AddOrUpdate(close, newVolume = volume, (price, oldVolume) => newVolume = oldVolume + volume);
										totalLastVolume += volume;

										if (newVolume > maxVolume)
											maxVolume = newVolume;
									
									}

									lastMaxIndex = request.Bars.Count - 1;

									// Repaint the column on the SuperDOM
									OnPropertyChanged();
								}

								if (SuperDom != null && !clearLoadingSent)
								{
									SuperDom.Dispatcher.InvokeAsync(() => SuperDom.ClearLoadingString());
									clearLoadingSent = true;
								}
							}
						});
				}
			}
			else if (State == State.Active)
			{
				if (!DisplayText)
				{
					WeakEventManager<System.Windows.Controls.Panel, MouseEventArgs>.AddHandler(UiWrapper, "MouseMove", OnMouseMove);
					WeakEventManager<System.Windows.Controls.Panel, MouseEventArgs>.AddHandler(UiWrapper, "MouseEnter", OnMouseEnter);
					WeakEventManager<System.Windows.Controls.Panel, MouseEventArgs>.AddHandler(UiWrapper, "MouseLeave", OnMouseLeave);
					mouseEventsSubscribed = true;
				}
			}
			else if (State == State.Terminated)
			{
				if (BarsRequest != null)
				{
					BarsRequest.Update -= OnBarsUpdate;
					BarsRequest.Dispose();
				}

				BarsRequest = null;

				if (SuperDom != null && !clearLoadingSent)
				{
					SuperDom.Dispatcher.InvokeAsync(() => SuperDom.ClearLoadingString());
					clearLoadingSent = true;
				}

				if (!DisplayText && mouseEventsSubscribed)
				{
					WeakEventManager<System.Windows.Controls.Panel, MouseEventArgs>.RemoveHandler(UiWrapper, "MouseMove", OnMouseMove);
					WeakEventManager<System.Windows.Controls.Panel, MouseEventArgs>.RemoveHandler(UiWrapper, "MouseEnter", OnMouseEnter);
					WeakEventManager<System.Windows.Controls.Panel, MouseEventArgs>.RemoveHandler(UiWrapper, "MouseLeave", OnMouseLeave);
					mouseEventsSubscribed = false;
				}

				lastMaxIndex	= 0;
				maxVolume		= 0;
				totalAskVolume	= 0;
				totalLastVolume = 0;
				totalBidVolume = 0;
				Bids.Clear();
				Asks.Clear();
				LastBids.Clear();
				LastAsks.Clear();
				LastVolumes.Clear();
			}
		}

		#region Bar Collections
		[XmlIgnore]
		[Browsable(false)]
		public ConcurrentDictionary<double, long> Asks { get; set; }

		[XmlIgnore]
		[Browsable(false)]
		public ConcurrentDictionary<double, long> LastVolumes { get; set; }

		[XmlIgnore]
		[Browsable(false)]
		public ConcurrentDictionary<double, long> Bids { get; set; }
		
		[XmlIgnore]
		[Browsable(false)]
		public ConcurrentDictionary<double, DateTime> LastAsks { get; set; }
		
		[XmlIgnore]
		[Browsable(false)]
		public ConcurrentDictionary<double, DateTime> LastBids { get; set; }		
		#endregion

		#region Properties
		[XmlIgnore]
		[Display(ResourceType = typeof(Resource), Name = "Background", GroupName = "Visual", Order = 130)]
		public Brush BackColor { get; set; }

		[Browsable(false)]
		public string BackColorSerialize
		{
			get { return NinjaTrader.Gui.Serialize.BrushToString(BackColor); }
			set { BackColor = NinjaTrader.Gui.Serialize.StringToBrush(value); }
		}

		[XmlIgnore]
		[Display(ResourceType = typeof(Resource), Name = "Frame", GroupName = "Visual", Order = 110)]
		public Brush BarColor { get; set; }

		[Browsable(false)]
		public string BarColorSerialize
		{
			get { return NinjaTrader.Gui.Serialize.BrushToString(BarColor); }
			set { BarColor = NinjaTrader.Gui.Serialize.StringToBrush(value); }
		}

		[XmlIgnore]
		[Display(ResourceType = typeof(Resource), Name = "Ask", GroupName = "Visual", Order = 120)]
		public Brush AskColor { get; set; }

		[Browsable(false)]
		public string AskColorSerialize
		{
			get { return NinjaTrader.Gui.Serialize.BrushToString(AskColor); }
			set { AskColor = NinjaTrader.Gui.Serialize.StringToBrush(value); }
		}

		[Display(ResourceType = typeof(Resource), Name = "Show text", GroupName = "Visual", Order = 175)]
		public bool DisplayText { get; set; }

		[XmlIgnore]
		[Display(ResourceType = typeof(Resource), Name = "Text", GroupName = "Visual", Order = 140)]
		public Brush ForeColor { get; set; }

		[Browsable(false)]
		public string ForeColorSerialize
		{
			get { return NinjaTrader.Gui.Serialize.BrushToString(ForeColor); }
			set { ForeColor = NinjaTrader.Gui.Serialize.StringToBrush(value); }
		}

		[XmlIgnore]
		[Browsable(false)]
		public Brush ImmutableBarColor { get; set; }

		[Browsable(false)]
		public string ImmutableBarColorSerialize
		{
			get { return NinjaTrader.Gui.Serialize.BrushToString(ImmutableBarColor, "CustomVolume.ImmutableBarColor"); }
			set { ImmutableBarColor = NinjaTrader.Gui.Serialize.StringToBrush(value, "CustomVolume.ImmutableBarColor"); }
		}

		[XmlIgnore]
		[Browsable(false)]
		public Brush ImmutableForeColor { get; set; }

		[Browsable(false)]
		public string ImmutableForeColorSerialize
		{
			get { return NinjaTrader.Gui.Serialize.BrushToString(ImmutableForeColor, "CustomVolume.ImmutableForeColor"); }
			set { ImmutableForeColor = NinjaTrader.Gui.Serialize.StringToBrush(value, "CustomVolume.ImmutableForeColor"); }
		}

		[XmlIgnore]
		[Display(ResourceType = typeof(Resource), Name = "Bid", GroupName = "PropertyCategoryVisual", Order = 125)]
		public Brush BidColor { get; set; }

		[Browsable(false)]
		public string BidColorSerialize
		{
			get { return NinjaTrader.Gui.Serialize.BrushToString(BidColor); }
			set { BidColor = NinjaTrader.Gui.Serialize.StringToBrush(value); }
		}

		[Display(ResourceType = typeof(Resource), Name = "IndicatorSuperDomBaseTradingHoursTemplate", GroupName = "NinjaScriptTimeFrame", Order = 60)]
		[RefreshProperties(RefreshProperties.All)]
		[TypeConverter(typeof(NinjaTrader.NinjaScript.TradingHoursDataConverter))]
		public string TradingHoursData
		{
			get { return tradingHoursData; }
			set { tradingHoursData = value; }
		}

		[Display(ResourceType = typeof(Resource), Name = "GuiType", GroupName = "NinjaScriptSetup", Order = 180)]		
		public TradeType TradeType { get; set; }
		
		[Display(ResourceType = typeof(Resource), Name = "Delay", GroupName = "NinjaScriptSetup", Order = 190)]		
		public int Delay { get; set; }
		
		[Display(ResourceType = typeof(Resource), Name = "Filter", GroupName = "NinjaScriptSetup", Order = 195)]		
		public int Filter { get; set; }
		
		[Display(ResourceType = typeof(Resource), Name = "koeff", GroupName = "NinjaScriptSetup", Order = 210)]		
		public int koeff { get; set; }
		
		#endregion
	}
	
	public enum TradeType
	{
		Delta,
		Ask,
		Bid
	}
}
