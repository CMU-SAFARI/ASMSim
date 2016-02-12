using System;
using System.Collections.Generic;
using System.Text;

using System.IO;
using System.Linq;

namespace MemMap
{
    public class PARBS : MemSched
    {
        //rank
        int[] rank;

        //batch
        uint marked_load;
        uint[] marked_max_load_per_proc;
        uint[] marked_total_load_per_proc;
        List<Req>[,] markable_q;

        public PARBS()
        {
            rank = new int[Config.N];
            marked_max_load_per_proc = new uint[Config.N];
            marked_total_load_per_proc = new uint[Config.N];
        }

        public override void initialize(){
            markable_q = new List<Req>[Config.N, meta_mctrl.get_bmax()];
            for (int p = 0; p < Config.N; p++) {
                for (int b = 0; b < meta_mctrl.get_bmax(); b++) {
                    markable_q[p, b] = new List<Req>(Config.sched.batch_cap);
                }
            }
        }

        public override void enqueue_req(Req req) { }
        public override void dequeue_req(Req req) 
        {
            if (!req.marked)
                return;

            Dbg.Assert(marked_load > 0);
            marked_load--;
        }

        public override Req better_req(Req req1, Req req2)
        {
            bool marked1 = req1.marked;
            bool marked2 = req2.marked;
            if (marked1 ^ marked2) {
                if (marked1) return req1;
                else return req2;
            }

            bool hit1 = is_row_hit(req1);
            bool hit2 = is_row_hit(req2);
            if (hit1 ^ hit2) {
                if (hit1) return req1;
                else return req2;
            }

            int rank1 = rank[req1.pid];
            int rank2 = rank[req2.pid];
            if (rank1 != rank2) {
                if (rank1 > rank2) return req1;
                else return req2;
            }

            if (req1.ts_arrival <= req2.ts_arrival) return req1;
            else return req2;
        }

        public override void tick()
        {
            base.tick();

            if (marked_load > 0 || meta_mctrl.get_rload() < 3)
                return;

            //new batch
            form_batch();
            assign_rank();
        }

        private void form_batch()
        {
            //initialization
            for (int b = 0; b < meta_mctrl.get_bmax(); b++) {
                for (int p = 0; p < Config.N; p++) {
                    markable_q[p, b].Clear();
                    marked_max_load_per_proc[p] = 0;
                    marked_total_load_per_proc[p] = 0;
                }
            }

            //demultiplex request buffer into separate processors
            for (int b = 0; b < meta_mctrl.get_bmax(); b++) {
                Bank bank = meta_mctrl.banks[b];
                List<Req> q = meta_mctrl.get_readq(bank);
                foreach (Req req in q) {
                    Dbg.Assert(!req.marked);
                    int p = req.pid;
                    markable_q[p, b].Add(req);
                }
            }

            //find earliest arriving requests for each processor at each bank
            for (uint b = 0; b < meta_mctrl.get_bmax(); b++) {
                for (int p = 0; p < Config.N; p++) {
                    markable_q[p, b].Sort((req1, req2) => req1.ts_arrival.CompareTo(req2.ts_arrival));
                }
            }

            //mark requests
            for (int p = 0; p < Config.N; p++) {
                for (int b = 0; b < meta_mctrl.get_bmax(); b++) {
                    List<Req> q = markable_q[p, b];
                    uint marked_cnt = 0;
                    foreach (Req req in q) {
                        if (marked_cnt == Config.sched.batch_cap)
                            break;
                        req.marked = true;
                        marked_cnt++;
                    }

                    marked_load += marked_cnt;
                    marked_total_load_per_proc[p] += marked_cnt;
                    if (marked_cnt > marked_max_load_per_proc[p])
                        marked_max_load_per_proc[p] = marked_cnt;
                }
            }
        }

        private void assign_rank()
        {
            int[] tids = new int[Config.N];
            for (int p = 0; p < Config.N; p++)
                tids[p] = p;

            Array.Sort(tids, sort_maxtot);
            for (int p = 0; p < Config.N; p++) {
                rank[p] = Array.IndexOf(tids, p);
            }
        }

        private int sort_maxtot(int tid1, int tid2)
        {
            //return 1 if first argument is "greater" (higher rank)
            uint max1 = marked_max_load_per_proc[tid1];
            uint max2 = marked_max_load_per_proc[tid2];
            uint tot1 = marked_total_load_per_proc[tid1];
            uint tot2 = marked_total_load_per_proc[tid2];

            if (max1 != max2) {
                if (max1 < max2) return 1;
                else return -1;
            }

            if (tot1 != tot2) {
                if (tot1 < tot2) return 1;
                else return -1;
            }

            return 0;
        }

        public override void issue_req(Req req)
        {
            count_streaks(req);
        }

    }
}
