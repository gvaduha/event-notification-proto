﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace gvaduha.proto.EventNotification
{
    internal class Alarm : ICloneable, IEquatable<Alarm>
    {
        public enum AlarmState
        {
            New,
            AlarmPending,
            AlarmSent,
            FinishPending,
            FinishedSent,
            Completed  // for backpropagation merge
        }

        public Guid Id {get;} = Guid.NewGuid();
        public AlarmState State { get; set; }
        public EventLinkKey Key { get; }

        private readonly HashSet<Event>  _events;
        private readonly List<long> _eventIdHistory = new List<long>();

        public Alarm(Alarm rhs)
        {
            State = rhs.State;
            Key = rhs.Key;
            _events = rhs._events.Select(x=>(Event)x.Clone()).ToHashSet();
            _eventIdHistory = rhs._eventIdHistory.ToList();
        }

        public Alarm(Event evt)
        {
            State = AlarmState.New;
            Key = evt.LinkedTo;
            _events = new HashSet<Event> {evt};
        }

        public object Clone() => new Alarm(this);

        public bool AddEvent(Event evt)
        {
            _eventIdHistory.Add(evt.Id);
            return _events.Add(evt);
        }
        public bool RemoveEvent(Event evt) => _events.Remove(evt);

        public bool Equals(Alarm other)
        {
            if (other == null) return false;
            if (ReferenceEquals(other, this)) return true;
            return Id == other.Id &&
                State == other.State &&
                Key.Equals(other.Key);
        }

        public int EventCount => _events.Count;

        public override string ToString() =>
            $"id:{Id}, s:{State}, k:{Key}, actev:{_events.Count}, histev:{_events.Count}";

        public struct ShortView
        {
            public Guid Id {get;}
            public AlarmState State {get;}
            public EventLinkKey Key {get;}

            public ShortView(Guid id, AlarmState state, EventLinkKey key)
            {
                Id = id;
                State = state;
                Key = key;
            }
        }

        public ShortView GetShortView() => new ShortView(Id, State, Key);
    }

    internal interface IAlarmCache
    {
        bool TryGetValue(EventLinkKey key, out Alarm value);
        void Add(EventLinkKey key, Alarm value);
        //bool Remove(EventLinkKey key);
        IReadOnlyCollection<Alarm> GetUnnotifiedAlarms();
        void PurgeCompletedAlarms();
    }

    internal class AlarmCache : IAlarmCache
    {
        private readonly Dictionary<EventLinkKey, Alarm> _alarms = new Dictionary<EventLinkKey, Alarm>();

        public AlarmCache(IEnumerable<Alarm> alarms)
        {
            _alarms = alarms.ToDictionary(x => x.Key);
            //RefreshAlarmStates(); // consider uncomment for "dirty" data
        }

        public virtual bool TryGetValue(EventLinkKey key, out Alarm value) => _alarms.TryGetValue(key,out value);
        public virtual void Add(EventLinkKey key, Alarm value) => _alarms.Add(key, value);
        public virtual bool Remove(EventLinkKey key) => _alarms.Remove(key);

        private void RefreshAlarmStatesForExternalViewer()
        {
            _alarms.Values.ToList().ForEach(x=>
            {
                if (x.State == Alarm.AlarmState.New) x.State = Alarm.AlarmState.AlarmPending;
                if (x.EventCount == 0) x.State = Alarm.AlarmState.FinishPending;
            });
        }

        private IReadOnlyCollection<T> GetUnnotifiedAlarmsSkeleton<T>(Func<Alarm,T> fun)
        {
            RefreshAlarmStatesForExternalViewer();
            return _alarms.Values.Where(
                x => new List<Alarm.AlarmState>
                        {Alarm.AlarmState.AlarmPending, Alarm.AlarmState.FinishPending}
                        .Contains(x.State))
            .Select(x => fun(x))
            .ToList();
        }

        public void PurgeCompletedAlarms()
        {
            _alarms.Where(x=>x.Value.State == Alarm.AlarmState.Completed).Select(x=>x.Key)
                .ToList().ForEach(k=>Remove(k));
        }

        /// <summary>
        /// Returns deep copied list of Alarms that are not sent to subscriber
        /// </summary>
        /// <returns>List of alarms in *Unsent state</returns>
        public virtual IReadOnlyCollection<Alarm> GetUnnotifiedAlarms()
        {
            return GetUnnotifiedAlarmsSkeleton( x => (Alarm) x.Clone());
        }

        public virtual IReadOnlyCollection<Alarm.ShortView> GetUnnotifiedAlarmsShortView()
        {
            return GetUnnotifiedAlarmsSkeleton( x => (Alarm.ShortView) x.GetShortView());
        }

    }
}
