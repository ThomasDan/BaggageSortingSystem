using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BaggageSortingSystem.classes;

namespace BaggageSortingSystem.events
{
    public class BaggageManagerEventArgs : EventArgs
    {
        public BaggageManager BM { get; set; }

        public BaggageManagerEventArgs(BaggageManager bM)
        {
            this.BM = bM;
        }
    }
}
