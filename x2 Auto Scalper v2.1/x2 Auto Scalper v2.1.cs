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

        [Parameter("Slow MA Period", Group = "EMA", DefaultValue = 21)]
        public int EMASlowPeriod { get; set; }

        [Parameter("Bot Label ( Magic Name )", Group = "Identity", DefaultValue = "BotLabel")]
        public string BotLabel { get; set; }


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

        [Parameter("ATR To Avoid", Group = "Strategy", DefaultValue = 0.002)]
        public double ATRToAvoid { get; set; }

        [Parameter("Lot Size Multplier", Group = "Strategy", DefaultValue = 2)]
        public int LotSizeMultiplier { get; set; }

        [Parameter("Seconds LookBack ", Group = "Strategy", DefaultValue = 60)]
        public int Seconds { get; set; }

        #endregion

        private ExponentialMovingAverage EmaFast;
        private ExponentialMovingAverage EmaSlow;
        private BollingerBands BB;
        private AverageTrueRange ATR;

        public Position LastLongTradePosition;
        public Position LastShortTradePosition;
        public Position LastLongHedgePosition;
        public Position LastShortHedgePosition;
        public List<Position> PositionsLongTrades;
        public List<Position> PositionsShortTrades;
        public List<Position> PositionsLongHedges;
        public List<Position> PositionsShortHedges;
        public List<Position> PositionsAll;


        public double HighestHigh { get; set; }
        public double LowestLow { get; set; }


        protected override void OnStart()
        {
            //Create instances of the Indicators used
            BB = Indicators.BollingerBands(BollingBandsSource, BollingBandsPeriod, BollingBandsSD, BollingBandsMAType);
            EmaFast = Indicators.ExponentialMovingAverage(EMASource, EMAFastPeriod);
            EmaSlow = Indicators.ExponentialMovingAverage(EMASource, EMASlowPeriod);
            ATR = Indicators.AverageTrueRange(2, MovingAverageType.Hull);

            //Setup the Position Lists<> 
            PositionsLongTrades = new List<Position>();
            PositionsShortTrades = new List<Position>();
            PositionsLongHedges = new List<Position>();
            PositionsShortHedges = new List<Position>();
            PositionsAll = new List<Position>();


            //Run each time a position is opened or closed
            Positions.Opened += OnOpenPositions;
            Positions.Closed += OnClosePositions;


        }

        protected override void OnTick()
        {
            //Close Long trades if price crosses slow EMA
            if (hasOpenPositions(PositionsLongTrades) && PriceAboveExponentialMovingAverage(EmaSlow))
                ClosePositions(PositionsLongTrades);

            //Close Short trades if price crosses slow EMA
            if (hasOpenPositions(PositionsShortTrades) && PriceBelowExponentialMovingAverage(EmaSlow))
                ClosePositions(PositionsShortTrades);

            //Open next Long order
            if (hasOpenPositions(PositionsLongTrades) && PriceMovedLowerThan(StepInPips, LastLongTradePosition.EntryPrice))
                OpenMarketOrder(TradeType.Buy, NextTradeVolume(PositionsLongTrades), "Trade-" + BotLabel, StopLoss, TakeProfit);

            //Open next Short order
            if (hasOpenPositions(PositionsShortTrades) && PriceMovedHigherThan(StepInPips, LastLongTradePosition.EntryPrice))
                OpenMarketOrder(TradeType.Sell, NextTradeVolume(PositionsShortTrades), "Trade-" + BotLabel, StopLoss, TakeProfit);

            //Open first Long order
            if (PriceBelowLowerBollingerBands(BB) && !hasOpenPositions(PositionsLongTrades))
                OpenMarketOrder(TradeType.Buy, NextTradeVolume(PositionsLongTrades), "Trade-" + BotLabel, StopLoss, TakeProfit);

            //Open first Short order
            if (PriceAboveUpperBollingerBands(BB) && !hasOpenPositions(PositionsShortTrades))
                OpenMarketOrder(TradeType.Buy, NextTradeVolume(PositionsLongTrades), "Trade-" + BotLabel, StopLoss, TakeProfit);

        }

        protected override void OnBar()
        {
            // check if the price is going faster than 12 pips in 30 seconds
            //PriceMovesFasterThan(10, 30);
            // Print("pps ", PipsPerSecond(Seconds));


        }

        protected override void OnStop()
        {
            // Put your deinitialization logic here
        }

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
            if (Symbol.Bid > price && pipsDistance > price + Symbol.Bid)
                return true;


            return false;
        }

        public Boolean PriceMovedLowerThan(double pipsDistance, double price)
        {
            if (Symbol.Ask < price && pipsDistance > price - Symbol.Ask)
                return true;


            return false;
        }

        public Boolean hasOpenPositions(List<Position> positions)
        {
            if (positions.Count > 1)
                return true;


            return false;

        }

        #endregion

        #region Actions
        public void OpenMarketOrder(TradeType tradeType, double volume, string label, double stopLoss, double takeProfit)
        {
            var result = ExecuteMarketRangeOrder(tradeType, SymbolName, volume, SLIPPAGE, Symbol.Ask, label, stopLoss, takeProfit);
            if (result.IsSuccessful)
            {
                Print("BUY: Volume: {0} Ask: {1} Slippage {2} StopLoss: {3} SymbolName: {4}", volume, Symbol.Bid, SLIPPAGE, stopLoss, SymbolName);
            }
            else
            {
                Stop();
            }
        }

        public void ClosePositions(List<Position> positions)
        {
            foreach (var position in Positions)
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
        public double DigitsToPips(Symbol thisSymbol, double Pips)
        {

            return Math.Round(Pips / thisSymbol.PipSize, 2);

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

            double volume = positions.Count == 0 ? StartingSize : StartingSize * Positions.Count * LotSizeMultiplier;

            return Symbol.NormalizeVolumeInUnits(volume, RoundingMode.Down);
        }

        #endregion

        #region Events

        private void OnOpenPositions(PositionOpenedEventArgs eventArgs)
        {
            //Add the <Position> to the correct Lists
            PositionsAll.Add(eventArgs.Position);

            if (eventArgs.Position.TradeType == TradeType.Buy && eventArgs.Position.Label == "Trade" + BotLabel)
            {
                LastLongTradePosition = eventArgs.Position;
                PositionsLongTrades.Add(eventArgs.Position);
            }

            if (eventArgs.Position.TradeType == TradeType.Sell && eventArgs.Position.Label == "Trade" + BotLabel)
            {
                LastShortTradePosition = eventArgs.Position;
                PositionsShortTrades.Add(eventArgs.Position);
            }

            if (eventArgs.Position.TradeType == TradeType.Buy && eventArgs.Position.Label == "Hedge" + BotLabel)
            {
                LastLongHedgePosition = eventArgs.Position;
                PositionsLongHedges.Add(eventArgs.Position);
            }

            if (eventArgs.Position.TradeType == TradeType.Sell && eventArgs.Position.Label == "Hedge" + BotLabel)
            {
                LastShortHedgePosition = eventArgs.Position;
                PositionsShortHedges.Add(eventArgs.Position);
            }

        }

        private void OnClosePositions(PositionClosedEventArgs eventArgs)
        {
            //Add the <Position> to the correct Lists
            PositionsAll.Remove(eventArgs.Position);

            if (eventArgs.Position.TradeType == TradeType.Buy && eventArgs.Position.Label == "Trade" + BotLabel)
                PositionsLongTrades.Remove(eventArgs.Position);

            if (eventArgs.Position.TradeType == TradeType.Sell && eventArgs.Position.Label == "Trade" + BotLabel)
                PositionsShortTrades.Remove(eventArgs.Position);

            if (eventArgs.Position.TradeType == TradeType.Buy && eventArgs.Position.Label == "Hedge" + BotLabel)
                PositionsLongHedges.Remove(eventArgs.Position);

            if (eventArgs.Position.TradeType == TradeType.Sell && eventArgs.Position.Label == "Hedge" + BotLabel)
                PositionsShortHedges.Remove(eventArgs.Position);

        }

        #endregion
    }
}
