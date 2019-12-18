using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")] 

namespace gvaduha.proto.EventNotification
{
    internal class Event : ICloneable, IEquatable<Event>
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
        //public override bool Equals(object obj) => obj is Event && Equals((Event)obj);
        public bool Equals(Event other) => other?.Id == Id;
        //{
        //    if (other == null) return false;
        //    if (ReferenceEquals(other,this)) return true;
        //    return other.Id == Id;
        //}
    }

    internal class Alarm : ICloneable, IEquatable<Alarm>
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

        public bool Equals(Alarm other)
        {
            if (other == null) return false;
            if (ReferenceEquals(other, this)) return true;
            return State == other.State
                && Key == other.Key
                && _events.SetEquals(other._events);
        }

        public int EventCount => _events.Count;
    }

    internal interface IAlarmCache
    {
        bool TryGetValue(string key, out Alarm value);
        void Add(string key, Alarm value);
        IReadOnlyCollection<Alarm> GetUnnotifiedAlarms();
    }

    internal class AlarmCache : IAlarmCache
    {
        private readonly Dictionary<string, Alarm> _alarms = new Dictionary<string, Alarm>();

        public AlarmCache(IEnumerable<Alarm> alarms)
        {
            _alarms = alarms.ToDictionary(x => x.Key);
            //RefreshAlarmStates(); // consider uncomment for "dirty" data
        }

        public virtual bool TryGetValue(string key, out Alarm value) => _alarms.TryGetValue(key,out value);
        public virtual void Add(string key, Alarm value) => _alarms.Add(key, value);

        private void RefreshAlarmStates()
        {
            _alarms.Values.Where(x => x.EventCount == 0).ToList()
                .ForEach(x=>x.FinilizeAlarm());
        }

        /// <summary>
        /// Returns deep copied list of Alarms that are not sent to subscriber
        /// </summary>
        /// <returns>List of alarms in *Unsent state</returns>
        public virtual IReadOnlyCollection<Alarm> GetUnnotifiedAlarms()
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

    /// <summary>
    /// Strategy transforms invoming event to Alarm and push to AlarmCache
    /// without checking alarm item current state
    /// Preconditions: start and finish of the same event shouldn't arrive in one pack
    ///                and completed events have to have respective alarm event (debug.asserted)
    /// </summary>
    internal class SimpleEventProcessStrategy
    {
        public void TossEvent(Event evt, IAlarmCache alarms)
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

        public void ProcessEventBatch(IEnumerable<Event> events, IAlarmCache alarms)
        {
            events.ToList().ForEach(x => TossEvent(x, alarms));
        }
    }
}
