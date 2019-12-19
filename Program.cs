using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")] 

namespace gvaduha.proto.EventNotification
{
    internal struct EventLinkKey : IEquatable<EventLinkKey>
    {
        public int Dim1 {get;}
        public int Dim2 {get;}

        public EventLinkKey(int d1, int d2)
        {
            Dim1 = d1;
            Dim2 = d2;
        }

        public bool Equals(EventLinkKey other) =>
            Dim1 == other.Dim1 && Dim2 == other.Dim2;

        public override int GetHashCode() => ToString().GetHashCode();
        public override string ToString() => $"{{{Dim1}:{Dim2}}}";
    }

    internal class Event : ICloneable, IEquatable<Event>
    {
        public long Id;
        public EventLinkKey LinkedTo;
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

        public override string ToString() => 
            $"id:{Id}, link:{LinkedTo}, c:{completed}";
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
