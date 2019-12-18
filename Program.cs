using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace gvaduha.proto.EventNotification
{
    class Event : ICloneable
    {
        public long Id;
        public string LinkedTo;
        public bool completed;

        public Event() {}
        public Event(Event rhs)
        {
            Id = rhs.Id;
            LinkedTo = rhs.LinkedTo;
            completed = rhs.completed;
        }

        public object Clone() => new Event(this);

        public override int GetHashCode() => Id.GetHashCode();
        public override bool Equals(object obj) => obj is Event && Equals((Event)obj);
        public bool Equals(Event evt) => evt.Id == Id;
    }

    class Alarm : ICloneable
    {
        public enum AlarmState
        {
            AlarmUnsent,
            AlarmSent,
            FinishUnsent,
            Finished // for backpropagation merge
        }

        public AlarmState State { get; private set; }
        public string Key { get; private set; }
        private readonly HashSet<Event>  _events;

        public Alarm(Alarm rhs)
        {
            State = rhs.State;
            Key = rhs.Key;
            _events = rhs._events.Select(x=>(Event)x.Clone()).ToHashSet();
        }

        public Alarm(Event evt)
        {
            State = AlarmState.AlarmUnsent;
            Key = evt.LinkedTo;
            _events = new HashSet<Event> {evt};
        }

        public object Clone() => new Alarm(this);

        public void FinilizeAlarm() => State = AlarmState.FinishUnsent;
            
        public bool AddEvent(Event evt) => _events.Add(evt);
        public bool RemoveEvent(Event evt) => _events.Remove(evt);
        public int EventCount => _events.Count;
    }

    class AlarmCache
    {
        private readonly Dictionary<string, Alarm> _alarms = new Dictionary<string, Alarm>();

        public AlarmCache(IEnumerable<Alarm> alarms)
        {
            _alarms = alarms.ToDictionary(x => x.Key);
            //RefreshAlarmStates(); // consider uncomment for "dirty" data
        }

        public bool TryGetValue(string key, out Alarm value) => _alarms.TryGetValue(key,out value);
        public void Add(string key, Alarm value) => _alarms.Add(key, value);

        private void RefreshAlarmStates()
        {
            _alarms.Values.Where(x => x.EventCount == 0).ToList()
                .ForEach(x=>x.FinilizeAlarm());
        }

        /// <summary>
        /// Returns deep copied list of Alarms that are not sent to subscriber
        /// </summary>
        /// <returns>List of alarms in *Unsent state</returns>
        public IReadOnlyCollection<Alarm> GetUnnotifiedAlarms()
        {
            RefreshAlarmStates();
            return _alarms.Values.Where(
                x=> new List<Alarm.AlarmState>
                        {Alarm.AlarmState.AlarmUnsent, Alarm.AlarmState.FinishUnsent}
                        .Contains(x.State))
            .Select(x => (Alarm)x.Clone())
            .ToList();
        }
    }

    class SimpleEventProcessStrategy
    {
        public void TossEvent(Event evt, AlarmCache alarms)
        {
            var potKey = evt.LinkedTo;
            (bool completed, bool alarmExist) @case = (evt.completed, alarms.TryGetValue(potKey, out var alarm));

            switch(@case)
            {
                case var x when x.completed && x.alarmExist:
                    alarm.RemoveEvent(evt);
                    break;
                case var x when x.completed && !x.alarmExist:
                    Debug.Assert(false, $"Completed EmissionEvent that has no alarm event {evt}");
                    break;
                case var x when !x.completed && x.alarmExist:
                    alarm.AddEvent(evt);
                    break;
                case var x when !x.completed && !x.alarmExist:
                    alarms.Add(potKey, new Alarm(evt));
                    break;
            }
        }

        public void ProcessEventBatch(IEnumerable<Event> events, AlarmCache alarms)
        {
            events.ToList().ForEach(x => TossEvent(x, alarms));
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var x = new SimpleEventProcessStrategy();

            var e01 = new Event {Id=1, LinkedTo = "1:1", completed = false};
            var e02 = new Event {Id=2, LinkedTo = "1:1", completed = false};
            var e03 = new Event {Id=1, LinkedTo = "1:1", completed = false};

            var e05 = new Event {Id=2, LinkedTo = "1:1", completed = true};
            var e06 = new Event {Id=1, LinkedTo = "1:1", completed = true};
            var e04 = new Event {Id=3, LinkedTo = "1:1", completed = false};

            var e07 = new Event {Id=5, LinkedTo = "1:2", completed = false};
            var e08 = new Event {Id=3, LinkedTo = "1:1", completed = true};
            var e09 = new Event {Id=9, LinkedTo = "1:2", completed = false};

            var l1 = new List<Event> {e01,e02,e03};
            var l2 = new List<Event> {e04,e05,e06 };
            var l3 = new List<Event> {e07,e08,e09};

            //es.ForEach(i=>x.PotNotification(i));

            var cache = new AlarmCache(new List<Alarm>());
            x.ProcessEventBatch(l1, cache);
            x.ProcessEventBatch(l2, cache);
            x.ProcessEventBatch(l3, cache);

            Console.WriteLine("Finish");
        }
    }
}
