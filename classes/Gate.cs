using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using BaggageSortingSystem.classes;

namespace BaggageSortingSystem.classes
{
    /*
    Gates are responsible for:
        -Emptying their shared baggage conveyorbelt from the Sorter
        -Loading the locally stored baggage onto the appropriate flight
    */
    class Gate : BaggageManagementComponent
    {
        private int gateNumber;
        private Flight[] flights;

        public int GateNumber
        {
            get { return this.gateNumber; }
        }
        
        public Flight[] Flights
        {
            get { return this.flights; }
            set { this.flights = value; }
        }


        public Gate(int GateNumber) : base("Gate " + GateNumber, 1024)
        {
            this.gateNumber = GateNumber;
            this.flights = new Flight[0];
        }

        public string ToString()
        {
            string print = this._Thread.Name + ": is alive and not stopped? " + (!this.Stop && this._Thread.IsAlive) + ". Shared Storage: " + BaggageManager.CountBaggage(CentralServer.bM.GateConveyorBelts[this.gateNumber]) + "/" + CentralServer.bM.GateConveyorBelts[this.gateNumber].Length + ". Local Storage: " + BaggageManager.CountBaggage(this.LocalBaggageBuffer) + "/" + this.LocalBaggageBuffer.Length;

            for (int i = 0; i < this.flights.Length; i++)
            {
                print += "\n    " + this.flights[i].ToString();
            }
            
            return print;
        }


        public override void Run()
        {
            while (!this.Killed) 
            {
                if (this.Stop)
                {
                    // Here the thread will rest until told not to stop anymore.
                    Thread.Sleep(Convert.ToInt32(15000 * CentralServer.timeScale));
                }
                while ((!this.Stop && !this.Killed) || !this.ReadyToStop())
                {
                    if(CentralServer.bM.GateConveyorBelts[this.gateNumber][0] != null)
                    {
                        // Acquire Baggage
                        AcquireBaggage();
                    }

                    Thread.Sleep(DateTime.Now.AddMinutes(3 * CentralServer.timeScale) - DateTime.Now);

                    // Load Baggage onto Flight [TIMESTAMP] .. if it's not too late (5 minutes before departure at timeScale 1.0)
                    if(this.LocalBaggageBuffer[0] != null)
                    {
                        LoadBaggageOntoFlights();
                    }
                }
            }
        }

        public bool ReadyToStop()
        {
            return CentralServer.bM.GateConveyorBelts[this.gateNumber][0] == null && this.LocalBaggageBuffer[0] == null && this.flights.Length == 0;
        }

        /// <summary>
        /// Acquire Baggage from the shared Baggage Buffer between the Gate and the Sorter.
        /// </summary>
        void AcquireBaggage()
        {
            object _lock = BaggageManager.gateConveyorBeltLocks[this.gateNumber];
            bool baggageAcquired = false;
            int sharedBaggageAmount = BaggageManager.CountBaggage(CentralServer.bM.GateConveyorBelts[this.gateNumber]);
            while (!baggageAcquired)
            {
                if (Monitor.IsEntered(_lock))
                {
                    try
                    {
                        for (int i = 0; i < sharedBaggageAmount; i++)
                        {
                            this.LocalBaggageBuffer = BaggageManager.AddBaggageToBack(CentralServer.bM.GateConveyorBelts[this.gateNumber][0], this.LocalBaggageBuffer);
                            CentralServer.bM.GateConveyorBelts[this.gateNumber] = BaggageManager.MoveBaggagesForward(CentralServer.bM.GateConveyorBelts[this.gateNumber]);
                        }
                    }
                    finally
                    {
                        baggageAcquired = true;
                        Monitor.PulseAll(_lock);
                        Monitor.Exit(_lock);
                    }
                }
                else
                {
                    Monitor.Enter(_lock);
                }
            }
        }

        /// <summary>
        /// Loads and Timestamps each piece of baggage in the local buffer onto their respective flights.
        /// </summary>
        void LoadBaggageOntoFlights()
        {
            int amountOfBaggageLocally = BaggageManager.CountBaggage(this.LocalBaggageBuffer);
            for (int i = 0; i < amountOfBaggageLocally; i++)
            {
                for (int j = 0; j < this.flights.Length; j++)
                {
                    if (this.LocalBaggageBuffer[0].FlightID == this.flights[j].ID)
                    {
                        if (this.flights[j].DepartureTime > DateTime.Now.AddMinutes(5 * CentralServer.timeScale))
                        {
                            // Timestamp
                            this.LocalBaggageBuffer[0].GatedTS = DateTime.Now;
                            // Load onto plane
                            this.flights[j].AcquireBaggage(this.LocalBaggageBuffer[0]);
                            // Remove from local storage
                            this.LocalBaggageBuffer = BaggageManager.MoveBaggagesForward(this.LocalBaggageBuffer);
                        }
                        else
                        {
                            // Baggage did not make it on time and is Lost!
                            Console.WriteLine(this._Thread.Name + " lost baggage: " + this.LocalBaggageBuffer[0].ToString());
                            CentralServer.lateBaggage++;
                        }
                        break;
                    }
                }
            }
        }
    }
}
