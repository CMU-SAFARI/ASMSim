using System;
using System.Collections.Generic;
using System.Text;

using System.IO;

namespace MemMap {
    public delegate void Callback(Req req);

    public class MemCtrl {
        static uint cmax;
        public uint cid;

        //state
        public long cycles;
        public uint rmax;
        public uint bmax;

        //DDR3
        public DDR3DRAM.Timing timing;
        public uint col_max;
        public uint row_size;   //in bytes

        //components
        public Channel chan;
        public MetaMemCtrl meta_mctrl;

        //row-hit finder
        public RowHitFinder rh_finder;

        //waiting queues
        public List<Req>[,] readqs;
        public List<Req>[,] writeqs;

        //global waiting queue
        public int writeq_max;
        public List<Req> mctrl_writeq;

        //transitional queues
        public List<Req>[,] inflightqs;
        public List<Cmd>[,] cmdqs;
        static int INFLIGHTQ_MAX = 8;
        static int CMDQ_MAX = 3;

        //reserved bus transations (when request is serviced)
        public List<BusTransaction> bus_q;
        static uint BUS_TRANSACTIONS_MAX = 16;

        //writeback
        public bool wb_mode;
        public MemWBMode mwbmode;
        public uint reads_to_drain;
        public uint writes_to_drain;

        public long ts_start_wbmode = -1;
        public long ts_end_wbmode = -1;

        //stats
        public uint read_unloaded_time;
        public uint read_loaded_time;

        public uint rload;
        public uint[] rload_per_proc;
        public uint[, ,] rload_per_procrankbank;
        public ulong[, ,] shadow_rowid_per_procrankbank;
        public ulong[,] rowid_per_procrankbank;
        public int[,] pid_rowid_per_procrankbank;

        public uint wload;
        public uint[] wload_per_proc;
        public uint[, ,] wload_per_procrankbank;

        //writeback throttle
        public WBThrottle wbthrottle;

        //writeback mode stats
        public uint rds_per_wb_mode;
        public uint wbs_per_wb_mode;

        public ulong[] total_queueing_latency;

        public int[] curr_proc;

        //constructor
        public MemCtrl(uint rmax, DDR3DRAM ddr3)
        {
            this.cid = cmax;
            cmax++;

            //states
            this.rmax = rmax;
            this.bmax = ddr3.BANK_MAX;

            //DDR3
            timing = ddr3.timing;
            this.col_max = ddr3.COL_MAX;
            this.row_size = ddr3.COL_MAX * ddr3.CHANNEL_WIDTH;

            //components
            chan = new Channel(this, rmax, ddr3.BANK_MAX);

            //row-hit finder
            rh_finder = new RowHitFinder(this);

            //queues
            int readq_max = Config.mctrl.readq_max_per_bank;
            writeq_max = (int)this.rmax * (int)this.bmax * Config.mctrl.writeq_max_per_bank;

            readqs = new List<Req>[rmax, bmax];
            writeqs = new List<Req>[rmax, bmax];
            mctrl_writeq = new List<Req>(writeq_max);
            inflightqs = new List<Req>[rmax, bmax];
            cmdqs = new List<Cmd>[rmax, bmax];
            for (uint r = 0; r < rmax; r++) {
                for (uint b = 0; b < bmax; b++) {
                    readqs[r, b] = new List<Req>(readq_max);
                    writeqs[r, b] = new List<Req>(writeq_max);
                    inflightqs[r, b] = new List<Req>(INFLIGHTQ_MAX);
                    cmdqs[r, b] = new List<Cmd>(CMDQ_MAX);
                }
            }
            bus_q = new List<BusTransaction>((int)BUS_TRANSACTIONS_MAX);

            //stats
            rload_per_proc = new uint[Config.N];
            rload_per_procrankbank = new uint[Config.N, rmax, bmax];
            shadow_rowid_per_procrankbank = new ulong[Config.N, rmax, bmax];
            rowid_per_procrankbank = new ulong[rmax, bmax];
            pid_rowid_per_procrankbank = new int[rmax, bmax];

            wload_per_proc = new uint[Config.N];
            wload_per_procrankbank = new uint[Config.N, rmax, bmax];

            //writeback throttler
            wbthrottle = Activator.CreateInstance(Config.sched.typeof_wbthrottle_algo) as WBThrottle;

            total_queueing_latency = new ulong[Config.N];

            curr_proc = new int[bmax];
        }

        public void tick()
        {
            //must be the very first thing that's done
            cycles++;
            meta_mctrl.tick(cid);
            wbthrottle.tick();
            mwbmode.tick(cid);

            //load stats
            for (int p = 0; p < Config.N; p++) {
                //read load
                if (rload_per_proc[p] > 0)
                    Stat.mctrls[cid].rbinaryloadtick_per_proc[p].Collect();
                Stat.mctrls[cid].rloadtick_per_proc[p].Collect(rload_per_proc[p]);

                //write load
                if (wload_per_proc[p] > 0)
                    Stat.mctrls[cid].wbinaryloadtick_per_proc[p].Collect();
                Stat.mctrls[cid].wloadtick_per_proc[p].Collect(wload_per_proc[p]);
            }

            //busy/idle stats
            if (rload > 0) {
                read_loaded_time++;
                if (read_unloaded_time > 0) {
                    //Stat.mctrls[cid].read_unloaded_time.Collect(read_unloaded_time);
                }
                read_unloaded_time = 0;
            }
            else {
                read_unloaded_time++;
                if (read_loaded_time > 0) {
                    //Stat.mctrls[cid].read_loaded_time.Collect(read_loaded_time);
                }
                read_loaded_time = 0;
            }

            /*** writeback mode ***/
            update_wb_mode();

            /*** clock factor ***/
            if (cycles % Config.mem.clock_factor != 0)
                return;

            /*** serve completed request ***/
            if (bus_q.Count > 0 && bus_q[0].ts <= cycles) {
                MemAddr addr = bus_q[0].addr;
                for (int p = 0; p < Config.N; p ++) meta_mctrl.interference_bit_bus[p] = false;
                foreach (BusTransaction trans in bus_q)
                {
                    if (trans.pid != bus_q[0].pid) 
                    {
                        meta_mctrl.interference_bit_bus[trans.pid] = true;
                        meta_mctrl.interference_bit_bus_proc[trans.pid] = bus_q[0].pid;
                    }
                }        
                bus_q.RemoveAt(0);
                meta_mctrl.sched.reset_rbhit_bit(addr);

                List<Req> inflight_q = inflightqs[addr.rid, addr.bid];
                Dbg.Assert(inflight_q.Count > 0);
                Dbg.Assert(addr == inflight_q[0].addr);
                Req req = inflight_q[0];
                inflight_q.RemoveAt(0);

                dequeue_req(req);
            }

            Cmd best_cmd = find_best_cmd();
            Req best_req = find_best_req();

            //nothing to issue
            if (best_cmd == null && best_req == null) {
                return;
            }

            //arbitrate between command and request
            bool is_issue_req = false;
            if (best_req != null && best_cmd == null) {
                is_issue_req = true;
            }
            else if (best_req == null && best_cmd != null) {
                is_issue_req = false;
            }
            else {
                if (best_req == __better_req(best_cmd.req, best_req))
                    is_issue_req = true;
                else
                    is_issue_req = false;
            }

            //issue command or request
            if (is_issue_req) 
            {
                issue_req(best_req);
                if (!meta_mctrl.is_omniscient) meta_mctrl.prev_req_pid = best_req.pid;
                else if (cid == 0) meta_mctrl.prev_req_pid = best_req.pid;
            }
            else 
            {
                issue_cmd(best_cmd);
                if (!meta_mctrl.is_omniscient) meta_mctrl.prev_req_pid = best_cmd.pid;
                else if (cid == 0) meta_mctrl.prev_req_pid = best_cmd.pid;
            }
        }

        public void update_wb_mode()
        {
            bool prev_wb_mode = wb_mode;
            wb_mode = mwbmode.is_wb_mode(cid);
            if (wb_mode) {
                Stat.mctrls[cid].wbmode_fraction.Collect();
            }

            if (prev_wb_mode == false && wb_mode == true) {
                //stats
                ts_start_wbmode = cycles;
                if (ts_end_wbmode != -1) {
                    Stat.mctrls[cid].wbmode_distance.Collect((int) (ts_start_wbmode - ts_end_wbmode));
                }
                

                //stats: longest write transaction
                int longest_transaction = 0;
                for (uint r = 0; r < rmax; r++) {
                    for (uint b = 0; b < bmax; b++) {
                        List<Req> q = writeqs[r, b];
                        Dictionary<ulong, int> dict = new Dictionary<ulong, int>();
                        foreach(Req req in q) {
                            if (!dict.ContainsKey(req.addr.rowid))
                                dict.Add(req.addr.rowid, 0);
                            dict[req.addr.rowid] += 1;
                        }

                        foreach (int transaction in dict.Values)
                            if (transaction > longest_transaction)
                                longest_transaction = transaction;
                    }
                }
                Stat.mctrls[cid].wbmode_longest_transaction.Collect(longest_transaction);

                //flush/drain reads
                reads_to_drain = 0;
                for (uint r = 0; r < rmax; r++) {
                    for (uint b = 0; b < bmax; b++) {
                        List<Cmd> cmdq = cmdqs[r, b];
                        if (cmdq.Count == 0)
                            continue;

                        //only column command
                        if (cmdq.Count == 1) {
                            //increment the number of reads to drain during the first part of the writeback mode
                            Dbg.Assert(cmdq[0].type == Cmd.TypeEnum.READ || cmdq[0].type == Cmd.TypeEnum.WRITE);
                            if (cmdq[0].type == Cmd.TypeEnum.READ) {
                                reads_to_drain++;
                                cmdq[0].is_drain = true;
                            }
                            continue;
                        }

                        //activate+column command
                        Dbg.Assert(cmdq.Count == 2);
                        Dbg.Assert(cmdq[0].type == Cmd.TypeEnum.ACTIVATE);
                        Dbg.Assert(cmdq[1].type == Cmd.TypeEnum.READ || cmdq[1].type == Cmd.TypeEnum.WRITE);

                        //write requests don't matter
                        if (cmdq[1].type == Cmd.TypeEnum.WRITE)
                            continue;

                        //don't flush read request
                        if (Config.mctrl.read_bypass) {
                            if (writeqs[r, b].Count == 0)
                                continue;
                        }

                        //flush read request
                        Req req = cmdq[1].req;

                        List<Req> inflightq = get_inflight_q(req);
                        Req last_req = inflightq[inflightq.Count - 1];
                        Dbg.Assert(last_req.block_addr == req.block_addr);
                        inflightq.RemoveAt(inflightq.Count - 1);

                        List<Req> q = get_q(req);
                        Dbg.Assert(q.Count < q.Capacity);
                        q.Add(req);

                        //flush read command
                        cmdq.RemoveRange(0, 2);
                    }
                }
            }
            else if (prev_wb_mode == true && wb_mode == false) {
                //stats
                ts_end_wbmode = cycles;
                Stat.mctrls[cid].wbmode_length.Collect((int)(ts_end_wbmode - ts_start_wbmode));

                
                Stat.mctrls[cid].rds_per_wb_mode.Collect(rds_per_wb_mode);
                Stat.mctrls[cid].wbs_per_wb_mode.Collect(wbs_per_wb_mode);
                rds_per_wb_mode = 0;
                wbs_per_wb_mode = 0;

                //flush/drain writes
                writes_to_drain = 0;
                foreach (List<Cmd> cmdq in cmdqs) {
                    if (cmdq.Count == 0)
                        continue;

                    //only column command
                    if (cmdq.Count == 1) {
                        //increment the number of reads to drain during the first part of the writeback mode
                        Dbg.Assert(cmdq[0].type == Cmd.TypeEnum.READ || cmdq[0].type == Cmd.TypeEnum.WRITE);
                        if (cmdq[0].type == Cmd.TypeEnum.WRITE) {
                            writes_to_drain++;
                            cmdq[0].is_drain = true;
                        }
                        continue;
                    }

                    //activate+column command
                    Dbg.Assert(cmdq.Count == 2);
                    Dbg.Assert(cmdq[0].type == Cmd.TypeEnum.ACTIVATE);
                    Dbg.Assert(cmdq[1].type == Cmd.TypeEnum.READ || cmdq[1].type == Cmd.TypeEnum.WRITE);

                    if (cmdq[1].type == Cmd.TypeEnum.READ)
                        continue;

                    //flush read request
                    Req req = cmdq[1].req;

                    List<Req> inflightq = get_inflight_q(req);
                    Req last_req = inflightq[inflightq.Count - 1];
                    Dbg.Assert(last_req.block_addr == req.block_addr);
                    inflightq.RemoveAt(inflightq.Count - 1);

                    List<Req> q = get_q(req);
                    Dbg.Assert(q.Count < q.Capacity);
                    q.Add(req);

                    //flush read command
                    cmdq.RemoveRange(0, 2);
                }
            }
        }

        public Req find_best_req()
        {
            Req best_req = null;
            for (int r = 0; r < rmax; r++) {
                for (int b = 0; b < bmax; b++) {
                    Req req = null;

                    //find best request from a bank
                    req = __find_best_req(r, b);

                    //no request
                    if (req == null)
                        continue;

                    //update best request
                    if (best_req == null) {
                        best_req = req;
                        continue;
                    }

                    //arbitrate between requests from different banks
                    best_req = __better_req(best_req, req);
                }
            }
            return best_req;
        }

        private Req __find_best_req(int r, int b)
        {
            //no need to search for request, already outstanding commands
            if (cmdqs[r, b].Count > 0)
                return null;

            /*** find best request ***/
            List<Req> rq = readqs[r, b];
            List<Req> wq = writeqs[r, b];

            if (rq.Count == 0 && wq.Count == 0)
                return null;

            Req best_req = null;
            Cmd cmd = null;

            //find best writeback request
            if (wb_mode) {
                best_req = meta_mctrl.find_best_wb_req(wq);
                if (best_req != null) {
                    //check if best writeback request is schedulable
                    cmd = decode_req(best_req)[0];

                    if (!can_schedule_cmd(cmd))
                        return null;

                    return best_req;
                }

                //writeq is empty: should we let reads bypass?
                if (!Config.mctrl.read_bypass)
                    return null;
            }

            //find best read request
            best_req = meta_mctrl.find_best_rd_req(rq);

            /*** row-hit bypass ***/
            if (Config.mctrl.row_hit_bypass) {
                Req hit_req = rh_finder.find_best_req(rq);
                if (!meta_mctrl.is_row_hit(best_req) && hit_req != null) {
                    Bank bank = chan.ranks[r].banks[b];
                    Dbg.Assert(bank.ts_act != -1);

                    long ts_pre = bank.ts_act + timing.tRAS;
                    long speculative_ts_pre = cycles + timing.tRTP;
                    if (speculative_ts_pre <= ts_pre) {
                        best_req = hit_req;
                    }
                }
            }

            if (best_req == null)
                return null;

            //check if best request is schedulable
            cmd = decode_req(best_req)[0];
            if (!can_schedule_cmd(cmd))
                return null;

            return best_req;
        }

        private Req __better_req(Req req1, Req req2)
        {
            bool is_wr1 = req1.type == ReqType.WR;
            bool is_wr2 = req2.type == ReqType.WR;

            if (is_wr1 && is_wr2) {
                return meta_mctrl.better_wb_req(req1, req2);
            }

            if (is_wr1 ^ is_wr2) {
                if (is_wr1) return req1;
                else return req2;
            }

            //two reads
            return meta_mctrl.better_req(req1, req2);
        }

        public Cmd find_best_cmd()
        {
            Cmd best_cmd = null;
            for (int r = 0; r < rmax; r++) {
                for (int b = 0; b < bmax; b++) {
                    if (cmdqs[r, b].Count == 0)
                        continue;

                    Cmd cmd = cmdqs[r, b][0];

                    //check if best command is schedulable
                    if (!can_schedule_cmd(cmd))
                        continue;

                    //update best command for this bank
                    if (best_cmd == null) {
                        best_cmd = cmd;
                        continue;
                    }

                    //arbitrate between commands from different banks
                    Req best_req = __better_req(best_cmd.req, cmd.req);
                    if (best_req == cmd.req) best_cmd = cmd;
                }
            }
            return best_cmd;
        }

        private List<Cmd> decode_req(Req req)
        {
            MemAddr addr = req.addr;
            List<Cmd> cmd_q = cmdqs[addr.rid, addr.bid];
            int pid = req.pid;
            Bank b = chan.ranks[addr.rid].banks[addr.bid];

            List<Cmd> decode_cmd_q = new List<Cmd>(CMDQ_MAX);
            if (b.curr_rowid == -1) {
                //row-closed
                req.row_hit = false;
                decode_cmd_q.Add(new Cmd(Cmd.TypeEnum.ACTIVATE, addr, pid, req, cmd_q));
            }
            else if (b.curr_rowid != (long)addr.rowid) {
                //row-conflict
                req.row_hit = false;
                decode_cmd_q.Add(new Cmd(Cmd.TypeEnum.PRECHARGE, addr, pid, req, cmd_q));
                decode_cmd_q.Add(new Cmd(Cmd.TypeEnum.ACTIVATE, addr, pid, req, cmd_q));
            }

            Cmd.TypeEnum RW = (req.type == ReqType.WR ? Cmd.TypeEnum.WRITE : Cmd.TypeEnum.READ);
            decode_cmd_q.Add(new Cmd(RW, addr, pid, req, cmd_q));

            return decode_cmd_q;
        }

        public void __dequeue_req(Req req)
        {
            req.ts_departure = cycles;
            Dbg.Assert(req.ts_departure - req.ts_arrival > 0);

            //sched
            meta_mctrl.dequeue_req(req);

            //load stat management
            if (req.type == ReqType.RD) {
                rload--;
                rload_per_proc[req.pid]--;
                rload_per_procrankbank[req.pid, req.addr.rid, req.addr.bid]--;
                Dbg.Assert(rload >= 0);
                Dbg.Assert(rload_per_proc[req.pid] >= 0);
                Dbg.Assert(rload_per_procrankbank[req.pid, req.addr.rid, req.addr.bid] >= 0);
            }
            else {
                wload--;
                wload_per_proc[req.pid]--;
                wload_per_procrankbank[req.pid, req.addr.rid, req.addr.bid]--;
                Dbg.Assert(wload >= 0);
                Dbg.Assert(wload_per_proc[req.pid] >= 0);
                Dbg.Assert(wload_per_procrankbank[req.pid, req.addr.rid, req.addr.bid] >= 0);
            }

            meta_mctrl.sched.service_counter(req);

            //dequeue proper
            if (req.type == ReqType.RD) {
                //traverse crossbar
                Sim.xbar.enqueue(req);
            }
            else {
                bool removeok = mctrl_writeq.Remove(req);
                Dbg.Assert(removeok);
                req.latency = (int)(req.ts_departure - req.ts_arrival);

                Callback cb = req.callback;
                cb(req);
            }
        }

        public void dequeue_req(Req req)
        {
            __dequeue_req(req);
        }

        public void enqueue_req(Req req)
        {
            //check if writeback hit
            List<Req> q = get_q(req);
            MemAddr addr = req.addr;
            if (req.type == ReqType.RD) {
                List<Req> wq = writeqs[addr.rid, addr.bid];

                int idx = wq.FindIndex(delegate(Req w) { return w.block_addr == req.block_addr; });
                if (idx != -1) {
                    //writeback hit
                    Sim.xbar.enqueue(req);
                    Stat.procs[req.pid].wb_hit.Collect();
                    return;
                }
            }

            //writeback dumpster
            if (req.type == ReqType.WR && Config.mctrl.wb_dump) {
                req.addr.rowid = 0;
            }

            //enqueue proper
            Dbg.Assert(q.Count < q.Capacity);
            __enqueue_req(req, q);
        }

        public void __enqueue_req(Req req, List<Req> q)
        {

            //timestamp
            req.ts_arrival = cycles;

            //add to queue
            q.Add(req);
            if (req.type == ReqType.WR) {
                Dbg.Assert(mctrl_writeq.Count < mctrl_writeq.Capacity);
                mctrl_writeq.Add(req);
            }

            //sched
            meta_mctrl.enqueue_req(req);    //does nothing for now

            //stats
            if (req.type == ReqType.RD) {
                rload++;
                rload_per_proc[req.pid]++;
                rload_per_procrankbank[req.pid, req.addr.rid, req.addr.bid]++;
            }
            else {
                wload++;
                wload_per_proc[req.pid]++;
                wload_per_procrankbank[req.pid, req.addr.rid, req.addr.bid]++;
            }
        }

        public bool can_schedule_cmd(Cmd cmd)
        {
            //drain reads during the first part of the writeback mode
            if (wb_mode && reads_to_drain > 0) {
                if (cmd.type == Cmd.TypeEnum.WRITE)
                    return false;
            }

            //drain writes right after the writeback mode
            if (!wb_mode && writes_to_drain > 0) {
                if (cmd.type == Cmd.TypeEnum.READ)
                    return false;
            }

            //DRAM timing
            MemAddr addr = cmd.addr;
            switch (cmd.type) {
                case Cmd.TypeEnum.ACTIVATE:
                    return can_activate(addr);
                case Cmd.TypeEnum.PRECHARGE:
                    return can_precharge(addr);
                case Cmd.TypeEnum.READ:
                    return can_read(addr);
                case Cmd.TypeEnum.WRITE:
                    return can_write(addr);
            }
            //should never get here
            throw new System.Exception("DRAM: Invalid Cmd.");
        }

        private void issue_req(Req req)
        {
            //remove request from waiting queue
            List<Req> q = get_q(req);
            Dbg.Assert(q.Contains(req));
            q.Remove(req);

            req.queueing_latency = (int) (cycles - req.ts_arrival);  
            total_queueing_latency[req.pid] += (ulong)req.queueing_latency;
            if (Sim.highest_rank_proc == req.pid) Sim.procs[req.pid].queueing_latency += (ulong)req.queueing_latency;
            Stat.mctrls[cid].queueing_latency_per_proc[req.pid].Collect(req.queueing_latency);

            //add to inflight queue
            MemAddr addr = req.addr;
            List<Req> inflight_q = inflightqs[addr.rid, addr.bid];
            Dbg.Assert(inflight_q.Count < inflight_q.Capacity);
            inflight_q.Add(req);

            //add to command queue
            List<Cmd> cmd_q = cmdqs[addr.rid, addr.bid];
            Dbg.Assert(cmd_q.Count == 0);
            List<Cmd> new_cmd_q = decode_req(req);
            Dbg.Assert(new_cmd_q.Count > 0);
            cmd_q.AddRange(new_cmd_q);
            Cmd cmd = cmd_q[0];


            //meta_mctrl
            meta_mctrl.issue_req(req);

            req.ts_issue = cycles;

            //stats
            BankStat bstat = Stat.banks[addr.cid, addr.rid, addr.bid];
            bstat.access.Collect();
            if (cmd.type == Cmd.TypeEnum.PRECHARGE || cmd.type == Cmd.TypeEnum.ACTIVATE) {
                //bank stat
                bstat.row_miss.Collect();
                bstat.row_miss_perproc[req.pid].Collect();

                //proc stat
                if (cmd.req.type == ReqType.RD) {
                    Stat.procs[req.pid].row_hit_rate_read.Collect(0);
                    Stat.procs[req.pid].row_miss_read.Collect();
                }
                else {
                    Stat.procs[req.pid].row_hit_rate_write.Collect(0);
                    Stat.procs[req.pid].row_miss_write.Collect();
                }
            }
            else {
                //bank stat
                bstat.row_hit.Collect();
                bstat.row_hit_perproc[req.pid].Collect();

                //proc stat
                if (cmd.req.type == ReqType.RD) {
                    Stat.procs[req.pid].row_hit_rate_read.Collect(1);
                    Stat.procs[req.pid].row_hit_read.Collect();
                }
                else {
                    Stat.procs[req.pid].row_hit_rate_write.Collect(1);
                    Stat.procs[req.pid].row_hit_write.Collect();
                }
            }

            //issue command
            issue_cmd(cmd);
        }

        private void issue_cmd(Cmd cmd)
        {
            MemAddr addr = cmd.addr;

            List<Cmd> cmd_q = cmdqs[addr.rid, addr.bid];
            Dbg.Assert(cmd == cmd_q[0]);
            cmd_q.RemoveAt(0);
            BankStat bank_stat = Stat.banks[addr.cid, addr.rid, addr.bid];
            BusStat bus_stat = Stat.busses[addr.cid];

            //writeback mode stats
            if (wb_mode) {
                if (cmd.type == Cmd.TypeEnum.READ)
                    rds_per_wb_mode++;
                else if (cmd.type == Cmd.TypeEnum.WRITE)
                    wbs_per_wb_mode++;
            }

            //string dbg;
            switch (cmd.type) {
                case Cmd.TypeEnum.ACTIVATE:
                    activate(addr);
                    /*dbg = String.Format("@{0,6} DRAM ACTI: Channel {1}, Rank {2}, Bank {3}, Row {4}, Col {5}",
                        cycles, cid, addr.rid, addr.bid, addr.rowid, addr.colid);*/
                    //stats                    
                    bank_stat.cmd_activate.Collect();
                    bank_stat.utilization.Collect(timing.tRCD);

                    //shadow row-buffer id
                    meta_mctrl.sched.count_queueing(cmd, pid_rowid_per_procrankbank[addr.rid, addr.bid]);
                    shadow_rowid_per_procrankbank[cmd.pid, addr.rid, addr.bid] = addr.rowid;
                    rowid_per_procrankbank[addr.rid, addr.bid] = addr.rowid;
                    pid_rowid_per_procrankbank[addr.rid, addr.bid] = cmd.req.pid;
                    break;
                case Cmd.TypeEnum.PRECHARGE:
                    precharge(addr);
                    /*dbg = String.Format("@{0,6} DRAM PREC: Channel {1}, Rank {2}, Bank {3}, Row {4}, Col {5}",
                        cycles, cid, addr.rid, addr.bid, addr.rowid, addr.colid);*/
                    //stats
                    bank_stat.cmd_precharge.Collect();
                    bank_stat.utilization.Collect(timing.tRP);
                    break;
                case Cmd.TypeEnum.READ:
                    read(addr, cmd.req.pid);
                    /*dbg = String.Format("@{0,6} DRAM READ: Channel {1}, Rank {2}, Bank {3}, Row {4}, Col {5}",
                        cycles, cid, addr.rid, addr.bid, addr.rowid, addr.colid);*/

                    //writeback mode
                    if (wb_mode && cmd.is_drain) {
                        Dbg.Assert(reads_to_drain > 0);
                        reads_to_drain--;
                    }

                    //stats
                    bank_stat.cmd_read.Collect();
                    bank_stat.utilization.Collect(timing.tCL);
                    bus_stat.access.Collect();
                    bus_stat.utilization.Collect(timing.tBL);
//                    Console.Write("HERE\n");
                    meta_mctrl.sched.bus_interference_count(cmd);
                    break;
                case Cmd.TypeEnum.WRITE:
                    write(addr, cmd.req.pid);

                    //writeback mode
                    if (!wb_mode && cmd.is_drain) {
                        Dbg.Assert(writes_to_drain > 0);
                        writes_to_drain--;
                    }
                    else {
                        mwbmode.issued_write_cmd(cmd);
                    }

                    //stats
                    bank_stat.cmd_write.Collect();
                    bank_stat.utilization.Collect(timing.tCL);
                    bus_stat.access.Collect();
                    bus_stat.utilization.Collect(timing.tBL);
                    break;

                default:
                    //should never get here
                    throw new System.Exception("DRAM: Invalid Cmd.");
            }
            //Debug.WriteLine(dbg);
        }


        public bool is_q_full(int pid, ReqType rw, uint rid, uint bid)
        {
            /* read queue */
            if (rw == ReqType.RD) {
                List<Req> q = readqs[rid, bid];
                return q.Count == q.Capacity;
            }

            /* write queue */
            if (mctrl_writeq.Count == mctrl_writeq.Capacity)
                return true;

            //writeback throttle
            bool is_throttle = wbthrottle.is_throttle(pid);
            return is_throttle;
        }

        public List<Req> get_q(Req req)
        {
            List<Req>[,] rw_qs = (req.type == ReqType.RD ? readqs : writeqs);
            List<Req> q = rw_qs[req.addr.rid, req.addr.bid];
            return q;
        }

        public List<Req> get_inflight_q(Req req)
        {
            List<Req> q = inflightqs[req.addr.rid, req.addr.bid];
            return q;
        }

        public void freeze_stat(int pid)
        {
            Stat.mctrls[cid].Finish(Sim.cycles, pid);
        }

        private void activate(MemAddr addr)
        {
            chan.activate(addr.rid, addr.bid, addr.rowid);
        }
        private void precharge(MemAddr addr)
        {
            chan.precharge(addr.rid, addr.bid);
        }
        private void read(MemAddr addr, int pid)
        {
            chan.read(addr.rid, addr.bid);
            Dbg.Assert(bus_q.Count < bus_q.Capacity);
            BusTransaction trans = new BusTransaction(addr, cycles + (timing.tCL + timing.tBL), pid);
            //check for bus conflict
            if (bus_q.Count > 0) {
                BusTransaction last_trans = bus_q[bus_q.Count - 1];
                Dbg.Assert(trans.ts - last_trans.ts >= timing.tBL);
            }
            bus_q.Add(trans);
        }
        private void write(MemAddr addr, int pid)
        {
            chan.write(addr.rid, addr.bid);
            Dbg.Assert(bus_q.Count < bus_q.Capacity);
            BusTransaction trans = new BusTransaction(addr, cycles + (timing.tCWL + timing.tBL), pid);
            //check for bus conflict
            if (bus_q.Count > 0) {
                BusTransaction last_trans = bus_q[bus_q.Count - 1];
                Dbg.Assert(trans.ts - last_trans.ts >= timing.tBL);
            }
            bus_q.Add(trans);
        }
        private bool can_activate(MemAddr addr)
        {
            return chan.can_activate(addr.rid, addr.bid);
        }
        private bool can_precharge(MemAddr addr)
        {
            return chan.can_precharge(addr.rid, addr.bid);
        }
        private bool can_read(MemAddr addr)
        {
            return chan.can_read(addr.rid, addr.bid);
        }
        private bool can_write(MemAddr addr)
        {
            return chan.can_write(addr.rid, addr.bid);
        }

        public void reset()
        {
            chan.reset();
        }
    }
}
