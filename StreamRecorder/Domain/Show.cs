﻿using System;

namespace StreamRecorder.Domain
{
    public class Show
    {
        public string Title { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string FileName { get; set; }
    }
}