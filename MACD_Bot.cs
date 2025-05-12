using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using cAlgo.Indicators;

using System.Collections.Generic;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class MACDBot : Robot
    {
        [Parameter(DefaultValue = 0.0)]
        public double Parameter { get; set; }

        //------------------
        private MacdCrossOver myMACD;

        List<TradeIdea> tradesToExecute = new List<TradeIdea>();
        List<MyPosition> tradesToManage = new List<MyPosition>();

        //___________________struct to hold trade idea_______________________
        public struct TradeIdea
        {
            public Bar candle;
            public string tradeDirection;
            public double volume;
            public double stopPrice;
            public double targetPrice;

            public TradeIdea(Bar xCandle, string dir, double vol = 0, double sl = 0, double tp = 0)
            {
                this.candle = xCandle;
                this.tradeDirection = dir;
                this.volume = vol;
                this.stopPrice = sl;
                this.targetPrice = tp;
            }
        }
        //_______________________________________________________________
        
        //___________________struct to hold open trade_______________________
        public struct MyPosition
        {
            public Position thePosition;
            public double stopPrice;
            public double targetPrice;

            public MyPosition(Position pos, double stop = 0, double target = 0)
            {
                this.thePosition = pos;
                this.stopPrice = stop;
                this.targetPrice = target;
            }
        }
        //_______________________________________________________________


        [Parameter()]
        public DataSeries Source { get; set; }

        protected override void OnStart()
        {
            // Put your initialization logic here
            myMACD = Indicators.MacdCrossOver(26, 12, 9);

        }

        protected override void OnTick()
        {
            // Put your core logic here
            
            ManageOpenTrades();
            
            for (int i = 0; i < tradesToExecute.Count; i++)
            {   
                if (tradesToExecute[i].tradeDirection == "B") {MarketBuy(i); break;}
                if (tradesToExecute[i].tradeDirection == "S") {MarketSell(i); break;}
            }
        }
        
         protected override void OnStop()
        {
            // Put your deinitialization logic here
        }
        
        protected void MarketBuy(int pos) {
            TradeResult result = ExecuteMarketOrder(TradeType.Buy, 
            SymbolName, 
            tradesToExecute[pos].volume, 
            "Label: Buy"); 
            
            if (result.IsSuccessful)
            {   
                MyPosition buyPosition = new MyPosition(result.Position, tradesToExecute[pos].stopPrice, tradesToExecute[pos].targetPrice);
                tradesToManage.Add(buyPosition);
                tradesToExecute.Clear();
            }
        }
        
        protected void MarketSell(int pos) {
            TradeResult result = ExecuteMarketOrder(TradeType.Sell, 
            SymbolName, 
            tradesToExecute[pos].volume, 
            "Label: Sell"); 
            
            if (result.IsSuccessful)
            {
                MyPosition sellPosition = new MyPosition(result.Position, tradesToExecute[pos].stopPrice, tradesToExecute[pos].targetPrice);
                tradesToManage.Add(sellPosition);
                tradesToExecute.Clear();
            }
        }
        
        protected bool BetweenTradeHours(Bar xbar)
        {   
            double hourStart = 10;
            double hourEnd = 18;
            
            if ((xbar.OpenTime.Hour >= hourStart) && (xbar.OpenTime.Hour <= hourEnd)) {
                return true;
            }
            else return false;
        }
        
        //returns true if current symbol is an indice
        //---
        protected bool SymbolIsAnIndice()
        {
            if (SymbolName == "USTEC" || SymbolName == "US30" || SymbolName == "US500") {return true;} 
            else return false;
        }
        
        //get trade volume depending on instrument 
        //---
        protected double GetTradeVolume()
        {
            if (SymbolIsAnIndice()) {return 1;} 
            else return (Symbol.LotSize)/100; //returns volume for 0.01 lots based on symbol
        }
        
        protected bool IsBullishBar(Bar b) {  
            if (b.Close > b.Open) {return true;}
            else return false;
        }
        
        protected void ManageOpenTrades() {
            for (int i = 0; i < tradesToManage.Count; i++) {
                
                //manage buys --------------------------------------------------------
                if (tradesToManage[i].thePosition.TradeType == TradeType.Buy) {ManageBuys(i);}
                
                //manage sells --------------------------------------------------------
                else if (tradesToManage[i].thePosition.TradeType == TradeType.Sell) {ManageSells(i);}
            }
        }
        
        protected void ManageBuys(int pos) {
            if ((Bars.Last(1).Close > tradesToManage[pos].targetPrice) || 
                (Bars.Last(1).Close < tradesToManage[pos].stopPrice)) 
                {
                    TradeResult closePos = ClosePosition(tradesToManage[pos].thePosition);
                    if (closePos.IsSuccessful) {
                       tradesToManage.RemoveAt(pos);
                    }
                }
        }
        
        protected void ManageSells(int pos) {
        if ((Bars.Last(1).Close < tradesToManage[pos].targetPrice) || 
            (Bars.Last(1).Close > tradesToManage[pos].stopPrice)) {
                TradeResult closePos = ClosePosition(tradesToManage[pos].thePosition);
                
                if (closePos.IsSuccessful) {
                    tradesToManage.RemoveAt(pos);
                }
            }
        }
   
   
        protected bool MACDBuySignal()
        {   
            if ((myMACD.MACD.Last(1) > myMACD.Signal.Last(1)) && (myMACD.Histogram.Last(1)>0))
            {
                return true;
            }    
            else return false;
        }
        
        protected bool MACDSellSignal()
        {   
            if ((myMACD.MACD.Last(1) < myMACD.Signal.Last(1)) && (myMACD.Histogram.Last(1)<0))
            {
                return true;
            }    
            else return false;
        }
        
        protected void ActivateBuy() {
        
            double slPrice;
            double tpPrice;
                
            slPrice = Bars.Last(1).Low;
            tpPrice = Bars.Last(1).High;
            
            TradeIdea entry = new TradeIdea(Bars.Last(1), "B", GetTradeVolume(), slPrice, tpPrice);
            tradesToExecute.Add(entry);
        }
        
        protected void ActivateSell() {
            
            double slPrice;
            double tpPrice;
            
            slPrice = Bars.Last(1).High;
            tpPrice = Bars.Last(1).Low;
                
            TradeIdea entry = new TradeIdea(Bars.Last(1), "S", GetTradeVolume(), slPrice, tpPrice);
            tradesToExecute.Add(entry); 
        }

        protected override void OnBar()
        {
            //clear list of trades to take with each new bar formed
            tradesToExecute.Clear();
            //ManageOpenTrades();

            //buys
            if (MACDBuySignal() && !IsBullishBar(Bars.Last(1)))
            {   
                ActivateBuy();
            }

            //sells
            else if (MACDSellSignal() && IsBullishBar(Bars.Last(1)))
            {                
                ActivateSell();       
            }    
        }
            
    }
}
