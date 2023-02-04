﻿using System.Collections.Concurrent;

namespace USPSAddressValidator.Models
{

    public class Logger
    {
        public ConcurrentBag<LogItem> Items { get; set; } = new ConcurrentBag<LogItem>();

        public Logger() { }

        public bool Message(string message, int id)
        {
            Items.Add(new LogItem(message, id, LogItemType.Message));
            return true;
        }

        public bool Success(string message, int id)
        {
            Items.Add(new LogItem(message, id, LogItemType.Success));
            return true;
        }

        public bool Error(string message, int id)
        {
            Items.Add(new LogItem(message, id, LogItemType.Error));
            return true;
        }

        public void Clear()
        {
            Items.Clear();
        }

    }

    public enum LogItemType
    {
        Message = 0,
        Success = 1,
        Error = 2
    }

    public class LogItem
    {
        public string Message { get; set; }
        public int ID { get; set; } 
        public LogItemType Type { get; set; }
        public LogItem(string message, int id, LogItemType type = LogItemType.Message)
        {
            Message = message;
            ID = id;
         
            Type = type;
        }
    }
}
