using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

namespace JCommonUtils.Tests
{
    /// <summary>仅使用独立实例验证派发契约，不读写全局事件单例。</summary>
    public sealed class EventCenterDispatchTests
    {
        [Test]
        public void Fire_DeliversStructReferenceDefaultAndNullPayloads()
        {
            var events = new EventCenterSystem();
            var values = new List<int>();
            var references = new List<ReferenceEvent>();
            events.Subscribe<ValueEvent>(value => values.Add(value.Value));
            events.Subscribe<ReferenceEvent>(value => references.Add(value));

            events.Fire(new ValueEvent(17));
            events.Fire<ValueEvent>();
            var payload = new ReferenceEvent();
            events.Fire(payload);
            events.Fire<ReferenceEvent>();
            events.Fire<ReferenceEvent>(null);

            Assert.That(values, Is.EqualTo(new[] { 17, 0 }));
            Assert.That(references.Count, Is.EqualTo(3));
            Assert.That(references[0], Is.SameAs(payload));
            Assert.That(references[1], Is.Null);
            Assert.That(references[2], Is.Null);
            Assert.DoesNotThrow(() => events.Fire<DerivedEvent>());
        }

        [Test]
        public void Fire_AcceptsContravariantHandlersAndPreservesMulticastOrder()
        {
            var events = new EventCenterSystem();
            var trace = new List<string>();
            var received = new List<IEvent>();
            Action<IEvent> interfaceHandler = value => { trace.Add("interface"); received.Add(value); };
            Action<ReferenceEvent> baseHandler = value => { trace.Add("base"); received.Add(value); };
            Action<DerivedEvent> multicast = value => { trace.Add("multi-first"); received.Add(value); };
            multicast += value => { trace.Add("multi-second"); received.Add(value); };
            events.Subscribe<DerivedEvent>(interfaceHandler);
            events.Subscribe<DerivedEvent>(baseHandler);
            events.Subscribe(multicast);
            int otherTypeCalls = 0;
            events.Subscribe<ReferenceEvent>(_ => otherTypeCalls++);

            var payload = new DerivedEvent();
            events.Fire(payload);

            Assert.That(trace, Is.EqualTo(new[] { "multi-first", "multi-second", "base", "interface" }));
            Assert.That(received.Count, Is.EqualTo(4));
            foreach (IEvent value in received) Assert.That(value, Is.SameAs(payload));
            Assert.That(otherTypeCalls, Is.Zero, "按 Fire 的泛型类型查表，不额外向父事件类型广播。");
        }

        [Test]
        public void Subscription_DeduplicatesEqualDelegatesAndUnsubscribesByEventType()
        {
            var events = new EventCenterSystem();
            var trace = new List<string>();
            var receiver = new Receiver(trace);
            Action<ValueEvent> first = receiver.Receive;
            Action<ValueEvent> equalDelegate = receiver.Receive;
            Action<ValueEvent> second = _ => trace.Add("second");
            Assert.That(first.Equals(equalDelegate), Is.True);
            events.Unsubscribe(equalDelegate);
            events.Subscribe(first);
            events.Subscribe(second);
            events.Subscribe(equalDelegate);
            events.Subscribe(second);
            events.Fire<ValueEvent>();
            Assert.That(trace, Is.EqualTo(new[] { "second", "first" }));

            trace.Clear();
            events.Unsubscribe(equalDelegate);
            events.Unsubscribe(equalDelegate);
            events.Fire<ValueEvent>();
            Assert.That(trace, Is.EqualTo(new[] { "second" }));
            events.Unsubscribe(second);
            trace.Clear();
            events.Fire<ValueEvent>();
            Assert.That(trace, Is.Empty);

            int referenceCalls = 0;
            Action<ReferenceEvent> shared = _ => referenceCalls++;
            events.Subscribe<ReferenceEvent>(shared);
            events.Subscribe<DerivedEvent>(shared);
            events.Unsubscribe<ReferenceEvent>(shared);
            events.Fire<ReferenceEvent>();
            events.Fire(new DerivedEvent());
            Assert.That(referenceCalls, Is.EqualTo(1));
            events.Unsubscribe<DerivedEvent>(shared);
            events.Fire(new DerivedEvent());
            Assert.That(referenceCalls, Is.EqualTo(1));
        }

        [Test]
        public void Fire_AddingSubscriptionDefersItUntilNextDispatch()
        {
            var events = new EventCenterSystem();
            var trace = new List<string>();
            Action<ValueEvent> added = _ => trace.Add("added");
            events.Subscribe<ValueEvent>(_ => trace.Add("first"));
            events.Subscribe<ValueEvent>(_ => trace.Add("second"));
            events.Subscribe<ValueEvent>(_ => { trace.Add("third"); events.Subscribe(added); });

            events.Fire<ValueEvent>();
            Assert.That(trace, Is.EqualTo(new[] { "third", "second", "first" }));
            trace.Clear();
            events.Fire<ValueEvent>();
            Assert.That(trace, Is.EqualTo(new[] { "added", "third", "second", "first" }));
        }

        [Test]
        public void Fire_RemovingSubscriptionsPreservesLiveReverseListSemantics()
        {
            // 原实现遍历实时列表：删除较小下标会让当前回调再次被访问，并不是快照派发。
            var events = new EventCenterSystem();
            var trace = new List<string>();
            Action<ValueEvent> first = _ => trace.Add("first");
            bool removeFirst = true;
            events.Subscribe(first);
            events.Subscribe<ValueEvent>(_ => trace.Add("second"));
            events.Subscribe<ValueEvent>(_ =>
            {
                trace.Add("third");
                if (!removeFirst) return;
                removeFirst = false;
                events.Unsubscribe(first);
            });
            events.Fire<ValueEvent>();
            Assert.That(trace, Is.EqualTo(new[] { "third", "third", "second" }));
            trace.Clear();
            events.Fire<ValueEvent>();
            Assert.That(trace, Is.EqualTo(new[] { "third", "second" }));

            var selfEvents = new EventCenterSystem();
            trace.Clear();
            Action<ValueEvent> self = null;
            self = _ => { trace.Add("self"); selfEvents.Unsubscribe(self); };
            selfEvents.Subscribe<ValueEvent>(_ => trace.Add("earlier"));
            selfEvents.Subscribe(self);
            selfEvents.Fire<ValueEvent>();
            selfEvents.Fire<ValueEvent>();
            Assert.That(trace, Is.EqualTo(new[] { "self", "earlier", "earlier" }));

            // 全部移除后旧循环仍会访问下一下标；保留该现状，不在此次优化中悄然修正。
            var clearedEvents = new EventCenterSystem();
            trace.Clear();
            Action<ValueEvent> a = _ => trace.Add("a");
            Action<ValueEvent> b = _ => trace.Add("b");
            Action<ValueEvent> clear = null;
            clear = _ =>
            {
                trace.Add("clear");
                clearedEvents.Unsubscribe(a);
                clearedEvents.Unsubscribe(b);
                clearedEvents.Unsubscribe(clear);
            };
            clearedEvents.Subscribe(a);
            clearedEvents.Subscribe(b);
            clearedEvents.Subscribe(clear);
            Assert.Throws<ArgumentOutOfRangeException>(() => clearedEvents.Fire<ValueEvent>());
            Assert.That(trace, Is.EqualTo(new[] { "clear" }));
            Assert.DoesNotThrow(() => clearedEvents.Fire<ValueEvent>());
        }

        [Test]
        public void Fire_NestedDispatchIsSynchronousAndPreservesOuterOrder()
        {
            var events = new EventCenterSystem();
            var trace = new List<string>();
            events.Subscribe<ValueEvent>(value => trace.Add("earlier-" + value.Value));
            events.Subscribe<ValueEvent>(value =>
            {
                trace.Add("enter-" + value.Value);
                if (value.Value == 1) events.Fire(new ValueEvent(2));
                trace.Add("exit-" + value.Value);
            });

            events.Fire(new ValueEvent(1));

            Assert.That(trace, Is.EqualTo(new[]
                { "enter-1", "enter-2", "exit-2", "earlier-2", "exit-1", "earlier-1" }));
        }

        [Test]
        public void Fire_WrapsHandlerExceptionsAndStopsRemainingHandlers()
        {
            var original = new InvalidOperationException("Listener failure.");
            // 已经是 TargetInvocationException 的业务异常也必须再包装一层，不能剥掉原异常。
            foreach (Exception failure in new Exception[] { original, new TargetInvocationException(original) })
            {
                var events = new EventCenterSystem();
                var trace = new List<string>();
                events.Subscribe<ValueEvent>(_ => trace.Add("earlier"));
                events.Subscribe<ValueEvent>(_ => { trace.Add("throwing"); throw failure; });
                events.Subscribe<ValueEvent>(_ => trace.Add("later"));
                TargetInvocationException error =
                    Assert.Throws<TargetInvocationException>(() => events.Fire<ValueEvent>());
                Assert.That(error.InnerException, Is.SameAs(failure));
                Assert.That(trace, Is.EqualTo(new[] { "later", "throwing" }));
            }

            var multicastEvents = new EventCenterSystem();
            var multicastTrace = new List<string>();
            Action<ValueEvent> multicast = _ => multicastTrace.Add("multi-first");
            multicast += _ => { multicastTrace.Add("multi-throw"); throw original; };
            multicast += _ => multicastTrace.Add("multi-unreached");
            multicastEvents.Subscribe<ValueEvent>(_ => multicastTrace.Add("earlier"));
            multicastEvents.Subscribe(multicast);
            TargetInvocationException multicastError =
                Assert.Throws<TargetInvocationException>(() => multicastEvents.Fire<ValueEvent>());
            Assert.That(multicastError.InnerException, Is.SameAs(original));
            Assert.That(multicastTrace, Is.EqualTo(new[] { "multi-first", "multi-throw" }));

            var nestedEvents = new EventCenterSystem();
            nestedEvents.Subscribe<ReferenceEvent>(_ => throw original);
            nestedEvents.Subscribe<ValueEvent>(_ => nestedEvents.Fire<ReferenceEvent>());
            TargetInvocationException nestedError =
                Assert.Throws<TargetInvocationException>(() => nestedEvents.Fire<ValueEvent>());
            Assert.That(nestedError.InnerException, Is.TypeOf<TargetInvocationException>());
            Assert.That(nestedError.InnerException.InnerException, Is.SameAs(original));
        }

        [Test]
        public void Fire_NullHandlerRemainsAnUnwrappedNullReferenceFailure()
        {
            var events = new EventCenterSystem();
            var trace = new List<string>();
            events.Unsubscribe<ValueEvent>(null);
            events.Subscribe<ValueEvent>(_ => trace.Add("earlier"));
            Assert.DoesNotThrow(() => events.Subscribe<ValueEvent>(null));
            events.Subscribe<ValueEvent>(null);
            events.Subscribe<ValueEvent>(_ => trace.Add("later"));
            Assert.Throws<NullReferenceException>(() => events.Fire<ValueEvent>());
            Assert.That(trace, Is.EqualTo(new[] { "later" }));

            events.Unsubscribe<ValueEvent>(null);
            trace.Clear();
            events.Fire<ValueEvent>();
            Assert.That(trace, Is.EqualTo(new[] { "later", "earlier" }), "重复 null 订阅同样去重，一次退订即可移除。");
        }

        private readonly struct ValueEvent : IEvent
        {
            internal readonly int Value;
            internal ValueEvent(int value) { Value = value; }
        }

        private class ReferenceEvent : IEvent { }
        private sealed class DerivedEvent : ReferenceEvent { }

        private sealed class Receiver
        {
            private readonly List<string> trace;
            internal Receiver(List<string> trace) { this.trace = trace; }
            internal void Receive(ValueEvent value) { trace.Add("first"); }
        }
    }
}
