using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MemMap
{
    public class TaxRMPKI : WBThrottle
    {
        public override bool is_throttle(int pid)
        {
            if (throttle_cycles_perproc[pid] > 0) {
                return true;
            }

            ulong read_cnt = Stat.procs[pid].read_req.Count;
            ulong inst_cnt = Stat.procs[pid].ipc.Count;
            double rmpki = 1000 * ((double)read_cnt) / inst_cnt;

            if (rmpki > Config.sched.wbthrottle_rmpki_threshold) {
                throttle_cycles_perproc[pid] = Config.sched.wbthrottle_cycles;
            }
            return false;
        }
    }
}
