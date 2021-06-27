using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace BaggageSortingSystem.classes
{
    class SortingSystem : BaggageManagementComponent
    {
        /*
        The Sorter's job is to:
            -Cycle through each COunter's conveyor belt, take the baggage [TIMESTAMP] and store it locally.
            -Cycle through each Gate's conveyor belt and put the locally stored baggage [TIMESTAMP] where it needs to go. Or put it in Lost&Found.
        */
        
        private Dictionary<int, Baggage[]> baggageBatchesForGates;

        public SortingSystem() : base("The Sorting System", 4096)
        {
            this.baggageBatchesForGates = new Dictionary<int, Baggage[]>();
        }

        public string ToString()
        {
            string print = this._Thread.Name + " is alive & not stopping: " + (this._Thread.IsAlive && !this.Stop) + ", Unsorted Baggage: " + BaggageManager.CountBaggage(this.LocalBaggageBuffer) + "/" + this.LocalBaggageBuffer.Length;

            /*if(this.baggageBatchesForGates.Count > 0)
            {
                print += "\n   Sorting System's Gate Buffers:";
                foreach (KeyValuePair<int, Baggage[]> keyPair in this.baggageBatchesForGates)
                {
                    print += "\n     -Gate " + keyPair.Key + ": " + BaggageManager.CountBaggage(keyPair.Value) + "/" + keyPair.Value.Length;
                }
            }//*/

            return print;
        }

        public override void Run()
        {
            Thread.Sleep(Convert.ToInt32(5500 * CentralServer.timeScale));
            while (!this.Killed)
            {
                if (this.Stop)
                {
                    // Here the thread will rest until told not to stop anymore.
                    Thread.Sleep(Convert.ToInt32(15000 * CentralServer.timeScale));
                }
                while (!this.Stop && !this.Killed)
                {
                    // Attempt to empty each Counter's shared conveyorbelt for baggage and store it locally. - If it's own storage isn't full!
                    if(BaggageManager.CountBaggage(this.LocalBaggageBuffer) < this.LocalBaggageBuffer.Length)
                    {
                        for (int i = 0; i < CentralServer.bM.Counters.Length; i++)
                        {
                            // It will use TryEnter instead of Enter, and move on if it fails, as the SOrter is a very busy system and can't afford to wait.
                            PullFromCounterConvenyorBelt(CentralServer.bM.Counters[i].CounterNumber);
                        }
                    }

                    // Ensure we have a local baggage batch storage buffer for each gate in existence.
                    if (this.baggageBatchesForGates.Count < CentralServer.bM.Gates.Length)
                    {
                        for (int i = 0; i < CentralServer.bM.Gates.Length; i++)
                        {
                            if (!this.baggageBatchesForGates.Keys.Contains(CentralServer.bM.Gates[i].GateNumber))
                            {
                                // I realized that it is Very important for the new Baggage[] not to be longer than the gateConveyorBelt's Baggage[], else there can be a deadlock.
                                this.baggageBatchesForGates.Add(CentralServer.bM.Gates[i].GateNumber, new Baggage[512]);
                            }
                        }
                    }

                    // Grouping Baggage for their corresponding Gates
                    GroupBaggage();

                    Thread.Sleep(DateTime.Now.AddMinutes(1 * CentralServer.timeScale) - DateTime.Now);

                    // Attempt to empty its own local buffers onto the various gates' belts
                    for (int i = 0; i < this.baggageBatchesForGates.Count; i++)
                    {
                        PushToGateConveyorBelt(CentralServer.bM.Gates[i].GateNumber);
                    }
                }
            }
        }

        /// <summary>
        /// TryEnter Once on this counter number, move each of its baggage over into local baggage storage and remove from the shared.
        /// </summary>
        /// <param name="counterNumber"></param>
        void PullFromCounterConvenyorBelt(int counterNumber)
        {
            object _lock = BaggageManager.counterConveyorBeltLocks[counterNumber];
            int baggageOnBelt = BaggageManager.CountBaggage(CentralServer.bM.CounterConveyorBelts[counterNumber]);
            int remainingLocalBaggageSpace = this.LocalBaggageBuffer.Length - BaggageManager.CountBaggage(this.LocalBaggageBuffer);

            // I am confident in my code, that it would work just fine 99.9999999999999999999999999999% of the time without fail.. However,
            // shit happens, and therefore, I want to be ready for if or when it does. It is Possible for there to somehow be gaps of null in my arrays,
            // so I make sure to skip those. This should ideally be implemented in a lot more places.
            int nullGaps = 0;
            
            // The Sorting System is a very busy one, it will only try to enter CounterConveyorBeltLock once per cycle, but it will, if there's space,
            // take all shared stored baggage in one big gulp!
            if (baggageOnBelt > 0 && baggageOnBelt < remainingLocalBaggageSpace && Monitor.TryEnter(_lock))
            {
                try
                {
                    for (int i = 0; i < baggageOnBelt + nullGaps; i++)
                    {
                        if(CentralServer.bM.CounterConveyorBelts[counterNumber][0] != null)
                        {
                            // Here it puts a piece of baggage into its own storage.
                            CentralServer.bM.CounterConveyorBelts[counterNumber][0].SortingTS = DateTime.Now;
                            this.LocalBaggageBuffer = BaggageManager.AddBaggageToBack(CentralServer.bM.CounterConveyorBelts[counterNumber][0], this.LocalBaggageBuffer);
                            CentralServer.bM.CounterConveyorBelts[counterNumber] = BaggageManager.MoveBaggagesForward(CentralServer.bM.CounterConveyorBelts[counterNumber]);
                        }
                        else
                        {
                            nullGaps++;
                        }
                    }
                }
                finally
                {
                    Monitor.PulseAll(_lock);
                    Monitor.Exit(_lock);
                }
            }
        }

        /// <summary>
        /// Groups the baggage in LocalBaggageBuffer into their flight's corresponding gate's corresponding baggageBatchesForGates (sorter's local buffers)
        /// </summary>
        void GroupBaggage()
        {
            int unsorted = 0;
            int howMuchToGroup = BaggageManager.CountBaggage(this.LocalBaggageBuffer);
            for (int i = 0; i < howMuchToGroup; i++)
            {
                if (CentralServer.bM.flightsAtGatesMap.ContainsKey(this.LocalBaggageBuffer[0 + unsorted].FlightID))
                {
                    int gateNumber = CentralServer.bM.flightsAtGatesMap[this.LocalBaggageBuffer[0 + unsorted].FlightID];
                    if (BaggageManager.CountBaggage(this.baggageBatchesForGates[gateNumber]) < this.baggageBatchesForGates[gateNumber].Length)
                    {
                        BaggageManager.AddBaggageToBack(this.LocalBaggageBuffer[0 + unsorted], this.baggageBatchesForGates[gateNumber]);
                        this.LocalBaggageBuffer = BaggageManager.MoveBaggagesForward(this.LocalBaggageBuffer, unsorted);
                    }
                    else
                    {
                        // Baggage is unsorted, because there was not room for it in the sorter's corresponding local buffer for the gate.
                        unsorted++;
                    }
                }
                else
                {
                    // Baggage is Lost! That flight does not and has not existed!
                    Console.WriteLine(this._Thread.Name + " lost baggage: " + this.LocalBaggageBuffer[0].ToString());
                    CentralServer.lateBaggage++;
                    this.LocalBaggageBuffer = BaggageManager.MoveBaggagesForward(this.LocalBaggageBuffer, unsorted);
                }
            }
        }

        /// <summary>
        /// If there is room on the gate belt for the local buffer's contents
        /// </summary>
        /// <param name="gateNumber"></param>
        void PushToGateConveyorBelt(int gateNumber)
        {
            object _lock = BaggageManager.gateConveyorBeltLocks[gateNumber];

            int amountOfBaggageForGate = BaggageManager.CountBaggage(this.baggageBatchesForGates[gateNumber]);
            int spaceOnGateConveyorBelt = CentralServer.bM.GateConveyorBelts[gateNumber].Length - BaggageManager.CountBaggage(CentralServer.bM.GateConveyorBelts[gateNumber]);
            
            // Once again, the sorter is a very busy component, so it uses TryEnter, and does not wait for the lock or for the gate to make room on its belt.
            if (spaceOnGateConveyorBelt >= amountOfBaggageForGate && Monitor.TryEnter(_lock))
            {
                try
                {
                    for (int i = 0; i < amountOfBaggageForGate; i++)
                    {
                        this.baggageBatchesForGates[gateNumber][0].SortedTS = DateTime.Now;
                        CentralServer.bM.GateConveyorBelts[gateNumber] = BaggageManager.AddBaggageToBack(this.baggageBatchesForGates[gateNumber][0], CentralServer.bM.GateConveyorBelts[gateNumber]);
                        this.baggageBatchesForGates[gateNumber] = BaggageManager.MoveBaggagesForward(this.baggageBatchesForGates[gateNumber]);
                    }
                }
                finally
                {
                    Monitor.PulseAll(_lock);
                    Monitor.Exit(_lock);
                }
            }
        }
    }
}
