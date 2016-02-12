using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MemMap
{
    public abstract class WBThrottle
    {
        public long cycles;
        public uint[] throttle_cycles_perproc;

        public WBThrottle()
        {
            throttle_cycles_perproc = new uint[Config.N];
        }

        public virtual void tick()
        {
            cycles++;

            //writeback stats
            for (int pid = 0; pid < throttle_cycles_perproc.Length; pid++) {
                if (throttle_cycles_perproc[pid] > 0)
                    throttle_cycles_perproc[pid]--;
            }
        }

        public abstract bool is_throttle(int pid);
    }
}
