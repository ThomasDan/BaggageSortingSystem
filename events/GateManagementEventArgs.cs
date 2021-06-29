using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BaggageSortingSystem.classes;

namespace BaggageSortingSystem.events
{
    public class GateManagementEventArgs : EventArgs
    {
        public Gate[] Gates { get; set; }

        public GateManagementEventArgs(Gate[] gates)
        {
            this.Gates = gates;
        }
    }
}
