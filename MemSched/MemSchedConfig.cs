using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

namespace MemMap
{
    public class MemSchedConfig : ConfigGroup
    {
        public bool is_omniscient = true;    //whether all memory share the same controller
        
        //read scheduling algorithm
        public string sched_algo = "FRFCFS";
        public Type typeof_sched_algo;

        //write scheduling algorithm
        public bool same_sched_algo = true;
        public string wbsched_algo = "FRFCFS";
        public Type typeof_wbsched_algo;
        public double preempt_fraction = 0.75;
        public bool tcm_only_rmpki = false;

        //writeback throttle
        public string wbthrottle_algo = "Tax";
        public Type typeof_wbthrottle_algo;
        public uint wbthrottle_cycles = 0;
        public double wbthrottle_rmpki_threshold = 8;

        public double wbthrottle_wmpki_threshold = 8;
        public double wbthrottle_fraction = 0.5;

        //prioritize row-hits
        public bool prioritize_row_hits = false;

        /*************************
         * FRFCFS Scheduler
         *************************/
        public int row_hit_cap = 4;

        /*************************
         * STFM Scheduler
         *************************/
        public double alpha = 1.1;
        public ulong beta = 1048576;
        public int ignore_gamma = 0;

        /*************************
         * ATLAS Scheduler
         *************************/
        public int threshold_cycles = 100000;
        public double history_weight = 0.875;
        public bool service_overlap = false;

        /*************************
         * PAR-BS Scheduler
         *************************/
        public int batch_cap = 5;
        public int prio_max = 11;   //0~9 are real priorities, 10 is no-priority

        //schedulers: FR_FCFS_Cap, NesbitFull
        public ulong prio_inv_thresh = 0;        //FRFCFS_Cap, NesbitFull schedulers; in memory cycles

        //schedulers: STFM, Nesbit{Basic, Full}
        public int use_weights = 0;
        public double[] weights = new double[128];

        /*************************
         * TCM Scheduler
         *************************/
        public double AS_cluster_factor = 0.10;

        //shuffle
        public TCM.ShuffleAlgo shuffle_algo = TCM.ShuffleAlgo.Hanoi;
        public bool is_adaptive_shuffle = true;
        public double adaptive_threshold = 0.1;  

        // PerfIsolation scheduling
        public int profile_interval_cycles = 1000000;
        public int warmup_cycles = 5000000;

        public bool static_ranking = false;
        public bool bandwidth_partition = true;
        public double base_bandwidth = 0;
        public int bw_fraction = 20;
        public int fix_rank_proc = 23;
        public bool close_loop = true;
        public bool bw_partition_by_as = false;
        public bool bw_partition_by_speedup = true;
        public bool bw_partition_by_slowdown = false;
        public int shuffle_cycles = 10000;
        public int quantum_cycles = 5000000;

        public double speedup_threshold = 0.1;
        public double speedup_threshold_delta = 0.05;
        public double steal_size = 2;
        public double initial_steal_size = 10;
        public int limit_value = 80;
        public double as_factor = 0.5;
        public bool use_cum_speedup = false;
        public bool use_updated_as_rate = true;
        public bool rank_by_speedup = false;
        public bool rank_by_as_rate = true;
        public double low_int_prio_fraction = 0;
        public double partition_for_proc_1 = 50;
        public bool is_adaptive_threshold = false;
        public bool rbhit = false;
        public bool no_queueing = false;
        public bool compute_slowdown_allocation = false;
        public double gamma = 0;
        public bool compute_missrate_allocation = false;
        public bool compute_scaled_missrate_allocation = false;
        public bool compute_service_allocation = false;
        public bool compute_queuelat_allocation = false;
        public bool compute_queuelat_mpki_allocation = false;

        /*************************
         * Prioritize Queueing Latency Scheduler
         *************************/
        public bool channel_level = false;
        public bool same_lowint_rank = false; 
        public bool no_highint_rank = false;
        public bool no_lowint_rank = false;
        public bool max_stall_crit = true;
        public bool no_delay_lowint = true;
        public int num_wait_cycles = 20;
        public double nowait_percentage = 90;
        public bool cache_alloc_unaware = true;
 

        protected override bool set_special_param(string param, string val)
        {
            return false;
        }

        public override void finalize()
        {
            //memory scheduling algo
            string type_name = typeof(Sim).Namespace + "." + Config.sched.sched_algo;
            try{
                typeof_sched_algo = Type.GetType(type_name);
            }
            catch{
                throw new Exception(String.Format("Scheduler not found {0}", Config.sched.sched_algo));
            }

            type_name = typeof(Sim).Namespace + "." + Config.sched.wbsched_algo;
            try {
                typeof_wbsched_algo = Type.GetType(type_name);
            }
            catch {
                throw new Exception(String.Format("Writeback scheduler not found {0}", Config.sched.wbsched_algo));
            }

            type_name = typeof(Sim).Namespace + "." + Config.sched.wbthrottle_algo;
            try {
                typeof_wbthrottle_algo = Type.GetType(type_name);
            }
            catch {
                throw new Exception(String.Format("Writeback throttler not found {0}", Config.sched.wbthrottle_algo));
            }


        }
    }
}
