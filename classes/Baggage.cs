using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BaggageSortingSystem.classes
{
    public class Baggage
    {
        private int iD;
        private int passengerID;
        private int flightID;
        // TS is short for Time Stamp
        private DateTime checkedInTS;
        private DateTime sortingTS;
        private DateTime sortedTS;
        private DateTime gatedTS;

        public int ID
        {
            get { return this.iD; }
        }
        public int PassengerID
        {
            get { return this.passengerID; }
        }
        public int FlightID
        {
            // It is not impossible that it does not make it onto its original flight, and thus will need to get reassigned.
            get { return this.flightID; }
            set { this.flightID = value; }
        }
        public DateTime CheckedInTS
        {
            get { return this.checkedInTS; }
            set { this.checkedInTS = value; }
        }
        public DateTime SortingTS
        {
            get { return this.sortingTS; }
            set { this.sortingTS = value; }
        }
        public DateTime SortedTS
        {
            get { return this.sortedTS; }
            set { this.sortedTS = value; }
        }
        public DateTime GatedTS
        {
            get { return this.gatedTS; }
            set { this.gatedTS = value; }
        }

        public Baggage(int ID, int PassengerID, int FlightID)
        {
            this.iD = ID;
            this.passengerID = PassengerID;
            this.flightID = FlightID;
            this.checkedInTS = DateTime.Now;
        }

        public string ToString()
        {
            return "Baggage ID-Passenger-Flight: " + this.iD + "-" + this.passengerID + "-" + this.flightID;
        }
    }
}
