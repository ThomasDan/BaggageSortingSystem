using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using BaggageSortingSystem.classes;
using Bogus;

namespace BaggageSortingSystem
{
    public static class CentralServer
    {
        public static bool stop;
        public static Random rnd = new Random();

        // Faker from the Bogus Nuget Package is used to generate random data. Notably Names for Passengers.
        public static Faker faker = new Faker();

        // The lower it is, the shorter the times between events will be.
        // I.e. Checking In takes 1.5 to 5.5 minutes: (rnd.Next(90, 240) + (30 * passenger.numberOfBaggage)) * timeScale
        /*
         At timeScale 1.0:
            Flight Manager: Will check every 3 minutes if there's a need to generate new Flights. It will generate 4-5 hours into the future.
            BaggageManager: Sleeps for 2 minutes per cycle.
            Counter: Only ALlows Passengers to check in 30 minutes before their flight.
            Gate: Only allows baggage to be loaded onto the flight if it's there 5 minutes before the departuretime. Sleeps for 3 minute per cycle.
        */
        public static double timeScale = 0.025; // One hour is just 1½ minutes at 0.025

        // The higher it is, the more flights are generated per time segment, and more passengers are generated per flight.
        /*
         At Busyness 1.0:
            Flight manager: Each hour there are 5-10 flights (10-20 at 2.0 BusynessMod)
                    Every Flight has 50-300 passengers(100-600 at 2.0 busynessMod)
         */
        public static double busynessModifier = 0.5;
        public static int totalPassengerBaggage = 0;
        public static volatile int latePassengers = 0;
        public static volatile int lateBaggage = 0;
        private static BaggageManager bM = new BaggageManager();
        private static FlightManager fM = new FlightManager();
        public static FlightManager FM
        {
            get { return fM; }
            set { fM = value; }
        }
        public static BaggageManager BM
        {
            get { return bM; }
            set { bM = value; }
        }
        public static Thread csThread = new Thread(new ThreadStart(RunCentralServer));

        public static void RunCentralServer()
        {
            stop = false;
            InitializeManagers();
            
            while (!stop)
            {
                Thread.Sleep(1000);
            }
            fM.Stop = true;
            Thread.Sleep(Convert.ToInt32(5000 * timeScale));
            bM.Stop = true;
        }
        public static string ToString()
        {
            return "\n-------------------------------------------\n" + bM.ToString() + "\n\n" + fM.ToString() +  "\n\n###   Lost and Late:   ###\n Baggage: " + lateBaggage + "\n Passengers who didn't get to checkin in time: " + latePassengers + "\n-------------------------------------------";
        }

        public static void InitializeManagers()
        {
            fM.InitializeThread();
            bM.InitializeThread();
        }
    }
}
