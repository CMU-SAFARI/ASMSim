using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace MemMap
{
    public class RowHitFinder
    {
        MemCtrl mctrl;

        public RowHitFinder(MemCtrl mctrl)
        {
            this.mctrl = mctrl;
        }

        public Req find_best_req(List<Req> q)
        {
            if (q.Count == 0)
                return null;

            Req best_req = q[0];
            for (int i = 1; i < q.Count; i++) {
                best_req = better_req(best_req, q[i]);
            }

            if (is_row_hit(best_req))
                return best_req;

            return null;
        }

        public bool is_row_hit(Req req)
        {
            Dbg.Assert(mctrl.cid == req.addr.cid);

            Bank bank = mctrl.chan.ranks[req.addr.rid].banks[req.addr.bid];
            return bank.curr_rowid == (long)req.addr.rowid;
        }

        public Req better_req(Req req1, Req req2)
        {
            bool hit1 = is_row_hit(req1);
            bool hit2 = is_row_hit(req2);
            if (hit1 ^ hit2) {
                if (hit1) 
                    return req1;
                else 
                    return req2;
            }

            if (req1.ts_arrival <= req2.ts_arrival) 
                return req1;
            else 
                return req2;
        }
    }
}
