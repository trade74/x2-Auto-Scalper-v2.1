using System;
using System.Linq;
using System.Collections;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using cAlgo.Indicators;
using System.Collections.Generic;


namespace cAlgo
{
    public class Extentions : Robot
    {





    }

}

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class x2AutoScalperV21 : Robot
    {
        #region Params
        //Bollinger Bands
        [Parameter("Source", Group = "Bollinger Bands")]
        public DataSeries BollingBandsSource { get; set; }

        [Parameter("Period", Group = "Bollinger Bands", DefaultValue = 50)]
        public int BollingBandsPeriod { get; set; }

        [Parameter("Std Deviation", Group = "Bollinger Bands", DefaultValue = 2.0)]
        public double BollingBandsSD { get; set; }

        [Parameter("MA Type", Group = "Bollinger Bands", DefaultValue = MovingAverageType.Simple)]
        public MovingAverageType BollingBandsMAType { get; set; }

        //MA Cloud Settings
        [Parameter("Source", Group = "EMA")]
        public DataSeries EMASource { get; set; }

        [Parameter("Fast MA Period", Group = "EMA", DefaultValue = 8)]
        public int EMAFastPeriod { get; set; }

        [Parameter("Fast MA Type", Group = "EMA", DefaultValue = MovingAverageType.Simple)]
        public MovingAverageType FastMAType { get; set; }

        [Parameter("Slow MA Period", Group = "EMA", DefaultValue = 21)]
        public int EMASlowPeriod { get; set; }

        [Parameter("Slow MA Type", Group = "EMA", DefaultValue = MovingAverageType.Simple)]
        public MovingAverageType SlowMAType { get; set; }

        [Parameter("Bot Label ( Magic Name )", Group = "Identity", DefaultValue = "BotLabel")]
        public string BotLabel { get; set; }

        //Strategy Settings
        [Parameter("Slippage (pips)", Group = "Strategy", DefaultValue = 2.0, MinValue = 0.5, Step = 0.1)]
        public double SLIPPAGE { get; set; }

        [Parameter("Stop Loss (pips)", Group = "Strategy", DefaultValue = 50, MinValue = 1, Step = 1)]
        public double StopLoss { get; set; }

        [Parameter("Take Profit (pips)", Group = "Strategy", DefaultValue = 10, MinValue = 1, Step = 1)]
        public double TakeProfit { get; set; }

        [Parameter("Step (Pips)", Group = "Strategy", DefaultValue = 10.0)]
        public double StepInPips { get; set; }

        [Parameter("Starting Size", Group = "Strategy", DefaultValue = 1000)]
        public int StartingSize { get; set; }

        [Parameter("Bar to switch To FastEMA Exit", Group = "Strategy", DefaultValue = 4)]
        public int SwitchFastEMA { get; set; }

        [Parameter("Lot Size Multplier", Group = "Strategy", DefaultValue = 1.2, MaxValue = 3)]
        public double LotSizeMultiplier { get; set; }

        [Parameter("Seconds Between Trades", Group = "Strategy", DefaultValue = 300)]
        public int Seconds { get; set; }

        //Hedging Settings
        [Parameter("Hedging On", Group = "Hedging", DefaultValue = false)]
        public Boolean HedgingOn { get; set; }

        [Parameter("$ Loss Trigger ", Group = "Hedging", DefaultValue = 10)]
        public double HedgingTriggerDollarLoss { get; set; }

        [Parameter("Exit $ Win", Group = "Hedging", DefaultValue = 100)]
        public double HedgingExitTakeProfit { get; set; }

        [Parameter("Exit $ Loss", Group = "Hedging", DefaultValue = 5)]
        public double HedgingExitDollarLoss { get; set; }



        //Email Settings
        [Parameter("Send Danger Emails", Group = "Email", DefaultValue = false)]
        public Boolean SendOrderEmails { get; set; }

        [Parameter("Email from", Group = "Email", DefaultValue = "fm@punchingdata.com")]
        public string EmailFrom { get; set; }

        [Parameter("Email to", Group = "Email", DefaultValue = "fm@punchingdata.com")]
        public string EmailTo { get; set; }

        //Trade Panel
        [Parameter("Vertical Position", Group = "Panel alignment", DefaultValue = VerticalAlignment.Top)]
        public VerticalAlignment PanelVerticalAlignment { get; set; }

        [Parameter("Horizontal Position", Group = "Panel alignment", DefaultValue = HorizontalAlignment.Left)]
        public HorizontalAlignment PanelHorizontalAlignment { get; set; }

        [Parameter("Default Lots", Group = "Default trade parameters", DefaultValue = 0.5)]
        public double DefaultLots { get; set; }

        [Parameter("Default Take Profit (pips)", Group = "Default trade parameters", DefaultValue = 200)]
        public double DefaultTakeProfitPips { get; set; }

        [Parameter("Default Stop Loss (pips)", Group = "Default trade parameters", DefaultValue = 200)]
        public double DefaultStopLossPips { get; set; }



        #endregion

        private ExponentialMovingAverage EmaFast;
        private ExponentialMovingAverage EmaSlow;
        private MovingAverage MaFast;
        private MovingAverage MaSlow;
        private BollingerBands BB;
        private AverageTrueRange ATR;

        public Position LastLongTradePosition;
        public Position LastShortTradePosition;
        public Position LastLongHedgePosition;
        public Position LastShortHedgePosition;
        public Position LastHedgedPosition;
        public List<Position> PositionsLongTrades;
        public List<Position> PositionsShortTrades;
        public List<Position> PositionsLongHedges;
        public List<Position> PositionsShortHedges;
        public List<Position> PositionsAll;
        public List<Position> PositionsAllTrade;
        public List<Position> PositionsAllHedge;

        public Boolean IsHedging { get; set; }
        public Boolean TradingHalt { get; set; }


        protected override void OnStart()
        {
            //Create instances of the Indicators used
            BB = Indicators.BollingerBands(BollingBandsSource, BollingBandsPeriod, BollingBandsSD, BollingBandsMAType);
            EmaFast = Indicators.ExponentialMovingAverage(EMASource, EMAFastPeriod);
            EmaSlow = Indicators.ExponentialMovingAverage(EMASource, EMASlowPeriod);
            MaFast = Indicators.MovingAverage(EMASource, EMAFastPeriod, FastMAType);
            MaSlow = Indicators.MovingAverage(EMASource, EMASlowPeriod, SlowMAType);


            //Setup the Position Lists<> 
            PositionsLongTrades = new List<Position>();
            PositionsShortTrades = new List<Position>();
            PositionsLongHedges = new List<Position>();
            PositionsShortHedges = new List<Position>();
            PositionsAll = new List<Position>();
            PositionsAllTrade = new List<Position>();
            PositionsAllHedge = new List<Position>();


            //Run each time a position is opened or closed
            Positions.Opened += OnOpenPositions;
            Positions.Closed += OnClosePositions;

            IsHedging = false;

            //Draw Trading Panel
            TradingPanel();
        }


        protected override void OnTick()
        {

            if (TradingHalt)
                ActionRemoveTradingHalt();

            if (IsHedging)
                ActionCloseHedging();

            if (HedgingOn)
            {
                if (TriggerOpenHedgeLong())
                    ActionOpenHedgeLong();

                if (TriggerOpenHedgeShort())
                    ActionOpenHedgeShort();
            }


            if (TriggerCloseLong())
                ActionCloseLongTrades();

            if (TriggerCloseShort())
                ActionCloseShortTrades();


            if (TriggerOpenNextLong() && !IsHedging)
                ActionOpenLongTrade();

            if (TriggerOpenNextShort() && !IsHedging)
                ActionOpenShortTrade();


            if (TriggerOpenFirstLong() && !IsHedging)
                ActionOpenLongTrade();

            if (TriggerOpenFirstShort() && !IsHedging)
                ActionOpenShortTrade();

        }

        protected override void OnBar()
        {
            // check if the price is going faster than 12 pips in 30 seconds
            //PriceMovesFasterThan(10, 30);
            // Print("pps ", PipsPerSecond(Seconds));


        }

        protected override void OnStop()
        {
            Positions.Opened -= OnOpenPositions;
        }

        #region Triggers & Actions

        public Boolean TriggerOpenFirstLong()
        {
            if (PriceBelowLowerBollingerBands(BB) && !hasOpenPositions(PositionsLongTrades))
                return true;

            return false;
        }

        public Boolean TriggerOpenFirstShort()
        {
            if (PriceAboveUpperBollingerBands(BB) && !hasOpenPositions(PositionsShortTrades))
                return true;

            return false;
        }

        public Boolean TriggerOpenNextShort()
        {
            if (PositionsLongHedges.Count > 0)
                return false;

            if (LastShortTradePosition != null)
            {
                var ticks = MarketData.GetTicks();
                TimeSpan countSeconds;
                DateTime entry = LastShortTradePosition.EntryTime;
                DateTime waitUntil = entry.AddSeconds(Seconds);

                if (ticks.LastTick.Time < waitUntil)
                    return false;

            }

            if (hasOpenPositions(PositionsShortTrades) && PriceMovedHigherThan(StepInPips, LastShortTradePosition.EntryPrice))
                return true;

            return false;
        }

        public Boolean TriggerOpenNextLong()
        {
            if (PositionsShortHedges.Count > 0)
                return false;

            if (LastLongTradePosition != null)
            {
                var ticks = MarketData.GetTicks();
                TimeSpan countSeconds;
                DateTime entry = LastLongTradePosition.EntryTime;
                DateTime waitUntil = entry.AddSeconds(Seconds);

                if (ticks.LastTick.Time < waitUntil)
                    return false;

            }

            if (hasOpenPositions(PositionsLongTrades) && PriceMovedLowerThan(StepInPips, LastLongTradePosition.EntryPrice))
                return true;

            return false;
        }

        public Boolean TriggerCloseLong()
        {
            if (hasOpenPositions(PositionsLongTrades))
                if (PositionsLongTrades.Count >= SwitchFastEMA && PriceAboveMovingAverage(MaFast))
                {
                    return true;
                }
                else if (PositionsLongTrades.Count < SwitchFastEMA && PriceAboveMovingAverage(MaSlow))
                {
                    return true;
                }

            return false;
        }

        public Boolean TriggerCloseShort()
        {
            if (hasOpenPositions(PositionsShortTrades))
                if (PositionsShortTrades.Count >= SwitchFastEMA && PriceBelowMovingAverage(MaFast))
                {
                    return true;
                }
                else if (PositionsShortTrades.Count < SwitchFastEMA && PriceBelowMovingAverage(MaSlow))
                {
                    return true;
                }


            return false;
        }

        public Boolean TriggerOpenHedgeShort()
        {
            //Skip if no open trades
            if (PositionsAll.Count == 0)
                return false;

            //Skip if already Hedging
            if (PositionsShortHedges.Count > 0)
                return false;

            //If net Profit of Longs falls below x Enter a Hedged short
            double profit = 0;

            foreach (Position position in PositionsLongTrades)
            {
                profit += position.GrossProfit;

            }

            if (profit < -HedgingTriggerDollarLoss)
                return true;

            return false;

        }

        public Boolean TriggerOpenHedgeLong()
        {
            //Skip if no open trades
            if (PositionsAll.Count == 0)
                return false;

            //Skip if already Hedging
            if (PositionsLongHedges.Count > 0)
                return false;

            //If net Profit of Shorts falls below x Enter a Hedged long
            double profit = 0;
            foreach (Position position in PositionsShortTrades)
            {
                profit += position.GrossProfit;

            }

            if (profit < -HedgingTriggerDollarLoss)
                return true;

            return false;

        }

        public void ActionOpenHedgeLong()
        {
            //Set a take profit of the same size as the Trades Stop Loss.
            double takeprofit = 100;
            double stoploss = 100;
            double volume = TotalVolumePositions(PositionsShortTrades) * 1.5;
            volume = Symbol.NormalizeVolumeInUnits(volume, RoundingMode.Down);

            OpenMarketOrder(TradeType.Buy, volume, "Hedge-" + BotLabel, stoploss, takeprofit);

        }

        public void ActionOpenHedgeShort()
        {
            //Set a take profit of the same size as the Trades Stop Loss.
            double takeprofit = 100;
            double stoploss = 100;
            double volume = TotalVolumePositions(PositionsLongTrades) * 1.5;
            volume = Symbol.NormalizeVolumeInUnits(volume, RoundingMode.Down);
            OpenMarketOrder(TradeType.Sell, volume, "Hedge-" + BotLabel, stoploss, takeprofit);

        }

        public void ActionOpenShortTrade()
        {

            OpenMarketOrder(TradeType.Sell, NextTradeVolume(PositionsShortTrades), "Trade-" + BotLabel, StopLoss, TakeProfit);
        }

        public void ActionOpenLongTrade()
        {
            OpenMarketOrder(TradeType.Buy, NextTradeVolume(PositionsLongTrades), "Trade-" + BotLabel, StopLoss, TakeProfit);
        }

        public void ActionCloseLongTrades()
        {
            ClosePositions(PositionsLongTrades);
        }

        public void ActionCloseShortTrades()
        {
            ClosePositions(PositionsShortTrades);
        }

        public void ActionRemoveTradingHalt()
        {
            if (LastHedgedPosition.TradeType == TradeType.Buy && Symbol.Ask < BB.Main.LastValue)
                IsHedging = false;

            if (LastHedgedPosition.TradeType == TradeType.Sell && Symbol.Bid > BB.Main.LastValue)
                IsHedging = false;

            Print("TradingHalt {0}", TradingHalt.ToString());
        }

        public void ActionCloseHedging()
        {
            //Hedges
            double profithedge = 0;
            foreach (Position position in PositionsAllHedge)
            {
                profithedge += position.GrossProfit;

            }

            if (profithedge < -HedgingExitDollarLoss)
                ClosePositions(PositionsAllHedge);

            //Trades
            double profit = 0;
            foreach (Position position in PositionsAll)
            {
                profit += position.GrossProfit;

            }

            if (profit > HedgingExitTakeProfit)
                ClosePositions(PositionsAll);



        }

        #endregion

        #region Trade Setups

        public Boolean PriceAboveUpperBollingerBands(BollingerBands bb)
        {
            if (Symbol.Bid > bb.Top.LastValue)
                return true;

            return false;
        }

        public Boolean PriceBelowLowerBollingerBands(BollingerBands bb)
        {
            if (Symbol.Ask < bb.Bottom.LastValue)
                return true;

            return false;
        }

        public Boolean PriceAboveExponentialMovingAverage(ExponentialMovingAverage ema)
        {
            if (Symbol.Bid > ema.Result.LastValue)
                return true;

            return false;
        }

        public Boolean PriceBelowExponentialMovingAverage(ExponentialMovingAverage ema)
        {
            if (Symbol.Ask < ema.Result.LastValue)
                return true;

            return false;
        }

        public Boolean PriceAboveMovingAverage(MovingAverage ma)
        {
            if (Symbol.Bid > ma.Result.LastValue)
                return true;

            return false;
        }

        public Boolean PriceBelowMovingAverage(MovingAverage ma)
        {
            if (Symbol.Ask < ma.Result.LastValue)
                return true;

            return false;
        }

        public Boolean PriceMovesFasterThan(int pips, int seconds)
        {

            if (PipRangePerSecond(seconds) > pips)
            {
                Print("MovementOfPips(seconds) {0} distanceOfPips(seconds) {1}", PipRangePerSecond(seconds), PipsPerSecond(seconds));
                return true;
            }


            return false;
        }

        public Boolean PriceMovedHigherThan(double pipsDistance, double price)
        {
            if (Symbol.Bid > price && DigitsToPips(Symbol, Symbol.Bid - price) > pipsDistance)
                return true;


            return false;
        }

        public Boolean PriceMovedLowerThan(double pipsDistance, double price)
        {

            if (Symbol.Ask < price && DigitsToPips(Symbol, price - Symbol.Ask) > pipsDistance)
                return true;


            return false;
        }

        public Boolean hasOpenPositions(List<Position> positions)
        {
            if (positions.Count > 0)
                return true;


            return false;

        }

        #endregion

        #region Trade Actions
        public void OpenMarketOrder(TradeType tradeType, double volume, string label, double stopLoss, double takeProfit)
        {
            var result = ExecuteMarketRangeOrder(tradeType, SymbolName, volume, SLIPPAGE, Symbol.Ask, label, stopLoss, takeProfit);
            if (result.IsSuccessful)
            {
                //  Print("Volume: {0} Bid: {1} Ask: {2} Slippage {3} StopLoss: {4} SymbolName: {5} TradeType {6}", volume, Symbol.Bid,  Symbol.Ask, SLIPPAGE, stopLoss, SymbolName, tradeType);
            }
            else
            {
                //Print("FAILED TRADE Data: Volume: {0} Bid: {1} Ask: {2} Slippage {3} StopLoss: {4} SymbolName: {5} TradeType {6}", volume, Symbol.Bid, Symbol.Ask, SLIPPAGE, stopLoss, SymbolName, tradeType);
                //  Stop();

            }
        }

        public void ClosePositions(List<Position> positions)
        {
            foreach (var position in positions)
                ClosePosition(position);

        }
        #endregion

        #region Calculations
        /// <summary>
        /// The highes-high - lowest-low: This number will get bigger if the price takes off in one direction.
        /// </summary>
        /// <param name="seconds"></param>
        /// <returns></returns>
        public double PipRangePerSecond(int seconds = 1)
        {
            var ticks = MarketData.GetTicks();
            DateTime startTime = ticks.LastTick.Time;
            DateTime endTime = startTime.AddSeconds(-seconds);
            double highestHigh = ticks.LastTick.Bid;
            double lowestLow = ticks.LastTick.Ask;


            int i = 0;
            while (ticks.Last(i).Time > endTime)
            {
                highestHigh = ticks.Last(i).Bid > highestHigh ? ticks.Last(i).Bid : highestHigh;
                lowestLow = ticks.Last(i).Ask < lowestLow ? ticks.Last(i).Ask : lowestLow;

                i++;
            }

            double priceDifference = Math.Abs(DigitsToPips(Symbol, highestHigh - lowestLow));

            //Print("highestHigh {0}, lowestLow {1} priceDifference {2}", highestHigh, lowestLow, priceDifference);

            return priceDifference;
        }

        public double PipsPerSecond(int seconds = 1)
        {
            var ticks = MarketData.GetTicks();
            DateTime startTime = ticks.LastTick.Time;
            DateTime endTime = startTime.AddSeconds(-seconds);
            double totalDistance = 0;

            int i = 1;
            while (ticks.Last(i).Time > endTime)
            {
                var spread = ticks.Last(i).Ask - ticks.Last(i).Bid;
                var rising = Math.Abs(ticks.Last(i).Bid - ticks.Last(i - 1).Bid);
                var falling = Math.Abs(ticks.Last(i - 1).Ask - ticks.Last(i).Ask);

                var tickDistance = ticks.Last(i).Bid - ticks.Last(i - 1).Bid > 0 ? rising : falling;

                totalDistance = totalDistance + tickDistance + spread;

                i++;
            }

            return DigitsToPips(Symbol, totalDistance);
        }

        /// <summary>
        /// Converts the number of pips current from Digits to Double
        /// </summary>
        /// <param name="Pips">The number of pips in the Digits format</param>
        /// <returns></returns>
        public double DigitsToPips(Symbol thisSymbol, double Digits)
        {
            return Math.Round(Digits / thisSymbol.PipSize, 2);
        }

        /// <summary>
        /// Converts the number of pips current from Double to Digits
        /// </summary>
        /// <param name="Pips">The number of pips in the Double format (2)</param>
        /// <returns></returns>
        public double PipsToDigits(Symbol thisSymbol, double Pips)
        {

            return Math.Round(Pips * thisSymbol.PipSize, thisSymbol.Digits);

        }

        public double NextTradeVolume(List<Position> positions)
        {
            double maxVolumeUnits = 0;
            foreach (Position position in positions)
            {
                if (position.VolumeInUnits > maxVolumeUnits)
                    maxVolumeUnits = position.VolumeInUnits;

            }

            double volume = positions.Count == 0 ? StartingSize : maxVolumeUnits * LotSizeMultiplier;

            //Print("maxVolumeUnits {0}, maxVolume {1}", maxVolumeUnits, maxVolumeUnits);

            return Symbol.NormalizeVolumeInUnits(volume, RoundingMode.Up);
        }

        public double TotalVolumePositions(List<Position> positions)
        {
            double volume = 0;

            foreach (Position position in positions)
            {
                volume += position.VolumeInUnits;

            }

            return volume;
        }

        #endregion

        #region Events

        private void OnOpenPositions(PositionOpenedEventArgs eventArgs)
        {
            //Add the <Position> to the correct Lists
            PositionsAll.Add(eventArgs.Position);

            if (eventArgs.Position.TradeType == TradeType.Buy && eventArgs.Position.Label == "Trade-" + BotLabel)
            {
                LastLongTradePosition = eventArgs.Position;
                PositionsLongTrades.Add(eventArgs.Position);
            }

            if (eventArgs.Position.TradeType == TradeType.Sell && eventArgs.Position.Label == "Trade-" + BotLabel)
            {
                LastShortTradePosition = eventArgs.Position;
                PositionsShortTrades.Add(eventArgs.Position);
            }

            if (eventArgs.Position.Label == "Trade-" + BotLabel)
            {
                PositionsAllTrade.Add(eventArgs.Position);
            }

            if (eventArgs.Position.TradeType == TradeType.Buy && eventArgs.Position.Label == "Hedge-" + BotLabel)
            {
                LastLongHedgePosition = eventArgs.Position;
                PositionsLongHedges.Add(eventArgs.Position);

            }

            if (eventArgs.Position.TradeType == TradeType.Sell && eventArgs.Position.Label == "Hedge-" + BotLabel)
            {
                LastShortHedgePosition = eventArgs.Position;
                PositionsShortHedges.Add(eventArgs.Position);

            }

            if (eventArgs.Position.Label == "Hedge-" + BotLabel)
            {
                LastHedgedPosition = eventArgs.Position;
                PositionsAllHedge.Add(eventArgs.Position);
                IsHedging = true;
                TradingHalt = true;
            }

            string subject = "Order Opened " + eventArgs.Position.VolumeInUnits.ToString();
            string body = eventArgs.Position.ToString();

            if (SendOrderEmails)
                SendOrderEmail(subject, body);

        }

        private void OnClosePositions(PositionClosedEventArgs eventArgs)
        {
            //Add the <Position> to the correct Lists
            PositionsAll.Remove(eventArgs.Position);

            if (eventArgs.Position.TradeType == TradeType.Buy && eventArgs.Position.Label == "Trade-" + BotLabel)
                PositionsLongTrades.Remove(eventArgs.Position);

            if (eventArgs.Position.TradeType == TradeType.Sell && eventArgs.Position.Label == "Trade-" + BotLabel)
                PositionsShortTrades.Remove(eventArgs.Position);

            if (eventArgs.Position.Label == "Trade-" + BotLabel)
                PositionsAllTrade.Remove(eventArgs.Position);

            if (eventArgs.Position.Label == "Hedge-" + BotLabel)
                PositionsAllHedge.Remove(eventArgs.Position);

            if (eventArgs.Position.TradeType == TradeType.Buy && eventArgs.Position.Label == "Hedge-" + BotLabel)
            {
                LastHedgedPosition = eventArgs.Position;
                PositionsLongHedges.Remove(eventArgs.Position);
                IsHedging = false;
            }

            if (eventArgs.Position.TradeType == TradeType.Sell && eventArgs.Position.Label == "Hedge-" + BotLabel)
            {
                LastHedgedPosition = eventArgs.Position;
                PositionsShortHedges.Remove(eventArgs.Position);
                IsHedging = false;
            }

        }

        private void SendOrderEmail(string subject, string body)
        {

            Notifications.SendEmail(EmailFrom, EmailTo, subject, body);
            Print("Email Sent To {0}, From {1}, Subject {2}, Body {3}", EmailTo, EmailFrom, subject, body);
        }

        #endregion

        #region Chart Elements
        public void TradingPanel()
        {
            var tradingPanel = new TradingPanel(this, Symbol, DefaultLots, DefaultStopLossPips, DefaultTakeProfitPips);

            var border = new Border 
            {
                VerticalAlignment = PanelVerticalAlignment,
                HorizontalAlignment = PanelHorizontalAlignment,
                Style = Styles.CreatePanelBackgroundStyle(),
                Margin = "20 40 20 20",
                Width = 225,
                Child = tradingPanel
            };

            Chart.AddControl(border);
        }
        #endregion
    }

    public class TradingPanel : CustomControl
    {
        private const string LotsInputKey = "LotsKey";
        private const string TakeProfitInputKey = "TPKey";
        private const string StopLossInputKey = "SLKey";
        private readonly IDictionary<string, TextBox> _inputMap = new Dictionary<string, TextBox>();
        private readonly Robot _robot;
        private readonly Symbol _symbol;

        public TradingPanel(Robot robot, Symbol symbol, double defaultLots, double defaultStopLossPips, double defaultTakeProfitPips)
        {
            _robot = robot;
            _symbol = symbol;
            AddChild(CreateTradingPanel(defaultLots, defaultStopLossPips, defaultTakeProfitPips));
        }

        private ControlBase CreateTradingPanel(double defaultLots, double defaultStopLossPips, double defaultTakeProfitPips)
        {
            var mainPanel = new StackPanel();

            var header = CreateHeader();
            mainPanel.AddChild(header);

            var contentPanel = CreateContentPanel(defaultLots, defaultStopLossPips, defaultTakeProfitPips);
            mainPanel.AddChild(contentPanel);

            return mainPanel;
        }

        private ControlBase CreateHeader()
        {
            var headerBorder = new Border 
            {
                BorderThickness = "0 0 0 1",
                Style = Styles.CreateCommonBorderStyle()
            };

            var header = new TextBlock 
            {
                Text = "Quick Trading Panel",
                Margin = "10 7",
                Style = Styles.CreateHeaderStyle()
            };

            headerBorder.Child = header;
            return headerBorder;
        }

        private StackPanel CreateContentPanel(double defaultLots, double defaultStopLossPips, double defaultTakeProfitPips)
        {
            var contentPanel = new StackPanel 
            {
                Margin = 10
            };
            var grid = new Grid(4, 3);
            grid.Columns[1].SetWidthInPixels(5);

            var sellButton = CreateTradeButton("SELL", Styles.CreateSellButtonStyle(), TradeType.Sell);
            grid.AddChild(sellButton, 0, 0);

            var buyButton = CreateTradeButton("BUY", Styles.CreateBuyButtonStyle(), TradeType.Buy);
            grid.AddChild(buyButton, 0, 2);

            var lotsInput = CreateInputWithLabel("Quantity (Lots)", defaultLots.ToString("F2"), LotsInputKey);
            grid.AddChild(lotsInput, 1, 0, 1, 3);

            var stopLossInput = CreateInputWithLabel("Stop Loss (Pips)", defaultStopLossPips.ToString("F1"), StopLossInputKey);
            grid.AddChild(stopLossInput, 2, 0);

            var takeProfitInput = CreateInputWithLabel("Take Profit (Pips)", defaultTakeProfitPips.ToString("F1"), TakeProfitInputKey);
            grid.AddChild(takeProfitInput, 2, 2);

            var closeAllButton = CreateCloseAllButton();
            grid.AddChild(closeAllButton, 3, 0, 1, 3);

            contentPanel.AddChild(grid);

            return contentPanel;
        }

        private Button CreateTradeButton(string text, Style style, TradeType tradeType)
        {
            var tradeButton = new Button 
            {
                Text = text,
                Style = style,
                Height = 25
            };

            tradeButton.Click += args => ExecuteMarketOrderAsync(tradeType);

            return tradeButton;
        }

        private ControlBase CreateCloseAllButton()
        {
            var closeAllBorder = new Border 
            {
                Margin = "0 10 0 0",
                BorderThickness = "0 1 0 0",
                Style = Styles.CreateCommonBorderStyle()
            };

            var closeButton = new Button 
            {
                Style = Styles.CreateCloseButtonStyle(),
                Text = "Close All",
                Margin = "0 10 0 0"
            };

            closeButton.Click += args => CloseAll();
            closeAllBorder.Child = closeButton;

            return closeAllBorder;
        }

        private Panel CreateInputWithLabel(string label, string defaultValue, string inputKey)
        {
            var stackPanel = new StackPanel 
            {
                Orientation = Orientation.Vertical,
                Margin = "0 10 0 0"
            };

            var textBlock = new TextBlock 
            {
                Text = label
            };

            var input = new TextBox 
            {
                Margin = "0 5 0 0",
                Text = defaultValue,
                Style = Styles.CreateInputStyle()
            };

            _inputMap.Add(inputKey, input);

            stackPanel.AddChild(textBlock);
            stackPanel.AddChild(input);

            return stackPanel;
        }

        private void ExecuteMarketOrderAsync(TradeType tradeType)
        {
            var lots = GetValueFromInput(LotsInputKey, 0);
            if (lots <= 0)
            {
                _robot.Print(string.Format("{0} failed, invalid Lots", tradeType));
                return;
            }

            var stopLossPips = GetValueFromInput(StopLossInputKey, 0);
            var takeProfitPips = GetValueFromInput(TakeProfitInputKey, 0);

            _robot.Print(string.Format("Open position with: LotsParameter: {0}, StopLossPipsParameter: {1}, TakeProfitPipsParameter: {2}", lots, stopLossPips, takeProfitPips));

            var volume = _symbol.QuantityToVolumeInUnits(lots);
            _robot.ExecuteMarketOrderAsync(tradeType, _symbol.Name, volume, "Trade Panel Sample", stopLossPips, takeProfitPips);
        }

        private double GetValueFromInput(string inputKey, double defaultValue)
        {
            double value;

            return double.TryParse(_inputMap[inputKey].Text, out value) ? value : defaultValue;
        }

        private void CloseAll()
        {
            foreach (var position in _robot.Positions)
                _robot.ClosePositionAsync(position);
        }

    }
    /* public void ToggleTradingHalt()
        {
            if (_robot.TradingHalt)
                _robot.TradingHalt = false;

            if (_robot.TradingHalt)
                _robot.TradingHalt = true;

           



        }*/

    public static class Styles
    {
        public static Style CreatePanelBackgroundStyle()
        {
            var style = new Style();
            style.Set(ControlProperty.CornerRadius, 3);
            style.Set(ControlProperty.BackgroundColor, GetColorWithOpacity(Color.FromHex("#292929"), 0.85m), ControlState.DarkTheme);
            style.Set(ControlProperty.BackgroundColor, GetColorWithOpacity(Color.FromHex("#FFFFFF"), 0.85m), ControlState.LightTheme);
            style.Set(ControlProperty.BorderColor, Color.FromHex("#3C3C3C"), ControlState.DarkTheme);
            style.Set(ControlProperty.BorderColor, Color.FromHex("#C3C3C3"), ControlState.LightTheme);
            style.Set(ControlProperty.BorderThickness, new Thickness(1));

            return style;
        }

        public static Style CreateCommonBorderStyle()
        {
            var style = new Style();
            style.Set(ControlProperty.BorderColor, GetColorWithOpacity(Color.FromHex("#FFFFFF"), 0.12m), ControlState.DarkTheme);
            style.Set(ControlProperty.BorderColor, GetColorWithOpacity(Color.FromHex("#000000"), 0.12m), ControlState.LightTheme);
            return style;
        }

        public static Style CreateHeaderStyle()
        {
            var style = new Style();
            style.Set(ControlProperty.ForegroundColor, GetColorWithOpacity("#FFFFFF", 0.70m), ControlState.DarkTheme);
            style.Set(ControlProperty.ForegroundColor, GetColorWithOpacity("#000000", 0.65m), ControlState.LightTheme);
            return style;
        }

        public static Style CreateInputStyle()
        {
            var style = new Style(DefaultStyles.TextBoxStyle);
            style.Set(ControlProperty.BackgroundColor, Color.FromHex("#1A1A1A"), ControlState.DarkTheme);
            style.Set(ControlProperty.BackgroundColor, Color.FromHex("#111111"), ControlState.DarkTheme | ControlState.Hover);
            style.Set(ControlProperty.BackgroundColor, Color.FromHex("#E7EBED"), ControlState.LightTheme);
            style.Set(ControlProperty.BackgroundColor, Color.FromHex("#D6DADC"), ControlState.LightTheme | ControlState.Hover);
            style.Set(ControlProperty.CornerRadius, 3);
            return style;
        }

        public static Style CreateBuyButtonStyle()
        {
            return CreateButtonStyle(Color.FromHex("#009345"), Color.FromHex("#10A651"));
        }

        public static Style CreateSellButtonStyle()
        {
            return CreateButtonStyle(Color.FromHex("#F05824"), Color.FromHex("#FF6C36"));
        }

        public static Style CreateCloseButtonStyle()
        {
            return CreateButtonStyle(Color.FromHex("#F05824"), Color.FromHex("#FF6C36"));
        }

        private static Style CreateButtonStyle(Color color, Color hoverColor)
        {
            var style = new Style(DefaultStyles.ButtonStyle);
            style.Set(ControlProperty.BackgroundColor, color, ControlState.DarkTheme);
            style.Set(ControlProperty.BackgroundColor, color, ControlState.LightTheme);
            style.Set(ControlProperty.BackgroundColor, hoverColor, ControlState.DarkTheme | ControlState.Hover);
            style.Set(ControlProperty.BackgroundColor, hoverColor, ControlState.LightTheme | ControlState.Hover);
            style.Set(ControlProperty.ForegroundColor, Color.FromHex("#FFFFFF"), ControlState.DarkTheme);
            style.Set(ControlProperty.ForegroundColor, Color.FromHex("#FFFFFF"), ControlState.LightTheme);
            return style;
        }

        private static Color GetColorWithOpacity(Color baseColor, decimal opacity)
        {
            var alpha = (int)Math.Round(byte.MaxValue * opacity, MidpointRounding.AwayFromZero);
            return Color.FromArgb(alpha, baseColor);
        }
    }
}
