using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Globalization;
using System.Threading;
using System.Diagnostics;
using System.Reflection;

namespace MemMap
{
    static class Dbg
    {
        //*IMPORTANT* mono's default Assert() statement just keeps on trucking even if it fails
        public static void Assert(bool val){
            Debug.Assert(val);
            if (!val) {
                throw new System.Exception("EXCEPTION");
            }
        }
    }

    class Sim
    {
        public static Proc[] procs;
        public static Xbar xbar;
        public static MemCtrl[] mctrls;
        public static MemWBMode mwbmode;
        public static BLPTracker blptracker;
        public static Cache shared_cache;
        public static CacheController cache_controller;
        public static StridePrefetcher stride_prefetcher;

        public static Stat stat;

        public static int PROC_MAX_LIMIT = 128;         //maximum number of processors supported
        public static ulong cycles = 0;                 //number of clock cycles past

        public static Random rand = new Random(0); 

        public static TextWriter periodic_writer_ipc;
        public static TextWriter periodic_writer_service;
 
        public static string rep_name_periodic_ipc;
        public static string rep_name_periodic_service;

        //Global hooks to support ASM
	public static int highest_rank_proc;
	public static int[] queuing_cycles;
        public static bool is_queueing_cycle;


        static void Main(string[] args)
        {
            //*IMPORTANT* without a trace listener, mono can't output Dbg.Assert() */
            Debug.Listeners.Add(new ConsoleTraceListener());

            Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;

            Config config = new Config();
            config.read(args);
            stat = new Stat();

            initialize();
            run();
            finish();

            //stats
            TextWriter tw = new StreamWriter(Config.output);
            stat.Report(tw);
            tw.Close();

        }

        static void initialize()
        {
            //processors

            //crossbar
            xbar = new Xbar();
            cache_controller = new CacheController();
            stride_prefetcher = new StridePrefetcher();

            //ddr3
            DDR3DRAM ddr3 = new DDR3DRAM(Config.mem.ddr3_type, Config.mem.clock_factor, Config.mem.tWR, Config.mem.tWTR);
            uint cmax = (uint)Config.mem.channel_max;
            uint rmax = (uint)Config.mem.rank_max;

            //randomized page table
            ulong page_size = 4 * 1024;
            PageRandomizer prand = new PageRandomizer(page_size);
            Req.prand = prand;


            //sequential page table
            PageSequencer pseq = new PageSequencer(page_size, cmax, rmax, ddr3.BANK_MAX);
            Req.pseq = pseq;

            if (Config.is_shared_cache)
            {
                shared_cache = new Cache();         
            }
            else
            {
                shared_cache = null;
            }
    
            procs = new Proc[Config.N];
            queuing_cycles = new int[Config.N];

            if (Config.aux_cache)
            {
                for (int p = 0; p < Config.N; p++)
                    if (shared_cache == null)              
                        procs[p] = new Proc(new Cache(), new AuxCache(), new L1Cache(), Config.traceFileNames[p]);
                    else
                    {
                        procs[p] = new Proc(shared_cache, new AuxCache(), new L1Cache(), Config.traceFileNames[p]);
                    }
            }
            else
            {
                for (int p = 0; p < Config.N; p++)
                    if (shared_cache == null)              
                        procs[p] = new Proc(new Cache(), new L1Cache(), Config.traceFileNames[p]);
                    else
                    {
                        Console.Write("Shared\n");
                        procs[p] = new Proc(shared_cache, new L1Cache(), Config.traceFileNames[p]);
                    }
            }

            if (Config.periodic_dump)
            {
                rep_name_periodic_service = Config.periodic_name + "_service_periodic.txt";
                periodic_writer_service = File.CreateText(rep_name_periodic_service);

                rep_name_periodic_ipc = Config.periodic_name + "_ipc_periodic.txt";
                periodic_writer_ipc = File.CreateText(rep_name_periodic_ipc);

            }


            //memory mapping
            MemMap.init(Config.mem.map_type, Config.mem.channel_max, Config.mem.rank_max, Config.mem.col_per_subrow, ddr3);

            //memory controllers
            mctrls = new MemCtrl[cmax];
            for (int i = 0; i < mctrls.Length; i++) {
                mctrls[i] = new MemCtrl(rmax, ddr3);
            }

            //memory schedulers and metamemory controllers
            if (!Config.sched.is_omniscient) {
                MemSched[] scheds = new MemSched[cmax];
                for (int i = 0; i < cmax; i++) {
                    scheds[i] = Activator.CreateInstance(Config.sched.typeof_sched_algo) as MemSched;
                }

                MemSched[] wbscheds = new MemSched[cmax];
                if (!Config.sched.same_sched_algo) {
                    for (int i = 0; i < cmax; i++) {
                        wbscheds[i] = Activator.CreateInstance(Config.sched.typeof_wbsched_algo) as MemSched;
                    }
                }
                else {
                    for (int i = 0; i < cmax; i++) {
                        wbscheds[i] = scheds[i];
                    }
                }

                MetaMemCtrl[] meta_mctrls = new MetaMemCtrl[cmax];
                for (int i = 0; i < cmax; i++) {
                    meta_mctrls[i] = new MetaMemCtrl(mctrls[i], scheds[i], wbscheds[i]);
                    mctrls[i].meta_mctrl = meta_mctrls[i];
                    scheds[i].meta_mctrl = meta_mctrls[i];
                    scheds[i].initialize();
                    wbscheds[i].meta_mctrl = meta_mctrls[i];
                    wbscheds[i].initialize();
                }
            }
            else {
                MemSched sched = Activator.CreateInstance(Config.sched.typeof_sched_algo) as MemSched;
                MemSched wbsched;
                if (!Config.sched.same_sched_algo) {
                    wbsched = Activator.CreateInstance(Config.sched.typeof_wbsched_algo) as MemSched;
                }
                else {
                    wbsched = sched;
                }
                
                MetaMemCtrl meta_mctrl = new MetaMemCtrl(mctrls, sched, wbsched);
                for (int i = 0; i < cmax; i++) {
                    mctrls[i].meta_mctrl = meta_mctrl;
                }
                sched.meta_mctrl = meta_mctrl;
                sched.initialize();
                wbsched.meta_mctrl = meta_mctrl;
                wbsched.initialize();
            }

            //wbmode
            mwbmode = Activator.CreateInstance(Config.mctrl.typeof_wbmode_algo, new Object[] { mctrls }) as MemWBMode;
            for (int i = 0; i < cmax; i++) {
                mctrls[i].mwbmode = mwbmode;
            }

            //blp tracker
            blptracker = new BLPTracker(mctrls);

        }

        static void run()
        {
            //DateTime start_time = DateTime.Now;

            bool[] is_done = new bool[Config.N];
            for (int i = 0; i < Config.N; i++) {
                is_done[i] = false;
            }

            bool finished = false;
            while (!finished) {
                finished = true;

                //cache controller

                //processors
                int pid = rand.Next(Config.N);
                for (int i = 0; i < Config.N; i++) {
                    Proc curr_proc = procs[pid];
                    curr_proc.tick();
                    pid = (pid + 1) % Config.N;
                }

                //memory controllers
                for (int i = 0; i < Config.mem.channel_max; i++) {
                    mctrls[i].tick();
                }

                //blp tracker
                blptracker.tick();

                //xbar
                xbar.tick();

                //progress simulation time
                cycles++;

                //case #1: instruction constrained simulation
                if (Config.sim_type == Config.SIM_TYPE.INST) {
                    for (int p = 0; p < Config.N; p++) {
                        if (is_done[p]) continue;

                        if (Stat.procs[p].ipc.Count >= Config.sim_inst_max) {
                            //simulation is now finished for this processor
                            finish_proc(p);
                            is_done[p] = true;
                        }
                        else {
                            //simulation is still unfinished for this processor
                            finished = false;
                        }
                    }
                }

                //case #2: cycle constrained simulation  // default case
                else if (Config.sim_type == Config.SIM_TYPE.CYCLE) {
                    if (cycles >= Config.sim_cycle_max) {
                        finish_proc();
                        finished = true;
                    }
                    else {
                        finished = false;
                    }
                }

                //case #3: run to completion
                else if (Config.sim_type == Config.SIM_TYPE.COMPLETION) {
                    for (int p = 0; p < Config.N; p++) {
                        if (is_done[p]) continue;

                        if (procs[p].trace.finished) {
                            //simulation is now finished for this processor
                            finish_proc(p);
                            is_done[p] = true;
                        }
                        else {
                            //simulation is still unfinished for this processor
                            finished = false;
                        }
                    }
                }
            }
        }

        static void finish()
        {
            foreach (MemCtrlStat mctrl in Stat.mctrls) {
                mctrl.Finish(Sim.cycles);
            }
            foreach (BusStat bus in Stat.busses) {
                bus.Finish(Sim.cycles);
            }
            foreach (BankStat bank in Stat.banks) {
                bank.Finish(Sim.cycles);
            }


        }

        static void finish_proc()
        {
            for (int pid = 0; pid < Config.N; pid++) {
                finish_proc(pid);
            }
        }

        static void finish_proc(int pid)
        {
            Stat.procs[pid].Finish(Sim.cycles);
            foreach (MemCtrlStat mctrl in Stat.mctrls) {
                mctrl.Finish(Sim.cycles, pid);
            }
            foreach (BankStat bank in Stat.banks) {
                bank.Finish(Sim.cycles, pid);
            }
            if (!Config.sched.is_omniscient) 
            {

                for (int i = 0; i < Config.mem.channel_max; i ++) mctrls[i].meta_mctrl.sched.set_proc_done(pid);
            } 
            else
            {
                mctrls[0].meta_mctrl.sched.set_proc_done(pid);
            }
            
        }
    }
}
