using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using BaggageSortingSystem.classes;
using BaggageSortingSystem.events;

namespace BaggageSortingSystem
{
    public class BaggageManager
    {
        bool stop;
        Thread thread;
        private SortingSystem sorter;
        private int counterNumber;
        private Counter[] counters;
        private Dictionary<int, Baggage[]> counterConveyorBelts;
        public static Dictionary<int, object> counterConveyorBeltLocks = new Dictionary<int, object>();
        private int gateNumber;
        private Gate[] gates;
        private Dictionary<int, Baggage[]> gateConveyorBelts;
        public static Dictionary<int, object> gateConveyorBeltLocks = new Dictionary<int, object>();
        public Dictionary<int, int> flightsAtGatesMap = new Dictionary<int, int>();

        public event EventHandler CountersChanged;
        public event EventHandler GatesChanged;
        public event EventHandler FlightTakeOff;

        public bool Stop
        {
            get { return this.stop; }
            set { this.stop = value; }
        }
        public Thread _Thread
        {
            get { return this.thread; }
            set { this.thread = value; }
        }
        public SortingSystem Sorter
        {
            get { return this.sorter; }
        }
        public Counter[] Counters
        {
            get { return this.counters; }
        }
        public Gate[] Gates
        {
            get { return this.gates; }
        }
        public Dictionary<int, Baggage[]> CounterConveyorBelts
        {
            get { return this.counterConveyorBelts; }
            set { this.counterConveyorBelts = value; }
        }
        public Dictionary<int, Baggage[]> GateConveyorBelts
        {
            get { return this.gateConveyorBelts; }
            set { this.gateConveyorBelts = value; }
        }

        public BaggageManager()
        {
            this.stop = false;
            this.thread = new Thread(new ThreadStart(ManageBaggage));
            this.thread.Name = "Baggage Manager";
            this.sorter = new SortingSystem();
            this.counterNumber = 0;
            this.counters = new Counter[0];
            this.counterConveyorBelts = new Dictionary<int, Baggage[]>();
            this.gateNumber = 0;
            this.gates = new Gate[0];
            this.gateConveyorBelts = new Dictionary<int, Baggage[]>();
        }

        public string ToString()
        {
            string print = "\n###   Baggage Manager:   ###\n Counters working/alive/total: " + CountUnstoppedBMComponents(this.counters) + "/" + CountLivingBMComponents(this.counters) + "/" + this.counters.Length;
            /*for (int i = 0; i < this.counterNumber; i++)
            {
                print += "\n   " + this.counters[i].ToString();
            }//*/
            print += "\n " + this.sorter.ToString();

            print += "\n Gates working/alive/total: " + CountUnstoppedBMComponents(this.gates) + "/" + CountLivingBMComponents(this.gates) + "/" + this.gates.Length;
            /*for (int i = 0; i < this.gateNumber; i++)
            {
                print += "\n  " + this.gates[i].ToString();
            }//*/
            return print;
        }

        public void InitializeThread()
        {
            this.thread.Start();
        }

        private void ManageBaggage()
        {
            CreateCounter();
            CreateCounter();
            this.sorter.InitializeThread();

            // Counter Efficiency is meant to make slow adjustments to how many Counters are open, based on how well they are keeping up.
            // 0 means they're doing fine, and no more or less are required.
            // 1-2 means open one more counter, 3 means two more counters.
            // Opposite for Negative.
            int counterEfficiency = 2;
            while (!this.stop)
            {
                // Create/Reopen and assign Gates as required by Flights, update FlightGateMap
                if (CentralServer.FM.flightsRequiringGate.Length > 0)
                {
                    AssignGates();
                }

                // Remove flights which have flown (Departure Time has passed) from gates.. known to cause exception errors! Needs Monitor Locks.
                for (int i = 0; i < this.gates.Length; i++)
                {
                    for (int j = 0; j < this.gates[i].Flights.Length; j++)
                    {
                        if (this.gates[i].Flights[j].DepartureTime < DateTime.Now)
                        {
                            Console.WriteLine("Now taking off: " + this.gates[i].Flights[j].ToString());
                            this.gates[i].Flights = FlightManager.CutFrontFlight(this.gates[i].Flights, j);
                            FlightTakeOffEvented();
                            GateChangeEvented();
                        }
                    }
                }

                // Close Gates which have no upcoming flights.
                for (int i = 0; i < this.gates.Length; i++)
                {
                    if (!gates[i].Stop && gates[i].Flights.Length == 0)
                    {
                        gates[i].Stop = true;
                        GateChangeEvented();
                    }
                }


                // Create/Reopen Counters, to ensure that the queue doesn't grow beyond a certain size.. Hopefully!
                counterEfficiency = DetermineCounterEfficiency(counterEfficiency);
                int unstoppedCounters = CountUnstoppedBMComponents(this.counters);
                int passengersPerCounter = Convert.ToInt32((CentralServer.FM.passengersQueue.Length == 0 ? 1 : CentralServer.FM.passengersQueue.Length) / (unstoppedCounters == 0 ? 1 : unstoppedCounters));

                // We never want less than 10 passengers per counter.
                if (this.counters.Length == 0 || counterEfficiency == 3 && passengersPerCounter > 7)
                {
                    // Open and reopen Counters, as required
                    OpenCounters(1);
                }
                // We never wanted less than 2 counters open.
                else if((passengersPerCounter < 7 || counterEfficiency == -3) && unstoppedCounters > 2)
                {
                    CloseCounters(1);
                }

                Thread.Sleep(Convert.ToInt32(5000 * CentralServer.timeScale));
            }
            KillAll();
        }

        private void GateChangeEvented()
        {
            GatesChanged?.Invoke(this, new GateManagementEventArgs(this.gates));
        }

        private void CounterChangeEvented()
        {
            CountersChanged?.Invoke(this, new CounterManagementEventArgs(this.counters));
        }

        private void FlightTakeOffEvented()
        {
            FlightTakeOff?.Invoke(this, new FlightDepartureEventArgs());
        }

        private void AssignGates()
        {
            // Acquire all flights requiring gates
            Flight[] flightsToBeAssignedGates = AcquireAllFlightsRequiringGate();

            // assign those flights to gates. Update FlightGateMap
            for (int i = 0; i < flightsToBeAssignedGates.Length; i++)
            {
                // Find an available Gate:
                Gate gate = FindAvailableGate(flightsToBeAssignedGates[i]);

                // If gate == null, then no gate with available time span for this flight was found, therefore we will have to reactivate a gate, or create a new gate.
                if (gate == null)
                {
                    gate = AcquireStoppedDeadGate();

                    if (gate != null)
                    {
                        // Reactivate dead gate
                        gate.Stop = false;
                    }
                    else
                    {
                        // There are no non-stopped or dead gates available, ergo we must create a new Gate to facilitate the flight
                        CreateGate();
                        gate = this.gates[this.gates.Length - 1];
                    }
                }

                // Add Flight to available Gate:
                gate.Flights = FlightManager.AddFlightToBack(flightsToBeAssignedGates[i], gate.Flights);
                // Save gate to gates:
                for (int j = 0; j < this.gates.Length; j++)
                {
                    if (this.gates[j].GateNumber == gate.GateNumber)
                    {
                        this.gates[j] = gate;
                        GateChangeEvented();
                        break;
                    }
                }
                // Map this flightID to this gateNumber by Updating FlightsAtGatesMap:
                this.flightsAtGatesMap.Add(flightsToBeAssignedGates[i].ID, gate.GateNumber);
            }
        }

        /// <summary>
        /// Goes through all the steps required to create and initialize a counter.
        /// </summary>
        private void CreateCounter()
        {
            Counter[] newCounters = new Counter[this.counters.Length + 1];

            for (int i = 0; i < this.counters.Length; i++)
            {
                newCounters[i] = this.counters[i];
            }


            Counter counter = new Counter(this.counterNumber);
            this.counterNumber++;

            this.counterConveyorBelts.Add(counter.CounterNumber, new Baggage[16]);
            counterConveyorBeltLocks.Add(counter.CounterNumber, new object());

            this.counters = newCounters;
            this.counters[counters.Length - 1] = counter;
            this.counters[counters.Length - 1].InitializeThread();
            CounterChangeEvented();
        }

        /// <summary>
        /// Goes through all the steps required to create and initialize a Gate.
        /// </summary>
        private void CreateGate()
        {
            Gate[] newGates = new Gate[this.gates.Length + 1];

            for (int i = 0; i < this.gates.Length; i++)
            {
                newGates[i] = this.gates[i];
            }


            Gate gate = new Gate(this.gateNumber);
            this.gateNumber++;

            this.gateConveyorBelts.Add(gate.GateNumber, new Baggage[512]);
            gateConveyorBeltLocks.Add(gate.GateNumber, new object());

            this.gates = newGates;
            this.gates[gates.Length - 1] = gate;
            this.gates[gates.Length - 1].InitializeThread();
            GateChangeEvented();
        }

        /// <summary>
        /// Tell all Baggage Management Components to Stop.
        /// </summary>
        void KillAll()
        {
            // Kill all counters
            for (int i = 0; i < this.counters.Length; i++)
            {
                this.counters[i].Stop = true;
                this.counters[i].Killed = true;
            }

            // Kill all gates
            for (int i = 0; i < this.gates.Length; i++)
            {
                this.gates[i].Stop = true;
                this.gates[i].Killed = true;
            }

            // The sorter may still have a job to do if any of the other threads are alive.
            while (CountLivingBMComponents(this.counters) > 0 || CountLivingBMComponents(this.gates) > 0)
            {
                Thread.Sleep(1000);
            }
            // Kill Sorter
            this.sorter.Stop = true;
            this.sorter.Killed = true;
        }


        void OpenCounters(int amount)
        {
            int currentClosedCounters = this.counters.Length - CountUnstoppedBMComponents(this.counters);
            // Reopen
            for (int i = 0; i < currentClosedCounters; i++)
            {
                if(amount == 0)
                {
                    break;
                }
                for (int j = 0; j < this.counters.Length; j++)
                {
                    if (this.counters[j].Stop) //  && this.counters[j].ReadyToStop() - If it was going to stop, then nevermind!
                    {
                        this.counters[j].Stop = false;
                        CounterChangeEvented();
                        amount--;
                        break;
                    }
                }
            }

            // If need, create more
            for (int i = 0; i < amount; i++)
            {
                CreateCounter();
            }
        }


        void CloseCounters(int amount)
        {
            for (int i = 0; i < this.counters.Length; i++)
            {
                if (!this.counters[i].Stop)
                {
                    this.counters[i].Stop = true;
                    CounterChangeEvented();
                    amount--;
                    if (amount == 0)
                    {
                        break;
                    }
                }
            }
        }


        int CountUnstoppedBMComponents(BaggageManagementComponent[] components)
        {
            int count = 0;
            for (int i = 0; i < components.Length; i++)
            {
                if (!components[i].Stop)
                {
                    count++;
                }
            }
            return count;
        }


        int CountLivingBMComponents(BaggageManagementComponent[] components)
        {
            int count = 0;
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i]._Thread.IsAlive)
                {
                    count++;
                }
            }
            return count;
        }


        private int DetermineCounterEfficiency(int efi)
        {
            int passengerQueued = CentralServer.FM.passengersQueue.Length;
            if (efi > 0)
            {
                if(efi == 3)
                {
                    efi = 0;
                }
                else
                {
                    if(passengerQueued > 50)
                    {
                        efi++;
                    }
                    else if(passengerQueued < 50)
                    {
                        efi--;
                    }
                }
            }
            else if(efi == 0)
            {
                if(passengerQueued < 10)
                {
                    efi--;
                }
                else if (passengerQueued > 50)
                {
                    efi++;
                }
            }
            else
            {
                if(efi == -3)
                {
                    efi = 0;
                }
                else
                {
                    if(passengerQueued > 50)
                    {
                        efi++;
                    }
                    else if(passengerQueued < 10)
                    {
                        efi--;
                    }
                }
            }

            return efi;
        }

        /// <summary>
        /// Acquires all the flights requiring gates from FlightManager
        /// </summary>
        /// <returns></returns>
        Flight[] AcquireAllFlightsRequiringGate()
        {
            Flight[] flightsToBeAssignedGates = null;
            bool flightsAcquired = false;
            object _lock = CentralServer.FM.flightsRequiringGateLock;

            while (!flightsAcquired)
            {
                if (Monitor.IsEntered(_lock))
                {
                    try
                    {
                        flightsToBeAssignedGates = new Flight[CentralServer.FM.flightsRequiringGate.Length];
                        for (int i = 0; i < flightsToBeAssignedGates.Length; i++)
                        {
                            flightsToBeAssignedGates[i] = CentralServer.FM.flightsRequiringGate[i];
                        }
                        CentralServer.FM.flightsRequiringGate = new Flight[0];
                    }
                    finally
                    {
                        Monitor.PulseAll(_lock);
                        Monitor.Exit(_lock);
                        flightsAcquired = true;
                    }
                }
                else
                {
                    Monitor.Enter(CentralServer.FM.flightsRequiringGateLock);
                }
            }
            return flightsToBeAssignedGates;
        }

        /// <summary>
        /// Finds an available Gate and sends it back. If there are none available, it returns null.
        /// </summary>
        /// <param name="flight">Flight to find a gate for.</param>
        /// <returns>A Gate which has room for the Flight, or null</returns>
        Gate FindAvailableGate(Flight flight)
        {
            Gate gate = null;

            // For each flight, check each gate
            for (int j = 0; j < this.gates.Length; j++)
            {
                if (!this.gates[j].Stop)
                {
                    bool gatePotential = true;
                    // Check if any of the DepartureTimes of the flgihts of the gate conflict with the new flight
                    for (int k = 0; k < this.gates[j].Flights.Length; k++)
                    {
                        // There is a conflict: if there's less than one hour (at timeScale 1.0) between them.
                        if (this.gates[j].Flights[k].DepartureTime >= flight.DepartureTime.AddMinutes(-60 * CentralServer.timeScale) && this.gates[j].Flights[k].DepartureTime.AddMinutes(-60 * CentralServer.timeScale) <= flight.DepartureTime)
                        {
                            gatePotential = false;
                            break;
                        }
                    }
                    // If the gate has not lost its potential for this flight, it will be the gate we are looking for!
                    if (gatePotential)
                    {
                        gate = this.gates[j];
                        break;
                    }
                }
            }
            
            return gate;
        }

        /// <summary>
        /// Acquires a stopped and dead gate, or returns null.
        /// </summary>
        /// <returns>A gate with a dead thread, or null</returns>
        Gate AcquireStoppedDeadGate()
        {
            Gate deadGate = null;
            for (int i = 0; i < this.gates.Length; i++)
            {
                if(this.gates[i].Stop && this.gates[i]._Thread.IsAlive)
                {
                    deadGate = this.gates[i];
                }
            }
            return deadGate;
        }



        #region Array Handling - Unlike Flight and Passenger queues, all Baggage arrays will stay a Fixed size.

        /// <summary>
        /// Counts how many positions filled with Baggage there are in the array.
        /// </summary>
        /// <param name="baggages"></param>
        /// <returns></returns>
        public static int CountBaggage(Baggage[] baggages)
        {
            int count = 0;
            for (int i = 0; i < baggages.Length; i++)
            {
                if (baggages[i] != null)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// Finds the first unused spot in the array, and puts the baggage there. MAKE CERTAIN THAT THERE IS SPACE FOR IT FIRST, OR THE BAGGAGE IS LOST.
        /// </summary>
        /// <param name="baggage">Baggage to be inserted.</param>
        /// <param name="array">Array for the baggage to be inserted into.</param>
        /// <returns>Array now with the Baggage inserted at the lowest positional spot of none.</returns>
        public static Baggage[] AddBaggageToBack(Baggage baggage, Baggage[] array)
        {
            for (int i = 0; i < array.Length; i++)
            {
                if(array[i] == null)
                {
                    array[i] = baggage;
                    break;
                }
            }
            return array;
        }

        /// <summary>
        /// Overwrites position 0 with position 1, position 1 with position 2... 
        /// </summary>
        /// <param name="array"></param>
        /// <param name="offset">In some cases, the baggage at position 0 or more could not be processed, and thus should be spared the purge.</param>
        /// <returns></returns>
        public static Baggage[] MoveBaggagesForward(Baggage[] array, int offset = 0)
        {
            for (int i = 1 + offset; i < array.Length; i++)
            {
                array[i - 1] = array[i];
            }
            return array;
        }

        #endregion
    }
}
