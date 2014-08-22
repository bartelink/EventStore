using System;
using System.Collections.Generic;
using System.Threading;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;

namespace EventStore.Core.Services.PersistentSubscription
{
    public class PersistentSubscriptionClient
    {
        public readonly int MaximumFreeSlots;

        private readonly Guid _correlationId;
        private readonly Guid _connectionId;
        private readonly IEnvelope _envelope;
        private int _freeSlots;
        public readonly string Username;
        public readonly string From;
        private long _totalItems;

        public readonly List<SequencedEvent> _unconfirmedEvents = new List<SequencedEvent>();

        public PersistentSubscriptionClient(Guid correlationId, 
                                            Guid connectionId, 
                                            IEnvelope envelope, 
                                            int freeSlots, 
                                            string username, 
                                            string from)
        {
            _correlationId = correlationId;
            _connectionId = connectionId;
            _envelope = envelope;
            _freeSlots = freeSlots;
            Username = username;
            From = @from;
            MaximumFreeSlots = freeSlots;
        }

        public int FreeSlots
        {
            get { return _freeSlots; }
        }

        public Guid ConnectionId
        {
            get { return _connectionId; }
        }

        public long TotalItems
        {
            get { return _totalItems; }
        }

        public long LastTotalItems { get; set; }

        public Guid CorrelationId
        {
            get { return _correlationId; }
        }

        public IEnumerable<SequencedEvent> ConfirmProcessing(int numberOfFreeSlots, Guid[] processedEvents)
        {
            foreach (var processedEventId in processedEvents)
            {
                var eventIndex = _unconfirmedEvents.FindIndex(x => x.Event.Event.EventId == processedEventId);
                if (eventIndex >= 0)
                {
                    _freeSlots++;
                    var evnt = _unconfirmedEvents[eventIndex];
                    _unconfirmedEvents.RemoveAt(eventIndex);
                    yield return evnt;
                }
            }
        }

        public void Push(SequencedEvent evnt)
        {
            _freeSlots--;
            Interlocked.Increment(ref _totalItems);
            _envelope.ReplyWith(new ClientMessage.PersistentSubscriptionStreamEventAppeared(CorrelationId, evnt.Event));
            _unconfirmedEvents.Add(evnt);
        }

        public IEnumerable<SequencedEvent> GetUnconfirmedEvents()
        {
            return _unconfirmedEvents;
        }

        public void SendDropNotification()
        {
            _envelope.ReplyWith(new ClientMessage.SubscriptionDropped(CorrelationId, SubscriptionDropReason.Unsubscribed));
        }
    }
}