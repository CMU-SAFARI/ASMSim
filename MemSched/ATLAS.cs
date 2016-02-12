
using System;
using System.Collections.Generic;
using System.Text;

using System.IO;

namespace MemMap
{

    public class ATLAS : MemSched
    {
        //rank
        int[] rank;

        //attained service
        uint[] service_bank_cnt;
        double[] curr_service;
        double[] service;

        //quantum
        int quantum_cycles_left;

        public ATLAS()
        {
            rank = new int[Config.N];
            service_bank_cnt = new uint[Config.N];
            curr_service = new double[Config.N];
            service = new double[Config.N];

            quantum_cycles_left = Config.sched.quantum_cycles;
        }

        public override void enqueue_req(Req req) { }
        public override void dequeue_req(Req req) { }

        public override Req better_req(Req req1, Req req2)
        {
            bool marked1 = req1.marked;
            bool marked2 = req2.marked;
            if (marked1 ^ marked2) {
                if (marked1) return req1;
                else return req2;
            }

            int rank1 = rank[req1.pid];
            int rank2 = rank[req2.pid];
            if (rank1 != rank2) {
                if (rank1 > rank2) return req1;
                else return req2;
            }

            bool hit1 = is_row_hit(req1);
            bool hit2 = is_row_hit(req2);
            if (hit1 ^ hit2) {
                if (hit1) return req1;
                else return req2;
            }
            if (req1.ts_arrival <= req2.ts_arrival) return req1;
            else return req2;
        }

        public override void tick()
        {
            base.tick();
            
            increment_service();
            mark_old_requests();

            if (quantum_cycles_left > 0) {
                quantum_cycles_left--;
                return;
            }

            //new quantum
            quantum_cycles_left = Config.sched.quantum_cycles;
            decay_service();
            assign_rank();
        }

        private void increment_service()
        {
            for (int p = 0; p < Config.N; p++)
                service_bank_cnt[p] = 0;

            //count banks
            foreach (Bank b in meta_mctrl.banks) {
                Req curr_req = meta_mctrl.get_curr_req(b);
                if (curr_req == null) continue;
                service_bank_cnt[curr_req.pid]++;
            }

            //update service
            for (int p = 0; p < Config.N; p++) {
                if (!Config.sched.service_overlap) {
                    curr_service[p] += service_bank_cnt[p];
                }
                else {
                    if (service_bank_cnt[p] > 0)
                        curr_service[p] += 1;
                }
            }
        }

        private void mark_old_requests()
        {
            foreach (MemCtrl mc in meta_mctrl.mctrls) {
                foreach (List<Req> read_q in mc.readqs) {
                    foreach (Req req in read_q) {
                        if (meta_mctrl.get_cycles() - req.ts_arrival > Config.sched.threshold_cycles) {
                            req.marked = true;
                        }
                    }
                }
            }
        }

        private void decay_service()
        {
            for (int p = 0; p < Config.N; p++) {
                if (Config.sched.use_weights != 0) {
                    curr_service[p] = curr_service[p] / Config.sched.weights[p];
                }

                service[p] = Config.sched.history_weight * service[p] + (1 - Config.sched.history_weight) * curr_service[p];
                curr_service[p] = 0;
            }
        }

        private void assign_rank()
        {
            int[] tids = new int[Config.N];
            for (int p = 0; p < Config.N; p++)
                tids[p] = p;

            Array.Sort(tids, sort);
            for (int p = 0; p < Config.N; p++) {
                rank[p] = Array.IndexOf(tids, p);
            }


        }

        private int sort(int tid1, int tid2)
        {
            if (service[tid1] != service[tid2]) {
                if (service[tid1] < service[tid2]) return 1;
                else return -1;
            }
            return 0;
        }
    }
}
