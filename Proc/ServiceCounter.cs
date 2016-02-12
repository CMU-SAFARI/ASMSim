using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace MemMap
{
    public class ServiceCounter
    {
        public double shared_request_counter;
        public double alone_request_counter;
        public double shared_cache_alone_request_counter;
        public double alone_request_counter_mem; 
        public double alone_request_counter_other; 
        public double srsr;
        public double arsr;
        public double arsr_mem;
        public double arsr_other;
        public double t_together;
        public double t_excess;
        public double [] t_excess_core;
  
        public ServiceCounter()
        {

            shared_request_counter = 0;
            alone_request_counter = 0;
            shared_cache_alone_request_counter = 0;
            srsr = 0;
            arsr = 0;
            t_excess_core = new double[Config.N];
        } 

        public void clear_service_counters()
        {
            shared_request_counter = 0;
            alone_request_counter = 0;
            shared_cache_alone_request_counter = 0;
        }


        public void clear_time_counters()
        {
            t_together = 0;
            t_excess = 0;
            for (int proc = 0; proc < Config.N; proc ++)
                for (int p = 0; p < Config.N; p ++)
                    t_excess_core[p] = 0;
        }
    
        public void inc_shared_counter()
        {
            shared_request_counter ++;
        }
        
        public void inc_alone_counter_mem()
        {
            alone_request_counter_mem ++;
        }

        public void inc_alone_counter_other()
        {
            alone_request_counter_other ++;
        }
    
        public void inc_alone_counter()
        {
            alone_request_counter ++;
        }

        public void inc_shared_cache_alone_counter()
        {
            shared_cache_alone_request_counter ++;
        }

        public void inc_shared_counter(double inc_value)
        {
            shared_request_counter += inc_value;
        }


        public void inc_shared_cache_alone_counter(double inc_value)
        {
            shared_cache_alone_request_counter += inc_value;
        }
    
        public void inc_alone_counter(double inc_value)
        {
            alone_request_counter += inc_value;
        }
 
        public void inc_alone_counter_mem(double inc_value)
        {
            alone_request_counter_mem += inc_value;
        }

        public void inc_alone_counter_other(double inc_value)
        {
            alone_request_counter_other += inc_value;
        }

        public double get_shared_cache_alone_counter()
        {
            return shared_cache_alone_request_counter;
        }

        public double get_shared_counter()
        {
            return shared_request_counter;
        }

        public double get_alone_counter()
        {
            return alone_request_counter;
        }

        public double compute_shared_cache_alone_service_rate(int interval_length)
        {
            srsr = (double)shared_cache_alone_request_counter/(double)interval_length;
            return srsr;
        } 
        public double compute_shared_service_rate(int interval_length)
        {
            srsr = (double)shared_request_counter/(double)interval_length;
            return srsr;
        } 
        
        public double compute_alone_service_rate(int interval_length)
        {
            arsr = (double)alone_request_counter/(double)interval_length;
            return arsr;
        }

        public double compute_alone_service_rate_mem(int interval_length)
        {
            arsr_mem = (double)(alone_request_counter_mem)/(double)interval_length;
            return arsr_mem;
        }

        public double compute_alone_service_rate_other(int interval_length)
        {
            arsr_other = (double)(alone_request_counter_other)/(double)interval_length;
            return arsr_other;
        }

        public void inc_together_counter()
        {
            t_together ++;
        }

        public void inc_excess_counter()
        {
            t_excess ++;
        }
        public void inc_excess_counter(ulong cycles)
        {
            t_excess += cycles;
        }

        public void inc_excess_counter(int interfering_pid)
        {
            t_excess_core[interfering_pid] ++;
        }

        public double get_together_counter()
        {
            return t_together;
        }

        public double get_excess_counter()
        {
            return t_excess;
        }

        public double get_excess_counter(int interfering_pid)
        {
            return t_excess_core[interfering_pid];
        }
 
    }

}
