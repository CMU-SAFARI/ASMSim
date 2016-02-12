using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Globalization;


using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.GZip;

namespace MemMap
{
    class Proc
    {
        public static readonly ulong NULL_ADDRESS = ulong.MaxValue;
        //throttle
        public static Random rand = new Random(0);
        public double throttle_fraction = 0;

        //processor id
        private static int pmax = 0;
        public int pid;

        //components
        public InstWnd inst_wnd;
        public List<Req> mshr;
        public List<Req> wb_q;
        public Cache cache;
        public L1Cache l1_cache;
        public AuxCache aux_cache;

        //other components
        public Trace trace;


        //current status
        public ulong cycles;
        public int curr_cpu_inst_cnt;
        public ulong pc;
        public Req curr_rd_req;

        //retry memory request
        private bool mctrl_retry = false;
        private bool mshr_retry = false;

        //etc: outstanding requests
        public int out_read_req;

        //etc: stats
        ulong curr_quantum;
        ulong prev_read_req;
        ulong prev_write_req;

        private int prev_dump = 0;
        private ulong stall_shared_delta = 0;
        private ulong memory_fraction_cycles = 0;

        public ulong prev_inst_cnt = 0;
        public ulong prev_cycle = 0; 
        public LinkedList<Req> cache_hit_queue;
        public LinkedList<Req> mem_queue;
        public int quantum_cycles_left = Config.proc.quantum_cycles;
        //TOREM
        public double[] t_excess_per_proc;
        public double slowdown;

        public int alone_req_serv_rate_counter = 0;
        public int alone_counter_tracker = 0;
        public ulong alone_counter_tracker_addr = 0;

        public bool is_in_cache = false;
        public bool is_alone_hit = false;


        public CachePollutionVector pollution_vector;
        public ServiceCounter service_counter; 

        public StridePrefetcher stride_prefetcher;        
 
        public bool interference_bit;
        public int interference_bit_core;
        public ulong interference_bit_set_addr;

        public int setting_counter;
        public int high_priority_cycles;
        public int num_stall_cycles;
        public int service;
        public int service_alone;
        static int proc_count = 0;
        public bool just_set_full = false;
        public bool set_full = true;

        ulong total_read_latency;
        ulong high_priority_total_miss_latency;
        ulong high_priority_total_hit_latency;
        ulong high_priority_total_misses;
        ulong total_miss_latency;
        ulong total_hit_latency;
        public int inflight_mem_requests;
        public double queueing_latency;

       
        public Proc(Cache cache, L1Cache l1_cache, string trace_fname)
        {
            pid = pmax;
            pmax++;

            //components
            inst_wnd = new InstWnd(Config.proc.inst_wnd_max);
            mshr = new List<Req>(Config.proc.mshr_max);
            wb_q = new List<Req>(2 * Config.proc.wb_q_max);

            //other components
            Stat.procs[pid].trace_fname = trace_fname;
            trace = new Trace(pid, trace_fname);
            this.cache = cache;
            this.l1_cache = l1_cache;

            cache_hit_queue = new LinkedList<Req>();
            mem_queue = new LinkedList<Req>();
            pollution_vector = new CachePollutionVector();
            service_counter = new ServiceCounter();
            t_excess_per_proc = new double[Config.N];
            stride_prefetcher = new StridePrefetcher();

            //initialize
            curr_rd_req = get_req();
        }

	public Proc(Cache cache, AuxCache aux_cache, L1Cache l1_cache, string trace_fname)
        {
            pid = pmax;
            pmax++;

            //components
            inst_wnd = new InstWnd(Config.proc.inst_wnd_max);
            mshr = new List<Req>(Config.proc.mshr_max);
            wb_q = new List<Req>(2 * Config.proc.wb_q_max);

            //other components
            Stat.procs[pid].trace_fname = trace_fname;
            trace = new Trace(pid, trace_fname);
            this.cache = cache;
            this.aux_cache = aux_cache;
            this.l1_cache = l1_cache;


            cache_hit_queue = new LinkedList<Req>();
            mem_queue = new LinkedList<Req>();
            pollution_vector = new CachePollutionVector();
            service_counter = new ServiceCounter();
            t_excess_per_proc = new double[Config.N];
            stride_prefetcher = new StridePrefetcher();

            //initialize
            curr_rd_req = get_req();
            total_read_latency = 0;            
            high_priority_total_miss_latency = 0;
            high_priority_total_hit_latency = 0;
            high_priority_total_misses = 0;

            total_miss_latency = 0;
            total_hit_latency = 0;
 
        }



        public void recv_req(Req req)
        {
            //stats
            Stat.procs[pid].read_req_served.Collect();
            Stat.procs[pid].read_avg_latency.Collect(req.latency);
            total_read_latency += (ulong) req.latency;
            if (pid == Sim.highest_rank_proc) high_priority_total_misses += 1;

            
            inflight_mem_requests --;
            Req first_request = req;
            ulong wb_addr = Proc.NULL_ADDRESS;
            
            //free up instruction window and mshr
            inst_wnd.set_ready(req.block_addr);
            mshr.RemoveAll(x => x.block_addr == req.block_addr);
            if (Config.fst)
            {
                if ((req.block_addr == interference_bit_set_addr) && (interference_bit == true))
                {
                    interference_bit = false;
                    interference_bit_core = Config.N;
                }
            }

            if (!cache.has_addr(first_request.block_addr, ReqType.RD))
            {
                wb_addr = cache.cache_add(first_request.block_addr, first_request.proc_req_type, (ulong)pid);
                if (!l1_cache.has_addr(first_request.block_addr, ReqType.RD) && !first_request.is_prefetch) 
                {
                    l1_cache.cache_add(first_request.block_addr, first_request.proc_req_type, (ulong)pid);
                }
                l1_cache.cache_remove(wb_addr, ReqType.RD);                
            }
           
            if (Config.aux_cache)
            {
                if (!aux_cache.has_addr(first_request.block_addr, ReqType.RD))
                {
                    aux_cache.cache_add(first_request.block_addr, first_request.proc_req_type, (ulong)pid);
                }
            }

            if (Config.proc.wb == false) wb_addr = Proc.NULL_ADDRESS; 
            
            if (wb_addr != Proc.NULL_ADDRESS)
            {
                Req wb_req = RequestPool.depool();
                wb_req.set(pid, ReqType.WR, ReqType.NULL, wb_addr);
                bool wb_merge = wb_q.Exists(x => x.block_addr == wb_req.block_addr);
                if (!wb_merge) {
                    wb_q.Add(wb_req);
                }
                else {
                    RequestPool.enpool(wb_req);
                }
            }

            //destory req
            RequestPool.enpool(req);
            out_read_req--;
        }

        public void recv_wb_req(Req req)
        {
            //stats
            Stat.procs[pid].write_req_served.Collect();
            Stat.procs[pid].write_avg_latency.Collect(req.latency);

            //destroy req
            RequestPool.enpool(req);
        }

        public Req get_req()
        {
            Dbg.Assert(curr_cpu_inst_cnt == 0);

            Req wb_req = null;
            if (Config.pc_trace) 
            {
                trace.get_req(ref curr_cpu_inst_cnt, out curr_rd_req, out wb_req, ref pc);
                curr_rd_req.pc = pc;
            }
            else trace.get_req(ref curr_cpu_inst_cnt, out curr_rd_req, out wb_req);

            return curr_rd_req;
        }


        public bool issue_wb_req(Req wb_req)
        {
            if (Config.model_memory)
            {
                bool mctrl_ok = insert_mctrl(wb_req);
                return mctrl_ok;
            }
            else
            {
                add_to_mem_queue(curr_rd_req);
                return true; 
            }
        }

        public bool reissue_rd_req()
        {
            //retry mshr
            if (mshr_retry) {
                Dbg.Assert(!mctrl_retry);

                //retry mshr
                bool mshr_ok = insert_mshr(curr_rd_req);
                if (!mshr_ok) 
                    return false;
                
                //success
                mshr_retry = false;

                //check if true miss
                bool false_miss = inst_wnd.is_duplicate(curr_rd_req.block_addr);
                Dbg.Assert(!false_miss);

            }

            mctrl_retry = request_retry(curr_rd_req);

            //retry mctrl
            if (mctrl_retry) {
                Dbg.Assert(!mshr_retry);

                //retry mctrl
                bool mctrl_ok = insert_mctrl(curr_rd_req);
                if (!mctrl_ok) 
                    return false;
                
                //success
                mctrl_retry = false;
                Stat.procs[pid].l2_cache_miss_count.Collect();
                return true;
            }
            return true;

        }

        public void add_to_cache_queue(Req req)
        {
            req.ts_departure = (long)(cycles + (ulong)Config.cache_hit_latency); 
            cache_hit_queue.AddLast(req);
            inst_wnd.add(req.block_addr, true, false, req.pc);
            return;
            
        }

        public void add_to_mem_queue(Req req)
        {
          
            req.ts_departure = (long)(cycles + (ulong)Config.mem_latency); 
            mem_queue.AddLast(req);
            return;
            
        }

        public void service_cache_queue()
        {
            while (cache_hit_queue.Count != 0)
            {
                if (pid == Sim.highest_rank_proc) high_priority_total_hit_latency ++;
                total_hit_latency ++;
                Req first_request = cache_hit_queue.First.Value;
                if ((ulong)first_request.ts_departure <= cycles) 
                {
                    if (!l1_cache.has_addr(first_request.block_addr, ReqType.RD)) {
                       l1_cache.cache_add(first_request.block_addr, first_request.type, (ulong)pid);
                    }    
                   
                    cache_hit_queue.RemoveFirst();
                    RequestPool.enpool(first_request); 
                    mshr.RemoveAll(x => x.block_addr == first_request.block_addr); 
                    inst_wnd.set_ready(first_request.block_addr);
                }
                else return;
            }
        }


        public void service_mem_queue()
        {
            while (mem_queue.Count != 0)
            {
                Req first_request = mem_queue.First.Value;
                if ((ulong)first_request.ts_departure <= cycles) 
                {
                    Stat.procs[pid].read_req_served.Collect();
                    Stat.procs[pid].read_avg_latency.Collect(first_request.latency);
                    ulong wb_addr = Proc.NULL_ADDRESS;
                    if (Config.fst)
                    {
                        if ((first_request.block_addr == interference_bit_set_addr) && (interference_bit == true))
                        {
                            interference_bit = false;
                            interference_bit_core = Config.N;
                        }
                    }

                    if (!cache.has_addr(first_request.block_addr, first_request.type))
                    {
                        wb_addr = cache.cache_add(first_request.block_addr, first_request.proc_req_type, (ulong)pid);
                        if (!l1_cache.has_addr(first_request.block_addr, ReqType.RD))
                        {
                            l1_cache.cache_add(first_request.block_addr, first_request.proc_req_type, (ulong)pid);
                        }
                        l1_cache.cache_remove(wb_addr, ReqType.RD);
                    }
 
                    if (Config.aux_cache)
                    {
                        if (!aux_cache.has_addr(first_request.block_addr, ReqType.RD))
                        {
                            aux_cache.cache_add(first_request.block_addr, first_request.type, (ulong)pid);
                        }
                    }

                    if (Config.proc.wb == false) wb_addr = Proc.NULL_ADDRESS; 
                    if (wb_addr != Proc.NULL_ADDRESS)
                    {
                        Req wb_req = RequestPool.depool();
                        wb_req.set(pid, ReqType.WR, ReqType.NULL, wb_addr);
                        bool wb_merge = wb_q.Exists(x => x.block_addr == wb_req.block_addr);
                        if (!wb_merge) {
                            wb_q.Add(wb_req);
                        }
                        else {
                            RequestPool.enpool(wb_req);
                        }
                    }
                    mem_queue.RemoveFirst();
                    RequestPool.enpool(first_request); 
                    inst_wnd.set_ready(first_request.block_addr);
                }
                else return;
            }
        }

        public void issue_insts(bool issued_rd_req)
        {
            //issue instructions
            for (int i = 0; i < Config.proc.ipc; i++) {
                if (inst_wnd.is_full()) {
                    if (i == 0) Stat.procs[pid].stall_inst_wnd.Collect();
                    return;
                }

                //cpu instructions
                if (curr_cpu_inst_cnt > 0) {
                    curr_cpu_inst_cnt--;
                    inst_wnd.add(0, false, true, 0);
                    continue;
                }

                //only one memory instruction can be issued per cycle
                if (issued_rd_req)
                    return;

                //check if true miss
                bool false_miss = inst_wnd.is_duplicate(curr_rd_req.block_addr);
                if (false_miss) {
                    bool get_ready = inst_wnd.get_ready_status(curr_rd_req.block_addr);
                    bool get_alone_hit = inst_wnd.get_alone_hit_status(curr_rd_req.block_addr);
                    inst_wnd.add(curr_rd_req.block_addr, true, get_ready, get_alone_hit, curr_rd_req.pc);
                    Dbg.Assert(curr_rd_req.wb_req == null);
                    RequestPool.enpool(curr_rd_req);
                    curr_rd_req = get_req();
                    continue;
                }

                if (!Config.is_cache_filtered)
                {
                   bool is_in_l1_cache = false;
                   is_in_l1_cache = l1_cache.has_addr(curr_rd_req.block_addr, curr_rd_req.proc_req_type);
                   if (is_in_l1_cache)
                   {
                      if (pid == Sim.highest_rank_proc) Sim.cache_controller.count_l1_hits(pid, true); 
                      Sim.cache_controller.count_l1_hits(pid, false); 
                      Stat.procs[pid].l1_cache_hit_count.Collect();
                      inst_wnd.add(curr_rd_req.block_addr, true, true, true, curr_rd_req.pc);
                      RequestPool.enpool(curr_rd_req);
                      curr_rd_req = get_req();
                      continue;
                   }
                   bool mshr_ok = insert_mshr(curr_rd_req);
                   if (!mshr_ok) {
                       mshr_retry = true;
                       return;
                   }
                   Stat.procs[pid].l1_cache_miss_count.Collect();
                   if (pid == Sim.highest_rank_proc) Sim.cache_controller.count_l1_misses(pid, true);
                   Sim.cache_controller.count_l1_misses(pid, false); 
                }

                is_in_cache = false;
                is_alone_hit = false;

                if (!Config.is_cache_filtered)
                {
                   int way_sampled_set = cache.has_addr_sampled_set(curr_rd_req.block_addr, curr_rd_req.proc_req_type);
                   is_in_cache = cache.has_addr(curr_rd_req.block_addr, curr_rd_req.proc_req_type);
                   //Count high-priority and non-high-priority hits and misses in the sampled sets
                   if (!is_in_cache) Sim.cache_controller.count_misses(pid, way_sampled_set, false);
                   if (!is_in_cache && (pid == Sim.highest_rank_proc)) Sim.cache_controller.count_misses(pid, way_sampled_set, true);
                   if (is_in_cache && (pid == Sim.highest_rank_proc)) Sim.cache_controller.count_current_hits(pid, way_sampled_set, true);
                   if (is_in_cache) Sim.cache_controller.count_current_hits(pid, way_sampled_set, false);

                   //Count high-priority and non-high-priority hits and misses in all sets
                   if (!is_in_cache) Sim.cache_controller.count_all_misses(pid, false);
                   if (!is_in_cache && (pid == Sim.highest_rank_proc)) Sim.cache_controller.count_all_misses(pid, true);
                   if (is_in_cache && (pid == Sim.highest_rank_proc)) Sim.cache_controller.count_all_hits(pid, true);
                   if (is_in_cache) Sim.cache_controller.count_all_hits(pid, false);

                   if (Config.aux_cache)
                   {
                       way_sampled_set = aux_cache.has_addr_sampled_set(curr_rd_req.block_addr, curr_rd_req.proc_req_type);
                       is_alone_hit = aux_cache.has_addr(curr_rd_req.block_addr, curr_rd_req.proc_req_type);
                       //count aux tag store hits in sampled sets
                       if (pid == Sim.highest_rank_proc) Sim.cache_controller.count_aux_hits_alone(pid, way_sampled_set);
                       Sim.cache_controller.count_aux_hits(pid, way_sampled_set);
                   }
                   if (Config.fst)
                   {
                       if (Config.poll_filter) is_alone_hit = pollution_vector.check_filter(curr_rd_req.block_addr);
                       else if (Config.aux_cache) is_alone_hit = aux_cache.has_addr(curr_rd_req.block_addr, curr_rd_req.proc_req_type);
                   }
                }
                if (Config.fst)
                {
                    if (is_alone_hit)
                    {
                        curr_rd_req.alone_counter_tracker = (int)Config.mem_latency;
                        setting_counter ++;
                        interference_bit = true;
                        if (Config.poll_filter) interference_bit_core = pollution_vector.get_interfering_core(curr_rd_req.block_addr);
                        interference_bit_set_addr = curr_rd_req.block_addr;
                    }
                }
                if (is_alone_hit)
                {
                   curr_rd_req.is_alone_hit = true;
                }

//                if (Config.stride_prefetcher_on) stride_prefetcher.process_request(pid, curr_rd_req.block_addr, curr_rd_req.pc);

                //check if already in cache
                if (!Config.is_cache_filtered)
                {
                   if (is_in_cache)
                   {
                      Stat.procs[pid].l2_cache_hit_count.Collect();
                      add_to_cache_queue(curr_rd_req);
//                      RequestPool.enpool(curr_rd_req); 
                      curr_rd_req = get_req();
                      continue;
                   }
                }
          
                if (Config.stride_prefetcher_on) stride_prefetcher.process_request(pid, curr_rd_req.block_addr, curr_rd_req.pc);

                inst_wnd.add(curr_rd_req.block_addr, true, false, curr_rd_req.is_alone_hit, curr_rd_req.pc);

                if (Config.model_memory)
                {
                    //try memory controller
                    bool mctrl_ok = insert_mctrl(curr_rd_req);
                    if (!mctrl_ok) {
                        mctrl_retry = true;
                        return;
                    }
                }
                else
                {
                    add_to_mem_queue(curr_rd_req);
                }
                Stat.procs[pid].l2_cache_miss_count.Collect();

                //issued memory request
                issued_rd_req = true;

                //get new read request
                curr_rd_req = get_req();
            }

        }

        public bool request_retry(Req curr_rd_req)
        {
            Stat.procs[pid].l1_cache_miss_count.Collect();
            if (pid == Sim.highest_rank_proc) Sim.cache_controller.count_l1_misses(pid, true);
            Sim.cache_controller.count_l1_misses(pid, false); 

            is_in_cache = false;
            is_alone_hit = false;

            if (!Config.is_cache_filtered)
            {
               int way_sampled_set = cache.has_addr_sampled_set(curr_rd_req.block_addr, curr_rd_req.proc_req_type);
               is_in_cache = cache.has_addr(curr_rd_req.block_addr, curr_rd_req.proc_req_type);
               if (!is_in_cache) Sim.cache_controller.count_misses(pid, way_sampled_set, false);
               if (!is_in_cache && (pid == Sim.highest_rank_proc)) Sim.cache_controller.count_misses(pid, way_sampled_set, true);
               if (!is_in_cache) Sim.cache_controller.count_all_misses(pid, false);
               if (!is_in_cache && (pid == Sim.highest_rank_proc)) Sim.cache_controller.count_all_misses(pid, true);
               if (is_in_cache && (pid == Sim.highest_rank_proc)) Sim.cache_controller.count_current_hits(pid, way_sampled_set, true);
               if (is_in_cache) Sim.cache_controller.count_current_hits(pid, way_sampled_set, false);
               if (is_in_cache && (pid == Sim.highest_rank_proc)) Sim.cache_controller.count_all_hits(pid, true);
               if (is_in_cache) Sim.cache_controller.count_all_hits(pid, false);

               if (Config.aux_cache)
               {
                   way_sampled_set = aux_cache.has_addr_sampled_set(curr_rd_req.block_addr, curr_rd_req.proc_req_type);
                   is_alone_hit = aux_cache.has_addr(curr_rd_req.block_addr, curr_rd_req.proc_req_type);
                   if (pid == Sim.highest_rank_proc) Sim.cache_controller.count_aux_hits_alone(pid, way_sampled_set);
                   Sim.cache_controller.count_aux_hits(pid, way_sampled_set);
               }
               if (Config.fst)
               {
                   if (Config.poll_filter) is_alone_hit = pollution_vector.check_filter(curr_rd_req.block_addr);
                   else if (Config.aux_cache) is_alone_hit = aux_cache.has_addr(curr_rd_req.block_addr, curr_rd_req.proc_req_type);
//                   is_alone_hit = pollution_vector.check_filter(curr_rd_req.block_addr);
               }
            }
            if (Config.fst)
            {
                if (is_alone_hit)
                {
                    curr_rd_req.alone_counter_tracker = (int)Config.mem_latency;
                    interference_bit = true;
                    if (Config.poll_filter) interference_bit_core = pollution_vector.get_interfering_core(curr_rd_req.block_addr);
                    interference_bit_set_addr = curr_rd_req.block_addr;
                }
            }
            if (is_alone_hit  && !is_in_cache)
            {
               curr_rd_req.is_alone_hit = true;
            }

            //check if already in cache
            if (!Config.is_cache_filtered)
            {
               if (is_in_cache)
               {
                  Stat.procs[pid].l2_cache_hit_count.Collect();
                  add_to_cache_queue(curr_rd_req);
                  return false;
               }
            }

            inst_wnd.add(curr_rd_req.block_addr, true, false, curr_rd_req.is_alone_hit, curr_rd_req.pc);

            if (Config.model_memory)
            {
                return true;    
            }
            else
            {
                add_to_mem_queue(curr_rd_req);
                return false;            
            }
        }

        public void dec_alone_counter()
        {
            foreach (Req req in mem_queue)
            {
                if (req.alone_counter_tracker > 0) req.alone_counter_tracker --;
            }
        }

        public void tick()
        {
            /*** Preamble ***/
            
            cycles++;
            
            Stat.procs[pid].cycle.Collect();

            if (Config.asm && pid == Sim.highest_rank_proc) high_priority_cycles ++;
            if (Config.asm && pid == Sim.highest_rank_proc && inflight_mem_requests > 0) high_priority_total_miss_latency ++;
            if (inflight_mem_requests > 0) total_miss_latency ++;
            
            ulong inst_cnt = Stat.procs[pid].ipc.Count;
            Stat.procs[pid].num_outstanding_req.Collect(out_read_req);
//            if ((inst_cnt/(ulong)Config.eriodicDumpWindow) > (ulong)prev_dump)
            if (cycles % (ulong)Config.proc.quantum_cycles  == 0)
            {
                prev_dump ++;
//                Sim.periodic_writer_ipc.WriteLine(" Proc " + pid + " Cycles " + cycles + " Instructions " + inst_cnt  + " " + (double)(inst_cnt - prev_inst_cnt) / (double)(cycles - prev_cycle));
//                Sim.periodic_writer_ipc.WriteLine(" Proc " + pid + " Cycles " + cycles + " Instructions " + ((ulong)prev_dump * (ulong)Config.periodicDumpWindow)  +  " " + (double)(inst_cnt - prev_inst_cnt) / (double)(cycles - prev_cycle));
                prev_inst_cnt = inst_cnt;
                prev_cycle = cycles;
                Sim.periodic_writer_ipc.Flush();

            }

            if (Config.poll_filter && cycles % (ulong)Config.proc.clear_poll_filter_cycles == 0)
            {
                pollution_vector.clear_vector();
            }

            if (quantum_cycles_left > 0)
            {
                quantum_cycles_left --;
            }
            else
            {
                if (Config.fst)
                {
                    double alone_counter = 0;
                    if (Config.aux_cache) alone_counter = service_counter.get_together_counter() - (service_counter.get_excess_counter() * (Sim.cache_controller.hit_count_current_notsampled[pid] + Sim.cache_controller.miss_count_current_notsampled[pid])/(Sim.cache_controller.hit_count_current[pid] + Sim.cache_controller.miss_count_current[pid]));
                    else alone_counter = service_counter.get_together_counter() - service_counter.get_excess_counter();
                    
                    slowdown = service_counter.get_together_counter()/alone_counter;
                    Sim.cache_controller.set_slowdown(pid,slowdown); 
                    Sim.periodic_writer_service.WriteLine(" Proc " + pid + " Slowdown " + slowdown + "\n");
                    Sim.periodic_writer_service.Flush();
 
                }
                else if (Config.asm) 
                {
                    if (Config.count_queueing_cycles == false) Sim.queuing_cycles[pid] = 0;
                    Sim.cache_controller.compute_mrc(pid, (int)(inst_cnt - prev_inst_cnt), (high_priority_cycles - Sim.queuing_cycles[pid]));
                    Sim.cache_controller.compute_hrc(pid, (int)(inst_cnt - prev_inst_cnt), (high_priority_cycles - Sim.queuing_cycles[pid]));
                    slowdown = Sim.cache_controller.compute_l1_mpkc_slowdown(pid, high_priority_cycles, (high_priority_total_miss_latency), high_priority_total_hit_latency, (ulong)Sim.queuing_cycles[pid]);
                    if (Config.slowdown_allocation || Config.speedup_allocation || Config.qos_allocation || Config.naive_qos_allocation) Sim.cache_controller.compute_l1_mpkc_slowdown_associativity(pid, high_priority_cycles, high_priority_total_miss_latency, high_priority_total_hit_latency, total_miss_latency, total_hit_latency, (ulong)Sim.queuing_cycles[pid]);

                    Sim.periodic_writer_service.WriteLine(" Proc " + pid + " Slowdown " + slowdown + "\n");
                    Sim.periodic_writer_service.Flush();
                    Sim.cache_controller.clear_hits(pid);
                    high_priority_total_miss_latency = 0;
                    high_priority_total_hit_latency = 0;
                    total_miss_latency = 0;
                    total_hit_latency = 0;
                    high_priority_total_misses = 0;
                    high_priority_cycles = 0;
                    prev_inst_cnt = inst_cnt;
                }
                
                if (proc_count < Config.N-1) proc_count ++;
                else {
                    proc_count = 0;
                    if (Config.ucp) Sim.cache_controller.compute_ucp_allocation();
                    else if (Config.slowdown_allocation || Config.speedup_allocation) Sim.cache_controller.compute_overall_slowdown_allocation();
                    else if (Config.qos_allocation) Sim.cache_controller.compute_qos_slowdown_allocation();
                    else if (Config.naive_qos_allocation) Sim.cache_controller.compute_naive_qos_slowdown_allocation();
                    if (Config.fst) 
                    {
                        for (int i = 0; i < Config.N; i ++) {
                            Proc curr_proc = Sim.procs[i];
                            curr_proc.service_counter.clear_time_counters();
                        }
                    }
                }
                 
                quantum_cycles_left = Config.proc.quantum_cycles;
            }
 
            if (!Config.model_memory) service_mem_queue();
            service_cache_queue(); 
            if (inst_cnt != 0 && inst_cnt % 1000000 == 0) {
                ulong quantum = inst_cnt / 1000000;
                if (quantum > curr_quantum) {
                    curr_quantum = quantum;

                    ulong read_req = Stat.procs[pid].read_req.Count;
                    Stat.procs[pid].read_quantum.EndQuantum(read_req - prev_read_req);
                    prev_read_req = read_req;

                    ulong write_req = Stat.procs[pid].write_req.Count;
                    Stat.procs[pid].write_quantum.EndQuantum(write_req - prev_write_req);
                    prev_write_req = write_req;
                }
            }


            /*** Retire ***/
            int retired = inst_wnd.retire(Config.proc.ipc);
            Stat.procs[pid].ipc.Collect(retired);

            if (!inst_wnd.is_oldest_ready() && retired != Config.proc.ipc) {
                stall_shared_delta++;   //used only in the STFM algorithm
                num_stall_cycles ++;
            }


            if (Config.fst)
            {
                dec_alone_counter();
                service_counter.inc_together_counter();
                if (Config.model_memory && Config.sched.is_omniscient)
                {
                    if (Config.aux_cache)
                    {                       
                        if ((Sim.mctrls[0].meta_mctrl.interference_bit_bank[pid] == true || Sim.mctrls[0].meta_mctrl.interference_bit_rowbuffer[pid] == true || (interference_bit == true && inst_wnd.is_oldest_alone_hit() && inst_wnd.is_full())) && (inst_wnd.is_full() && cache.is_sampled_set(inst_wnd.addr_oldest())))
                        {
                            service_counter.inc_excess_counter();
                        }
                    }
                    else
                    {
                        if ((Sim.mctrls[0].meta_mctrl.interference_bit_bank[pid] == true || Sim.mctrls[0].meta_mctrl.interference_bit_rowbuffer[pid] == true || (interference_bit == true && inst_wnd.is_full())) && inst_wnd.is_full())
                        {
                            service_counter.inc_excess_counter();
                        }
                    }
                }
            }

            if (is_req_outstanding())
            {
                Stat.procs[pid].memory_cycle.Collect();
                memory_fraction_cycles ++;
            }
            /*** Issue writeback request ***/
            if (Config.proc.wb && wb_q.Count > 0) {
                bool wb_ok = issue_wb_req(wb_q[0]);
                if (wb_ok) {
                    wb_q.RemoveAt(0);
                }

                //writeback stall
                bool stalled_wb = wb_q.Count > Config.proc.wb_q_max;
                if (stalled_wb)
                    return;
            }

            /*** Reissue previous read request ***/
            bool issued_rd_req = false;
            if (mshr_retry || mctrl_retry) {
                if (inst_wnd.is_full()) {
                    Stat.procs[pid].stall_inst_wnd.Collect();
                    return;
                }
                Dbg.Assert(curr_rd_req != null && curr_cpu_inst_cnt == 0);

                //mshr/mctrl stall
                bool reissue_ok = reissue_rd_req();
                if (!reissue_ok) 
                    return;

                //reissue success
                Dbg.Assert(!mshr_retry && !mctrl_retry);
                issued_rd_req = true;
                curr_rd_req = get_req();
            }

            /*** Issue instructions ***/
            Dbg.Assert(curr_rd_req != null);
            issue_insts(issued_rd_req);
        }

        private bool is_req_outstanding()
        {
            return (mshr.Count != 0);
        }

        private bool insert_mshr(Req req)
        {
            if (mshr.Count == mshr.Capacity) {
                Stat.procs[pid].stall_mshr.Collect();
                return false;
            }
            mshr.Add(req);
            return true;
        }

        private bool insert_mctrl(Req req)
        {
            MemAddr addr = req.addr;

//            req.type = ReqType.RD;
            //failure
            if (Sim.mctrls[addr.cid].is_q_full(pid, req.type, addr.rid, addr.bid)) {
                if (req.type == ReqType.RD) {
                    Stat.procs[req.pid].stall_read_mctrl.Collect();
                }
                else {
                    Stat.procs[req.pid].stall_write_mctrl.Collect();
                }
                return false;
            }
            //success
            send_req(req);
            return true;
        }

        public void send_req(Req req)
        {
            inflight_mem_requests ++;
            switch (req.type) {
                case ReqType.RD:
//                case ReqType.WR:
                    Stat.procs[pid].rmpc.Collect();
                    Stat.procs[pid].read_req.Collect();
                    req.callback = new Callback(recv_req);
                    out_read_req++;
                    break;
                case ReqType.WR:
                    Stat.procs[pid].wmpc.Collect();
                    Stat.procs[pid].write_req.Collect();
                    req.callback = new Callback(recv_wb_req);
                    break;
            }

            Stat.procs[pid].req.Collect();
            Sim.mctrls[req.addr.cid].enqueue_req(req);
        }

        public override string ToString()
        {
            return "Processor " + pid;
        }

        /**
         * The number of shared stalled cycles are used in the STFM (Stall Time Fair Memory Scheduler, aka "IDEAL_MICRO_MEMSCHED")
         */
        public ulong get_stall_shared_delta()
        {
            ulong temp = stall_shared_delta;
            stall_shared_delta = 0;
            return temp;
        }

        public ulong get_memory_fraction_cycles()
        {
            ulong temp = memory_fraction_cycles;
            memory_fraction_cycles = 0;
            return temp;
        }

        public ulong get_total_read_latency()
        {
            return total_read_latency;
        }

        public void reset_total_read_latency()
        {
            total_read_latency = 0;
        }


    }
}
