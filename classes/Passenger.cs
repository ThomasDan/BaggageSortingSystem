using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BaggageSortingSystem.classes
{
    class Passenger
    {
        private int iD;
        private string name;
        private int numberOfBaggage;
        private Flight flight;

        public int ID
        {
            get { return this.iD; }
        }
        public string Name
        {
            get { return this.name; }
        }
        public int NumberOfBaggage
        {
            get { return this.numberOfBaggage; }
        }
        public Flight _Flight
        {
            // It is not impossible that this passenger does not make it onto its original flight, and thus will need to get reassigned.
            get { return this.flight; }
            set { this.flight = value; }
        }

        public Passenger(int ID, string Name, Flight _Flight)
        {
            this.iD = ID;
            this.name = Name;
            this.numberOfBaggage = CentralServer.rnd.Next(0, 4);
            this.flight = _Flight;
        }

        public string ToString()
        {
            return this.name + "(#" + this.iD + "): " + this.numberOfBaggage + " piece" + (this.numberOfBaggage == 1 ? "" : "s") + " of baggage.";
        }
    }
}

