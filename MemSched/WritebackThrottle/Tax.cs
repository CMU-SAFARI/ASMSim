using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MemMap
{
    public class Tax : WBThrottle
    {
        public override bool is_throttle(int pid)
        {
            if (throttle_cycles_perproc[pid] > 0) {
                return true;
            }

            throttle_cycles_perproc[pid] = Config.sched.wbthrottle_cycles;
            return false;
        }
    }
}
