using Moq;
using System.Collections.Generic;
using Xunit;

namespace gvaduha.proto.EventNotification
{
    class TestData
    {
        public static EventLinkKey e1k = new EventLinkKey(1,1);
        public static Event e1b1 = new Event {Id=1, LinkedTo = e1k, completed = false};
        public static Event e1e1 = new Event {Id=1, LinkedTo = e1k, completed = true};
        public static Event e1b2 = new Event {Id=2, LinkedTo = e1k, completed = false};
        public static Event e1e2 = new Event {Id=2, LinkedTo = e1k, completed = true};
        public static Event e1b3 = new Event {Id=3, LinkedTo = e1k, completed = false};
        public static Event e1e3 = new Event {Id=3, LinkedTo = e1k, completed = true};
        public static EventLinkKey e2k = new EventLinkKey(2,2);
        public static Event e2b4 = new Event {Id=4, LinkedTo = e2k, completed = false};
        public static Event e2e4 = new Event {Id=4, LinkedTo = e2k, completed = true};
        public static Event e2b5 = new Event {Id=5, LinkedTo = e2k, completed = false};
        public static Event e2e5 = new Event {Id=5, LinkedTo = e2k, completed = true};
        public static EventLinkKey e3k = new EventLinkKey(3,3);
        public static Event e3b6 = new Event {Id=6, LinkedTo = e3k, completed = false};
        public static Event e3e6 = new Event {Id=6, LinkedTo = e3k, completed = true};
    }

    public class Test
    {
        //HACK: rewrite to parametric behaviour test
        [Fact]
        public void StupidTestBatches()
        {
            var b1 = new List<Event> {TestData.e1b1,TestData.e1b2,TestData.e1b3};
            var b2 = new List<Event> {TestData.e1e1,TestData.e1e3,TestData.e2b4};
            var b3 = new List<Event> {TestData.e2b5,TestData.e1e2};
            var b4 = new List<Event> {TestData.e3b6};

            var sut = new SimpleEventProcessStrategy();
            
            var cache = new Mock<AlarmCache>(new List<Alarm>()){ CallBase=true };

            // Phase #1
            sut.ProcessEventBatch(b1, cache.Object);

            Alarm _;
            cache.Verify(x=>x.TryGetValue(TestData.e1k, out _),
                Times.Exactly(3));
            cache.Verify(x=>x.Add(It.IsAny<EventLinkKey>(), It.IsAny<Alarm>()),
                Times.Once());
            cache.Verify(x=>x.Add(TestData.e1k, It.IsAny<Alarm>()),
                Times.Once());

            var alarms = cache.Object.GetUnnotifiedAlarmsShortView();
            Assert.Equal(alarms.Count, 1);


            // Phase #2
            sut.ProcessEventBatch(b2, cache.Object);

            cache.Verify(x=>x.TryGetValue(TestData.e1k, out _),
                Times.Exactly(2+3));
            cache.Verify(x=>x.TryGetValue(TestData.e2k, out _),
                Times.Once());
            cache.Verify(x=>x.Add(TestData.e2k, It.IsAny<Alarm>()),
                Times.Once());

            alarms = cache.Object.GetUnnotifiedAlarmsShortView();
            Assert.Equal(alarms.Count, 2);

            // Phase #3
            sut.ProcessEventBatch(b3, cache.Object);

            cache.Verify(x=>x.TryGetValue(TestData.e1k, out _),
                Times.Exactly(2+3+1));

            // Totals
            cache.Verify(x=>x.TryGetValue(It.IsAny<EventLinkKey>(), out _),
                Times.Exactly(8));
            cache.Verify(x=>x.Add(It.IsAny<EventLinkKey>(), It.IsAny<Alarm>()),
                Times.Exactly(2));

            // Phase #4
            sut.ProcessEventBatch(b4, cache.Object);

            alarms = cache.Object.GetUnnotifiedAlarmsShortView();
            Assert.Equal(alarms.Count, 3);

            // Merge test
            var mlist = new List<Alarm.ShortView>();
            bool firstone = true; // imitate unsent item
            foreach (var a in alarms)
            {
                if (a.State == Alarm.AlarmState.AlarmPending)
                {
                    if (firstone)
                    {
                        firstone = false;
                        mlist.Add(new Alarm.ShortView(a.Id, Alarm.AlarmState.AlarmSent, a.Key));
                    }
                    else
                        mlist.Add(new Alarm.ShortView(a.Id, Alarm.AlarmState.AlarmPending, a.Key));
                }
                else if (a.State == Alarm.AlarmState.FinishPending)
                {
                    mlist.Add(new Alarm.ShortView(a.Id, Alarm.AlarmState.FinishedSent, a.Key));
                }
            }
            
            sut.MergeSentAlarms(mlist, cache.Object);

            cache.Verify(x=>x.Remove(TestData.e1k), Times.Once());

            alarms = cache.Object.GetUnnotifiedAlarmsShortView();
            Assert.Equal(alarms.Count, 1);
        }
    }
}
