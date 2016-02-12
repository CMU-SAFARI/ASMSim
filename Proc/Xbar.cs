using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace MemMap
{
    public class Xbar
    {
        public long cycles;
        public List<Req> reqs;

        public Xbar(){
            reqs = new List<Req>(128);
        }

        public void enqueue(Req req)
        {
            reqs.Add(req);
        }

        public void tick()
        {
            cycles++;

            int sent = 0;
            for(int i = 0; i < reqs.Count; i++){
                Req req = reqs[i];
                if (cycles - req.ts_departure < Config.mctrl.xbar_latency) break;

                //send back to processor
                sent += 1;
                req.ts_departure = cycles;
                req.latency = (int)(req.ts_departure - req.ts_arrival);
                req.service_latency = (int)(req.ts_departure - req.ts_issue);
                Callback cb = req.callback;
                cb(req);
            }
            reqs.RemoveRange(0, sent);
        }
    }
}
