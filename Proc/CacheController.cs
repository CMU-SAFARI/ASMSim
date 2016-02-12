using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.Linq;


namespace MemMap
{
    public class CacheController
    {
        public double [] allocation;
        public double [] temp_allocation;
        int [,] hit_count;
        int [,] hit_count_alone;
        int [] miss_count_aux;
        int [] miss_count_aux_alone;

        public int [] miss_count_current;
        public int [] hit_count_current;
        public int [] miss_count_current_alone;
        public int [] hit_count_current_alone;
        public int [] miss_count_current_notsampled;
        public int [] miss_count_current_alone_notsampled;
        public int [] hit_count_current_notsampled;
        public int [] hit_count_current_alone_notsampled;

//        int [] alone_miss_count;
        double [,] mpki;
        double [,] mpkc;
        public double [] mpki_current;
        double [] mpkc_current;
        double [] mpkc_current_alone;
        double [] mpkc_current_alone_notsampled;
        double [] mpkc_current_notsampled;
        double [,] mpkc_alone;
        int [,] miss_count;
        int [,] miss_count_alone;
        int [] min_blk_max_mu;
        double[] max_mu;
        public double [] mem_slowdown;
        public double sum_memory_slowdown = 0;
        public double sum_cache_slowdown = 0;

        public double [,] mem_alloc_slowdown;
        double [,] overall_slowdown;
        double [,] overall_speedup;
        int [] min_blk_max_ms;
        double[] max_ms;
        static Random random = new Random(100);
        public double[] slowdown_utility;
        public double[] slowdown_post_cache_allocation;
        public double[] slowdown_unaware_cache_allocation;

        //L1 cache
        public int[] l1_cache_hit_count; 
        public int[] l1_cache_miss_count; 
        public int[] l1_cache_hit_count_alone; 
        public int[] l1_cache_miss_count_alone; 
        public int[] app_type_prio;
        public double[] average_allocation;
        public double[] ip_increase_factor;


        double [] hpkc_current_alone;
        double [,] hpkc_alone;
        double [] hpkc_current;
        double [,] hpkc;
        int [,] hits_given_allocation;
        int [,] hits_alone_given_allocation;
       
        public CacheController()
        {
            allocation = new double[Config.N];
            temp_allocation = new double[Config.N];
            hit_count = new int[Config.N, Config.proc.cache_assoc];
            hit_count_alone = new int[Config.N, Config.proc.cache_assoc];
            mpki = new double[Config.N, Config.proc.cache_assoc + 1];
            mpkc = new double[Config.N, Config.proc.cache_assoc + 1];
            mpkc_alone = new double[Config.N, Config.proc.cache_assoc + 1];
            hpkc_alone = new double[Config.N, Config.proc.cache_assoc + 1];
            hpkc = new double[Config.N, Config.proc.cache_assoc + 1];
            hits_given_allocation = new int[Config.N, Config.proc.cache_assoc + 1];
            hits_alone_given_allocation = new int[Config.N, Config.proc.cache_assoc + 1];
            mpki_current = new double[Config.N];
            mpkc_current = new double[Config.N];
            mpkc_current_alone = new double[Config.N];
            mpkc_current_alone_notsampled = new double[Config.N];
            mpkc_current_notsampled = new double[Config.N];
            hpkc_current_alone = new double[Config.N];
            hpkc_current = new double[Config.N];


            miss_count_current = new int[Config.N];
            hit_count_current = new int[Config.N];
            miss_count_current_alone = new int[Config.N];
            hit_count_current_alone = new int[Config.N];
            miss_count_current_notsampled = new int[Config.N];
            miss_count_current_alone_notsampled = new int[Config.N];
            hit_count_current_notsampled = new int[Config.N];
            hit_count_current_alone_notsampled = new int[Config.N];


            miss_count_aux = new int[Config.N];
            miss_count_aux_alone = new int[Config.N];
            miss_count = new int[Config.N, Config.proc.cache_assoc+1];
            miss_count_alone = new int[Config.N, Config.proc.cache_assoc+1];
            min_blk_max_mu = new int[Config.N];
            max_mu = new double[Config.N];
            mem_slowdown = new double[Config.N];

            mem_alloc_slowdown = new double[Config.N, Config.proc.cache_assoc+1];
            overall_slowdown = new double[Config.N, Config.proc.cache_assoc+1];
            overall_speedup = new double[Config.N, Config.proc.cache_assoc+1];
            min_blk_max_ms = new int[Config.N];
            max_ms = new double[Config.N];

            //L1 cache
            l1_cache_hit_count = new int[Config.N];
            l1_cache_miss_count = new int[Config.N];
            l1_cache_hit_count_alone = new int[Config.N];
            l1_cache_miss_count_alone = new int[Config.N];



            for (int i = 0; i < Config.N; i ++)
            {
                for (int j = 0; j < Config.proc.cache_assoc; j ++)
                {
                    hit_count[i, j] = 0;
                    hit_count_alone[i, j] = 0;
                }
            }
            slowdown_utility = new double[Config.N]; 
            slowdown_post_cache_allocation = new double[Config.N]; 
            slowdown_unaware_cache_allocation = new double[Config.N]; 
            app_type_prio = new int[Config.N];
            average_allocation = new double[Config.N];
            ip_increase_factor = new double[Config.N];
            
        }


        private int RandomNumber(int min, int max)
        { 
            return random.Next(min, max);
        } 

        public void count_l1_hits(int pid, bool alone)
        {
            if (alone == true) l1_cache_hit_count_alone[pid] ++ ;
            if (alone == false) l1_cache_hit_count[pid] ++; 
        }

        public void count_l1_misses(int pid, bool alone)
        {
            if (alone == true) l1_cache_miss_count_alone[pid] ++; 
            if (alone == false) l1_cache_miss_count[pid] ++;
        }

        public void count_aux_hits(int pid, int way)
        {
            if (way == 1000) miss_count_aux[pid] ++;
            else if (way != 100000) {
                hit_count[pid, (way - 1000)] ++;
            }
        }

        public void count_aux_hits_alone(int pid, int way)
        {
            if (way == 1000) miss_count_aux_alone[pid] ++;
            else if (way != 100000)  hit_count_alone[pid, (way - 1000)] ++;
        }


        public void count_misses(int pid, int way, bool alone)
        {
              if (alone == true && way == 1000) miss_count_current_alone[pid] ++; 
              if (alone == false && way == 1000) miss_count_current[pid] ++;
        }


        public void count_all_misses(int pid, bool alone)
        {
              if (alone == true) miss_count_current_alone_notsampled[pid] ++;
              if (alone == false) miss_count_current_notsampled[pid] ++;       
        }


        public void count_all_hits(int pid, bool alone)
        {
              if (alone == true) hit_count_current_alone_notsampled[pid] ++;
              if (alone == false) hit_count_current_notsampled[pid] ++;       
        }


        public void count_current_hits (int pid, int way, bool alone)
        {
            if (alone == true && way != 1000 && way != 100000) hit_count_current_alone[pid] ++;
            if (alone == false && way != 1000 && way != 100000) hit_count_current[pid] ++;
        }

        public void clear_hits(int pid)
        {
            for (int j = 0; j < Config.proc.cache_assoc; j ++)
            {
                hit_count[pid, j] = 0;
                hit_count_alone[pid, j] = 0;
            }
            miss_count_aux[pid] = 0;
            miss_count_aux_alone[pid] = 0;
            miss_count_current[pid] = 0;
            miss_count_current_alone[pid] = 0;
            miss_count_current_notsampled[pid] = 0;
            miss_count_current_alone_notsampled[pid] = 0;
            hit_count_current[pid] = 0;
            hit_count_current_alone[pid] = 0;
            hit_count_current_notsampled[pid] = 0;
            hit_count_current_alone_notsampled[pid] = 0;
            l1_cache_hit_count[pid] = 0;
            l1_cache_miss_count[pid] = 0;
            l1_cache_hit_count_alone[pid] = 0;
            l1_cache_miss_count_alone[pid] = 0;
            Sim.queuing_cycles[pid] = 0;
        }


        public void compute_l1_mpkc_slowdown_associativity (int pid, int high_priority_cycles, ulong high_priority_total_miss_latency, ulong high_priority_total_hit_latency, ulong total_miss_latency, ulong total_hit_latency, ulong queueing_cycles)
        {
            double percentage_of_hits = (double)hits_given_allocation[pid,16]/(double)(hits_given_allocation[pid,16] + miss_count[pid,16])*100;
            double high_priority_avg_miss_latency = 0;
            if (miss_count_current_alone_notsampled[pid] > 0) high_priority_avg_miss_latency = (double)high_priority_total_miss_latency/(double)miss_count_current_alone_notsampled[pid];
            double high_priority_avg_hit_latency = 0;
            if (hit_count_current_alone_notsampled[pid] > 0) high_priority_avg_hit_latency = (double)high_priority_total_hit_latency/(double)hit_count_current_alone_notsampled[pid];
            double avg_miss_latency = 0;
            if (miss_count_current_notsampled[pid] > 0) avg_miss_latency = (double)total_miss_latency/(double)miss_count_current_notsampled[pid];
            double avg_hit_latency = 0;
            if (hit_count_current_notsampled[pid] > 0) avg_hit_latency = (double)total_hit_latency/(double)hit_count_current_notsampled[pid];
            else avg_hit_latency = 10;


            double high_priority_avg_queueing_time = 0;
            high_priority_avg_queueing_time = (double)queueing_cycles/(double)miss_count_current_alone_notsampled[pid];

            double l1_mpkc_alone;          
            double l2_hits_nomem_int, l2_misses_nomem_int;

            l2_hits_nomem_int = (double)percentage_of_hits/100 * (hit_count_current_alone_notsampled[pid] + miss_count_current_alone_notsampled[pid]); 

            l2_misses_nomem_int = (double)(100-percentage_of_hits)/100 * (hit_count_current_alone_notsampled[pid] + miss_count_current_alone_notsampled[pid]);
               
            double alone_high_priority_cycles = high_priority_cycles - ((high_priority_avg_miss_latency - high_priority_avg_hit_latency)*(l2_hits_nomem_int-hit_count_current_alone_notsampled[pid]));
            alone_high_priority_cycles = (double)alone_high_priority_cycles - (high_priority_avg_queueing_time * (double)l2_misses_nomem_int);
   
            l1_mpkc_alone = (double)(l2_misses_nomem_int + l2_hits_nomem_int)/(alone_high_priority_cycles) * 1000;


            double l1_mpkc;
            for (int i = 0; i <= Config.proc.cache_assoc; i ++)
            {
                percentage_of_hits = (double)hits_given_allocation[pid,i]/(double)(hits_given_allocation[pid,i] + miss_count[pid,i])*100;

                l2_hits_nomem_int = (double)percentage_of_hits/100 * (hit_count_current_notsampled[pid] + miss_count_current_notsampled[pid]); 

                l2_misses_nomem_int = (double)(100-percentage_of_hits)/100 * (hit_count_current_notsampled[pid] + miss_count_current_notsampled[pid]);
            

                double service_cycles = Config.proc.quantum_cycles - ((avg_miss_latency - avg_hit_latency)*(l2_hits_nomem_int-hit_count_current_notsampled[pid]));

                l1_mpkc = (double)(l2_misses_nomem_int + l2_hits_nomem_int)/(service_cycles) * 1000;
                

                if (l1_mpkc_alone > l1_mpkc) overall_slowdown[pid,i] = l1_mpkc_alone/l1_mpkc;
                else overall_slowdown[pid,i] = 1;

                if (l1_mpkc_alone > l1_mpkc) overall_speedup[pid,i] = l1_mpkc/l1_mpkc_alone;
                else overall_speedup[pid,i] = 1;

//                Sim.periodic_writer_service.WriteLine(" Proc " + pid + " Ways " + i + " Slowdown " + overall_slowdown[pid,i] + "\n");
//                Sim.periodic_writer_service.Flush();

            }
        }


        public double compute_l1_mpkc_slowdown (int pid, int high_priority_cycles, ulong high_priority_total_miss_latency, ulong high_priority_total_hit_latency, ulong queueing_cycles)
        {

            double l1_mpkc = (double)l1_cache_miss_count[pid]/Config.proc.quantum_cycles * 1000;

            double l2_misses_nomem_int = miss_count_current_alone_notsampled[pid];
            double l2_hits_nomem_int = hit_count_current_alone_notsampled[pid];
            double slowdown = 1;


            double percentage_of_hits = (double)hits_given_allocation[pid,16]/(double)(hits_given_allocation[pid,16] + miss_count[pid,16])*100;
            double high_priority_avg_miss_latency = 0;
            if (miss_count_current_alone_notsampled[pid] > 0) high_priority_avg_miss_latency = (double)high_priority_total_miss_latency/(double)miss_count_current_alone_notsampled[pid];
            double high_priority_avg_hit_latency = 0;
            if (hit_count_current_alone_notsampled[pid] > 0) high_priority_avg_hit_latency = (double)high_priority_total_hit_latency/(double)hit_count_current_alone_notsampled[pid];

            double high_priority_avg_queueing_time = 0;
            high_priority_avg_queueing_time = (double)queueing_cycles/(double)miss_count_current_alone_notsampled[pid];
            double l1_mpkc_alone;


            l2_hits_nomem_int = (double)percentage_of_hits/100 * (hit_count_current_alone_notsampled[pid] + miss_count_current_alone_notsampled[pid]); 

            l2_misses_nomem_int = (double)(100-percentage_of_hits)/100 * (hit_count_current_alone_notsampled[pid] + miss_count_current_alone_notsampled[pid]);
            
            double alone_high_priority_cycles = high_priority_cycles - ((high_priority_avg_miss_latency - high_priority_avg_hit_latency)*(l2_hits_nomem_int-hit_count_current_alone_notsampled[pid]));

            alone_high_priority_cycles = (double)alone_high_priority_cycles - (high_priority_avg_queueing_time * (double)l2_misses_nomem_int);

            l1_mpkc_alone = (double)(l2_misses_nomem_int + l2_hits_nomem_int)/(alone_high_priority_cycles) * 1000;

            if (l1_mpkc_alone > l1_mpkc) slowdown = l1_mpkc_alone/l1_mpkc; 
            else slowdown = 1;

            slowdown_unaware_cache_allocation[pid] = slowdown;

            return slowdown;
        }


        public void compute_hrc(int pid, int inst_cnt, int high_priority_cycles)
        {
            for (int i = 0; i <= Config.proc.cache_assoc; i ++)
            {
               int hits = 0;
               for (int j = Config.proc.cache_assoc-1; j > Config.proc.cache_assoc-i-1; j --)
               {
                   hits += hit_count_alone[pid,j];    
               }
               hits_alone_given_allocation[pid, i] = hits;
               hpkc_alone[pid, i] = (double)hits/(double)high_priority_cycles * 1000;
            }
            hpkc_current_alone[pid] = (double)hit_count_current_alone[pid]/(double)high_priority_cycles * 1000;
            hpkc_current[pid] = (double)hit_count_current[pid]/(double)Config.proc.quantum_cycles * 1000;


            for (int i = 0; i <= Config.proc.cache_assoc; i ++)
            {
               int hits = 0;
               for (int j = Config.proc.cache_assoc-1; j > Config.proc.cache_assoc-i-1; j --)
               {
                   hits += hit_count[pid,j];    
               }
               hits_given_allocation[pid, i] = hits;
               hpkc[pid, i] = (double)hits/(double)Config.proc.quantum_cycles * 1000;
            }
            
        }

        public void compute_mrc(int pid, int inst_cnt, int high_priority_cycles)
        {
            for (int i = 0; i <= Config.proc.cache_assoc; i ++)
            {
               int misses = 0;
               for (int j = 0; j < Config.proc.cache_assoc-i; j ++)
               {
                   misses += hit_count[pid,j];    
               }
               misses += miss_count_aux[pid];
               miss_count[pid, i] = misses;
               mpki[pid, i] = (double)misses/(double)inst_cnt * 1000;
               mpkc[pid, i] = (double)misses/Config.proc.quantum_cycles * 1000;
            }
            mpki_current[pid] = (double)miss_count_current[pid]/(double)inst_cnt*1000;
            mpkc_current[pid] = (double)miss_count_current[pid]/(double)Config.proc.quantum_cycles * 1000;
            mpkc_current_alone[pid] = (double)miss_count_current_alone[pid]/(double)high_priority_cycles * 1000;
            mpkc_current_alone_notsampled[pid] = (double)miss_count_current_alone_notsampled[pid]/(double)high_priority_cycles * 1000;
            mpkc_current_notsampled[pid] = (double)miss_count_current_notsampled[pid]/(double)Config.proc.quantum_cycles * 1000;
            for (int i = 0; i <= Config.proc.cache_assoc; i ++)
            {
               int misses = 0;
               for (int j = 0; j < Config.proc.cache_assoc-i; j ++)
               {
                   misses += hit_count_alone[pid,j];    
               }
               misses += miss_count_aux_alone[pid];
               miss_count_alone[pid, i] = misses;
               mpkc_alone[pid, i] = (double)misses/(double)high_priority_cycles * 1000;
            }

            return;
        }

        public void compute_naive_qos_slowdown_allocation()
        {
            Console.Write ("NAIVE QOS ALLOCATION\n");
            allocation[0] = Config.proc.cache_assoc;

            for (int i = 1; i < Config.N; i ++)
            {
                allocation[i] = 0;    
            }

            return;

        }

        public void compute_qos_slowdown_allocation()
        {
            bool set_allocation = false;
            for (int i = 0; i < Config.proc.cache_assoc; i ++)
            {
                if (overall_slowdown[0,(int)i] <= (Config.proc.slowdown_bound - 0.5)) 
                {
                    allocation[0] = i;
                    set_allocation = true; 
                    break;
                }
            }
            if (!set_allocation) allocation[0] = Config.proc.cache_assoc; 
            int balance = Config.proc.cache_assoc - (int)allocation[0];
            double alloc = 0;
            for (int i = 1; i < Config.N; i ++)
            {
                allocation[i] = 0;    
            }

            for (int i = 1; i < Config.N; i ++)
            {
                balance = balance - (int)allocation[i];
            }
            while (balance > 0)
            {
                for (int i = 1; i < Config.N; i ++)
                {
                    alloc = allocation[i];
                    get_max_ms(i, alloc, balance);
                }     
                int max_app = get_max_ms_app();
                allocation[max_app] += min_blk_max_ms[max_app];
                balance -= min_blk_max_ms[max_app];
            }
            return;

        }

        public void compute_overall_slowdown_allocation()
        {
            int balance = Config.proc.cache_assoc;
            double alloc = 0;
            for (int i = 0; i < Config.N; i ++)
            {
                allocation[i] = 0;    
            }

            for (int i = 0; i < Config.N; i ++)
            {
                balance = balance - (int)allocation[i];
            }
            while (balance > 0)
            {
                for (int i = 0; i < Config.N; i ++)
                {
                    alloc = allocation[i];
                    if (Config.slowdown_allocation) get_max_ms(i, alloc, balance);
                    else if (Config.speedup_allocation) get_max_speedup(i, alloc, balance);
                }     
                int max_app = get_max_ms_app();
                allocation[max_app] += min_blk_max_ms[max_app];
                balance -= min_blk_max_ms[max_app];
            }
            compute_su_post_allocation();
            return;
        }

        public void compute_su_post_allocation()
        {
            for (int i = 0; i < Config.N; i ++)
            {
                slowdown_post_cache_allocation[i] = overall_slowdown[i,(int)allocation[i]];              
            }
        }


        public void get_max_ms(int pid, double alloc, int balance)
        {
            double ms = 0;
            max_ms[pid] = -1000;
            min_blk_max_ms[pid] = 1;
            for (int i = 1; i <= balance; i ++)
            {
                ms = ((double)overall_slowdown[pid, (int)alloc] - (double)overall_slowdown[pid, (int)alloc + i])/((double)(i));
                if (Math.Abs(ms) < 0) ms = 0;
                if (ms >= max_ms[pid]) 
                {
                    max_ms[pid] = ms;
                    if (max_ms[pid] > 0) min_blk_max_ms[pid] = i;
                }
            }
        }


        public void get_max_speedup(int pid, double alloc, int balance)
        {
            double ms = 0;
            max_ms[pid] = -1000;
            min_blk_max_ms[pid] = 1;
            for (int i = 1; i <= balance; i ++)
            {
                ms = ((double)overall_speedup[pid, (int)alloc + i] - (double)overall_speedup[pid, (int)alloc])/((double)(i));
                if (Math.Abs(ms) < 0) ms = 0;
                if (ms >= max_ms[pid]) 
                {
                    max_ms[pid] = ms;
                    if (max_ms[pid] > 0) min_blk_max_ms[pid] = i;
                }
            }
        }


        public int get_max_ms_app()
        {
            double max_ms_local = 0;
            int max_ms_app = 0;
            bool set_max_ms_app = false;
            for (int i = 0; i < Config.N; i ++)
            {
                if (max_ms[i] > max_ms_local)
                {
                    max_ms_local = max_ms[i];
                    max_ms_app = i;
                    set_max_ms_app = true;
                }
            }

            while (!set_max_ms_app)
            {
                max_ms_app = RandomNumber(0,4);
                set_max_ms_app = true;
            }
             
            return max_ms_app;
        }

        public void compute_ucp_allocation()
        {
            int balance = Config.proc.cache_assoc;
            double alloc = 0;
            for (int i = 0; i < Config.N; i ++)
            {
                allocation[i] = 0;    
            }

            while (balance > 0)
            {
                for (int i = 0; i < Config.N; i ++)
                {
                    alloc = allocation[i];
                    get_max_mu(i, alloc, balance);
                }     
                int max_app = get_max_mu_app();
                allocation[max_app] += min_blk_max_mu[max_app];
                balance -= min_blk_max_mu[max_app];
            }
            return;
            
        }


        public void get_max_mu(int pid, double alloc, int balance)
        {
            double mu = 0;
            max_mu[pid] = -1000;
            min_blk_max_mu[pid] = 1;
            for (int i = 1; i <= balance; i ++)
            {
                mu = ((double)miss_count[pid, (int)alloc] - (double)miss_count[pid, (int)alloc + i])/((double)(i));
                if (mu >= max_mu[pid]) 
                {
                    max_mu[pid] = mu;
                    if (max_mu[pid] > 0) min_blk_max_mu[pid] = i;
                }
            }
        }

        public int get_max_mu_app()
        {
            double max_mu_local = 0;
            int max_mu_app = 0;
            for (int i = 0; i < Config.N; i ++)
            {
                if (max_mu[i] > max_mu_local || ((max_mu[i] == max_mu_local) && (RandomNumber(1,100) > 50)))
                {
                    max_mu_local = max_mu[i];
                    max_mu_app = i;
                }
            }
            return max_mu_app;
        }


        public double compute_miss_rate(int pid)
        {
            double miss_rate = (double)miss_count_current_notsampled[pid]/(double)Config.proc.quantum_cycles;
            miss_count_current_notsampled[pid] = 0;
            return miss_rate;
        }


        public double compute_miss_rate_alone(int pid, int high_priority_cycles)
        {
            double miss_rate_alone = (double)miss_count_current_alone_notsampled[pid]/(double)high_priority_cycles;
            return miss_rate_alone;
        }

        public double get_allocation(int pid)
        {
            return allocation[pid];
        }


        public void set_slowdown (int pid, double slowdown)
        {
            slowdown_unaware_cache_allocation[pid] = slowdown;
        }



        
    }
}
