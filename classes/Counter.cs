using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace BaggageSortingSystem.classes
{
    public class Counter : BaggageManagementComponent
    {
        /*
        The Counter's Responsibility is to alternatingly:
            -Receive a Passenger and generate their Baggage
            -Pass that baggage to the Sorter.
            -Be able to shut down peacefully without losing any currently-being-processed passengers or baggage.
        */

        private int counterNumber;
        private bool processingPassenger;

        // By scheduling at what point in time that the counter will, at the earliest (It could potentially get stuck in case of a full buffer),
        // be done processing the passenger, we can avoid using Thread.Sleep() and have the thread keep working while there's stuff for it to do.
        private DateTime checkInCompletionTime;
        
        public int CounterNumber
        {
            get { return this.counterNumber; }
        }
        public bool ProcessingPassenger
        {
            get { return this.processingPassenger; }
        }
        public DateTime CheckInCompletiontime
        {
            get { return this.checkInCompletionTime; }
        }
        
        public Counter(int CounterNumber) : base("Counter " + CounterNumber, 12)
        {
            this.counterNumber = CounterNumber;
            this.processingPassenger = false;
            this.checkInCompletionTime = DateTime.Now;
        }

        public string ToString()
        {
            return this._Thread.Name + " is alive: " + this._Thread.IsAlive + ", stopped: " + this.Stop + ", Has Passenger: " + this.processingPassenger + (this.processingPassenger? " until " + this.checkInCompletionTime.ToString("HH:mm:ss") : "") + ", Shared Buffer: " + BaggageManager.CountBaggage(CentralServer.BM.CounterConveyorBelts[this.counterNumber]) + "/" + CentralServer.BM.CounterConveyorBelts[this.counterNumber].Length;
        }

        public override void Run()
        {
            this.processingPassenger = false;

            while (!this.Killed)
            {
                if (this.Stop)
                {
                    // Here the thread will rest until told not to stop anymore.
                    Thread.Sleep(Convert.ToInt32(15000 * CentralServer.timeScale));
                }
                while ((!this.Stop && !this.Killed) || !this.ReadyToStop())
                {
                    int localBaggageCount = BaggageManager.CountBaggage(this.LocalBaggageBuffer);
                    if (localBaggageCount > this.LocalBaggageBuffer.Length - 3 || ((this.processingPassenger || this.Stop) && localBaggageCount > 0))
                    {
                        // Pushing the Baggage to the sorter needs to be prioritized over processing new passengers,
                        // for in the, unlikely, event that the local buffer fills up before the counter has had a chance to empty it while processing Passengers.
                        PushBaggage();
                    }
                    else if (!processingPassenger && CentralServer.FM.passengersQueue.Length > 0 && !Stop)
                    {
                        // Wait/Acquire Passenger
                        processingPassenger = true;
                        Passenger passenger = AcquirePassenger();
                        checkInCompletionTime = DateTime.Now.AddSeconds(CentralServer.rnd.Next(90, 240) * CentralServer.timeScale);

                        // Let's see if the passenger made it on time.
                        if (passenger._Flight.DepartureTime > DateTime.Now.AddMinutes(30 * CentralServer.timeScale))
                        {
                            // Generate + Locally Store Baggage
                            GenerateBaggage(passenger);
                        }
                        else
                        {
                            // They did not make it, too bad! .. But they'll stick around to go through the 7 stages of grief anyway.
                            CentralServer.latePassengers++;
                        }
                    }
                    // I have to add a second to the datetime, or else it might try to sleep for negative time, as I experienced Once.
                    else if (DateTime.Now.AddSeconds(1) > checkInCompletionTime)
                    {
                        processingPassenger = false;
                    }
                    else if (processingPassenger)
                    {
                        // If it's processing a Passenger, and the processing time is not over, and there is no baggage to be brought to the sorter,
                        // the thread may actually sleep until processing time is over.

                        Thread.Sleep(checkInCompletionTime - DateTime.Now);
                    }
                    else if (CentralServer.FM.passengersQueue.Length == 0)
                    {
                        // There are no Passengers to process, it seems.
                        Thread.Sleep(DateTime.Now.AddSeconds(CentralServer.rnd.Next(10, 30) * CentralServer.timeScale) - DateTime.Now);
                    }
                }
            }
        }

        public bool ReadyToStop()
        {
            return !this.processingPassenger && BaggageManager.CountBaggage(this.LocalBaggageBuffer) == 0;
        }
        
        /// <summary>
        /// Gets and removes the passenger at the front of the passengersQueue (Once it acquires Lock and there are passengers queued) found in Flight Manager.
        /// </summary>
        /// <returns></returns>
        Passenger AcquirePassenger()
        {
            Passenger passenger = null;
            bool passengerAcquired = false;
            object _lock = CentralServer.FM.passengerQueueLock;
            while (!passengerAcquired)
            {
                if (CentralServer.FM.passengersQueue.Length > 0 && Monitor.IsEntered(_lock))
                {
                    try
                    {
                        passenger = CentralServer.FM.passengersQueue[0];
                        CentralServer.FM.passengersQueue = FlightManager.CutFrontPassenger(CentralServer.FM.passengersQueue);
                    }
                    finally
                    {
                        Monitor.PulseAll(_lock);
                        Monitor.Exit(_lock);
                        passengerAcquired = true;
                    }
                }
                else
                {
                    if (Monitor.IsEntered(_lock))
                    {
                        // It has the lock, but there is nothing to consume, so we shall relinguish _lock and wait for pulse.
                        Monitor.Wait(_lock);
                    }
                    else
                    {
                        Monitor.Enter(_lock);
                    }
                }
            }
            return passenger;
        }

        /// <summary>
        /// Generates this passenger's baggage and stores it locally in this.localBaggageBuffer.
        /// </summary>
        /// <param name="passenger"></param>
        void GenerateBaggage(Passenger passenger)
        {
            for (int i = 0; i < passenger.NumberOfBaggage; i++)
            {
                Baggage baggage = new Baggage(i, passenger.ID, passenger._Flight.ID);
                baggage.CheckedInTS = DateTime.Now;
                
                this.LocalBaggageBuffer = BaggageManager.AddBaggageToBack(baggage, this.LocalBaggageBuffer);
            }
        }

        /// <summary>
        ///  Empty local baggage buffer into shared buffer between THIS Specific Counter and the Sorter.
        /// </summary>
        void PushBaggage()
        {
            bool baggagePushed = false;
            object _lock = BaggageManager.counterConveyorBeltLocks[this.counterNumber];
            while (!baggagePushed)
            {
                int localBaggageCount = BaggageManager.CountBaggage(this.LocalBaggageBuffer);
                Thread.Sleep(Convert.ToInt32(30000 * CentralServer.timeScale));
                if (CentralServer.BM.CounterConveyorBelts[this.counterNumber].Length - BaggageManager.CountBaggage(CentralServer.BM.CounterConveyorBelts[this.counterNumber]) > localBaggageCount && Monitor.IsEntered(_lock))
                {
                    try
                    {
                        for (int i = 0; i < localBaggageCount; i++)
                        {
                            CentralServer.BM.CounterConveyorBelts[this.counterNumber] = BaggageManager.AddBaggageToBack(this.LocalBaggageBuffer[0], CentralServer.BM.CounterConveyorBelts[this.counterNumber]);
                            this.LocalBaggageBuffer = BaggageManager.MoveBaggagesForward(this.LocalBaggageBuffer);
                        }
                    }
                    finally
                    {
                        Monitor.PulseAll(_lock);
                        Monitor.Exit(_lock);
                        baggagePushed = true;
                    }
                }
                else
                {
                    if (Monitor.IsEntered(_lock))
                    {
                        // It has the lock, but there is not room for the produce (Baggage) in the buffer, so we shall relinguish and wait for pulse.
                        Monitor.Wait(_lock);
                    }
                    else
                    {
                        Monitor.Enter(_lock);
                    }
                }
            }
        }
    }
}
