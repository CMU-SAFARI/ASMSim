using System;
using System.Collections.Generic;
using System.Text;

using System.IO;

namespace MemMap
{
    public class FRFCFS : MemSched
    {
  

        public override void enqueue_req(Req req) { }
        public override void dequeue_req(Req req) { }


        public override Req better_req(Req req1, Req req2)
        {
            bool hit1 = is_row_hit(req1);
            bool hit2 = is_row_hit(req2);
            if (hit1 ^ hit2) {
                if (hit1) return req1;
                else return req2;
            }
            if (req1.ts_arrival <= req2.ts_arrival) return req1;
            else return req2;
        }




  
    }

    public class FRFCFS_CAP : MemSched
    {
        //streak
        int[] streak;

        public override void initialize()
        {
            streak = new int[meta_mctrl.get_bmax()];
        }

        public override void enqueue_req(Req req) { }
        public override void dequeue_req(Req req) { }

        public override Req better_req(Req req1, Req req2)
        {
            bool hit1 = is_row_hit(req1);
            bool hit2 = is_row_hit(req2);

            uint bid1 = meta_mctrl.get_bid(req1);
            uint bid2 = meta_mctrl.get_bid(req2);
            bool capped1 = streak[bid1] >= Config.sched.row_hit_cap;
            bool capped2 = streak[bid2] >= Config.sched.row_hit_cap;

            hit1 = hit1 && (!capped1);
            hit2 = hit2 && (!capped2);

            if (hit1 ^ hit2) {
                if (hit1) return req1;
                else return req2;
            }
            if (req1.ts_arrival <= req2.ts_arrival) return req1;
            else return req2;
        }

        public override void issue_req(Req req)
        {
            if (req != null) {
                uint bid = meta_mctrl.get_bid(req);

                if (is_row_hit(req)) {
                    streak[bid] += 1;
                }
                else {
                    streak[bid] = 1;
                }
            }
        }
    }
}
