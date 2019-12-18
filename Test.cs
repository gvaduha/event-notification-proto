using Moq;
using System.Collections.Generic;
using Xunit;

namespace gvaduha.proto.EventNotification
{
    class TestData
    {
        public static Event e1b1 = new Event {Id=1, LinkedTo = "#1", completed = false};
        public static Event e1e1 = new Event {Id=1, LinkedTo = "#1", completed = true};
        public static Event e1b2 = new Event {Id=2, LinkedTo = "#1", completed = false};
        public static Event e1e2 = new Event {Id=2, LinkedTo = "#1", completed = true};
        public static Event e1b3 = new Event {Id=3, LinkedTo = "#1", completed = false};
        public static Event e1e3 = new Event {Id=3, LinkedTo = "#1", completed = true};
        public static Event e2b4 = new Event {Id=4, LinkedTo = "#2", completed = false};
        public static Event e2e4 = new Event {Id=4, LinkedTo = "#2", completed = true};
        public static Event e2b5 = new Event {Id=5, LinkedTo = "#2", completed = false};
        public static Event e2e5 = new Event {Id=5, LinkedTo = "#2", completed = true};
    }

    public class Test
    {
        [Fact]
        public void TestBatches()
        {
            var b1 = new List<Event> {TestData.e1b1,TestData.e1b2,TestData.e1b3};
            var b2 = new List<Event> {TestData.e1e2,TestData.e1e3,TestData.e2b4};
            var b3 = new List<Event> {TestData.e2b5,TestData.e1e2};

            var sut = new SimpleEventProcessStrategy();
            
            var cache = new Mock<AlarmCache>(new List<Alarm>()){ CallBase=true };

            sut.ProcessEventBatch(b1, cache.Object);

            Alarm _;
            cache.Verify(x=>x.TryGetValue("#1", out _),
                Times.Exactly(3));
            cache.Verify(x=>x.Add("#1", It.IsAny<Alarm>()),
                Times.Once());
            //cache.Verify(x=>x.Add("#1", new Alarm(TestData.e1b1)),
            //    Times.Once());

            var alarms = cache.Object.GetUnnotifiedAlarms();

            sut.ProcessEventBatch(b2, cache.Object);
            cache.Verify(x=>x.TryGetValue("#1", out _),
                Times.Exactly(3));
            cache.Verify(x=>x.Add("#2", It.IsAny<Alarm>()),
                Times.Once());

            alarms = cache.Object.GetUnnotifiedAlarms();

            sut.ProcessEventBatch(b3, cache.Object);
            cache.Verify(x=>x.TryGetValue("#1", out _),
                Times.Exactly(3));
            cache.Verify(x=>x.Add("#2", It.IsAny<Alarm>()),
                Times.Once());

            alarms = cache.Object.GetUnnotifiedAlarms();
        }
    }
}
