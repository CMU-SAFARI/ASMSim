using System;
using System.Collections.Generic;
using System.Text;

using System.IO;

namespace MemMap
{
    public enum WbPolicy
    {
        Squeeze,
        Drain,
        MarkedDrain,
    }

    public class MemCtrlConfig : ConfigGroup
    {
        //network latency
        public uint xbar_latency = 16;

        //queue size
        public int readq_max_per_bank = 256;
        public int writeq_max_per_bank = 4;

        //read row-hit bypass
        public bool row_hit_bypass = false;

        //wbmode
        public uint wb_window = 1000;
        public uint min_wb_window = 100;
        public uint max_wb_window = 1000;
        public double low_rmpki_threshold;
        public string wbmode_algo = "DecoupledWBFullDrain";
        public Type typeof_wbmode_algo;
        public uint serve_max = 16;

        public bool read_bypass = false;

        //log-structured writeback
        public bool wb_dump = false;

        //writeback buffer
        //public bool partition_wb_buffer = false;

        /* Auxiliary */
        //page-mapping
        public bool page_randomize = true;
        public bool page_sequence = false;

        //migration
        public bool bank_divert = false;
        public bool sequential_search = false;

        //migration
        public bool temporal_correlate = false;

        //migration parameters
        public bool migration_overhead = false;
        public uint migration_threshold = 8;

        //migration
        public bool threshold_migrate = false;

        //migration
        public bool sample_migrate = false;

        //migration
        public bool power2_nursery = false;
        public uint power2_access_threshold = 8;
        public long power2_cycle_threshold = 10000;

        protected override bool set_special_param(string param, string val)
        {
            return false;
        }

        public override void finalize()
        {
            //wbmode algo
            string type_name = typeof(Sim).Namespace + "." + Config.mctrl.wbmode_algo;
            try {
                typeof_wbmode_algo = Type.GetType(type_name);
            }
            catch {
                throw new Exception(String.Format("WBMode not found {0}", Config.mctrl.wbmode_algo));
            }
        }
    }
}