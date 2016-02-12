using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MemMap
{
    public class ThrottleWMPKI : WBThrottle
    {
        public override void tick()
        {
            base.tick();

            if (cycles % 1000 != 0)
                return;

            for (uint pid = 0; pid < Config.N; pid++) {
                ulong wb_cnt = Stat.procs[pid].write_req.Count;
                ulong inst_cnt = Stat.procs[pid].ipc.Count;
                double wmpki = 1000 * ((double)wb_cnt) / inst_cnt;

                if (wmpki > Config.sched.wbthrottle_wmpki_threshold) {
                    Sim.procs[pid].throttle_fraction = Config.sched.wbthrottle_fraction;
                }
                else {
                    Sim.procs[pid].throttle_fraction = 0;
                }
            }
        }

        public override bool is_throttle(int pid)
        {
            return false;
        }
    }
}
