using System;
using System.Collections.Generic;
using System.Text;

using System.IO;

namespace MemMap
{

    public class STFM : MemSched
    {
        //rank
        int[] rank;
        int[] t_alone;
        int[] t_shared;
        int[] t_interference_bus;
        int[] t_interference_bank;
        int[] t_interference_rhit;
        int[] t_interference;

        //blp
        double[] blp;
        uint[] blp_sample_sum;
        uint[] blp_sample_cnt;

        ulong[] prev_cache_miss;
        ulong[] prev_inst_cnt;
        double[] slowdown;

        bool[] set_bus_int;
        bool[] set_bank_int;


        //quantum
        int quantum_cycles_left;

        public STFM()
        {
            t_alone = new int[Config.N];
            t_shared = new int[Config.N];
            t_interference = new int[Config.N];
            t_interference_bus = new int[Config.N];
            t_interference_bank = new int[Config.N];
            t_interference_rhit = new int[Config.N];
           
            prev_cache_miss = new ulong[Config.N];
            prev_inst_cnt = new ulong[Config.N];

            blp = new double[Config.N];
            blp_sample_sum = new uint[Config.N];
            blp_sample_cnt = new uint[Config.N];
            slowdown = new double[Config.N];
            set_bus_int = new bool[Config.N];
            set_bank_int = new bool[Config.N];
            rank = new int[Config.N];
 

            quantum_cycles_left = Config.sched.quantum_cycles;
        }

        public override void enqueue_req(Req req) { }
        public override void dequeue_req(Req req) { }

        public override Req better_req(Req req1, Req req2)
        {
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
            
             
            increment_interference();
            mark_old_requests();

            //blp
            if (meta_mctrl.get_cycles() % 1000 == 0) {
                sample_blp();
            }


            if (quantum_cycles_left > 0) {
                quantum_cycles_left--;
                return;
            }

            //new quantum
            quantum_cycles_left = Config.sched.quantum_cycles;
            collect_stats();
           
            compute_slowdown();
            rank_by_slowdown();
            print_slowdown_periodic();
            clear_interference();

        }


        private void sample_blp()
        {
//            blp_sample_cnt++;
            for (uint p = 0; p < Config.N; p++) {
                uint curr_blp = 0;
                for (uint b = 0; b < meta_mctrl.get_bmax(); b++) {
                    if (meta_mctrl.get_load_per_procbank(p, b) > 0) {
                        curr_blp++;
                    }
                }
                if (curr_blp > 0) blp_sample_cnt[p] += 1;
                blp_sample_sum[p] += curr_blp;
            }
        }
 

        private void increment_interference()
        {
            for (int p = 0; p < Config.N; p ++)
            {
                set_bank_int[p] = false;
                set_bus_int[p] = false;
            }
            foreach (Bank b in meta_mctrl.banks) {
                Req curr_req = meta_mctrl.get_curr_req(b);
                if (curr_req == null) continue;
                int curr_req_proc = curr_req.pid;
                for (int p = 0; p < Config.N; p ++)
                {
                    if (curr_req_proc != p && meta_mctrl.proc_req_in_queue(p, b))
                    {
                        set_bank_int[p] = true;
                        t_interference_bank[p] ++;
                    }
                }
            }

        }


        private void compute_slowdown()
        {
            for (int p = 0; p < Config.N; p ++)
            {
                t_shared[p] = (int)Sim.procs[p].get_stall_shared_delta();
                t_interference[p] = (int)(t_interference_bus[p] + (t_interference_bank[p]/(blp[p])) + (t_interference_rhit[p]/(blp[p])));

                t_alone[p] = t_shared[p] - t_interference[p];
                slowdown[p] = (double)t_alone[p]/(double)t_shared[p];
                
            }
        }

        private void rank_by_slowdown()
        {
            double max_slowdown = 0;
            int max_rank_app = 100;
            for (int cur_proc = 0; cur_proc < Config.N; cur_proc ++)
            {
                rank[cur_proc] = 0;
                double slowdown_proc = 1/slowdown[cur_proc];
                if (slowdown_proc > 1.10 && slowdown_proc > max_slowdown)
                {
                    max_rank_app = cur_proc;
                    max_slowdown = slowdown_proc;
                }
            }
            if (max_rank_app != 100) rank[max_rank_app] = 100; 
        }

        public void print_slowdown_periodic()
        {
            for (int cur_proc = 0; cur_proc < Config.N; cur_proc ++)
            {
                Sim.periodic_writer_service.WriteLine(" Proc " + cur_proc + " " + slowdown[cur_proc]);
                Sim.periodic_writer_service.Flush();

            }
              
        }

        private void clear_interference()
        {
            for (int p = 0; p < Config.N; p ++)
            {
                t_interference_bus[p] = 0;
                t_interference_bank[p] = 0;
                t_interference_rhit[p] = 0;
                t_shared[p] = 0;
            }

        }



        public void collect_stats()
        {
            for (int p = 0; p < Config.N; p++) {
                ulong cache_miss;
                cache_miss = Stat.procs[p].req.Count;


                prev_cache_miss[p] = cache_miss;

                Console.Write("prev miss " + prev_cache_miss[p] + "\n");
                ulong inst_cnt = Stat.procs[p].ipc.Count;
                prev_inst_cnt[p] = inst_cnt;

                //blp
                double curr_blp = ((double)blp_sample_sum[p]) / blp_sample_cnt[p];
                blp[p] = curr_blp;
                blp_sample_sum[p] = 0;
                blp_sample_cnt[p] = 0;

            }

        } 



        public override void count_queueing(Cmd cmd)
        {
            if (cmd == null) return;
            Req req = cmd.req;

            MemCtrl mctrl = meta_mctrl.get_mctrl(req);
            ulong shadow_rowid = mctrl.shadow_rowid_per_procrankbank[req.pid, req.addr.rid, req.addr.bid];
            ulong rowid = mctrl.rowid_per_procrankbank[req.addr.rid, req.addr.bid];
            if (shadow_rowid == req.addr.rowid && rowid != req.addr.rowid) {
               t_interference_rhit[req.pid] += 150;
            } 
        }

        public override void bus_interference_count(Cmd cmd)
        {
            if (cmd == null) return;
            Req req = cmd.req;


            for (int p = 0; p < Config.N; p ++)
            {
                if (set_bank_int[p] == false && p != req.pid && meta_mctrl.proc_req_in_queue(p))
                {
                    set_bus_int[p] = true;
                    t_interference_bus[p] ++;
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



    }
}
