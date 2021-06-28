using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using BaggageSortingSystem.classes;
using System.Diagnostics;

namespace BaggageSortingSystem
{
    public class Program
    {
        static void Main(string[] args)
        {
            
            Debug.WriteLine("TEST FROM BaggageSortingSystem HELLLO???");

            bool stop = false;
            
            Thread.Sleep(500);
            TestPrintFlightPlan();

            while (!stop)
            {
                ConsoleKey choice = Console.ReadKey(true).Key;
                if(choice == ConsoleKey.Escape)
                {
                    stop = true;
                    Console.WriteLine("\n#######################################\n#####          STOPPING           #####\n#######################################\n");
                }
                Console.WriteLine(CentralServer.ToString());
            }

            CentralServer.stop = true;
            Thread.Sleep(10000);
            Console.WriteLine(CentralServer.BM.ToString());

            Console.ReadLine();
        }

        #region For Testing Purposes!
        private static void TestPrintFlightPlan()
        {
            for (int i = 0; i < CentralServer.FM.FlightPlan.Length; i++)
            {
                Flight flight = CentralServer.FM.FlightPlan[i];

                Console.WriteLine(flight.ToString() + "\n");
                /*
                for (int j = 0; j < flight.PassengerList.Length; j++)
                {
                    Passenger passenger = flight.PassengerList[j];
                    Console.Write(" -" + passenger.ToString() + (j % 2 == 0 ? " " : "\n    "));
                }
                Console.WriteLine("\n");
                //*/
            }
        }

        #endregion
    }
}
