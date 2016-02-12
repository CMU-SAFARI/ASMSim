using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace MemMap
{
    public class BWAllocation : MemSched
    {

        public int[] rank = new int[Config.N];
        public int[] service_bank_cnt = new int[Config.N];
        public int[] curr_service = new int[Config.N];
        public int[] cum_curr_service = new int[Config.N];
        public ulong[] max_as = new ulong[Config.N];
        public double[] as_rate = new double[Config.N];
        public bool profile_start, profile_done;
        public int warmup_cycles_left, profile_interval_cycles;
        public int quantum_cycles_left, shuffle_cycles_left;
        public double[] start_bw_partition = new double[Config.N];
        public double[] end_bw_partition = new double[Config.N];
        public double[] base_allocated_partition = new double[Config.N];
        public double[] extra_allocated_partition = new double[Config.N];
        public double[] speedup = new double[Config.N];
        public double[] cum_speedup = new double[Config.N];
        public double[] use_speedup = new double[Config.N];
        public bool[] is_low_int_proc = new bool[Config.N];
        public int[] speedup_rank = new int[Config.N];
        public int[] as_rate_rank = new int[Config.N];
        public int[] cum_speedup_rank = new int[Config.N];
        public int[] mpki_rank = new int[Config.N];
        public double[] mpki = new double[Config.N];
        public double[] scaled_mpki = new double[Config.N];
        public ulong[] prev_cache_miss = new ulong[Config.N];
        public ulong[] prev_inst_cnt = new ulong[Config.N];
        public ulong[] stall_cycles = new ulong[Config.N];
        public ulong[] memory_cycles = new ulong[Config.N];
        public int[]queuing_cycles = new int[Config.N];
        public int[]queuing_cycles_inst = new int[Config.N];
        public int num_low_int_procs;
        public long profile_cycles_count;
        public int profile_count;
        public int not_met_threshold = 0;
        public int met_threshold_count = 0;
        public int main_bw = 0;
        public int spilt_bw = 0;
        public double min_speedup = 1000;
        public double max_speedup = 0;

        public int[]prev_dump_proc = new int[Config.N];        


        public ulong[] shuffle_cycle_max_as = new ulong[Config.N];
        public ulong[] max_as_actual = new ulong[Config.N];
        public ulong[] max_as_actual_inst = new ulong[Config.N];
        public ulong[] max_as_actual_cycles = new ulong[Config.N];
        public ulong[] max_as_actual_cycles_inst = new ulong[Config.N];
        public double[] as_rate_actual = new double[Config.N];
        public double[] use_as_rate = new double[Config.N];
        public int waiting_max = 0;
        public int waiting_cycles;
        public int[] utility_rank = new int[Config.N];
        public int[] proc_of_rank = new int[Config.N];
        public double[] weighted_utility_slowdown = new double[Config.N];
        public double[] queueing_latency = new double[Config.N];
        public double[] mpki_scale_q_latency = new double[Config.N];

        static Random random = new Random(100);
        
        
        public BWAllocation()
        {
            for (int cur_proc = 0; cur_proc < Config.N; cur_proc++) {
                rank[cur_proc] = 0;
                service_bank_cnt[cur_proc] = 0;
                curr_service[cur_proc] = 0;
                max_as[cur_proc] = 0;
                is_low_int_proc[cur_proc] = false;
                cum_curr_service[cur_proc] = 0;
                stall_cycles[cur_proc] = 0;
                memory_cycles[cur_proc] = 0;
            }
            profile_start = true;
            profile_done = false;
            warmup_cycles_left = Config.sched.warmup_cycles;
            quantum_cycles_left = Config.sched.quantum_cycles;
            profile_cycles_count = 0;
            profile_count = 0;
            waiting_cycles = waiting_max;

        }

        private int RandomNumber(int min, int max)
        { 
            return random.Next(min, max);
        } 


        public override void enqueue_req(Req req) { }
        public override void dequeue_req(Req req) { }
        

        public override void tick()
        {

            base.tick();
            increment_queuing();
            if(meta_mctrl.get_cycles() < 10)
            {
                equal_bw_partition();
            }

            if (shuffle_cycles_left > 0)
            {
                shuffle_cycles_left --;
            }
            else if (meta_mctrl.get_cycles() > 0)
            {
                shuffle_cycles_left = Config.sched.shuffle_cycles;
                compute_bw_partition_rank();

                //Needed only for MISE
                if (Config.mise)
                {
                    capture_max_as_actual();
                    setup_capture_max_as_actual();
                }
            }

           
            if (quantum_cycles_left > 0)
            {
                quantum_cycles_left --;
            }
            else
            {
                if (Config.slowdown_allocation_memory) slowdown_friendly_bw_partition();
                else equal_bw_partition();
                //Needed only for MISE
                if (Config.mise)
                {
                    print_slowdown_periodic();
                    capture_as_rate_actual(); 
                }
                clear_queueing_cycles();
                quantum_cycles_left = Config.sched.quantum_cycles; 
            }

            
        }

        public void equal_bw_partition()
        {
            double start_partition = 0;

            for (int p = 0; p < Config.N; p ++)
            {
                start_bw_partition[p] = start_partition;
                base_allocated_partition[p] = Config.sched.base_bandwidth/(Config.N);
                extra_allocated_partition[p] =  (100 - Config.sched.base_bandwidth)/Config.N;
                end_bw_partition[p] = start_partition + base_allocated_partition[p] + extra_allocated_partition[p];
                start_partition += base_allocated_partition[p] + extra_allocated_partition[p];
            }
   
        }


        public void slowdown_friendly_bw_partition()
        {
            double start_partition = 0;
            double total_slowdown = 0;

            for (int p = 0; p < Config.N; p ++)
            {
                  if (Config.sched.cache_alloc_unaware) total_slowdown += (double)(Math.Pow(Sim.cache_controller.slowdown_unaware_cache_allocation[p],1));
                  else total_slowdown += (double)(Math.Pow(Sim.cache_controller.slowdown_post_cache_allocation[p],1));
            } 

            for (int p = 0; p < Config.N; p ++)
            {
                start_bw_partition[p] = start_partition;
                base_allocated_partition[p] = Config.sched.base_bandwidth/(Config.N);
                if (Config.sched.cache_alloc_unaware) extra_allocated_partition[p] = (100 - Config.sched.base_bandwidth) * ((double)(Math.Pow(Sim.cache_controller.slowdown_unaware_cache_allocation[p],1))/total_slowdown);
                else extra_allocated_partition[p] = (100 - Config.sched.base_bandwidth) * ((double)(Math.Pow(Sim.cache_controller.slowdown_post_cache_allocation[p],1))/total_slowdown);
                end_bw_partition[p] = start_partition + base_allocated_partition[p] + extra_allocated_partition[p];
                start_partition += base_allocated_partition[p] + extra_allocated_partition[p];
                queuing_cycles[p] = 0;              
            }
   
        } 


        public void compute_bw_partition_rank()
        {
            int random_value = RandomNumber(1, 100);
            for (int cur_proc = 0; cur_proc < Config.N; cur_proc ++)
            {
                rank[cur_proc] = 0;
                if ((double)random_value >= start_bw_partition[cur_proc] && (double)random_value < end_bw_partition[cur_proc])
                {
                    rank[cur_proc] = Config.N + 200;
                    meta_mctrl.set_highest_rank_proc(cur_proc); 
                }
            }
        }

        public void setup_capture_max_as_actual()
        {
            for (int p = 0; p < Config.N; p ++)
            {
                shuffle_cycle_max_as[p] = 0;
            }
        }
        
        public void capture_max_as_actual()
        {
            for (int p = 0; p < Config.N; p ++)
            {
                if (!is_low_int_proc[p] && rank[p] == Config.N + 200)
                {

                    ulong inst_cnt = Stat.procs[p].ipc.Count;
                    ulong delta_inst_cnt = inst_cnt - prev_inst_cnt[p];
                    prev_inst_cnt[p] = inst_cnt;

                    
                    max_as_actual[p] += shuffle_cycle_max_as[p]; 
                    max_as_actual_inst[p] += shuffle_cycle_max_as[p]; 
                    max_as_actual_cycles[p] += (ulong)(Config.sched.shuffle_cycles);
                    max_as_actual_cycles_inst[p] += (ulong)(delta_inst_cnt);
                }
            }
        }

        public void capture_as_rate_actual()
        {
            for (int p = 0; p < Config.N; p ++)
            {
                as_rate_actual[p] = (double)max_as_actual[p]/(double)(max_as_actual_cycles[p] - (ulong)queuing_cycles[p]);
                max_as_actual[p] = 0;
                max_as_actual_cycles[p] = 0;
                curr_service[p] = 0;
            }
        }

       public void clear_queueing_cycles()
       {
            for (int p = 0; p < Config.N; p ++)
            {
//                meta_mctrl.set_queuing_cycle(p, queuing_cycles[p]);
                queuing_cycles[p] = 0;
                Sim.queuing_cycles[p] = 0;
            }
       }


        public void print_slowdown_periodic()
        {
            for (int cur_proc = 0; cur_proc < Config.N; cur_proc ++)
            {
                ulong cycles = max_as_actual_cycles[cur_proc] - (ulong)queuing_cycles[cur_proc];
                double stall_fraction = (double)stall_cycles[cur_proc]/(double)Config.sched.quantum_cycles;
                double alone_rsr = (double)max_as_actual[cur_proc]/(double)cycles;
                double shared_rsr = (double)curr_service[cur_proc]/(double)Config.sched.quantum_cycles;
                double slowdown;
                if (stall_fraction > 0.4)
                    slowdown = alone_rsr/shared_rsr;
                else
                    slowdown = (1 - stall_fraction) + (stall_fraction * alone_rsr/shared_rsr);
                Sim.periodic_writer_service.WriteLine(" Proc " + cur_proc + " Slowdown " + slowdown);
            }
        }







        public override Req better_req(Req req1, Req req2)
        {
            int rank1, rank2;

            rank1 = rank[req1.pid];
            rank2 = rank[req2.pid];

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


        public override void service_counter(Req req)
        {
            curr_service[req.pid] ++;
            Sim.procs[req.pid].service ++;
            cum_curr_service[req.pid] ++;
//
            if (rank[req.pid] == Config.N + 200) 
            {
                shuffle_cycle_max_as[req.pid] ++;
                Sim.procs[req.pid].service_alone ++;
            }
               if (rank[req.pid] == Config.N + 200) main_bw ++;
                else spilt_bw ++;
            
        }

        public override Req find_best_req(List<Req> q)
        {
            Req req = base.find_best_req(q);
            if (q.Count == 0)
                return null;

            return req;

        } 

        private void increment_queuing()
        {
            for (int p = 0; p < Config.N; p ++)
            {
                if ((meta_mctrl.prev_req_pid != p) && meta_mctrl.proc_req_in_queue(p) && (rank[p] == Config.N + 200))
                {
                    queuing_cycles[p] ++;
                    Sim.queuing_cycles[p] ++;
                    meta_mctrl.is_queueing_cycle(true);
                }
                else meta_mctrl.is_queueing_cycle(false);
            }

        }


        

    }
}
