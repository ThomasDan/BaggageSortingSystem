using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace BaggageSortingSystem.classes
{
    abstract class BaggageManagementComponent
    {
        Thread thread;
        bool stop;
        bool killed;
        Baggage[] localBaggageBuffer;

        public Thread _Thread
        {
            get { return this.thread; }
            set { this.thread = value; }
        }
        public bool Stop
        {
            get { return this.stop; }
            set { this.stop = value; }
        }
        public bool Killed
        {
            get { return this.killed; }
            set { this.killed = value; }
        }
        public Baggage[] LocalBaggageBuffer
        {
            get { return this.localBaggageBuffer; }
            set { this.localBaggageBuffer = value; }
        }

        public BaggageManagementComponent(string name, int localBaggageBufferSize)
        {
            this.stop = false;
            this.killed = false;
            this.thread = new Thread(new ThreadStart(Run));
            this.thread.Name = name;
            this.localBaggageBuffer = new Baggage[localBaggageBufferSize];
        }

        public virtual void InitializeThread()
        {
            this.thread.Start();
        }

        public virtual void Run()
        {

        }
    }
}
