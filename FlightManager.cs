using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using BaggageSortingSystem.classes;

namespace BaggageSortingSystem
{
    public class FlightManager
    {
        int nextFlightID;
        int nextPassengerID;
        bool stop;
        DateTime lastGeneratedSegmentEnd;
        Flight[] flightPlan;
        Thread thread;
        public Passenger[] passengersQueue = new Passenger[0];
        public object passengerQueueLock = new object();
        public Flight[] flightsRequiringGate = new Flight[0];
        public object flightsRequiringGateLock = new object();

        public int NextFlightID
        {
            get { return this.nextFlightID; }
            set { this.nextFlightID = value; }
            
        }
        public int NextPassengerID
        {
            get { return this.nextPassengerID; }
            set { this.nextPassengerID = value; }
        }
        public bool Stop
        {
            get { return this.stop; }
            set { this.stop = value; }
        }
        public DateTime LastGeneratedSegmentEnd
        {
            get { return this.lastGeneratedSegmentEnd; }
        }
        public Flight[] FlightPlan
        {
            get { return this.flightPlan; }
            set { this.flightPlan = value; }
        }
        public Thread _Thread
        {
            get { return this.thread; }
        }

        public FlightManager()
        {
            this.stop = false;
            this.nextFlightID = 0;
            this.nextPassengerID = 0;
            this.lastGeneratedSegmentEnd = DateTime.Now.AddMinutes(105 * CentralServer.timeScale);
            this.flightPlan = new Flight[0];
        }

        public string ToString()
        {
            return "###   Flight Manager:   ###\n  Flights Planned: " + this.flightPlan.Length + "\n  Flights requesting Gates: " + this.flightsRequiringGate.Length + " | Passengers in Queue: " + this.passengersQueue.Length;
        }

        public void InitializeThread()
        {
            this.thread = new Thread(new ThreadStart(this.ManageFlights));
            this.thread.Name = "The Flight Manager";
            this.thread.Start();
        }

        public void ManageFlights()
        {
            while (!this.stop)
            {
                while(this.lastGeneratedSegmentEnd < DateTime.Now.AddMinutes(240 * CentralServer.timeScale))
                {
                    // Generates random amounts of flights in sub-segment (1 Sub-segment being 1 hour at timeScale 1.0)
                    GenerateFlightsInSegment();

                    // Throw Flights to be Gated
                    for (int i = 0; i < flightPlan.Length; i++)
                    {
                        if(flightPlan[i].DepartureTime > lastGeneratedSegmentEnd)
                        {
                            // Set Flight up for getting a Gate
                            ReadyFlight(flightPlan[i]);
                            // Enqueue Passengers
                            EnqueuePassengers(flightPlan[i].PassengerList);
                        }
                    }

                    this.lastGeneratedSegmentEnd = this.lastGeneratedSegmentEnd.AddMinutes(60 * CentralServer.timeScale);
                    Thread.Sleep(Convert.ToInt32(10000 * CentralServer.timeScale));
                }
                
                // Checking once per 3 minutes seems good to me.
                Thread.Sleep(Convert.ToInt32(180000 * CentralServer.timeScale));
            }
        }

        void GenerateFlightsInSegment()
        {
            int flightsInSegment = CentralServer.rnd.Next((int)Math.Round(5 * CentralServer.busynessModifier), (int)Math.Round(10 * CentralServer.busynessModifier));

            Flight newFlight;
            DateTime departure;

            for (int i = 0; i < flightsInSegment; i++)
            {
                departure = this.lastGeneratedSegmentEnd.AddMinutes(CentralServer.rnd.Next(0, 60) * CentralServer.timeScale);
                newFlight = GenerateFlight(departure);
                this.nextFlightID++;
                newFlight.PassengerList = GeneratePassengers(newFlight);
                AddToFlightPlan(newFlight);
            }
            SortFlightPlanChronologically();
        }

        Flight GenerateFlight(DateTime departureTime)
        {
            return new Flight(this.nextFlightID, departureTime, new Passenger[0]);
        }

        Passenger[] GeneratePassengers(Flight flight)
        {
            Passenger[] passengers = new Passenger[CentralServer.rnd.Next((int)Math.Round(50 * CentralServer.busynessModifier), (int)Math.Round(301 * CentralServer.busynessModifier))];
            for (int i = 0; i < passengers.Length; i++)
            {
                passengers[i] = new Passenger(this.nextPassengerID, CentralServer.faker.Name.FullName(), flight);
                this.nextPassengerID++;
            }
            return passengers;
        }

        public void AddToFlightPlan(Flight flight)
        {
            Flight[] newPlan = new Flight[this.flightPlan.Length + 1];

            for (int i = 0; i < this.flightPlan.Length; i++)
            {
                newPlan[i] = this.flightPlan[i];
            }
            newPlan[newPlan.Length - 1] = flight;

            this.flightPlan = newPlan;
        }

        void SortFlightPlanChronologically()
        {
            for (int i = 0; i < this.flightPlan.Length; i++)
            {
                for (int j = 0; j < this.flightPlan.Length-1; j++)
                {
                    if(this.flightPlan[j].DepartureTime > this.flightPlan[j+1].DepartureTime)
                    {
                        Flight tempFlight = this.flightPlan[j];
                        this.flightPlan[j] = this.flightPlan[j + 1];
                        this.flightPlan[j + 1] = tempFlight;
                    }
                }
            }
        }

        void ReadyFlight(Flight flight)
        {
            bool flightReadied = false;
            while (!stop && !flightReadied)
            {
                if (Monitor.IsEntered(flightsRequiringGateLock))
                {
                    try
                    {
                        // Add Flight to the TOp of the FlightRequiringGate array
                        this.flightsRequiringGate = AddFlightToBack(flight, this.flightsRequiringGate);
                    }
                    finally
                    {
                        Monitor.Pulse(flightsRequiringGateLock);
                        Monitor.Exit(flightsRequiringGateLock);
                        flightReadied = true;
                    }
                }
                else
                {
                    Monitor.Enter(flightsRequiringGateLock);
                }
            }
        }

        /// <summary>
        /// Acquires Lock on passengerQueueLock and then adds all passengers to passengerQueue for the Counters to consume.
        /// </summary>
        /// <param name="passengers">Passengers to the added to the Queue</param>
        void EnqueuePassengers(Passenger[] passengers)
        {
            bool passengersEnqueued = false;
            while (!stop && !passengersEnqueued)
            {
                if (Monitor.IsEntered(passengerQueueLock))
                {
                    try
                    {
                        // Add Passengers to the TOp of the passengersQueue array
                        for (int i = 0; i < passengers.Length; i++)
                        {
                            // IF the counters are unable to keep up, then it could result in some passengers being late forever, potentially.
                            CentralServer.totalPassengerBaggage += passengers[i].NumberOfBaggage;
                            passengersQueue = AddPassengerRandomly(passengers[i], passengersQueue);
                        }
                    }
                    finally
                    {
                        Monitor.PulseAll(passengerQueueLock);
                        Monitor.Exit(passengerQueueLock);
                        passengersEnqueued = true;
                    }
                }
                else
                {
                    Monitor.Enter(passengerQueueLock);
                }
            }
        }

        #region Array Handling

        /// <summary>
        /// Increases the length of the Flight Array by 1, and puts a flight there.
        /// </summary>
        /// <param name="flight"></param>
        /// <param name="array"></param>
        /// <returns></returns>
        public static Flight[] AddFlightToBack(Flight flight, Flight[] array)
        {
            Flight[] arrayNew = new Flight[array.Length + 1];
            arrayNew[arrayNew.Length - 1] = flight;
            for (int i = 0; i < array.Length; i++)
            {
                arrayNew[i] = array[i];
            }
            return arrayNew;
        }

        /// <summary>
        /// Cuts out the bottom passenger by not including them in the new, one element shorter array.
        /// </summary>
        /// <param name="array"></param>
        /// <returns>A flight[] that is one shorter (missing element [0])</returns>
        public static Flight[] CutFrontFlight(Flight[] array, int offset)
        {
            Flight[] newArray = new Flight[array.Length - 1];
            for (int i = 1 + offset; i < array.Length; i++)
            {
                newArray[i - 1] = array[i];
            }
            return newArray;
        }

        /// <summary>
        /// Increases the length of the Passenger Array by 1, and puts a passenger there.
        /// </summary>
        /// <param name="passenger"></param>
        /// <param name="array"></param>
        /// <returns></returns>
        public static Passenger[] AddPassengerToBack(Passenger passenger, Passenger[] array)
        {
            Passenger[] arrayNew = new Passenger[array.Length + 1];
            arrayNew[arrayNew.Length - 1] = passenger;
            for (int i = 0; i < array.Length; i++)
            {
                arrayNew[i] = array[i];
            }
            return arrayNew;
        }

        /// <summary>
        /// Will Randomly assign a Passenger to the Queue.
        /// </summary>
        /// <param name="passenger"></param>
        /// <param name="array"></param>
        /// <returns></returns>
        public static Passenger[] AddPassengerRandomly(Passenger passenger, Passenger[] array)
        {
            Passenger[] arrayNew = new Passenger[array.Length + 1];
            int randomPos = CentralServer.rnd.Next(0, arrayNew.Length);
            arrayNew[randomPos] = passenger;
            bool passedNewPassenger = false;
            for (int i = 0; i < array.Length; i++)
            {
                if(passedNewPassenger)
                {
                    arrayNew[i + 1] = array[i];
                }
                else
                {
                    if (arrayNew[i] == null)
                    {
                        arrayNew[i] = array[i];
                    }
                    else
                    {
                        arrayNew[i + 1] = array[i];
                        passedNewPassenger = true;
                    }
                }
            }
            return arrayNew;
        }

        /// <summary>
        /// Cuts out the bottom passenger by not including them in the new, one element shorter array.
        /// </summary>
        /// <param name="array"></param>
        /// <returns>A passenger[] that is one shorter (missing element [0])</returns>
        public static Passenger[] CutFrontPassenger(Passenger[] array)
        {
            Passenger[] newArray = new Passenger[array.Length - 1];
            for (int i = 1; i < array.Length; i++)
            {
                newArray[i - 1] = array[i];
            }
            return newArray;
        }

        #endregion
    }
}
