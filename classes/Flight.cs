using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BaggageSortingSystem.classes
{
    class Flight
    {
        private int iD;
        private DateTime departureTime;
        private Passenger[] passengerList;
        private Baggage[] acquiredBaggage;

        public int ID
        {
            get { return this.iD; }
        }
        public DateTime DepartureTime
        {
            get { return this.departureTime; }
            set { this.departureTime = value; }
        }
        public Passenger[] PassengerList
        {
            get { return this.passengerList; }
            set { this.passengerList = value; }
        }
        public Baggage[] AcquiredBaggage
        {
            get { return this.acquiredBaggage; }
            set { this.acquiredBaggage = value; }
        }

        public Flight(int ID, DateTime DepartureTime, Passenger[] passengers)
        {
            this.iD = ID;
            this.departureTime = DepartureTime;
            this.passengerList = passengers;
            this.acquiredBaggage = new Baggage[0];
        }

        public string ToString()
        {
            return "Flight " + this.iD + " with " + this.passengerList.Length + " passengers, " + this.acquiredBaggage.Length + " baggage loaded, " + (DateTime.Now < this.departureTime ? "takes":"took") + " off at " + this.departureTime.ToString("HH:mm:ss.fff dd/MM-yy");
        }

        public void AcquireBaggage(Baggage baggage)
        {
            Baggage[] arrayNew = new Baggage[this.acquiredBaggage.Length + 1];
            arrayNew[arrayNew.Length - 1] = baggage;
            for (int i = 0; i < this.acquiredBaggage.Length; i++)
            {
                arrayNew[i] = this.acquiredBaggage[i];
            }
            this.acquiredBaggage = arrayNew;
        }
    }
}
