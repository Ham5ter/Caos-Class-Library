﻿using System;
using System.Text;
using System.IO.MemoryMappedFiles;
using System.Threading;

namespace CAOS
{
    public static class CaosInjector
    {
        static public Mutex Mutex;
        static public MemoryMappedFile MemFile;
        static public MemoryMappedViewAccessor MemViewAccessor;
        static public EventWaitHandle ResultEventHandle;
        static public EventWaitHandle RequestRventHandle;
        static private string GameName = null;

        public static void SetGame(string gameName)
        {
            GameName = gameName;
            InitInjector();
            CloseInjector();
        }

        private static void InitInjector()
        {
            try
            {
                Mutex = Mutex.OpenExisting(GameName + "_mutex");
                MemFile = MemoryMappedFile.OpenExisting(GameName + "_mem");
                MemViewAccessor = MemFile.CreateViewAccessor();
                ResultEventHandle = EventWaitHandle.OpenExisting(GameName + "_result");
                RequestRventHandle = EventWaitHandle.OpenExisting(GameName + "_request");
            }
            catch (Exception)
            {
            }
        }

        private static void CloseInjector()
        {
            RequestRventHandle.Close();
            ResultEventHandle.Close();
            MemViewAccessor.Dispose();
            MemFile.Dispose();
            Mutex.Close();
        }

        public static CaosResult AddScriptToScriptorium(int Familiy, int Genus, int Species, int Event, string Script)
        {
            return ExecuteCaosGetResult(Script, "scrp " + Familiy + " " + Genus + " " + Species + " " + Event);
        }

        public static CaosResult ExecuteCaosGetResult(string CaosAsString, string Action = "execute")
        {
            InitInjector();
            byte[] CaosBytes = Encoding.UTF8.GetBytes(Action + "\n" + CaosAsString + "\n");
            int BufferPosition = 24;
            Mutex.WaitOne(1000);
            foreach (byte Byte in CaosBytes)
            {
                MemViewAccessor.Write(BufferPosition, Byte);
                BufferPosition++;
            }
            RequestRventHandle.Set();
            ResultEventHandle.WaitOne(5000);
            int ResultSize = MemViewAccessor.ReadInt16(12);
            byte[] ResultBytes = new byte[ResultSize];
            int ResultCode = Convert.ToInt16(MemViewAccessor.ReadByte(8));
            int ProcessID = Convert.ToInt16(MemViewAccessor.ReadByte(4));
            for (int i = 0; i < ResultSize; i++)
            {
                ResultBytes[i] = MemViewAccessor.ReadByte(24 + i);
            }
            for (int i = 0; i < CaosBytes.Length; i++)
            {
                MemViewAccessor.Write(24 + i, (byte)0);
            }
            for (int i = 0; i < ResultSize; i++)
            {
                MemViewAccessor.Write(24 + i, (byte)0);
            }
            Mutex.ReleaseMutex();
            CloseInjector();
            Thread.Sleep(50);
            return new CaosResult(ResultCode, Encoding.UTF8.GetString(ResultBytes), ProcessID);
        }

        public static int ProcessID()
        {
            Mutex.WaitOne();
            int ProcessID = MemViewAccessor.ReadInt16(4);
            Mutex.ReleaseMutex();
            return ProcessID;
        }
    }
    public class CaosResult
    {
        public int ResultCode { get; private set; }
        public bool Succeded { get; private set; }
        public int ProcessId { get; private set; }
        public string Content { get; private set; }

        public CaosResult(int resultCode, string content, int processID)
        {
            this.ResultCode = resultCode;
            this.Succeded = (resultCode == 0);
            this.Content = content;
            this.ProcessId = processID;
        }
    }
}
