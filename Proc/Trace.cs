using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Globalization;


using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Zip;

namespace MemMap
{
    public class Trace
    {
        public int pid;
        public bool finished;

        //trace file
        public string trace_fname;   //name of trace file
        public int line_num;

        //reader
        Stream gzip_reader;
        BinaryReader binary_reader;         //gzip trace file reader

        //size of temporary buffer
        public const int BUF_MAX = 1000;    //buffer length for reading from trace files

        public Trace(int pid, string trace_fname)
        {
            this.pid = pid;

      
            foreach (string dir in Config.TraceDirs.Split(',', ' ')) {
                Console.Write(" Dir " + dir + " File name " + trace_fname + "\n");
                if (File.Exists(dir + "/" + trace_fname)) {
                    trace_fname = dir + "/" + trace_fname;
                }
            }

            //trace file
            Dbg.Assert(File.Exists(trace_fname));
            this.trace_fname = trace_fname;

            //gzip_reader
            gzip_reader = new GZipInputStream(File.OpenRead(trace_fname));
            binary_reader = new BinaryReader (gzip_reader);

        }
        
        private string read_gzip_trace()
        {


            
            byte[] single_buf = new byte[1];

            bool copied = StreamUtils.Copy(gzip_reader, null, single_buf);
            if (!copied) {
                return null;
            }

            byte[] buf = new byte[BUF_MAX];
            int n = 0; 
            while (single_buf[0] != (byte)'\n') {
                Console.Write(single_buf[0] + "\n");
                buf[n++] = single_buf[0];
                copied = StreamUtils.Copy(gzip_reader, null, single_buf);
            }
            if (Encoding.ASCII.GetString(buf, 0, n) == "#eof") return null;
            return Encoding.ASCII.GetString(buf, 0, n);
        }

        public void get_req(ref int cpu_inst_cnt, out Req rd_req, out Req wb_req)
        {

            try
            {
                ulong rd_addr = binary_reader.ReadUInt64();
                cpu_inst_cnt = binary_reader.ReadInt32();


                ReqType req_type, proc_req_type;
                req_type = ReqType.RD;
                if (rd_addr >> 63 != 1) proc_req_type = ReqType.RD;
                else proc_req_type = ReqType.WR;
            
                rd_addr = rd_addr | (((ulong)pid) << 56);


                rd_req = RequestPool.depool();
                rd_req.set(pid, req_type, proc_req_type, rd_addr);
                wb_req = null;
               
            } 
            catch (EndOfStreamException)
            {
                gzip_reader = new GZipInputStream(File.OpenRead(trace_fname));
                binary_reader = new BinaryReader (gzip_reader);
                ulong rd_addr = binary_reader.ReadUInt64();
                cpu_inst_cnt = binary_reader.ReadInt32();


                ReqType req_type, proc_req_type;
                req_type = ReqType.RD;
                if (rd_addr >> 63 != 1) proc_req_type = ReqType.RD;
                else proc_req_type = ReqType.WR;
            
                rd_addr = rd_addr | (((ulong)pid) << 56);


                rd_req = RequestPool.depool();
                rd_req.set(pid, req_type, proc_req_type, rd_addr);
                wb_req = null;
            }
            
        }


        public void get_req(ref int cpu_inst_cnt, out Req rd_req, out Req wb_req, ref ulong pc)
        {

            try
            {
                ulong rd_addr = binary_reader.ReadUInt64();
                cpu_inst_cnt = binary_reader.ReadInt32();
                pc = binary_reader.ReadUInt64();

                ReqType req_type, proc_req_type;
                req_type = ReqType.RD;
                if (rd_addr >> 63 != 1) proc_req_type = ReqType.RD;
                else proc_req_type = ReqType.WR;
            
                rd_addr = rd_addr | (((ulong)pid) << 56);


                rd_req = RequestPool.depool();
                rd_req.set(pid, req_type, proc_req_type, rd_addr);
                wb_req = null;
               
            } 
            catch (EndOfStreamException)
            {
                gzip_reader = new GZipInputStream(File.OpenRead(trace_fname));
                binary_reader = new BinaryReader (gzip_reader);
                ulong rd_addr = binary_reader.ReadUInt64();
                cpu_inst_cnt = binary_reader.ReadInt32();
                pc = binary_reader.ReadUInt64();

                ReqType req_type, proc_req_type;
                req_type = ReqType.RD;
                if (rd_addr >> 63 != 1) proc_req_type = ReqType.RD;
                else proc_req_type = ReqType.WR;
            
                rd_addr = rd_addr | (((ulong)pid) << 56);


                rd_req = RequestPool.depool();
                rd_req.set(pid, req_type, proc_req_type, rd_addr);
                wb_req = null;
            }
            
        }

        public string read_trace()
        {
            line_num++;
            string line = read_gzip_trace();
            if(line != null){
                Console.Write (line);
                return line;
            }

            //reached EOF; reopen trace file
            finished = true;
            gzip_reader.Close();
            gzip_reader = new GZipInputStream(File.OpenRead(trace_fname));

            line_num = 0;
            line = read_trace();
            Dbg.Assert(line != null);
            return line;
        }
    }
}
