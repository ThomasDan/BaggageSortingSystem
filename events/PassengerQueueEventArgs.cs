using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BaggageSortingSystem.events
{
    public class PassengerQueueEventArgs : EventArgs
    {
        public int PassengersQueued { get; set; }
        public int LatePassengers { get; set; }
        public PassengerQueueEventArgs()
        {
            this.PassengersQueued = CentralServer.FM.passengersQueue.Length;
            this.LatePassengers = CentralServer.latePassengers;
        }
    }
}
