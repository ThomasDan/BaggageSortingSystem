using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BaggageSortingSystem.classes;

namespace BaggageSortingSystem.events
{
    public class CounterManagementEventArgs : EventArgs
    {
        public Counter[] Counters{ get; set; }
        public CounterManagementEventArgs(Counter[] counters)
        {
            this.Counters = counters;
        }
    }
}
