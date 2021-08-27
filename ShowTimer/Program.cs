using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ShowTimer
{
    internal class Program
    {
        private static System.Timers.Timer timer1;
        private static Show currentShow;
        private static List<Show> _shows;
        private static bool idle = false;

        private static ManualResetEvent _quitEvent = new ManualResetEvent(false);

        private static void Main(string[] args)
        {
            Console.CancelKeyPress += (sender, eArgs) =>
            {
                _quitEvent.Set();
                eArgs.Cancel = true;
            };

            _shows = GetShows();

            timer1 = new System.Timers.Timer();
            timer1.Interval = 5000;
            timer1.Elapsed += Timer1_Elapsed;
            timer1.Start();

            currentShow = GetCurrentShow();
            if (currentShow != null)
                Console.WriteLine($"Current show is {currentShow.Title}");

            _quitEvent.WaitOne();
        }

        private static void Timer1_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (currentShow != null)
            {
                if (!TimeBetween(DateTime.Now, currentShow.StartTime, currentShow.EndTime))
                {
                    currentShow = GetCurrentShow();
                    if (currentShow != null)
                        Console.WriteLine($"Change Show: Starting {currentShow.Title}");
                }
            }
            else
            {
                if (!idle)
                {
                    Console.WriteLine($"Idle.  Next show starts at {_shows.OrderBy(x => x.StartTime).FirstOrDefault().StartTime.ToString("hh\\:mm")}");
                    idle = true;
                }
            }
        }

        private static List<Show> GetShows()
        {
            var shows = new List<Show>();
            shows.Add(new Show() { Title = "The Musers", StartTime = new TimeSpan(5, 30, 0), EndTime = new TimeSpan(10, 00, 0) });
            shows.Add(new Show() { Title = "Norm and D", StartTime = new TimeSpan(10, 00, 0), EndTime = new TimeSpan(12, 0, 0) });
            shows.Add(new Show() { Title = "The Hang Zone", StartTime = new TimeSpan(12, 0, 0), EndTime = new TimeSpan(15, 0, 0) });
            shows.Add(new Show() { Title = "The Hardline", StartTime = new TimeSpan(15, 0, 0), EndTime = new TimeSpan(15, 42, 0) });
            return shows;
        }

        private static Show GetCurrentShow()
        {
            return _shows.Where(x => TimeBetween(DateTime.Now, x.StartTime, x.EndTime)).FirstOrDefault();
        }

        private static bool TimeBetween(DateTime datetime, TimeSpan start, TimeSpan end)
        {
            TimeSpan now = datetime.TimeOfDay;
            if (start < end)
                return start <= now && now <= end;
            return !(end < now && now < start);
        }
    }

    public class Show
    {
        public string Title { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
    }
}