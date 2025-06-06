// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine.UIElements.Experimental;

namespace UnityEngine.UIElements
{
    /// <summary>
    /// The base class for all UIElements events.  The class implements IDisposable to ensure proper release of the event from the pool and of any unmanaged resources, when necessary.
    /// </summary>
    public abstract class EventBase : IDisposable
    {
        private static long s_LastTypeId = 0;

        /// <summary>
        /// Registers an event class to the event type system.
        /// </summary>
        /// <returns>The type ID.</returns>
        protected static long RegisterEventType() { return ++s_LastTypeId; }

        /// <summary>
        /// Retrieves the type ID for this event instance.
        /// </summary>
        /// <remarks>
        /// This property provides an alternative to the `is` operator
        /// for checking whether a given event is of the expected type
        /// on platforms or build settings where that operator has performance overhead.
        /// </remarks>
        public virtual long eventTypeId => -1;

        [Flags]
        internal enum EventPropagation
        {
            None = 0,
            Bubbles = 1,
            TricklesDown = 2,
            SkipDisabledElements = 4,
            BubblesOrTricklesDown = Bubbles | TricklesDown,
        }

        [Flags]
        enum LifeCycleStatus
        {
            None = 0,
            PropagationStopped = 1,
            ImmediatePropagationStopped = 2,
            Dispatching = 4,
            Pooled = 8,
            IMGUIEventIsValid = 16,
            PropagateToIMGUI = 32,
            Dispatched = 64,
            Processed = 128,
            ProcessedByFocusController = 256,
        }

        internal int eventCategories { get; }

        static ulong s_NextEventId = 0;

        // Read-only state
        /// <summary>
        /// The time when the event was created, in milliseconds.
        /// </summary>
        /// <remarks>
        /// This value is relative to the start time of the current application.
        /// </remarks>
        public long timestamp { get; private set; }

        internal ulong eventId { get; private set; }

        internal ulong triggerEventId { get; private set; }

        internal void SetTriggerEventId(ulong id)
        {
            triggerEventId = id;
        }

        internal EventPropagation propagation { get; set; }

        LifeCycleStatus lifeCycleStatus { get; set; }


        [Obsolete("Override PreDispatch(IPanel panel) instead.")]
        protected virtual void PreDispatch() {}

        /// <summary>
        /// Allows subclasses to perform custom logic before the event is dispatched.
        /// </summary>
        /// <param name="panel">The panel where the event will be dispatched.</param>
        protected internal virtual void PreDispatch(IPanel panel)
        {
#pragma warning disable 618
            PreDispatch();
#pragma warning restore 618
        }


        [Obsolete("Override PostDispatch(IPanel panel) instead.")]
        protected virtual void PostDispatch() {}

        /// <summary>
        /// Allows subclasses to perform custom logic after the event has been dispatched.
        /// </summary>
        /// <param name="panel">The panel where the event has been dispatched.</param>
        protected internal virtual void PostDispatch(IPanel panel)
        {
#pragma warning disable 618
            PostDispatch();
#pragma warning restore 618
            processed = true;
        }

        internal virtual void Dispatch([NotNull] BaseVisualElementPanel panel)
        {
            EventDispatchUtilities.DefaultDispatch(this, panel);
        }

        /// <summary>
        /// Returns whether this event type bubbles up in the event propagation path during the BubbleUp phase.
        /// </summary>
        /// <remarks>
        /// Refer to the [[wiki:UIE-Events-Dispatching|Dispatch events]] manual page for more information and examples.
        /// </remarks>
        /// <seealso cref="PropagationPhase.BubbleUp"/>
        public bool bubbles
        {
            get { return (propagation & EventPropagation.Bubbles) != 0; }
            protected set
            {
                if (value)
                {
                    propagation |= EventPropagation.Bubbles;
                }
                else
                {
                    propagation &= ~EventPropagation.Bubbles;
                }
            }
        }

        /// <summary>
        /// Returns whether this event is sent down the event propagation path during the TrickleDown phase.
        /// </summary>
        /// <remarks>
        /// Refer to the [[wiki:UIE-Events-Dispatching|Dispatch events]] manual page for more information and examples.
        /// </remarks>
        /// <seealso cref="PropagationPhase.TrickleDown"/>
        public bool tricklesDown
        {
            get { return (propagation & EventPropagation.TricklesDown) != 0; }
            protected set
            {
                if (value)
                {
                    propagation |= EventPropagation.TricklesDown;
                }
                else
                {
                    propagation &= ~EventPropagation.TricklesDown;
                }
            }
        }

        internal bool skipDisabledElements
        {
            get { return (propagation & EventPropagation.SkipDisabledElements) != 0; }
            set
            {
                if (value)
                {
                    propagation |= EventPropagation.SkipDisabledElements;
                }
                else
                {
                    propagation &= ~EventPropagation.SkipDisabledElements;
                }
            }
        }

        internal bool bubblesOrTricklesDown => (propagation & EventPropagation.BubblesOrTricklesDown) != 0;

        [Bindings.VisibleToOtherModules("UnityEditor.UIBuilderModule")]
        internal VisualElement elementTarget
        {
            get;
            set;
        }

        /// <summary>
        /// The target visual element that received this event. Unlike currentTarget, this target does not change when
        /// the event is sent to other elements along the propagation path.
        /// </summary>
        public IEventHandler target
        {
            get => elementTarget;
            set => elementTarget = value as VisualElement;
        }

        /// <summary>
        /// Returns true if <see cref="StopPropagation"/> or <see cref="StopImmediatePropagation"/>
        /// was called for this event.
        /// </summary>
        public bool isPropagationStopped
        {
            get { return (lifeCycleStatus & LifeCycleStatus.PropagationStopped) != LifeCycleStatus.None; }
            private set
            {
                if (value)
                {
                    lifeCycleStatus |= LifeCycleStatus.PropagationStopped;
                }
                else
                {
                    lifeCycleStatus &= ~LifeCycleStatus.PropagationStopped;
                }
            }
        }

        /// <summary>
        /// Stops the propagation of the event to other targets.
        /// All subscribers to the event on this target still receive the event.
        /// </summary>
        /// <remarks>
        /// The event is not sent to other elements along the propagation path.
        /// If the propagation is in the <see cref="PropagationPhase.TrickleDown"/> phase,
        /// this prevents event handlers from executing on children of the <see cref="EventBase.currentTarget"/>,
        /// including on the event's <see cref="EventBase.target"/> itself, and prevents all event handlers using the
        /// <see cref="TrickleDown.NoTrickleDown"/> option from executing
        /// (see [[CallbackEventHandler.RegisterCallback]]).
        /// If the propagation is in the <see cref="PropagationPhase.BubbleUp"/> phase,
        /// this prevents event handlers from executing on parents of the <see cref="EventBase.currentTarget"/>.
        ///
        /// This method has the same effect as <see cref="EventBase.StopImmediatePropagation"/>
        /// except on execution of other event handlers on the <see cref="EventBase.currentTarget"/>.
        ///
        /// Calling this method does not prevent some internal actions to be processed,
        /// such as an element getting focused as a result of a <see cref="PointerDownEvent"/>.
        ///
        /// Refer to the [[wiki:UIE-Events-Dispatching|Dispatch events]] manual page for more information and examples.
        /// </remarks>
        /// <seealso cref="EventBase.StopImmediatePropagation"/>
        public void StopPropagation()
        {
            isPropagationStopped = true;
        }

        /// <summary>
        /// Indicates whether <see cref="StopImmediatePropagation"/> was called for this event.
        /// </summary>
        /// <seealso cref="isPropagationStopped"/>
        public bool isImmediatePropagationStopped
        {
            get { return (lifeCycleStatus & LifeCycleStatus.ImmediatePropagationStopped) != LifeCycleStatus.None; }
            private set
            {
                if (value)
                {
                    lifeCycleStatus |= LifeCycleStatus.ImmediatePropagationStopped;
                }
                else
                {
                    lifeCycleStatus &= ~LifeCycleStatus.ImmediatePropagationStopped;
                }
            }
        }

        /// <summary>
        /// Stops the propagation of the event to other targets, and
        /// prevents other subscribers to the event on this target to receive the event.
        /// </summary>
        /// <remarks>
        /// The event is not sent to other elements along the propagation path.
        /// If the propagation is in the <see cref="PropagationPhase.TrickleDown"/> phase,
        /// this prevents event handlers from executing on children of the <see cref="EventBase.currentTarget"/>,
        /// including on the event's <see cref="EventBase.target"/> itself, and prevents all event handlers using the
        /// <see cref="TrickleDown.NoTrickleDown"/> option from executing
        /// (see [[CallbackEventHandler.RegisterCallback]]).
        /// If the propagation is in the <see cref="PropagationPhase.BubbleUp"/> phase,
        /// this prevents event handlers from executing on parents of the <see cref="EventBase.currentTarget"/>.
        ///
        /// This method has the same effect as <see cref="EventBase.StopPropagation"/>
        /// except on execution of other event handlers on the <see cref="EventBase.currentTarget"/>.
        ///
        /// Calling this method does not prevent some internal actions to be processed,
        /// such as an element getting focused as a result of a <see cref="PointerDownEvent"/>.
        ///
        /// Refer to the [[wiki:UIE-Events-Dispatching|Dispatch events]] manual page for more information and examples.
        /// </remarks>
        /// <seealso cref="EventBase.StopPropagation"/>
        public void StopImmediatePropagation()
        {
            isPropagationStopped = true;
            isImmediatePropagationStopped = true;
        }

        /// <summary>
        /// Returns true if the default actions should not be executed for this event.
        /// </summary>
        [Obsolete("Use isPropagationStopped. Before proceeding, make sure you understand the latest changes to " +
                  "UIToolkit event propagation rules by visiting Unity's manual page " +
                  "https://docs.unity3d.com/Manual/UIE-Events-Dispatching.html")]
        public bool isDefaultPrevented => isPropagationStopped;

        /// <summary>
        /// Indicates whether the default actions are prevented from being executed for this event.
        /// </summary>
        [Obsolete("Use StopPropagation and/or FocusController.IgnoreEvent. Before proceeding, make sure you understand the latest changes to " +
                  "UIToolkit event propagation rules by visiting Unity's manual page " +
                  "https://docs.unity3d.com/Manual/UIE-Events-Dispatching.html")]
        public void PreventDefault()
        {
            StopPropagation();
            elementTarget?.focusController?.IgnoreEvent(this);
        }

        // Propagation state
        /// <summary>
        /// The current propagation phase for this event.
        /// </summary>
        public PropagationPhase propagationPhase { get; internal set; }

        IEventHandler m_CurrentTarget;

        /// <summary>
        /// The current target of the event. This is the VisualElement, in the propagation path, for which event handlers are currently being executed.
        /// </summary>
        public virtual IEventHandler currentTarget
        {
            get { return m_CurrentTarget; }
            internal set
            {
                m_CurrentTarget = value;

                if (imguiEvent != null)
                {
                    var element = currentTarget as VisualElement;
                    if (element != null)
                    {
                        imguiEvent.mousePosition = element.WorldToLocal3D(originalMousePosition);
                    }
                    else
                    {
                        imguiEvent.mousePosition = originalMousePosition;
                    }
                }
            }
        }

        /// <summary>
        /// Indicates whether the event is being dispatched to a visual element. An event cannot be redispatched while it being dispatched. If you need to recursively dispatch an event, it is recommended that you use a copy of the event.
        /// </summary>
        public bool dispatch
        {
            get { return (lifeCycleStatus & LifeCycleStatus.Dispatching) != LifeCycleStatus.None; }
            internal set
            {
                if (value)
                {
                    lifeCycleStatus |= LifeCycleStatus.Dispatching;
                    dispatched = true;
                }
                else
                {
                    lifeCycleStatus &= ~LifeCycleStatus.Dispatching;
                }
            }
        }

        internal void MarkReceivedByDispatcher()
        {
            Debug.Assert(dispatched == false, "Events cannot be dispatched more than once.");
            dispatched = true;
        }

        bool dispatched
        {
            get { return (lifeCycleStatus & LifeCycleStatus.Dispatched) != LifeCycleStatus.None; }
            set
            {
                if (value)
                {
                    lifeCycleStatus |= LifeCycleStatus.Dispatched;
                }
                else
                {
                    lifeCycleStatus &= ~LifeCycleStatus.Dispatched;
                }
            }
        }

        internal bool processed
        {
            get { return (lifeCycleStatus & LifeCycleStatus.Processed) != LifeCycleStatus.None; }
            private set
            {
                if (value)
                {
                    lifeCycleStatus |= LifeCycleStatus.Processed;
                }
                else
                {
                    lifeCycleStatus &= ~LifeCycleStatus.Processed;
                }
            }
        }

        internal bool processedByFocusController
        {
            get { return (lifeCycleStatus & LifeCycleStatus.ProcessedByFocusController) != LifeCycleStatus.None; }
            set
            {
                if (value)
                {
                    lifeCycleStatus |= LifeCycleStatus.ProcessedByFocusController;
                }
                else
                {
                    lifeCycleStatus &= ~LifeCycleStatus.ProcessedByFocusController;
                }
            }
        }

        internal bool propagateToIMGUI
        {
            get { return (lifeCycleStatus & LifeCycleStatus.PropagateToIMGUI) != LifeCycleStatus.None; }
            set
            {
                if (value)
                {
                    lifeCycleStatus |= LifeCycleStatus.PropagateToIMGUI;
                }
                else
                {
                    lifeCycleStatus &= ~LifeCycleStatus.PropagateToIMGUI;
                }
            }
        }

        private Event m_ImguiEvent;

        // Since we recycle events (in their pools) and we do not free/reallocate a new imgui event
        // at each recycling (m_ImguiEvent is never null), we use this flag to know whether m_ImguiEvent
        // represents a valid Event.
        bool imguiEventIsValid
        {
            get { return (lifeCycleStatus & LifeCycleStatus.IMGUIEventIsValid) != LifeCycleStatus.None; }
            set
            {
                if (value)
                {
                    lifeCycleStatus |= LifeCycleStatus.IMGUIEventIsValid;
                }
                else
                {
                    lifeCycleStatus &= ~LifeCycleStatus.IMGUIEventIsValid;
                }
            }
        }

        // We aim to make this internal.
        /// <summary>
        /// The IMGUIEvent at the source of this event. The source can be null since not all events are generated by IMGUI.
        /// </summary>
        public /*internal*/ Event imguiEvent
        {
            get { return imguiEventIsValid ? m_ImguiEvent : null; }
            protected set
            {
                if (m_ImguiEvent == null)
                {
                    m_ImguiEvent = new Event();
                }

                if (value != null)
                {
                    m_ImguiEvent.CopyFrom(value);
                    imguiEventIsValid = true;
                    originalMousePosition = value.mousePosition; // when assigned, it is assumed that the imguievent is not touched and therefore in world coordinates.
                }
                else
                {
                    imguiEventIsValid = false;
                }
            }
        }

        /// <summary>
        /// The original mouse position of the IMGUI event, before it is transformed to the current target local coordinates.
        /// </summary>
        public Vector2 originalMousePosition { get; private set; }

        internal EventDebugger eventLogger { get; set; }

        internal bool log => eventLogger != null;

        /// <summary>
        /// Resets all event members to their initial values.
        /// </summary>
        protected virtual void Init()
        {
            LocalInit();
        }

        void LocalInit()
        {
            timestamp = Panel.TimeSinceStartupMs();

            triggerEventId = 0;
            eventId = s_NextEventId++;

            propagation = EventPropagation.None;

            elementTarget = null;

            isPropagationStopped = false;
            isImmediatePropagationStopped = false;

            propagationPhase = default;

            originalMousePosition = Vector2.zero;
            m_CurrentTarget = null;

            dispatch = false;
            propagateToIMGUI = true;

            dispatched = false;
            processed = false;
            processedByFocusController = false;
            imguiEventIsValid = false;
            pooled = false;

            eventLogger = null;
        }

        /// <summary>
        /// Constructor. Avoid creating new event instances. Instead, use GetPooled() to get an instance from a pool of reusable event instances.
        /// </summary>
        protected EventBase() : this(EventCategory.Default)
        {
        }

        internal EventBase(EventCategory category)
        {
            eventCategories = 1 << (int) category;
            m_ImguiEvent = null;
            LocalInit();
        }

        /// <summary>
        /// Whether the event is allocated from a pool of events.
        /// </summary>
        protected bool pooled
        {
            get { return (lifeCycleStatus & LifeCycleStatus.Pooled) != LifeCycleStatus.None; }
            set
            {
                if (value)
                {
                    lifeCycleStatus |= LifeCycleStatus.Pooled;
                }
                else
                {
                    lifeCycleStatus &= ~LifeCycleStatus.Pooled;
                }
            }
        }

        internal abstract void Acquire();
        /// <summary>
        /// Implementation of IDisposable.
        /// </summary>
        public abstract void Dispose();
    }

    /// <summary>
    /// Generic base class for events, implementing event pooling and automatic registration to the event type system.
    /// </summary>
    [EventCategory(EventCategory.Default)]
    public abstract class EventBase<T> : EventBase where T : EventBase<T>, new()
    {
        static readonly long s_TypeId = RegisterEventType();
        static readonly ObjectPool<T> s_Pool = new ObjectPool<T>(() => new T());

        internal static void SetCreateFunction(Func<T> createMethod)
        {
            s_Pool.CreateFunc = createMethod;
        }

        int m_RefCount;

        protected EventBase() : base(EventCategory)
        {
            m_RefCount = 0;
        }

        /// <summary>
        /// Retrieves the type ID for this event instance.
        /// </summary>
        /// <returns>The type ID.</returns>
        public static long TypeId()
        {
            return s_TypeId;
        }

        internal static readonly EventCategory EventCategory = EventInterestReflectionUtils.GetEventCategory(typeof(T));

        /// <summary>
        /// Resets all event members to their initial values.
        /// </summary>
        protected override void Init()
        {
            base.Init();

            if (m_RefCount != 0)
            {
                Debug.Log("Event improperly released.");
                m_RefCount = 0;
            }
        }

        /// <summary>
        /// Gets an event from the event pool. Use this function instead of creating new events. Events obtained using this method need to be released back to the pool. You can use `Dispose()` to release them.
        /// </summary>
        /// <returns>An initialized event.</returns>
        public static T GetPooled()
        {
            T t = s_Pool.Get();
            t.Init();
            t.pooled = true;
            t.Acquire();
            return t;
        }

        internal static T GetPooled(EventBase e)
        {
            T t = GetPooled();
            if (e != null)
            {
                t.SetTriggerEventId(e.eventId);
            }
            return t;
        }

        static void ReleasePooled(T evt)
        {
            if (evt.pooled)
            {
                // Reset the event before pooling to avoid leaking VisualElement
                evt.Init();

                s_Pool.Release(evt);

                // To avoid double release from pool
                evt.pooled = false;
            }
        }

        internal override void Acquire()
        {
            m_RefCount++;
        }

        /// <summary>
        /// Implementation of IDispose.
        /// </summary>
        /// <remarks>
        /// If the event was instantiated from an event pool, the event is released when Dispose is called.
        /// </remarks>
        public sealed override void Dispose()
        {
            if (--m_RefCount == 0)
            {
                ReleasePooled((T)this);
            }
        }

        /// <summary>
        /// See <see cref="EventBase.eventTypeId"/>.
        /// </summary>
        public override long eventTypeId => s_TypeId;
    }
}
