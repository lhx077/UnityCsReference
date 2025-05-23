// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.Scripting;
using UnityEngine.Serialization;

namespace UnityEngine.Events
{
    [Serializable]
    public enum PersistentListenerMode
    {
        EventDefined,
        Void,
        Object,
        Int,
        Float,
        String,
        Bool
    }

    internal class UnityEventTools
    {
        // Fix for assembly type name containing version / culture. We don't care about this for UI.
        // we need to fix this here, because there is old data in existing projects.
        // Typically, we're looking for .net Assembly Qualified Type Names and stripping everything after '<namespaces>.<typename>, <assemblyname>'
        // Example: System.String, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089' -> 'System.String, mscorlib'
        internal static string TidyAssemblyTypeName(string assemblyTypeName)
        {
            if (string.IsNullOrEmpty(assemblyTypeName))
                return assemblyTypeName;

            int min = Int32.MaxValue;
            int i = assemblyTypeName.IndexOf(", Version=");
            if (i != -1)
                min = Math.Min(i, min);
            i = assemblyTypeName.IndexOf(", Culture=");
            if (i != -1)
                min = Math.Min(i, min);
            i = assemblyTypeName.IndexOf(", PublicKeyToken=");
            if (i != -1)
                min = Math.Min(i, min);

            if (min != Int32.MaxValue)
                assemblyTypeName = assemblyTypeName.Substring(0, min);

            // Strip module assembly name.
            // The non-modular version will always work, due to type forwarders.
            // This way, when a type gets moved to a differnet module, previously serialized UnityEvents still work.
            i = assemblyTypeName.IndexOf(", UnityEngine.");
            if (i != -1 && assemblyTypeName.EndsWith("Module"))
                assemblyTypeName = assemblyTypeName.Substring(0, i) + ", UnityEngine";
            return assemblyTypeName;
        }
    }

    [Serializable]
    class ArgumentCache : ISerializationCallbackReceiver
    {
        [FormerlySerializedAs("objectArgument")]
        [SerializeField] private Object m_ObjectArgument;
        [FormerlySerializedAs("objectArgumentAssemblyTypeName")]
        [SerializeField] private string m_ObjectArgumentAssemblyTypeName;
        [FormerlySerializedAs("intArgument")]
        [SerializeField] private int    m_IntArgument;
        [FormerlySerializedAs("floatArgument")]
        [SerializeField] private float  m_FloatArgument;
        [FormerlySerializedAs("stringArgument")]
        [SerializeField] private string m_StringArgument;
        [SerializeField] private bool m_BoolArgument;

        public Object unityObjectArgument
        {
            get { return m_ObjectArgument; }
            set
            {
                m_ObjectArgument = value;
                m_ObjectArgumentAssemblyTypeName = value != null ? value.GetType().AssemblyQualifiedName : string.Empty;
            }
        }

        public string unityObjectArgumentAssemblyTypeName
        {
            get { return m_ObjectArgumentAssemblyTypeName; }
        }

        public int    intArgument    { get { return m_IntArgument;    } set { m_IntArgument = value;    } }
        public float  floatArgument  { get { return m_FloatArgument;  } set { m_FloatArgument = value;  } }
        public string stringArgument { get { return m_StringArgument; } set { m_StringArgument = value; } }
        public bool   boolArgument   { get { return m_BoolArgument;   } set { m_BoolArgument = value; } }


        public void OnBeforeSerialize()
        {
            m_ObjectArgumentAssemblyTypeName = UnityEventTools.TidyAssemblyTypeName(m_ObjectArgumentAssemblyTypeName);
        }

        public void OnAfterDeserialize()
        {
            m_ObjectArgumentAssemblyTypeName = UnityEventTools.TidyAssemblyTypeName(m_ObjectArgumentAssemblyTypeName);
        }
    }

    internal abstract class BaseInvokableCall
    {
        protected BaseInvokableCall()
        {}

        protected BaseInvokableCall(object target, MethodInfo function)
        {
            if (function == null)
                throw new ArgumentNullException(nameof(function));

            if (function.IsStatic)
            {
                if (target != null)
                    throw new ArgumentException("target must be null");
            }
            else
            {
                if (target == null)
                    throw new ArgumentNullException("target");
            }
        }

        public abstract void Invoke(object[] args);

        protected static void ThrowOnInvalidArg<T>(object arg)
        {
            if (arg != null && !(arg is T))
                throw new ArgumentException(string.Format("Passed argument 'args[0]' is of the wrong type. Type:{0} Expected:{1}", arg.GetType(), typeof(T)));
        }

        protected static bool AllowInvoke(Delegate @delegate)
        {
            var target = @delegate.Target;

            // static
            if (target == null)
                return true;

            // UnityEngine object
            var unityObj = target as Object;
            if (!ReferenceEquals(unityObj, null))
                return unityObj != null;

            // Normal object
            return true;
        }

        public abstract bool Find(object targetObj, MethodInfo method);
    }

    class InvokableCall : BaseInvokableCall
    {
        private event UnityAction Delegate;

        public InvokableCall(object target, MethodInfo theFunction)
            : base(target, theFunction)
        {
            Delegate += (UnityAction)System.Delegate.CreateDelegate(typeof(UnityAction), target, theFunction);
        }

        public InvokableCall(UnityAction action)
        {
            Delegate += action;
        }

        public override void Invoke(object[] args)
        {
            if (AllowInvoke(Delegate))
                Delegate();
        }

        public void Invoke()
        {
            if (AllowInvoke(Delegate))
                Delegate();
        }

        public override bool Find(object targetObj, MethodInfo method)
        {
            // Case 827748: You can't compare Delegate.GetMethodInfo() == method, because sometimes it will not work, that's why we're using Equals instead, because it will compare that actual method inside.
            //              Comment from Microsoft:
            //              Desktop behavior regarding identity has never really been guaranteed. The desktop aggressively caches and reuses MethodInfo objects so identity checks often work by accident.
            //              .Net Native doesn’t guarantee identity and caches a lot less
            return Delegate.Target == targetObj && Delegate.Method.Equals(method);
        }
    }

    class InvokableCall<T1> : BaseInvokableCall
    {
        protected event UnityAction<T1> Delegate;

        public InvokableCall(object target, MethodInfo theFunction)
            : base(target, theFunction)
        {
            Delegate += (UnityAction<T1>)System.Delegate.CreateDelegate(typeof(UnityAction<T1>), target, theFunction);
        }

        public InvokableCall(UnityAction<T1> action)
        {
            Delegate += action;
        }

        public override void Invoke(object[] args)
        {
            if (args.Length != 1)
                throw new ArgumentException("Passed argument 'args' is invalid size. Expected size is 1");
            ThrowOnInvalidArg<T1>(args[0]);

            if (AllowInvoke(Delegate))
                Delegate((T1)args[0]);
        }

        public virtual void Invoke(T1 args0)
        {
            if (AllowInvoke(Delegate))
                Delegate(args0);
        }

        public override bool Find(object targetObj, MethodInfo method)
        {
            return Delegate.Target == targetObj && Delegate.Method.Equals(method);
        }
    }

    class InvokableCall<T1, T2> : BaseInvokableCall
    {
        protected event UnityAction<T1, T2> Delegate;

        public InvokableCall(object target, MethodInfo theFunction)
            : base(target, theFunction)
        {
            Delegate = (UnityAction<T1, T2>)System.Delegate.CreateDelegate(typeof(UnityAction<T1, T2>), target, theFunction);
        }

        public InvokableCall(UnityAction<T1, T2> action)
        {
            Delegate += action;
        }

        public override void Invoke(object[] args)
        {
            if (args.Length != 2)
                throw new ArgumentException("Passed argument 'args' is invalid size. Expected size is 1");
            ThrowOnInvalidArg<T1>(args[0]);
            ThrowOnInvalidArg<T2>(args[1]);

            if (AllowInvoke(Delegate))
                Delegate((T1)args[0], (T2)args[1]);
        }

        public void Invoke(T1 args0, T2 args1)
        {
            if (AllowInvoke(Delegate))
                Delegate(args0, args1);
        }

        public override bool Find(object targetObj, MethodInfo method)
        {
            return Delegate.Target == targetObj && Delegate.Method.Equals(method);
        }
    }

    class InvokableCall<T1, T2, T3> : BaseInvokableCall
    {
        protected event UnityAction<T1, T2, T3> Delegate;

        public InvokableCall(object target, MethodInfo theFunction)
            : base(target, theFunction)
        {
            Delegate = (UnityAction<T1, T2, T3>)System.Delegate.CreateDelegate(typeof(UnityAction<T1, T2, T3>), target, theFunction);
        }

        public InvokableCall(UnityAction<T1, T2, T3> action)
        {
            Delegate += action;
        }

        public override void Invoke(object[] args)
        {
            if (args.Length != 3)
                throw new ArgumentException("Passed argument 'args' is invalid size. Expected size is 1");
            ThrowOnInvalidArg<T1>(args[0]);
            ThrowOnInvalidArg<T2>(args[1]);
            ThrowOnInvalidArg<T3>(args[2]);

            if (AllowInvoke(Delegate))
                Delegate((T1)args[0], (T2)args[1], (T3)args[2]);
        }

        public void Invoke(T1 args0, T2 args1, T3 args2)
        {
            if (AllowInvoke(Delegate))
                Delegate(args0, args1, args2);
        }

        public override bool Find(object targetObj, MethodInfo method)
        {
            return Delegate.Target == targetObj && Delegate.Method.Equals(method);
        }
    }

    class InvokableCall<T1, T2, T3, T4> : BaseInvokableCall
    {
        protected event UnityAction<T1, T2, T3, T4> Delegate;

        public InvokableCall(object target, MethodInfo theFunction)
            : base(target, theFunction)
        {
            Delegate = (UnityAction<T1, T2, T3, T4>)System.Delegate.CreateDelegate(typeof(UnityAction<T1, T2, T3, T4>), target, theFunction);
        }

        public InvokableCall(UnityAction<T1, T2, T3, T4> action)
        {
            Delegate += action;
        }

        public override void Invoke(object[] args)
        {
            if (args.Length != 4)
                throw new ArgumentException("Passed argument 'args' is invalid size. Expected size is 1");
            ThrowOnInvalidArg<T1>(args[0]);
            ThrowOnInvalidArg<T2>(args[1]);
            ThrowOnInvalidArg<T3>(args[2]);
            ThrowOnInvalidArg<T4>(args[3]);

            if (AllowInvoke(Delegate))
                Delegate((T1)args[0], (T2)args[1], (T3)args[2], (T4)args[3]);
        }

        public void Invoke(T1 args0, T2 args1, T3 args2, T4 args3)
        {
            if (AllowInvoke(Delegate))
                Delegate(args0, args1, args2, args3);
        }

        public override bool Find(object targetObj, MethodInfo method)
        {
            return Delegate.Target == targetObj && Delegate.Method.Equals(method);
        }
    }

    class CachedInvokableCall<T> : InvokableCall<T>
    {
        private readonly T m_Arg1;

        public CachedInvokableCall(Object target, MethodInfo theFunction, T argument)
            : base(target, theFunction)
        {
            m_Arg1 = argument;
        }

        public override void Invoke(object[] args)
        {
            base.Invoke(m_Arg1);
        }

        public override void Invoke(T arg0)
        {
            base.Invoke(m_Arg1);
        }
    }

    public enum UnityEventCallState
    {
        Off = 0,
        EditorAndRuntime = 1,
        RuntimeOnly = 2,
    }

    [Serializable]
    class PersistentCall : ISerializationCallbackReceiver
    {
        //keep the layout of this class in sync with MonoPersistentCall in PersistentCallCollection.cpp
        [FormerlySerializedAs("instance")]
        [SerializeField]
        private Object m_Target;

        [SerializeField]
        private string m_TargetAssemblyTypeName;

        [FormerlySerializedAs("methodName")]
        [SerializeField]
        private string m_MethodName;

        [FormerlySerializedAs("mode")]
        [SerializeField]
        private PersistentListenerMode m_Mode = PersistentListenerMode.EventDefined;

        [FormerlySerializedAs("arguments")]
        [SerializeField]
        private ArgumentCache m_Arguments = new ArgumentCache();

        [FormerlySerializedAs("enabled")]
        [FormerlySerializedAs("m_Enabled")]
        [SerializeField]
        private UnityEventCallState m_CallState = UnityEventCallState.RuntimeOnly;

        public Object target
        {
            get { return m_Target; }
        }

        public string targetAssemblyTypeName
        {
            get
            {
                // Reconstruct TargetAssemblyTypeName from target if it's not present, for ex., when upgrading project
                if (string.IsNullOrEmpty(m_TargetAssemblyTypeName) && m_Target != null)
                {
                    m_TargetAssemblyTypeName = UnityEventTools.TidyAssemblyTypeName(m_Target.GetType().AssemblyQualifiedName);
                }

                return m_TargetAssemblyTypeName;
            }
        }

        public string methodName
        {
            get { return m_MethodName; }
        }

        public PersistentListenerMode mode
        {
            get { return m_Mode; }
            set { m_Mode = value; }
        }

        public ArgumentCache arguments
        {
            get { return m_Arguments; }
        }

        public UnityEventCallState callState
        {
            get { return m_CallState; }
            set { m_CallState = value; }
        }

        public bool IsValid()
        {
            // We need to use the same logic found in PersistentCallCollection.cpp, IsPersistentCallValid
            return !String.IsNullOrEmpty(targetAssemblyTypeName) && !String.IsNullOrEmpty(methodName);
        }

        public BaseInvokableCall GetRuntimeCall(UnityEventBase theEvent)
        {
            if (m_CallState == UnityEventCallState.RuntimeOnly && (target != null ? !Application.IsPlaying(target) : !Application.isPlaying))
                return null;
            if (m_CallState == UnityEventCallState.Off || theEvent == null)
                return null;

            var method = theEvent.FindMethod(this);
            if (method == null)
                return null;

            if (!method.IsStatic && target == null)
                return null;

            var targetObject = method.IsStatic ? null : target;

            switch (m_Mode)
            {
                case PersistentListenerMode.EventDefined:
                    return theEvent.GetDelegate(targetObject, method);
                case PersistentListenerMode.Object:
                    return GetObjectCall(targetObject, method, m_Arguments);
                case PersistentListenerMode.Float:
                    return new CachedInvokableCall<float>(targetObject, method, m_Arguments.floatArgument);
                case PersistentListenerMode.Int:
                    return new CachedInvokableCall<int>(targetObject, method, m_Arguments.intArgument);
                case PersistentListenerMode.String:
                    return new CachedInvokableCall<string>(targetObject, method, m_Arguments.stringArgument);
                case PersistentListenerMode.Bool:
                    return new CachedInvokableCall<bool>(targetObject, method, m_Arguments.boolArgument);
                case PersistentListenerMode.Void:
                    return new InvokableCall(targetObject, method);
            }
            return null;
        }

        // need to generate a generic typed version of the call here
        // this is due to the fact that we allow binding of 'any'
        // functions that extend object.
        private static BaseInvokableCall GetObjectCall(Object target, MethodInfo method, ArgumentCache arguments)
        {
            var type = typeof(Object);
            if (!string.IsNullOrEmpty(arguments.unityObjectArgumentAssemblyTypeName))
                type = Type.GetType(arguments.unityObjectArgumentAssemblyTypeName, false) ?? typeof(Object);

            var generic = typeof(CachedInvokableCall<>);
            var specific = generic.MakeGenericType(type);
            var ci = specific.GetConstructor(new[] { typeof(Object), typeof(MethodInfo), type});

            var castedObject = arguments.unityObjectArgument;
            if (castedObject != null && !type.IsAssignableFrom(castedObject.GetType()))
                castedObject = null;

            // need to pass explicit null here!
            return ci.Invoke(new object[] {target, method, castedObject}) as BaseInvokableCall;
        }

        public void RegisterPersistentListener(Object ttarget, Type targetType, string mmethodName)
        {
            m_Target = ttarget;
            m_TargetAssemblyTypeName = UnityEventTools.TidyAssemblyTypeName(targetType.AssemblyQualifiedName);
            m_MethodName = mmethodName;
        }

        public void UnregisterPersistentListener()
        {
            m_MethodName = string.Empty;
            m_Target = null;
            m_TargetAssemblyTypeName = string.Empty;
        }

        public void OnBeforeSerialize()
        {
            m_TargetAssemblyTypeName = UnityEventTools.TidyAssemblyTypeName(m_TargetAssemblyTypeName);
        }

        public void OnAfterDeserialize()
        {
            m_TargetAssemblyTypeName = UnityEventTools.TidyAssemblyTypeName(m_TargetAssemblyTypeName);
        }
    }

    [Serializable]
    internal class PersistentCallGroup
    {
        [FormerlySerializedAs("m_Listeners")]
        [SerializeField] private List<PersistentCall> m_Calls;

        public PersistentCallGroup()
        {
            m_Calls = new List<PersistentCall>();
        }

        public int Count
        {
            get { return m_Calls.Count; }
        }

        public PersistentCall GetListener(int index)
        {
            return m_Calls[index];
        }

        public IEnumerable<PersistentCall> GetListeners()
        {
            return m_Calls;
        }

        public void AddListener()
        {
            m_Calls.Add(new PersistentCall());
        }

        public void AddListener(PersistentCall call)
        {
            m_Calls.Add(call);
        }

        public void RemoveListener(int index)
        {
            m_Calls.RemoveAt(index);
        }

        public void Clear()
        {
            m_Calls.Clear();
        }

        public void RegisterEventPersistentListener(int index, Object targetObj, Type targetObjType, string methodName)
        {
            var listener = GetListener(index);
            listener.RegisterPersistentListener(targetObj, targetObjType, methodName);
            listener.mode = PersistentListenerMode.EventDefined;
        }

        public void RegisterVoidPersistentListener(int index, Object targetObj, Type targetObjType, string methodName)
        {
            var listener = GetListener(index);
            listener.RegisterPersistentListener(targetObj, targetObjType, methodName);
            listener.mode = PersistentListenerMode.Void;
        }

        public void RegisterObjectPersistentListener(int index, Object targetObj, Type targetObjType, Object argument, string methodName)
        {
            var listener = GetListener(index);
            listener.RegisterPersistentListener(targetObj, targetObjType, methodName);
            listener.mode = PersistentListenerMode.Object;
            listener.arguments.unityObjectArgument = argument;
        }

        public void RegisterIntPersistentListener(int index, Object targetObj, Type targetObjType, int argument, string methodName)
        {
            var listener = GetListener(index);
            listener.RegisterPersistentListener(targetObj, targetObjType, methodName);
            listener.mode = PersistentListenerMode.Int;
            listener.arguments.intArgument = argument;
        }

        public void RegisterFloatPersistentListener(int index, Object targetObj, Type targetObjType, float argument, string methodName)
        {
            var listener = GetListener(index);
            listener.RegisterPersistentListener(targetObj, targetObjType, methodName);
            listener.mode = PersistentListenerMode.Float;
            listener.arguments.floatArgument = argument;
        }

        public void RegisterStringPersistentListener(int index, Object targetObj, Type targetObjType, string argument, string methodName)
        {
            var listener = GetListener(index);
            listener.RegisterPersistentListener(targetObj, targetObjType, methodName);
            listener.mode = PersistentListenerMode.String;
            listener.arguments.stringArgument = argument;
        }

        public void RegisterBoolPersistentListener(int index, Object targetObj, Type targetObjType, bool argument, string methodName)
        {
            var listener = GetListener(index);
            listener.RegisterPersistentListener(targetObj, targetObjType, methodName);
            listener.mode = PersistentListenerMode.Bool;
            listener.arguments.boolArgument = argument;
        }

        public void UnregisterPersistentListener(int index)
        {
            var evt = GetListener(index);
            evt.UnregisterPersistentListener();
        }

        public void RemoveListeners(Object target, string methodName)
        {
            var toRemove = new List<PersistentCall>();
            for (int index = 0; index < m_Calls.Count; index++)
            {
                if (m_Calls[index].target == target && m_Calls[index].methodName == methodName)
                    toRemove.Add(m_Calls[index]);
            }
            m_Calls.RemoveAll(toRemove.Contains);
        }

        public void Initialize(InvokableCallList invokableList, UnityEventBase unityEventBase)
        {
            foreach (var persistentCall in m_Calls)
            {
                if (!persistentCall.IsValid())
                    continue;

                var call = persistentCall.GetRuntimeCall(unityEventBase);
                if (call != null)
                    invokableList.AddPersistentInvokableCall(call);
            }
        }
    }

    class InvokableCallList
    {
        private readonly List<BaseInvokableCall> m_PersistentCalls = new List<BaseInvokableCall>();
        private readonly List<BaseInvokableCall> m_RuntimeCalls = new List<BaseInvokableCall>();

        private List<BaseInvokableCall> m_ExecutingCalls = new List<BaseInvokableCall>();

        private bool m_NeedsUpdate = true;

        public int Count
        {
            get { return m_PersistentCalls.Count + m_RuntimeCalls.Count; }
        }

        public void AddPersistentInvokableCall(BaseInvokableCall call)
        {
            m_PersistentCalls.Add(call);
            m_NeedsUpdate = true;
        }

        public void AddListener(BaseInvokableCall call)
        {
            m_RuntimeCalls.Add(call);
            m_NeedsUpdate = true;
        }

        public void RemoveListener(object targetObj, MethodInfo method)
        {
            var toRemove = new List<BaseInvokableCall>();
            for (int index = 0; index < m_RuntimeCalls.Count; index++)
            {
                if (m_RuntimeCalls[index].Find(targetObj, method))
                    toRemove.Add(m_RuntimeCalls[index]);
            }
            m_RuntimeCalls.RemoveAll(toRemove.Contains);

            // removals are done synchronously to avoid leaks
            var newExecutingCalls = new List<BaseInvokableCall>(m_PersistentCalls.Count + m_RuntimeCalls.Count);
            newExecutingCalls.AddRange(m_PersistentCalls);
            newExecutingCalls.AddRange(m_RuntimeCalls);
            m_ExecutingCalls = newExecutingCalls;
            m_NeedsUpdate = false;
        }

        public void Clear()
        {
            m_RuntimeCalls.Clear();
            // removals are done synchronously to avoid leaks
            var newExecutingCalls = new List<BaseInvokableCall>(m_PersistentCalls);
            m_ExecutingCalls = newExecutingCalls;
            m_NeedsUpdate = false;
        }

        public void ClearPersistent()
        {
            m_PersistentCalls.Clear();
            // removals are done synchronously to avoid leaks
            var newExecutingCalls = new List<BaseInvokableCall>(m_RuntimeCalls);
            m_ExecutingCalls = newExecutingCalls;
            m_NeedsUpdate = false;
        }

        public List<BaseInvokableCall> PrepareInvoke()
        {
            if (m_NeedsUpdate)
            {
                m_ExecutingCalls.Clear();
                m_ExecutingCalls.AddRange(m_PersistentCalls);
                m_ExecutingCalls.AddRange(m_RuntimeCalls);
                m_NeedsUpdate = false;
            }

            return m_ExecutingCalls;
        }
    }

    [Serializable]
    [UsedByNativeCode]
    public abstract class UnityEventBase : ISerializationCallbackReceiver
    {
        private InvokableCallList m_Calls;

        [FormerlySerializedAs("m_PersistentListeners")]
        [SerializeField]
        private PersistentCallGroup m_PersistentCalls;

        // Dirtying can happen outside of MainThread, but we need to rebuild on the MainThread.
        private bool m_CallsDirty = true;

        protected UnityEventBase()
        {
            m_Calls = new InvokableCallList();
            m_PersistentCalls = new PersistentCallGroup();
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            DirtyPersistentCalls();
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            DirtyPersistentCalls();
        }

        protected MethodInfo FindMethod_Impl(string name, object targetObj)
        {
            return FindMethod_Impl(name, targetObj.GetType());
        }

        protected abstract MethodInfo FindMethod_Impl(string name, Type targetObjType);
        internal abstract BaseInvokableCall GetDelegate(object target, MethodInfo theFunction);

        internal MethodInfo FindMethod(PersistentCall call)
        {
            var type = typeof(Object);
            if (!string.IsNullOrEmpty(call.arguments.unityObjectArgumentAssemblyTypeName))
                type = Type.GetType(call.arguments.unityObjectArgumentAssemblyTypeName, false) ?? typeof(Object);

            var targetType = call.target != null ? call.target.GetType() : Type.GetType(call.targetAssemblyTypeName, false);
            return FindMethod(call.methodName, targetType, call.mode, type);
        }

        internal MethodInfo FindMethod(string name, Type listenerType, PersistentListenerMode mode, Type argumentType)
        {
            switch (mode)
            {
                case PersistentListenerMode.EventDefined:
                    return FindMethod_Impl(name, listenerType);
                case PersistentListenerMode.Void:
                    return GetValidMethodInfo(listenerType, name, new Type[0]);
                case PersistentListenerMode.Float:
                    return GetValidMethodInfo(listenerType, name, new[] { typeof(float) });
                case PersistentListenerMode.Int:
                    return GetValidMethodInfo(listenerType, name, new[] { typeof(int) });
                case PersistentListenerMode.Bool:
                    return GetValidMethodInfo(listenerType, name, new[] { typeof(bool) });
                case PersistentListenerMode.String:
                    return GetValidMethodInfo(listenerType, name, new[] { typeof(string) });
                case PersistentListenerMode.Object:
                    return GetValidMethodInfo(listenerType, name, new[] { argumentType ?? typeof(Object) });
                default:
                    return null;
            }
        }

        internal int GetCallsCount()
        {
            return m_Calls.Count;
        }

        public int GetPersistentEventCount()
        {
            return m_PersistentCalls.Count;
        }

        public Object GetPersistentTarget(int index)
        {
            var listener = m_PersistentCalls.GetListener(index);
            return listener != null ? listener.target : null;
        }

        public string GetPersistentMethodName(int index)
        {
            var listener = m_PersistentCalls.GetListener(index);
            return listener != null ? listener.methodName : string.Empty;
        }

        private void DirtyPersistentCalls()
        {
            m_Calls.ClearPersistent();
            m_CallsDirty = true;
        }

        // Can only run on MainThread
        private void RebuildPersistentCallsIfNeeded()
        {
            if (m_CallsDirty)
            {
                m_PersistentCalls.Initialize(m_Calls, this);
                m_CallsDirty = false;
            }
        }

        public void SetPersistentListenerState(int index, UnityEventCallState state)
        {
            var listener = m_PersistentCalls.GetListener(index);
            if (listener != null)
                listener.callState = state;

            DirtyPersistentCalls();
        }

        public UnityEventCallState GetPersistentListenerState(int index)
        {
            if (index < 0 || index > m_PersistentCalls.Count)
                throw new IndexOutOfRangeException($"Index {index} is out of range of the {GetPersistentEventCount()} persistent listeners.");
            return m_PersistentCalls.GetListener(index).callState;
        }

        protected void AddListener(object targetObj, MethodInfo method)
        {
            m_Calls.AddListener(GetDelegate(targetObj, method));
        }

        internal void AddCall(BaseInvokableCall call)
        {
            m_Calls.AddListener(call);
        }

        protected void RemoveListener(object targetObj, MethodInfo method)
        {
            m_Calls.RemoveListener(targetObj, method);
        }

        public void RemoveAllListeners()
        {
            m_Calls.Clear();
        }

        internal List<BaseInvokableCall> PrepareInvoke()
        {
            RebuildPersistentCallsIfNeeded();
            return m_Calls.PrepareInvoke();
        }

        protected void Invoke(object[] parameters)
        {
            List<BaseInvokableCall> calls = PrepareInvoke();

            for (var i = 0; i < calls.Count; i++)
                calls[i].Invoke(parameters);
        }

        public override string ToString()
        {
            return base.ToString() + " " + GetType().FullName;
        }

        // Find a valid method that can be bound to an event with a given name
        public static MethodInfo GetValidMethodInfo(object obj, string functionName, Type[] argumentTypes)
        {
            return GetValidMethodInfo(obj.GetType(), functionName, argumentTypes);
        }

        public static MethodInfo GetValidMethodInfo(Type objectType, string functionName, Type[] argumentTypes)
        {
            while (objectType != typeof(object) && objectType != null)
            {
                var method = objectType.GetMethod(functionName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static, null, argumentTypes, null);
                if (method != null)
                {
                    // We need to make sure the Arguments are sane. When using the Type.DefaultBinder like we are above,
                    // it is possible to receive a method that takes a System.Object enve though we requested a float, int or bool.
                    // This can be an issue when the user changes the signature of a function that he had already set up via inspector.
                    // When changing a float parameter to a System.Object the getMethod would still bind to the cahnged version, but
                    // the PersistentListenerMode would still be kept as Float.
                    // TODO: Should we allow anything else besides Primitive types and types derived from UnityEngine.Object?
                    var parameterInfos = method.GetParameters();
                    var methodValid = true;
                    var i = 0;
                    foreach (ParameterInfo pi in parameterInfos)
                    {
                        var requestedType = argumentTypes[i];
                        var receivedType = pi.ParameterType;
                        methodValid = requestedType.IsPrimitive == receivedType.IsPrimitive;

                        if (!methodValid)
                            break;
                        i++;
                    }
                    if (methodValid)
                        return method;
                }
                objectType = objectType.BaseType;
            }
            return null;
        }

        protected bool ValidateRegistration(MethodInfo method, object targetObj, PersistentListenerMode mode)
        {
            return ValidateRegistration(method, targetObj, mode, typeof(Object));
        }

        protected bool ValidateRegistration(MethodInfo method, object targetObj, PersistentListenerMode mode, Type argumentType)
        {
            if (method == null)
                throw new ArgumentNullException("method", string.Format("Can not register null method on {0} for callback!", targetObj));

            if (method.DeclaringType == null)
            {
                throw new NullReferenceException(
                    string.Format(
                        "Method '{0}' declaring type is null, global methods are not supported",
                        method.Name));
            }

            Type targetType;
            if (!method.IsStatic)
            {
                var obj = targetObj as Object;
                if (obj == null || obj.GetInstanceID() == 0)
                {
                    throw new ArgumentException(
                        string.Format(
                            "Could not register callback {0} on {1}. The class {2} does not derive from UnityEngine.Object",
                            method.Name,
                            targetObj,
                            targetObj == null ? "null" : targetObj.GetType().ToString()));
                }

                targetType = obj.GetType();

                if (!method.DeclaringType.IsAssignableFrom(targetType))
                    throw new ArgumentException(
                        string.Format(
                            "Method '{0}' declaring type '{1}' is not assignable from object type '{2}'",
                            method.Name,
                            method.DeclaringType.Name,
                            obj.GetType().Name));
            }
            else
            {
                targetType = method.DeclaringType;
            }

            if (FindMethod(method.Name, targetType, mode, argumentType) == null)
            {
                Debug.LogWarning(string.Format("Could not register listener {0}.{1} on {2} the method could not be found.", targetObj, method, GetType()));
                return false;
            }
            return true;
        }

        internal void AddPersistentListener()
        {
            m_PersistentCalls.AddListener();
        }

        protected void RegisterPersistentListener(int index, object targetObj, MethodInfo method)
        {
            RegisterPersistentListener(index, targetObj, targetObj.GetType(), method);
        }

        protected void RegisterPersistentListener(int index, object targetObj, Type targetObjType, MethodInfo method)
        {
            if (!ValidateRegistration(method, targetObj, PersistentListenerMode.EventDefined))
                return;

            m_PersistentCalls.RegisterEventPersistentListener(index, targetObj as Object, targetObjType, method.Name);
            DirtyPersistentCalls();
        }

        internal void RemovePersistentListener(Object target, MethodInfo method)
        {
            if (method == null)
                return;
            m_PersistentCalls.RemoveListeners(target, method.Name);
            DirtyPersistentCalls();
        }

        internal void RemovePersistentListener(int index)
        {
            m_PersistentCalls.RemoveListener(index);
            DirtyPersistentCalls();
        }

        internal void UnregisterPersistentListener(int index)
        {
            m_PersistentCalls.UnregisterPersistentListener(index);
            DirtyPersistentCalls();
        }

        internal void AddVoidPersistentListener(UnityAction call)
        {
            var count = GetPersistentEventCount();
            AddPersistentListener();
            RegisterVoidPersistentListener(count, call);
        }

        internal void RegisterVoidPersistentListener(int index, UnityAction call)
        {
            if (call == null)
            {
                Debug.LogWarning("Registering a Listener requires an action");
                return;
            }
            if (!ValidateRegistration(call.Method, call.Target, PersistentListenerMode.Void))
                return;

            m_PersistentCalls.RegisterVoidPersistentListener(index, call.Target as Object, call.Method.DeclaringType, call.Method.Name);
            DirtyPersistentCalls();
        }

        internal void RegisterVoidPersistentListenerWithoutValidation(int index, Object target, string methodName)
        {
            RegisterVoidPersistentListenerWithoutValidation(index, target, target.GetType(), methodName);
        }

        internal void RegisterVoidPersistentListenerWithoutValidation(int index, Object target, Type targetType, string methodName)
        {
            m_PersistentCalls.RegisterVoidPersistentListener(index, target, targetType, methodName);
            DirtyPersistentCalls();
        }

        internal void AddIntPersistentListener(UnityAction<int> call, int argument)
        {
            var count = GetPersistentEventCount();
            AddPersistentListener();
            RegisterIntPersistentListener(count, call, argument);
        }

        internal void RegisterIntPersistentListener(int index, UnityAction<int> call, int argument)
        {
            if (call == null)
            {
                Debug.LogWarning("Registering a Listener requires an action");
                return;
            }
            if (!ValidateRegistration(call.Method, call.Target, PersistentListenerMode.Int))
                return;

            m_PersistentCalls.RegisterIntPersistentListener(index, call.Target as Object, call.Method.DeclaringType, argument, call.Method.Name);
            DirtyPersistentCalls();
        }

        internal void AddFloatPersistentListener(UnityAction<float> call, float argument)
        {
            var count = GetPersistentEventCount();
            AddPersistentListener();
            RegisterFloatPersistentListener(count, call, argument);
        }

        internal void RegisterFloatPersistentListener(int index, UnityAction<float> call, float argument)
        {
            if (call == null)
            {
                Debug.LogWarning("Registering a Listener requires an action");
                return;
            }
            if (!ValidateRegistration(call.Method, call.Target, PersistentListenerMode.Float))
                return;

            m_PersistentCalls.RegisterFloatPersistentListener(index, call.Target as Object, call.Method.DeclaringType, argument, call.Method.Name);
            DirtyPersistentCalls();
        }

        internal void AddBoolPersistentListener(UnityAction<bool> call, bool argument)
        {
            var count = GetPersistentEventCount();
            AddPersistentListener();
            RegisterBoolPersistentListener(count, call, argument);
        }

        internal void RegisterBoolPersistentListener(int index, UnityAction<bool> call, bool argument)
        {
            if (call == null)
            {
                Debug.LogWarning("Registering a Listener requires an action");
                return;
            }
            if (!ValidateRegistration(call.Method, call.Target, PersistentListenerMode.Bool))
                return;

            m_PersistentCalls.RegisterBoolPersistentListener(index, call.Target as Object, call.Method.DeclaringType, argument, call.Method.Name);
            DirtyPersistentCalls();
        }

        internal void AddStringPersistentListener(UnityAction<string> call, string argument)
        {
            var count = GetPersistentEventCount();
            AddPersistentListener();
            RegisterStringPersistentListener(count, call, argument);
        }

        internal void RegisterStringPersistentListener(int index, UnityAction<string> call, string argument)
        {
            if (call == null)
            {
                Debug.LogWarning("Registering a Listener requires an action");
                return;
            }
            if (!ValidateRegistration(call.Method, call.Target, PersistentListenerMode.String))
                return;

            m_PersistentCalls.RegisterStringPersistentListener(index, call.Target as Object, call.Method.DeclaringType, argument, call.Method.Name);
            DirtyPersistentCalls();
        }

        internal void AddObjectPersistentListener<T>(UnityAction<T> call, T argument) where T : Object
        {
            var count = GetPersistentEventCount();
            AddPersistentListener();
            RegisterObjectPersistentListener(count, call, argument);
        }

        internal void RegisterObjectPersistentListener<T>(int index, UnityAction<T> call, T argument) where T : Object
        {
            if (call == null)
                throw new ArgumentNullException("call", "Registering a Listener requires a non null call");

            if (!ValidateRegistration(call.Method, call.Target, PersistentListenerMode.Object, argument == null ? typeof(Object) : argument.GetType()))
                return;

            m_PersistentCalls.RegisterObjectPersistentListener(index, call.Target as Object, call.Method.DeclaringType, argument, call.Method.Name);
            DirtyPersistentCalls();
        }

    }
}
