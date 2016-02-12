using System;
using System.Collections.Generic;
using System.Text;

using System.IO;

namespace MemMap
{
    public abstract class MemSched
    {
        //statistics counters
        public int[,] streak_length = new int[Config.N, 17]; 
        public int pid_last_req;
        public int last_streak_length = 1;
        public bool[] proc_done = new bool[Config.N];
        public int[] request_count = new int[Config.N];
        public int[,]interval_request_count = new int[Config.N, 101];
        
        //memory controller
        public MetaMemCtrl meta_mctrl;

        public virtual void initialize() { }
        public virtual void issue_req(Req req) { }
        public abstract void dequeue_req(Req req);
        public abstract void enqueue_req(Req req);

        //scheduler-specific overridden method
        public abstract Req better_req(Req req1, Req req2);
        
        public virtual void tick() 
        {

           for (int p = 0; p < Config.N; p ++)
           {
               meta_mctrl.interference_bit_bank[p] = false;
           }
           for (int p = 0; p < Config.N; p ++)
           {
               foreach (Bank b in meta_mctrl.banks) {
                   if (meta_mctrl.get_curr_req(b) != null)
                   {
                       if (meta_mctrl.get_curr_req(b).pid != p) 
                       {
                           if (meta_mctrl.proc_req_in_queue(p, b))
                           {
                               meta_mctrl.interference_bit_bank[p] = true;
                               meta_mctrl.interference_bit_bank_proc[p] = meta_mctrl.get_curr_req(b).pid;
                           }
                           
                       }
                       else meta_mctrl.interference_bit_bank_proc[p] = p;
                     
                   }
               }
           }
           if (meta_mctrl.get_cycles() % 1000 == 0)
           {
               count_requests_periodic();
               compute_blp();
           }
        }

        protected bool is_row_hit(Req req)
        {
            return meta_mctrl.is_row_hit(req);
        }

        public virtual void service_counter(Req req)
        {
            return;
        }

        
        public virtual void count_queueing(Cmd cmd)
        {
            if (cmd == null) return;
            Req req = cmd.req;

            MemCtrl mctrl = meta_mctrl.get_mctrl(req);
            ulong shadow_rowid = mctrl.shadow_rowid_per_procrankbank[req.pid, req.addr.rid, req.addr.bid];
            ulong rowid = mctrl.rowid_per_procrankbank[req.addr.rid, req.addr.bid];
            if (Config.stride_prefetcher_on)
            {
                if (!req.is_prefetch && shadow_rowid == req.addr.rowid && rowid != req.addr.rowid) 
                {
                    meta_mctrl.rbhit_interference_addr[req.pid] = req.addr.rowid;
                    meta_mctrl.interference_bit_rowbuffer[req.pid] = true;
                }
            }
            else
            {
                if (shadow_rowid == req.addr.rowid && rowid != req.addr.rowid) 
                {
                    meta_mctrl.rbhit_interference_addr[req.pid] = req.addr.rowid;
                    meta_mctrl.interference_bit_rowbuffer[req.pid] = true;
                }
            }
            return;
        }


        public virtual void count_queueing(Cmd cmd, int pid)
        {
            if (cmd == null) return;
            Req req = cmd.req;

            MemCtrl mctrl = meta_mctrl.get_mctrl(req);
            ulong shadow_rowid = mctrl.shadow_rowid_per_procrankbank[req.pid, req.addr.rid, req.addr.bid];
            ulong rowid = mctrl.rowid_per_procrankbank[req.addr.rid, req.addr.bid];
            if (Config.stride_prefetcher_on)
            {
                if (!req.is_prefetch && shadow_rowid == req.addr.rowid && rowid != req.addr.rowid && pid != req.pid) 
                {
                    meta_mctrl.rbhit_interference_addr[req.pid] = req.addr.rowid;
                    meta_mctrl.interference_bit_rowbuffer[req.pid] = true;
                    meta_mctrl.interference_bit_rowbuffer_proc[req.pid] = pid;
    
                    foreach (Req r in Sim.procs[req.pid].mshr)
                    {
                        if (r.block_addr == req.block_addr) r.interference_cycles ++;
                    }
    
                }
            }
            else
            {
                if (shadow_rowid == req.addr.rowid && rowid != req.addr.rowid && pid != req.pid) 
                {
                    meta_mctrl.rbhit_interference_addr[req.pid] = req.addr.rowid;
                    meta_mctrl.interference_bit_rowbuffer[req.pid] = true;
                    meta_mctrl.interference_bit_rowbuffer_proc[req.pid] = pid;
    
                    foreach (Req r in Sim.procs[req.pid].mshr)
                    {
                        if (r.block_addr == req.block_addr) r.interference_cycles ++;
                    }
    
                }
            }
            return;
        }

        public virtual void reset_rbhit_bit(MemAddr addr)
        {
            for (int p = 0; p < Config.N; p ++)
                if (addr.rowid == meta_mctrl.rbhit_interference_addr[p]) meta_mctrl.interference_bit_rowbuffer[p] = false;
        }

        public virtual void set_proc_done(int pid)
        {
            proc_done[pid] = true;
        }
 
        public virtual void count_streaks(Req req)
        {
            if (pid_last_req != req.pid) 
            {
                if (!proc_done[pid_last_req])
                {
                    if (last_streak_length < 16) streak_length[pid_last_req, last_streak_length] ++;
                    else streak_length[pid_last_req, 16] ++;
                }

                last_streak_length = 1;
                pid_last_req = req.pid;
 
            }
            else last_streak_length ++;          
        }


        public virtual void count_requests(Req req)
        {
            if (!proc_done[req.pid]) 
            {
                request_count[req.pid] ++;
            }
        }

        public virtual void count_requests_periodic()
        {
           for (int p = 0; p < Config.N; p ++) request_count[p] = 0;
        }



        public virtual void bus_interference_count(Cmd cmd)
        {
            return;
        }


        public virtual Req find_best_req(List<Req> q)
        {
            if (q.Count == 0)
                return null;

            Req best_req = q[0];
            for (int i = 1; i < q.Count; i++) {
                best_req = better_req(best_req, q[i]);
            }
            return best_req;
        }

        public virtual void compute_blp()
        {
            for (uint p = 0; p < Config.N; p++) {
                int count_blp = 0;
                for (uint b = 0; b < meta_mctrl.get_bmax(); b++) {
                    if (meta_mctrl.get_load_per_procbank(p, b) > 0) {
                        count_blp ++; 
                    }
                }
                if (count_blp > 0) Stat.procs[p].blp.Collect(count_blp);
            }
        }


    }
}
