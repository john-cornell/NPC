namespace NPC.Library.Messaging;

using System;
using System.Collections.Generic;
using System.Linq;

public class MessageDispatcher
{
    private readonly Dictionary<Type, List<Delegate>> _subscribers = new();
    private readonly Queue<IMessage> _messageQueue = new();

    /// <summary>
    /// Subscribe to a specific message type.
    /// </summary>
    public void Subscribe<T>(Action<T> handler) where T : IMessage
    {
        var type = typeof(T);
        if (!_subscribers.ContainsKey(type))
        {
            _subscribers[type] = new List<Delegate>();
        }
        _subscribers[type].Add(handler);
    }

    /// <summary>
    /// Unsubscribe from a specific message type.
    /// </summary>
    public void Unsubscribe<T>(Action<T> handler) where T : IMessage
    {
        var type = typeof(T);
        if (_subscribers.TryGetValue(type, out var handlers))
        {
            handlers.Remove(handler);
        }
    }

    /// <summary>
    /// Dispatches the message immediately. All listeners execute before this method returns.
    /// Used for urgent state updates or purely reactive systems like logging.
    /// </summary>
    public void DispatchImmediate<T>(T message) where T : IMessage
    {
        var type = message.GetType();
        if (_subscribers.TryGetValue(type, out var handlers))
        {
            // Iterate over a copy to allow listeners to unsubscribe during iteration
            foreach (var handler in handlers.ToList())
            {
                if (handler is Action<T> typedHandler)
                {
                    typedHandler(message);
                }
                else
                {
                    handler.DynamicInvoke(message);
                }
            }
        }
    }

    /// <summary>
    /// Queues a message to be processed later. 
    /// Used to avoid modifying state mid-tick or during collection iteration.
    /// </summary>
    public void DispatchDeferred(IMessage message)
    {
        _messageQueue.Enqueue(message);
    }

    /// <summary>
    /// Processes all deferred messages in the queue.
    /// Usually called by the SimulationEngine at the very end of the tick.
    /// </summary>
    public void ProcessQueue()
    {
        while (_messageQueue.Count > 0)
        {
            var message = _messageQueue.Dequeue();
            var type = message.GetType();
            
            if (_subscribers.TryGetValue(type, out var handlers))
            {
                foreach (var handler in handlers.ToList())
                {
                    // For deferred dispatch, the exact generic type T is lost to the queue,
                    // so we use DynamicInvoke.
                    handler.DynamicInvoke(message);
                }
            }
        }
    }
}
