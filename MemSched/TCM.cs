using System;
using System.Collections.Generic;
using System.Text;

using System.IO;
using System.Linq;

namespace MemMap
{
    public class TCM : MemSched
    {
        //rank
        int[] rank;
        int[] use_rank;

        //attained service
        double[] service;
        double[] curr_service;
        uint[] service_bank_cnt;

        //mpki
        double[] mpki;
        ulong[] prev_cache_miss;
        ulong[] prev_inst_cnt;

        //rbl
        double[] rbl;
        ulong[] shadow_row_hits;
        double rbl_diff;

        //blp
        double[] blp;
        uint[] blp_sample_sum;
        uint blp_sample_cnt;
        double blp_diff;

        //quantum
        int quantum_cnt;
        int quantum_cycles_left;

        //shuffle
        int[] nice;
        int shuffle_cnt;
        int shuffle_cycles_left;

        //shuffle
        Random rand = new Random(0);
        public enum ShuffleAlgo
        {
            Naive,
            Random,
            Hanoi,
            ControlledRandom
        }

        //cluster sizes
        int icluster_size;

        public TCM()
        {
            rank = new int[Config.N];

            service = new double[Config.N];
            curr_service = new double[Config.N];
            service_bank_cnt = new uint[Config.N];

            mpki = new double[Config.N];
            prev_cache_miss = new ulong[Config.N];
            prev_inst_cnt = new ulong[Config.N];

            rbl = new double[Config.N];
            shadow_row_hits = new ulong[Config.N];

            blp = new double[Config.N];
            blp_sample_sum = new uint[Config.N];

            quantum_cycles_left = Config.sched.quantum_cycles;

            nice = new int[Config.N];
            shuffle_cycles_left = Config.sched.shuffle_cycles;
            use_rank = new int[Config.N];
        }

        public override void enqueue_req(Req req) { }
        public override void dequeue_req(Req req) { }

        public override Req better_req(Req req1, Req req2)
        {
            int rank1 = rank[req1.pid];
            int rank2 = rank[req2.pid];
            if (Config.sched.same_lowint_rank)
            {
                if (rank1 > icluster_size) rank1 = 100;
                if (rank2 > icluster_size) rank2 = 100;
            }
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

            //service
            increment_service();

            //blp
            if (meta_mctrl.get_cycles() % 1000 == 0) {
                sample_blp();
            }


            //shuffle
            if (shuffle_cycles_left > 0) {
                shuffle_cycles_left--;
            }
            else if (quantum_cnt != 0 && icluster_size > 1) {
                shuffle();
                shuffle_cnt++;
                shuffle_cycles_left = Config.sched.shuffle_cycles;
            }


            //quantum
            if (quantum_cycles_left > 0) {
                quantum_cycles_left--;
                return;
            }
            print_rank();

            //new quantum
            decay_stats();

            quantum_cnt++;
            quantum_cycles_left = Config.sched.quantum_cycles;

            shuffle_cnt = 0;
            shuffle_cycles_left = Config.sched.shuffle_cycles;

            //cluster
            icluster_size = cluster();
            if (icluster_size > 1) assign_nice_rank();
        }

        public void clear_ranking_except_top()
        {
            for (int p = 0; p < Config.N; p ++)
            {
                if (rank[p] != (Config.N - 2)) use_rank[p] = 0;
                else use_rank[p] = Config.N;
            }
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

        private void sample_blp()
        {
            blp_sample_cnt++;
            for (uint p = 0; p < Config.N; p++) {
                uint curr_blp = 0;
                for (uint b = 0; b < meta_mctrl.get_bmax(); b++) {
                    if (meta_mctrl.get_load_per_procbank(p, b) > 0) {
                        curr_blp++;
                    }
                }
                blp_sample_sum[p] += curr_blp;
            }
        }

        private void decay_stats()
        {
            for (int p = 0; p < Config.N; p++) {
                ulong cache_miss;
                if (Config.sched.tcm_only_rmpki) cache_miss = Stat.procs[p].read_req.Count;
                else cache_miss = Stat.procs[p].req.Count;

                ulong delta_cache_miss = cache_miss - prev_cache_miss[p];
                prev_cache_miss[p] = cache_miss;

                ulong inst_cnt = Stat.procs[p].ipc.Count;
                ulong delta_inst_cnt = inst_cnt - prev_inst_cnt[p];
                prev_inst_cnt[p] = inst_cnt;

                //mpki
                double curr_mpki = 1000 * ((double)delta_cache_miss) / delta_inst_cnt;
                mpki[p] = Config.sched.history_weight * mpki[p] + (1 - Config.sched.history_weight) * curr_mpki;

                //rbl
                double curr_rbl = ((double)shadow_row_hits[p]) / delta_cache_miss;
                rbl[p] = Config.sched.history_weight * rbl[p] + (1 - Config.sched.history_weight) * curr_rbl;
                shadow_row_hits[p] = 0;

                //blp
                double curr_blp = ((double)blp_sample_sum[p]) / blp_sample_cnt;
                blp[p] = Config.sched.history_weight * blp[p] + (1 - Config.sched.history_weight) * curr_blp;
                blp_sample_sum[p] = 0;

                //service
                service[p] = curr_service[p];
                curr_service[p] = 0;
            }
            blp_sample_cnt = 0;
        }

        private int cluster()
        {
            //rank
            int[] tids = new int[Config.N];
            for (int p = 0; p < Config.N; p++)
                tids[p] = p;

            Array.Sort(tids, sort_mpki);
            for (int p = 0; p < Config.N; p++) {
                rank[p] = Array.IndexOf(tids, p);
            }

            //cluster
            int ncluster_size = 0;
            double service_total = 0;
            double service_runsum = 0;

            for (int p = 0; p < Config.N; p++)
                service_total += service[p];

            int r;
            for (r = Config.N - 1; r >= 0; r--) {
                int pid = Array.IndexOf(rank, r);
                service_runsum += service[pid];
                if (service_runsum > Config.sched.AS_cluster_factor * service_total)
                    break;
                ncluster_size++;
            }

            if (Config.sched.no_highint_rank)
            {
                for (int k = r; k >= 0; k --)
                {
                    int pid = Array.IndexOf(rank, k);
                    rank[pid] = 0;
                }
                return 0;

            }

            return Config.N - ncluster_size;
        }

        private void make_same_lowint_rank()
        {
            for (int k = Config.N - 1; k >= icluster_size; k --)
            {
                int pid = Array.IndexOf(rank, k);
                rank[pid] = 100;
            }
        }

        private void shuffle()
        {
            ShuffleAlgo shuffle_algo = Config.sched.shuffle_algo;
            if (Config.sched.is_adaptive_shuffle) {

                double blp_thresh = Config.sched.adaptive_threshold * meta_mctrl.banks.Count;
                double rbl_thresh = Config.sched.adaptive_threshold;
                if (blp_diff > blp_thresh && rbl_diff > rbl_thresh) {
                    shuffle_algo = ShuffleAlgo.Hanoi;
                }
                else {
                    shuffle_algo = ShuffleAlgo.ControlledRandom;
                }
            }

            //rank_to_pid translation
            int[] pids = new int[Config.N];
            for (int p = 0; p < Config.N; p++) {
                int r = rank[p];
                pids[r] = p;
            }

            //shuffle proper
            switch (shuffle_algo) {
                case ShuffleAlgo.Naive:
                    for (int r = 0; r < icluster_size; r++) {
                        int pid = pids[r];
                        rank[pid] = (r + (icluster_size - 1)) % icluster_size;
                    }
                    break;

                case ShuffleAlgo.ControlledRandom:
                    int step = icluster_size / 2 + 1;
                    for (int r = 0; r < icluster_size; r++) {
                        int pid = pids[r];
                        rank[pid] = (r + step) % icluster_size;
                    }
                    break;

                case ShuffleAlgo.Random:
                    for (int r = icluster_size - 1; r > 0; r--) {
                        int pid1 = Array.IndexOf(rank, r);

                        int chosen_r = rand.Next(r + 1);
                        int chosen_pid = Array.IndexOf(rank, chosen_r);

                        rank[pid1] = chosen_r;
                        rank[chosen_pid] = r;
                    }
                    break;

                case ShuffleAlgo.Hanoi:
                    int even = 2 * icluster_size;
                    int phase = shuffle_cnt % even;

                    if (phase < icluster_size) {
                        int grab_rank = (icluster_size - 1) - phase;
                        int grab_pid = Array.IndexOf(rank, grab_rank);
                        rank[grab_pid] = -1;

                        for (int r = grab_rank + 1; r <= icluster_size - 1; r++) {
                            int pid = Array.IndexOf(rank, r);
                            rank[pid] = r - 1;
                        }
                        rank[grab_pid] = icluster_size - 1;
                    }
                    else {
                        int grab_rank = (icluster_size - 1);
                        int grab_pid = Array.IndexOf(rank, grab_rank);
                        rank[grab_pid] = -1;

                        for (int r = grab_rank - 1; r >= (phase - 1) % icluster_size; r--) {
                            int pid = Array.IndexOf(rank, r);
                            rank[pid] = r + 1;
                        }
                        rank[grab_pid] = (phase - 1) % icluster_size;
                    }
                    break;
            }

            //sanity check
            for (int r = 0; r < Config.N; r++) {
                int pid = Array.IndexOf(rank, r);
                Dbg.Assert(pid != -1);
            }
        }

        public void print_rank()
        {
            for (int cur_proc = 0; cur_proc < Config.N; cur_proc ++)
            {
                Console.Write(" Proc " + cur_proc + " rank " + rank[cur_proc] + "\n"); 
            }
        }

        private void assign_nice_rank()
        {
            int[] icluster_pids = new int[icluster_size];
            for (int r = 0; r < icluster_pids.Length; r++) {
                icluster_pids[r] = Array.IndexOf(rank, r);
            }

            int[] pids = new int[icluster_size];

            //blp rank
            Array.Copy(icluster_pids, pids, icluster_size);
            int[] blp_rank = new int[Config.N];
            Array.Sort(pids, sort_blp);
            for (int r = 0; r < pids.Length; r++) {
                int pid = pids[r];
                blp_rank[pid] = r;
            }
            blp_diff = blp.Max() - blp.Min();

            //rbl rank
            Array.Copy(icluster_pids, pids, icluster_size);
            int[] rbl_rank = new int[Config.N];
            Array.Sort(pids, sort_rbl);
            for (int r = 0; r < pids.Length; r++) {
                int pid = pids[r];
                rbl_rank[pid] = r;
            }
            rbl_diff = rbl.Max() - rbl.Min();

            //nice
            Array.Clear(nice, 0, nice.Length);
            for (int r = 0; r < icluster_pids.Length; r++) {
                int pid = icluster_pids[r];
                nice[pid] = blp_rank[pid] - rbl_rank[pid];
            }

            //nice rank
            Array.Copy(icluster_pids, pids, icluster_size);
            int[] nice_rank = new int[Config.N];
            Array.Sort(pids, sort_nice);
            for (int r = 0; r < pids.Length; r++) {
                int pid = pids[r];
                nice_rank[pid] = r;
            }

            //copy
            foreach (int pid in icluster_pids) {
                rank[pid] = nice_rank[pid];
            }

            //sanity check
            for (int r = 0; r < Config.N; r++) {
                int pid = Array.IndexOf(rank, r);
                Dbg.Assert(pid != -1);
            }
        }

        private int sort_mpki(int pid1, int pid2)
        {
            //return 1 if first argument is "greater" (higher rank)
            if (pid1 == pid2) return 0;
            
            double mpki1 = mpki[pid1];
            double mpki2 = mpki[pid2];

            if (mpki1 < mpki2) return 1;
            else return -1;
        }

        private int sort_rbl(int pid1, int pid2)
        {
            //return 1 if first argument is "greater" (higher rank)
            if (pid1 == pid2) return 0;

            double rbl1 = rbl[pid1];
            double rbl2 = rbl[pid2];

            if (rbl1 < rbl2) return 1;
            else return -1;
        }

        private int sort_blp(int pid1, int pid2)
        {
            //return 1 if first argument is "greater" (higher rank)
            if (pid1 == pid2) return 0;

            double blp1 = blp[pid1];
            double blp2 = blp[pid2];

            if (blp1 > blp2) return 1;
            else return -1;
        }

        private int sort_nice(int pid1, int pid2)
        {
            //return 1 if first argument is "greater" (higher rank)
            int nice1 = nice[pid1];
            int nice2 = nice[pid2];

            if (nice1 != nice2) {
                if (nice1 > nice2) return 1;
                else return -1;
            }
            return 0;
        }

        public override void issue_req(Req req)
        {
            if (req == null) return;
            count_streaks(req);
            count_requests(req);

            MemCtrl mctrl = meta_mctrl.get_mctrl(req);
            ulong shadow_rowid = mctrl.shadow_rowid_per_procrankbank[req.pid, req.addr.rid, req.addr.bid];
            if (shadow_rowid == req.addr.rowid) {
                shadow_row_hits[req.pid]++;
            }
        }
    }
}
